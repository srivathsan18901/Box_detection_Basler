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
    }

    public class BoxDetectionService
    {
        private const double ACTUAL_WIDTH_MM = 300;
        private const double ACTUAL_LENGTH_MM = 250;
        private const double ACTUAL_HEIGHT_MM = 300;
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

            // Convert to OpenCV Mat - process the FULL image
            Mat src = BitmapConverter.ToMat(source);

            // Log image dimensions for debugging
            System.Diagnostics.Debug.WriteLine($"Image Dimensions: {src.Width} x {src.Height}");

            // Convert to grayscale
            Mat gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            // Calculate expected dimensions
            double expectedWidthPx = ACTUAL_WIDTH_MM / mmPerPixel;
            double expectedLengthPx = ACTUAL_LENGTH_MM / mmPerPixel;
            double expectedRatio = ACTUAL_WIDTH_MM / ACTUAL_LENGTH_MM;

            // Calculate frame center
            double frameCenterX = src.Width / 2.0;
            double frameCenterY = src.Height / 2.0;

            // Variables for best match
            RotatedRect bestBox = new RotatedRect();
            OpenCvSharp.Point[] bestContour = null;
            double bestScore = double.MinValue;
            int totalContoursFound = 0;
            string bestMethod = "None";

            // ---- TRY MULTIPLE DETECTION METHODS ----

            // Method 1: Adaptive Threshold with different parameters
            for (int blockSize = 11; blockSize <= 31; blockSize += 10)
            {
                for (int c = 2; c <= 10; c += 4)
                {
                    Mat thresh = new Mat();
                    Mat blurred = new Mat();
                    Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(5, 5), 0);
                    Cv2.AdaptiveThreshold(blurred, thresh, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.BinaryInv, blockSize, c);

                    // Try both with and without morphology
                    ProcessThreshold(thresh, ref totalContoursFound, ref bestContour, ref bestBox, ref bestScore,
                        expectedWidthPx, expectedLengthPx, expectedRatio, frameCenterX, frameCenterY,
                        "Adaptive_B" + blockSize + "_C" + c, ref bestMethod);

                    // With morphology
                    Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3));
                    Mat morphThresh = thresh.Clone();
                    Cv2.MorphologyEx(morphThresh, morphThresh, MorphTypes.Close, kernel);
                    Cv2.MorphologyEx(morphThresh, morphThresh, MorphTypes.Open, kernel);
                    ProcessThreshold(morphThresh, ref totalContoursFound, ref bestContour, ref bestBox, ref bestScore,
                        expectedWidthPx, expectedLengthPx, expectedRatio, frameCenterX, frameCenterY,
                        "Adaptive_Morph_B" + blockSize + "_C" + c, ref bestMethod);
                }
            }

            // Method 2: Simple Threshold (Otsu)
            Mat otsuThresh = new Mat();
            Cv2.Threshold(gray, otsuThresh, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
            ProcessThreshold(otsuThresh, ref totalContoursFound, ref bestContour, ref bestBox, ref bestScore,
                expectedWidthPx, expectedLengthPx, expectedRatio, frameCenterX, frameCenterY,
                "Otsu", ref bestMethod);

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
                        expectedWidthPx, expectedLengthPx, expectedRatio, frameCenterX, frameCenterY,
                        "Canny_T" + threshold1 + "_" + threshold2, ref bestMethod);
                }
            }

            // Method 4: Morphological Gradient
            Mat gradient = new Mat();
            Mat kernelGrad = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3));
            Cv2.MorphologyEx(gray, gradient, MorphTypes.Gradient, kernelGrad);
            for (int threshVal = 10; threshVal <= 50; threshVal += 10)
            {
                Mat gradThresh = new Mat();
                Cv2.Threshold(gradient, gradThresh, threshVal, 255, ThresholdTypes.Binary);
                ProcessThreshold(gradThresh, ref totalContoursFound, ref bestContour, ref bestBox, ref bestScore,
                    expectedWidthPx, expectedLengthPx, expectedRatio, frameCenterX, frameCenterY,
                    "Gradient_T" + threshVal, ref bestMethod);
            }

            // Method 5: Find largest rectangle by area (fallback)
            Mat simpleThresh = new Mat();
            Cv2.Threshold(gray, simpleThresh, 127, 255, ThresholdTypes.BinaryInv);
            Mat kernelClose = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(5, 5));
            Cv2.MorphologyEx(simpleThresh, simpleThresh, MorphTypes.Close, kernelClose);

            OpenCvSharp.Point[][] allContours;
            HierarchyIndex[] allHierarchy;
            Cv2.FindContours(simpleThresh, out allContours, out allHierarchy, RetrievalModes.List, ContourApproximationModes.ApproxSimple);

            foreach (var contour in allContours)
            {
                double area = Cv2.ContourArea(contour);
                if (area < 500) continue;

                RotatedRect rect = Cv2.MinAreaRect(contour);
                double cWidth = Math.Max(rect.Size.Width, rect.Size.Height);
                double cLength = Math.Min(rect.Size.Width, rect.Size.Height);

                // Very relaxed criteria for fallback
                if (cWidth < 50 || cLength < 30) continue;

                double ratio = cWidth / cLength;
                if (Math.Abs(ratio - expectedRatio) > 0.5) continue;

                if (area > 5000) // Prefer larger areas
                {
                    double score = area / 1000 + (1 - Math.Abs(ratio - expectedRatio)) * 100;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestContour = contour;
                        bestBox = rect;
                        bestMethod = "Fallback_Largest";
                    }
                }
            }

            // If no box detected, return default result with debug info
            if (bestContour == null)
            {
                System.Diagnostics.Debug.WriteLine($"No box detected. Total contours found: {totalContoursFound}");

                // Draw debug info on result image
                using (Graphics g = Graphics.FromImage(resultBmp))
                {
                    g.DrawString(
                        $"NO BOX DETECTED\n" +
                        $"Contours: {totalContoursFound}\n" +
                        $"MM/PX: {mmPerPixel:F4}\n" +
                        $"Expected W: {expectedWidthPx:F1}px\n" +
                        $"Expected L: {expectedLengthPx:F1}px",
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
                    DetectionMethod = "None"
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

            // Normalize angle to -90° to +90°
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

            // Use different variable names to avoid conflicts
            double detectedWidthPx = Math.Max(box.Size.Width, box.Size.Height);
            double detectedLengthPx = Math.Min(box.Size.Width, box.Size.Height);

            // Convert to millimeters using calibrated MM/PX
            double widthMM = detectedWidthPx * mmPerPixel;
            double lengthMM = detectedLengthPx * mmPerPixel;

            // Calculate position offsets in millimeters
            double offsetX = (boxCenterX - frameCenterX_int) * mmPerPixel;
            double offsetY = (boxCenterY - frameCenterY_int) * mmPerPixel;

            // Use the camera Z value (depth from 3D camera)
            double offsetZ = cameraZ;

            // Draw results on the image
            using (Graphics g = Graphics.FromImage(resultBmp))
            {
                // Draw expected box outline (green)
                int expectedWidthPx_int = (int)(ACTUAL_WIDTH_MM / mmPerPixel);
                int expectedLengthPx_int = (int)(ACTUAL_LENGTH_MM / mmPerPixel);

                Rectangle expectedRect = new Rectangle(
                    frameCenterX_int - expectedWidthPx_int / 2,
                    frameCenterY_int - expectedLengthPx_int / 2,
                    expectedWidthPx_int,
                    expectedLengthPx_int);

                g.DrawRectangle(new Pen(Color.Lime, 2), expectedRect);

                // Draw detected box outline (red)
                PointF[] drawPoints = pts.Select(p => new PointF(p.X, p.Y)).ToArray();
                g.DrawPolygon(new Pen(Color.Red, 4), drawPoints);

                // Draw crosshair at center
                g.DrawLine(Pens.Yellow, frameCenterX_int - 10, frameCenterY_int, frameCenterX_int + 10, frameCenterY_int);
                g.DrawLine(Pens.Yellow, frameCenterX_int, frameCenterY_int - 10, frameCenterX_int, frameCenterY_int + 10);

                // Draw information text
                string infoText = $"BOX DETECTED ✓\n" +
                                  $"Method: {bestMethod}\n" +
                                  $"W: {widthMM:F1} mm ({detectedWidthPx:F0}px)\n" +
                                  $"L: {lengthMM:F1} mm ({detectedLengthPx:F0}px)\n" +
                                  $"DX: {offsetX:F1} mm\n" +
                                  $"DY: {offsetY:F1} mm\n" +
                                  $"DZ: {offsetZ:F1} mm\n" +
                                  $"ANGLE: {angle:F1}°";

                g.DrawString(
                    infoText,
                    SystemFonts.DefaultFont,
                    Brushes.Red,
                    10,
                    10);
            }

            // Return the result
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
                DetectionMethod = bestMethod
            };
        }

        private void ProcessThreshold(Mat thresh, ref int totalContoursFound, ref OpenCvSharp.Point[] bestContour,
            ref RotatedRect bestBox, ref double bestScore, double expectedWidthPx, double expectedLengthPx,
            double expectedRatio, double frameCenterX, double frameCenterY, string methodName, ref string bestMethod)
        {
            OpenCvSharp.Point[][] contours;
            HierarchyIndex[] hierarchy;

            Cv2.FindContours(thresh, out contours, out hierarchy, RetrievalModes.List, ContourApproximationModes.ApproxSimple);

            totalContoursFound += contours.Length;

            foreach (var contour in contours)
            {
                double area = Cv2.ContourArea(contour);

                // Very relaxed minimum area
                if (area < 300)
                    continue;

                RotatedRect rect = Cv2.MinAreaRect(contour);

                double contourWidthPx = Math.Max(rect.Size.Width, rect.Size.Height);
                double contourLengthPx = Math.Min(rect.Size.Width, rect.Size.Height);

                // Very relaxed minimum size
                if (contourWidthPx < 30 || contourLengthPx < 20)
                    continue;

                // Very relaxed aspect ratio check
                double ratio = contourWidthPx / contourLengthPx;
                if (Math.Abs(ratio - expectedRatio) > 0.6)
                    continue;

                // Check physical size with very relaxed tolerance
                double widthDifference = Math.Abs(contourWidthPx - expectedWidthPx) / expectedWidthPx;
                double lengthDifference = Math.Abs(contourLengthPx - expectedLengthPx) / expectedLengthPx;

                if (widthDifference > 0.70 || lengthDifference > 0.70)
                    continue;

                // Calculate distance from center
                double distanceX = Math.Abs(rect.Center.X - frameCenterX);
                double distanceY = Math.Abs(rect.Center.Y - frameCenterY);

                // Score the contour - prioritize area and size match
                double sizeScore = 1.0 - (widthDifference + lengthDifference) / 2.0;
                double areaScore = Math.Min(area / 10000, 1.0);
                double centerPenalty = distanceX * 0.2 + distanceY * 0.2;
                double score = (sizeScore * 5000) + (areaScore * 1000) - centerPenalty;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestContour = contour;
                    bestBox = rect;
                    bestMethod = methodName;

                    System.Diagnostics.Debug.WriteLine($"New best: {methodName}, Score={score:F0}, Area={area:F0}, W={contourWidthPx:F1}, L={contourLengthPx:F1}");
                }
            }
        }
    }
}