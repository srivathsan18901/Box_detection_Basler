using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace VisioNeo_3D.Services
{
    public class BoxDetectionResult
    {
        public Bitmap ResultImage { get; set; }

        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
        public double OffsetZ { get; set; }

        public double WidthMM { get; set; }
        public double LengthMM { get; set; }
        public double CenterDepth { get; set; }

        public double HeightMM { get; set; }

        public double ActualWidthMM { get; set; }
        public double ActualLengthMM { get; set; }
        public double ActualHeightMM { get; set; }
        public double Angle { get; set; }

        // Debug info
        public int ContourCount { get; set; }
        public bool DetectionSucceeded { get; set; }
        public string DetectionMethod { get; set; }
        public string RejectionReason { get; set; } // NEW: Why box was rejected
    }

    public class BoxDetectionService
    {
        // ===== FIXED: Correct box dimensions =====
        private const double ACTUAL_WIDTH_MM = 300;   // Width of the box
        private const double ACTUAL_LENGTH_MM = 410;  // Length of the box (FIXED)
        private const double ACTUAL_HEIGHT_MM = 410;  // Height of the box

        // Tolerance for size validation (in percentage)
        private const double SIZE_TOLERANCE_PERCENT = 0.20; // 20% tolerance
        private const double ASPECT_RATIO_TOLERANCE = 0.30; // 30% tolerance

        private const double DEFAULT_MM_PER_PIXEL = 0.60;

        private double mmPerPixel = DEFAULT_MM_PER_PIXEL;

        public double MmPerPixel => mmPerPixel;

        public void SetMmPerPixel(double value)
        {
            if (value <= 0)
                throw new ArgumentException("MM per pixel must be greater than zero.");

            mmPerPixel = value;
        }

        public BoxDetectionResult DetectBox(Bitmap source, float cameraZ)
        {
            // Clone the source bitmap for result drawing
            Bitmap resultBmp = (Bitmap)source.Clone();

            // Convert to OpenCV Mat
            Mat src = BitmapConverter.ToMat(source);

            System.Diagnostics.Debug.WriteLine($"Image Dimensions: {src.Width} x {src.Height}");

            // Convert to grayscale
            Mat gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            // Calculate expected dimensions in pixels
            double expectedWidthPx = ACTUAL_WIDTH_MM / mmPerPixel;
            double expectedLengthPx = ACTUAL_LENGTH_MM / mmPerPixel;
            double expectedRatio = ACTUAL_WIDTH_MM / ACTUAL_LENGTH_MM; // 300/410 = 0.732

            // Calculate min and max allowed dimensions
            double minAllowedWidthPx = expectedWidthPx * (1 - SIZE_TOLERANCE_PERCENT);
            double maxAllowedWidthPx = expectedWidthPx * (1 + SIZE_TOLERANCE_PERCENT);
            double minAllowedLengthPx = expectedLengthPx * (1 - SIZE_TOLERANCE_PERCENT);
            double maxAllowedLengthPx = expectedLengthPx * (1 + SIZE_TOLERANCE_PERCENT);

            System.Diagnostics.Debug.WriteLine($"=== EXPECTED DIMENSIONS ===");
            System.Diagnostics.Debug.WriteLine($"Expected W: {expectedWidthPx:F1}px (min: {minAllowedWidthPx:F1}px, max: {maxAllowedWidthPx:F1}px)");
            System.Diagnostics.Debug.WriteLine($"Expected L: {expectedLengthPx:F1}px (min: {minAllowedLengthPx:F1}px, max: {maxAllowedLengthPx:F1}px)");
            System.Diagnostics.Debug.WriteLine($"Expected Ratio: {expectedRatio:F3}");

            // Calculate frame center
            double frameCenterX = src.Width / 2.0;
            double frameCenterY = src.Height / 2.0;

            // Variables for best match
            RotatedRect bestBox = new RotatedRect();
            OpenCvSharp.Point[] bestContour = null;
            double bestScore = double.MinValue;
            int totalContoursFound = 0;
            string bestMethod = "None";
            string rejectionReason = "";

            // ---- TRY MULTIPLE DETECTION METHODS ----
            for (int blockSize = 11; blockSize <= 31; blockSize += 10)
            {
                for (int c = 2; c <= 10; c += 4)
                {
                    Mat thresh = new Mat();
                    Mat blurred = new Mat();
                    Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(5, 5), 0);
                    Cv2.AdaptiveThreshold(blurred, thresh, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.BinaryInv, blockSize, c);

                    ProcessThreshold(thresh, ref totalContoursFound, ref bestContour, ref bestBox, ref bestScore,
                        expectedWidthPx, expectedLengthPx, expectedRatio,
                        minAllowedWidthPx, maxAllowedWidthPx,
                        minAllowedLengthPx, maxAllowedLengthPx,
                        frameCenterX, frameCenterY,
                        "Adaptive_B" + blockSize + "_C" + c, ref bestMethod, ref rejectionReason);

                    // With morphology
                    Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3));
                    Mat morphThresh = thresh.Clone();
                    Cv2.MorphologyEx(morphThresh, morphThresh, MorphTypes.Close, kernel);
                    Cv2.MorphologyEx(morphThresh, morphThresh, MorphTypes.Open, kernel);
                    ProcessThreshold(morphThresh, ref totalContoursFound, ref bestContour, ref bestBox, ref bestScore,
                        expectedWidthPx, expectedLengthPx, expectedRatio,
                        minAllowedWidthPx, maxAllowedWidthPx,
                        minAllowedLengthPx, maxAllowedLengthPx,
                        frameCenterX, frameCenterY,
                        "Adaptive_Morph_B" + blockSize + "_C" + c, ref bestMethod, ref rejectionReason);
                }
            }

            // Method 2: Otsu Threshold
            Mat otsuThresh = new Mat();
            Cv2.Threshold(gray, otsuThresh, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
            ProcessThreshold(otsuThresh, ref totalContoursFound, ref bestContour, ref bestBox, ref bestScore,
                expectedWidthPx, expectedLengthPx, expectedRatio,
                minAllowedWidthPx, maxAllowedWidthPx,
                minAllowedLengthPx, maxAllowedLengthPx,
                frameCenterX, frameCenterY,
                "Otsu", ref bestMethod, ref rejectionReason);

            // Method 3: Canny Edge Detection
            Mat edges = new Mat();
            for (int threshold1 = 30; threshold1 <= 100; threshold1 += 20)
            {
                for (int threshold2 = 100; threshold2 <= 200; threshold2 += 30)
                {
                    Cv2.Canny(gray, edges, threshold1, threshold2);
                    Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3));
                    Cv2.Dilate(edges, edges, kernel, iterations: 2);
                    Cv2.Erode(edges, edges, kernel, iterations: 1);
                    ProcessThreshold(edges, ref totalContoursFound, ref bestContour, ref bestBox, ref bestScore,
                        expectedWidthPx, expectedLengthPx, expectedRatio,
                        minAllowedWidthPx, maxAllowedWidthPx,
                        minAllowedLengthPx, maxAllowedLengthPx,
                        frameCenterX, frameCenterY,
                        "Canny_T" + threshold1 + "_" + threshold2, ref bestMethod, ref rejectionReason);
                }
            }

            // If no box detected that matches the expected size, return "No Box"
            if (bestContour == null || bestScore < 0)
            {
                System.Diagnostics.Debug.WriteLine($"No valid box detected. Rejection reason: {rejectionReason}");

                using (Graphics g = Graphics.FromImage(resultBmp))
                {
                    string message = $"NO BOX DETECTED\n";
                    if (!string.IsNullOrEmpty(rejectionReason))
                        message += $"Reason: {rejectionReason}\n";
                    message += $"Contours found: {totalContoursFound}\n" +
                               $"Expected W: {expectedWidthPx:F1}px\n" +
                               $"Expected L: {expectedLengthPx:F1}px\n" +
                               $"Expected Ratio: {expectedRatio:F3}";

                    g.DrawString(
                        message,
                        SystemFonts.DefaultFont,
                        Brushes.Red,
                        10,
                        10);
                }

                return new BoxDetectionResult
                {
                    ResultImage = resultBmp,
                    OffsetX = 0,
                    OffsetY = 0,
                    OffsetZ = 0,
                    WidthMM = 0,
                    LengthMM = 0,
                    HeightMM = 0,
                    Angle = 0,
                    ActualWidthMM = ACTUAL_WIDTH_MM,
                    ActualLengthMM = ACTUAL_LENGTH_MM,
                    ActualHeightMM = ACTUAL_HEIGHT_MM,
                    ContourCount = totalContoursFound,
                    DetectionSucceeded = false,
                    DetectionMethod = "None",
                    RejectionReason = rejectionReason
                };
            }

            // Process the detected box
            RotatedRect box = bestBox;

            // Calculate angle
            double angle = box.Angle;
            if (box.Size.Width < box.Size.Height)
            {
                angle += 90.0;
            }

            while (angle >= 90.0)
                angle -= 180.0;
            while (angle < -90.0)
                angle += 180.0;

            // Get box points
            Point2f[] pts = box.Points();

            // Calculate center and dimensions
            int frameCenterX_int = resultBmp.Width / 2;
            int frameCenterY_int = resultBmp.Height / 2;

            int boxCenterX = (int)box.Center.X;
            int boxCenterY = (int)box.Center.Y;

            double detectedWidthPx = Math.Max(box.Size.Width, box.Size.Height);
            double detectedLengthPx = Math.Min(box.Size.Width, box.Size.Height);

            // Convert to millimeters
            double widthMM = detectedWidthPx * mmPerPixel;
            double lengthMM = detectedLengthPx * mmPerPixel;

            // Calculate position offsets
            double offsetX = (boxCenterX - frameCenterX_int) * mmPerPixel;
            double offsetY = (boxCenterY - frameCenterY_int) * mmPerPixel;
            double offsetZ = cameraZ;

            // Draw results on the image
            using (Graphics g = Graphics.FromImage(resultBmp))
            {
                // Draw detected box outline (Lime Green)
                PointF[] drawPoints = pts.Select(p => new PointF(p.X, p.Y)).ToArray();
                g.DrawPolygon(new Pen(Color.Lime, 4), drawPoints);

                // Draw crosshair at center
                g.DrawLine(Pens.Yellow, frameCenterX_int - 10, frameCenterY_int, frameCenterX_int + 10, frameCenterY_int);
                g.DrawLine(Pens.Yellow, frameCenterX_int, frameCenterY_int - 10, frameCenterX_int, frameCenterY_int + 10);

                // Draw information text
                string infoText = $"BOX DETECTED ✓\n" +
                                  $"Method: {bestMethod}\n" +
                                  $"Expected: {ACTUAL_WIDTH_MM}x{ACTUAL_LENGTH_MM}mm\n" +
                                  $"Detected: {widthMM:F1}x{lengthMM:F1}mm\n" +
                                  $"DX: {offsetX:F1} mm\n" +
                                  $"DY: {offsetY:F1} mm\n" +
                                  $"DZ: {offsetZ:F1} mm\n" +
                                  $"ANGLE: {angle:F1}°";

                g.DrawString(
                    infoText,
                    SystemFonts.DefaultFont,
                    Brushes.Lime,
                    10,
                    10);
            }

            return new BoxDetectionResult
            {
                ResultImage = resultBmp,
                OffsetX = offsetX,
                OffsetY = offsetY,
                OffsetZ = offsetZ,
                WidthMM = widthMM,
                LengthMM = lengthMM,
                HeightMM = ACTUAL_HEIGHT_MM,
                Angle = angle,
                ActualWidthMM = ACTUAL_WIDTH_MM,
                ActualLengthMM = ACTUAL_LENGTH_MM,
                ActualHeightMM = ACTUAL_HEIGHT_MM,
                ContourCount = totalContoursFound,
                DetectionSucceeded = true,
                DetectionMethod = bestMethod,
                RejectionReason = ""
            };
        }

        private void ProcessThreshold(Mat thresh, ref int totalContoursFound, ref OpenCvSharp.Point[] bestContour,
            ref RotatedRect bestBox, ref double bestScore,
            double expectedWidthPx, double expectedLengthPx, double expectedRatio,
            double minAllowedWidthPx, double maxAllowedWidthPx,
            double minAllowedLengthPx, double maxAllowedLengthPx,
            double frameCenterX, double frameCenterY,
            string methodName, ref string bestMethod, ref string rejectionReason)
        {
            OpenCvSharp.Point[][] contours;
            HierarchyIndex[] hierarchy;

            Cv2.FindContours(thresh, out contours, out hierarchy, RetrievalModes.List, ContourApproximationModes.ApproxSimple);

            totalContoursFound += contours.Length;

            foreach (var contour in contours)
            {
                double area = Cv2.ContourArea(contour);

                if (area < 300)
                    continue;

                RotatedRect rect = Cv2.MinAreaRect(contour);

                double contourWidthPx = Math.Max(rect.Size.Width, rect.Size.Height);
                double contourLengthPx = Math.Min(rect.Size.Width, rect.Size.Height);

                if (contourWidthPx < 30 || contourLengthPx < 20)
                    continue;

                // ===== STRICT ASPECT RATIO CHECK =====
                double ratio = contourWidthPx / contourLengthPx;
                double ratioDifference = Math.Abs(ratio - expectedRatio) / expectedRatio;

                if (ratioDifference > ASPECT_RATIO_TOLERANCE)
                {
                    // Skip this contour - aspect ratio doesn't match
                    continue;
                }

                // ===== STRICT SIZE CHECK =====
                bool widthInRange = (contourWidthPx >= minAllowedWidthPx && contourWidthPx <= maxAllowedWidthPx);
                bool lengthInRange = (contourLengthPx >= minAllowedLengthPx && contourLengthPx <= maxAllowedLengthPx);

                if (!widthInRange || !lengthInRange)
                {
                    // Skip this contour - size doesn't match expected dimensions
                    string reason = "";
                    if (!widthInRange) reason += $"Width {contourWidthPx:F1}px outside range [{minAllowedWidthPx:F1}-{maxAllowedWidthPx:F1}]px ";
                    if (!lengthInRange) reason += $"Length {contourLengthPx:F1}px outside range [{minAllowedLengthPx:F1}-{maxAllowedLengthPx:F1}]px";
                    System.Diagnostics.Debug.WriteLine($"Rejected by size: {reason}");
                    continue;
                }

                // ===== STRICT SIZE VALIDATION IN MM =====
                double widthMM = contourWidthPx * mmPerPixel;
                double lengthMM = contourLengthPx * mmPerPixel;

                // Check if the detected size is within 20% of expected
                double widthTolerance = ACTUAL_WIDTH_MM * 0.20;
                double lengthTolerance = ACTUAL_LENGTH_MM * 0.20;

                if (Math.Abs(widthMM - ACTUAL_WIDTH_MM) > widthTolerance ||
                    Math.Abs(lengthMM - ACTUAL_LENGTH_MM) > lengthTolerance)
                {
                    System.Diagnostics.Debug.WriteLine($"Rejected by MM size: {widthMM:F1}x{lengthMM:F1}mm (expected {ACTUAL_WIDTH_MM}x{ACTUAL_LENGTH_MM}mm)");
                    continue;
                }

                // Calculate distance from center
                double distanceX = Math.Abs(rect.Center.X - frameCenterX);
                double distanceY = Math.Abs(rect.Center.Y - frameCenterY);

                // Score based on how well it matches
                double widthMatch = 1.0 - Math.Abs(contourWidthPx - expectedWidthPx) / expectedWidthPx;
                double lengthMatch = 1.0 - Math.Abs(contourLengthPx - expectedLengthPx) / expectedLengthPx;
                double sizeScore = (widthMatch + lengthMatch) / 2.0;

                double areaScore = Math.Min(area / 10000, 1.0);
                double centerPenalty = (distanceX / frameCenterX) * 0.3 + (distanceY / frameCenterY) * 0.3;

                double score = (sizeScore * 5000) + (areaScore * 1000) - (centerPenalty * 5000);

                // Only accept if score is positive
                if (score > 0 && score > bestScore)
                {
                    bestScore = score;
                    bestContour = contour;
                    bestBox = rect;
                    bestMethod = methodName;
                    rejectionReason = "";

                    System.Diagnostics.Debug.WriteLine($"New best: {methodName}, Score={score:F0}, W={contourWidthPx:F1}px ({widthMM:F1}mm), L={contourLengthPx:F1}px ({lengthMM:F1}mm)");
                }
            }
        }
    }
}