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

        // Validation info
        public bool SizeValidated { get; set; }
        public string ValidationMessage { get; set; }

        // Reference box info
        public double ReferenceWidthMM { get; set; }
        public double ReferenceLengthMM { get; set; }
        public double SizeDeviationPercent { get; set; }

        // RX Rotation (tilt around X-axis)
        public double RX { get; set; }  // Rotation around X-axis in degrees
        public bool RXValid { get; set; }
    }

    public class BoxDetectionService
    {
        // These are now REFERENCE values, not fixed constants
        // They will be updated during calibration
        private double referenceWidthMM = 355;
        private double referenceLengthMM = 305;
        private double referenceHeightMM = 410;

        // Maximum allowed size deviation from reference (%)
        private const double MAX_SIZE_DEVIATION_PERCENT = 50.0;
        private const double MIN_SIZE_DEVIATION_PERCENT = 5.0; // Minimum deviation to validate

        private const double DEFAULT_MM_PER_PIXEL = 1.28;
        private double mmPerPixel = DEFAULT_MM_PER_PIXEL;

        // Store point cloud data for RX calculation
        private float[] latestPointCloud;
        private int latestWidth;
        private int latestHeight;

        public double MmPerPixel => mmPerPixel;

        // Properties to get/set reference dimensions
        public double ReferenceWidthMM
        {
            get => referenceWidthMM;
            set
            {
                if (value > 0) referenceWidthMM = value;
            }
        }

        public double ReferenceLengthMM
        {
            get => referenceLengthMM;
            set
            {
                if (value > 0) referenceLengthMM = value;
            }
        }

        public double ReferenceHeightMM
        {
            get => referenceHeightMM;
            set
            {
                if (value > 0) referenceHeightMM = value;
            }
        }

        private class ZCalibrationPoint
        {
            public double Z { get; set; }
            public double MmPerPixel { get; set; }
        }

        private readonly List<ZCalibrationPoint> zCalibrationPoints = new List<ZCalibrationPoint>();

        public void AddZCalibration(double z, double newMmPerPixel)
        {
            if (z <= 0)
                throw new ArgumentException("Z value must be greater than zero.");

            if (newMmPerPixel <= 0)
                throw new ArgumentException("MM per pixel must be greater than zero.");

            const double zTolerance = 1.0;

            var existing = zCalibrationPoints
                .FirstOrDefault(p => Math.Abs(p.Z - z) <= zTolerance);

            if (existing != null)
            {
                existing.Z = z;
                existing.MmPerPixel = newMmPerPixel;
            }
            else
            {
                zCalibrationPoints.Add(new ZCalibrationPoint
                {
                    Z = z,
                    MmPerPixel = newMmPerPixel
                });
            }

            zCalibrationPoints.Sort((a, b) => a.Z.CompareTo(b.Z));
            mmPerPixel = newMmPerPixel;
        }

        public double GetMmPerPixelForZ(double z)
        {
            if (z <= 0)
                return mmPerPixel;

            if (zCalibrationPoints.Count == 0)
                return mmPerPixel;

            if (zCalibrationPoints.Count == 1)
                return zCalibrationPoints[0].MmPerPixel;

            if (z <= zCalibrationPoints[0].Z)
                return zCalibrationPoints[0].MmPerPixel;

            if (z >= zCalibrationPoints[^1].Z)
                return zCalibrationPoints[^1].MmPerPixel;

            for (int i = 0; i < zCalibrationPoints.Count - 1; i++)
            {
                var p1 = zCalibrationPoints[i];
                var p2 = zCalibrationPoints[i + 1];

                if (z >= p1.Z && z <= p2.Z)
                {
                    double zRange = p2.Z - p1.Z;

                    if (Math.Abs(zRange) < 0.000001)
                        return p1.MmPerPixel;

                    double ratio = (z - p1.Z) / zRange;

                    return p1.MmPerPixel +
                           ratio * (p2.MmPerPixel - p1.MmPerPixel);
                }
            }

            return mmPerPixel;
        }

        public void SetMmPerPixel(double value)
        {
            if (value <= 0)
                throw new ArgumentException("MM per pixel must be greater than zero.");

            mmPerPixel = value;
        }

        /// <summary>
        /// Calculate RX (rotation around X-axis) from point cloud data
        /// RX represents the tilt of the box forward/backward
        /// </summary>
        private double CalculateRX(float[] pointCloud, int width, int height, RotatedRect box)
        {
            if (pointCloud == null || pointCloud.Length == 0)
                return 0;

            // Get the four corners of the box in pixel coordinates
            Point2f[] corners = box.Points();

            // Collect Z values from the corners
            List<(Point2f point, float z)> cornerData = new List<(Point2f, float)>();

            foreach (var corner in corners)
            {
                int px = (int)Math.Round(corner.X);
                int py = (int)Math.Round(corner.Y);

                if (px < 0 || px >= width || py < 0 || py >= height)
                    continue;

                int pixelIndex = py * width + px;
                int pointIndex = pixelIndex * 3;

                if (pointIndex + 2 >= pointCloud.Length)
                    continue;

                float z = pointCloud[pointIndex + 2];

                if (float.IsNaN(z) || float.IsInfinity(z) || z <= 0)
                    continue;

                cornerData.Add((corner, z));
            }

            if (cornerData.Count < 4)
                return 0;

            // Order corners by position
            var sortedCorners = cornerData
                .OrderBy(c => c.point.Y)
                .ThenBy(c => c.point.X)
                .ToList();

            // Calculate RX (tilt around X-axis)
            // RX is the difference in Z between top and bottom of the box
            // Positive RX = box tilted forward (top closer to camera)
            // Negative RX = box tilted backward (top further from camera)

            // Get average Z of top corners (top of box = lower Y values)
            double topZ = sortedCorners.Take(2).Average(c => c.z);

            // Get average Z of bottom corners (bottom of box = higher Y values)
            double bottomZ = sortedCorners.Skip(2).Take(2).Average(c => c.z);

            // Calculate the height of the box in pixels (in Y direction)
            double boxHeightPx = Math.Min(box.Size.Width, box.Size.Height);

            if (boxHeightPx == 0)
                return 0;

            // Calculate RX angle in degrees
            // Using arctan of (Z difference / physical height in mm)
            double heightMM = boxHeightPx * mmPerPixel;
            double deltaZ = bottomZ - topZ;

            // RX angle = arctan(deltaZ / heightMM)
            double rx = Math.Atan2(deltaZ, heightMM) * 180.0 / Math.PI;

            // Clamp to reasonable range
            rx = Math.Clamp(rx, -45.0, 45.0);

            System.Diagnostics.Debug.WriteLine($"RX Calculation:");
            System.Diagnostics.Debug.WriteLine($"  Top Z: {topZ:F2}mm, Bottom Z: {bottomZ:F2}mm");
            System.Diagnostics.Debug.WriteLine($"  Delta Z: {deltaZ:F2}mm, Height: {heightMM:F2}mm");
            System.Diagnostics.Debug.WriteLine($"  RX: {rx:F2}°");

            return rx;
        }

        public BoxDetectionResult DetectBox(Bitmap source, float cameraZ)
        {
            Bitmap resultBmp = (Bitmap)source.Clone();
            Mat src = BitmapConverter.ToMat(source);

            // Use current reference dimensions
            double expectedWidthPx = referenceWidthMM / mmPerPixel;
            double expectedLengthPx = referenceLengthMM / mmPerPixel;
            double expectedRatio = referenceWidthMM / referenceLengthMM;

            System.Diagnostics.Debug.WriteLine($"=== DETECTION PARAMETERS ===");
            System.Diagnostics.Debug.WriteLine($"Reference: W={referenceWidthMM}mm, L={referenceLengthMM}mm, H={referenceHeightMM}mm");
            System.Diagnostics.Debug.WriteLine($"MM/PX: {mmPerPixel:F4}");
            System.Diagnostics.Debug.WriteLine($"Expected W: {expectedWidthPx:F1}px, L: {expectedLengthPx:F1}px");
            System.Diagnostics.Debug.WriteLine($"Expected Ratio: {expectedRatio:F3}");

            // Convert to grayscale
            Mat gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            // Calculate frame center
            double frameCenterX = src.Width / 2.0;
            double frameCenterY = src.Height / 2.0;

            // Variables for best match
            RotatedRect bestBox = new RotatedRect();
            OpenCvSharp.Point[] bestContour = null;
            double bestScore = double.MinValue;
            int totalContoursFound = 0;
            string bestMethod = "None";
            double bestWidthMM = 0;
            double bestLengthMM = 0;
            bool sizeValidated = false;
            string validationMessage = "";

            // ---- DETECTION METHODS ----
            // Method 1: Adaptive Threshold
            for (int blockSize = 11; blockSize <= 31; blockSize += 10)
            {
                for (int c = 2; c <= 10; c += 4)
                {
                    Mat thresh = new Mat();
                    Mat blurred = new Mat();
                    Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(5, 5), 0);
                    Cv2.AdaptiveThreshold(blurred, thresh, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, blockSize, c);

                    ProcessThreshold(thresh, ref totalContoursFound, ref bestContour, ref bestBox, ref bestScore,
                        expectedWidthPx, expectedLengthPx, expectedRatio, frameCenterX, frameCenterY,
                        "Adaptive_B" + blockSize + "_C" + c, ref bestMethod,
                        ref bestWidthMM, ref bestLengthMM, ref sizeValidated, ref validationMessage);

                    // With morphology
                    Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3));
                    Mat morphThresh = thresh.Clone();
                    Cv2.MorphologyEx(morphThresh, morphThresh, MorphTypes.Close, kernel);
                    Cv2.MorphologyEx(morphThresh, morphThresh, MorphTypes.Open, kernel);
                    ProcessThreshold(morphThresh, ref totalContoursFound, ref bestContour, ref bestBox, ref bestScore,
                        expectedWidthPx, expectedLengthPx, expectedRatio, frameCenterX, frameCenterY,
                        "Adaptive_Morph_B" + blockSize + "_C" + c, ref bestMethod,
                        ref bestWidthMM, ref bestLengthMM, ref sizeValidated, ref validationMessage);
                }
            }

            // Method 2: Otsu Threshold
            Mat otsuThresh = new Mat();
            Cv2.Threshold(gray, otsuThresh, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
            ProcessThreshold(otsuThresh, ref totalContoursFound, ref bestContour, ref bestBox, ref bestScore,
                expectedWidthPx, expectedLengthPx, expectedRatio, frameCenterX, frameCenterY,
                "Otsu", ref bestMethod, ref bestWidthMM, ref bestLengthMM, ref sizeValidated, ref validationMessage);

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
                        "Canny_T" + threshold1 + "_" + threshold2, ref bestMethod,
                        ref bestWidthMM, ref bestLengthMM, ref sizeValidated, ref validationMessage);
                }
            }

            // If no box detected, return default result
            if (bestContour == null)
            {
                System.Diagnostics.Debug.WriteLine($"No box detected. Total contours found: {totalContoursFound}");

                using (Graphics g = Graphics.FromImage(resultBmp))
                {
                    g.DrawString(
                        $"NO BOX DETECTED\n" +
                        $"Contours: {totalContoursFound}\n" +
                        $"Reference: {referenceWidthMM}x{referenceLengthMM}mm\n" +
                        $"Deviation threshold: ±{MAX_SIZE_DEVIATION_PERCENT}%",
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
                    ActualWidthMM = referenceWidthMM,
                    ActualLengthMM = referenceLengthMM,
                    ActualHeightMM = referenceHeightMM,
                    ContourCount = totalContoursFound,
                    DetectionSucceeded = false,
                    DetectionMethod = "None",
                    SizeValidated = false,
                    ValidationMessage = "No box contour found",
                    ReferenceWidthMM = referenceWidthMM,
                    ReferenceLengthMM = referenceLengthMM,
                    SizeDeviationPercent = 100,
                    RX = 0,
                    RXValid = false
                };
            }

            // Process the detected box
            RotatedRect box = bestBox;

            // Calculate angle (RZ - rotation around Z-axis)
            double angle = box.Angle;
            if (box.Size.Width < box.Size.Height)
            {
                angle += 90.0;
            }

            while (angle >= 90.0)
                angle -= 180.0;
            while (angle < -90.0)
                angle += 180.0;

            Point2f[] pts = box.Points();

            int frameCenterX_int = resultBmp.Width / 2;
            int frameCenterY_int = resultBmp.Height / 2;

            int boxCenterX = (int)box.Center.X;
            int boxCenterY = (int)box.Center.Y;

            double detectedWidthPx = Math.Max(box.Size.Width, box.Size.Height);
            double detectedLengthPx = Math.Min(box.Size.Width, box.Size.Height);

            double widthMM = detectedWidthPx * mmPerPixel;
            double lengthMM = detectedLengthPx * mmPerPixel;

            double offsetX = (boxCenterX - frameCenterX_int) * mmPerPixel;
            double offsetY = (boxCenterY - frameCenterY_int) * mmPerPixel;
            double offsetZ = cameraZ;

            // Calculate RX (rotation around X-axis) from point cloud data
            double rx = 0;
            bool rxValid = false;

            // Try to calculate RX if point cloud data is available
            if (latestPointCloud != null && latestWidth > 0 && latestHeight > 0)
            {
                rx = CalculateRX(latestPointCloud, latestWidth, latestHeight, box);
                rxValid = true;
            }
            else
            {
                // If no point cloud data, estimate RX from the 2D image
                // Using perspective distortion of the box
                rx = EstimateRXFrom2D(pts, widthMM, lengthMM);
                rxValid = true;
                System.Diagnostics.Debug.WriteLine($"RX estimated from 2D: {rx:F2}°");
            }

            // Calculate size deviation
            double widthDeviation = Math.Abs(widthMM - referenceWidthMM) / referenceWidthMM * 100;
            double lengthDeviation = Math.Abs(lengthMM - referenceLengthMM) / referenceLengthMM * 100;
            double avgDeviation = (widthDeviation + lengthDeviation) / 2;

            // VALIDATE: Check if detected size is within acceptable range
            bool isValidSize = (widthDeviation <= MAX_SIZE_DEVIATION_PERCENT) &&
                               (lengthDeviation <= MAX_SIZE_DEVIATION_PERCENT);

            string validationMsg = isValidSize
                ? $"Size validated: Deviation {avgDeviation:F1}% (within {MAX_SIZE_DEVIATION_PERCENT}%)"
                : $"Size REJECTED: Deviation {avgDeviation:F1}% (exceeds {MAX_SIZE_DEVIATION_PERCENT}%)";

            System.Diagnostics.Debug.WriteLine($"Validation: {validationMsg}");
            System.Diagnostics.Debug.WriteLine($"  Width: {widthMM:F1}mm (deviation {widthDeviation:F1}%)");
            System.Diagnostics.Debug.WriteLine($"  Length: {lengthMM:F1}mm (deviation {lengthDeviation:F1}%)");
            System.Diagnostics.Debug.WriteLine($"  RX: {rx:F2}°");

            // If size is not valid, treat as no detection
            if (!isValidSize)
            {
                using (Graphics g = Graphics.FromImage(resultBmp))
                {
                    g.DrawString(
                        $"BOX REJECTED - Size Mismatch\n" +
                        $"Detected: {widthMM:F1}x{lengthMM:F1}mm\n" +
                        $"Expected: {referenceWidthMM}x{referenceLengthMM}mm\n" +
                        $"Deviation: {avgDeviation:F1}% (max {MAX_SIZE_DEVIATION_PERCENT}%)",
                        SystemFonts.DefaultFont,
                        Brushes.Orange,
                        10,
                        10);

                    // Still draw the detected box but in orange to show rejection
                    PointF[] drawPoints = pts.Select(p => new PointF(p.X, p.Y)).ToArray();
                    g.DrawPolygon(new Pen(Color.Orange, 3), drawPoints);
                }

                return new BoxDetectionResult
                {
                    ResultImage = resultBmp,
                    OffsetX = offsetX,
                    OffsetY = offsetY,
                    OffsetZ = offsetZ,
                    WidthMM = widthMM,
                    LengthMM = lengthMM,
                    HeightMM = referenceHeightMM,
                    Angle = angle,
                    ActualWidthMM = referenceWidthMM,
                    ActualLengthMM = referenceLengthMM,
                    ActualHeightMM = referenceHeightMM,
                    ContourCount = totalContoursFound,
                    DetectionSucceeded = false,
                    DetectionMethod = bestMethod,
                    SizeValidated = false,
                    ValidationMessage = validationMsg,
                    ReferenceWidthMM = referenceWidthMM,
                    ReferenceLengthMM = referenceLengthMM,
                    SizeDeviationPercent = avgDeviation,
                    RX = rx,
                    RXValid = rxValid
                };
            }

            // Draw results on the image - SUCCESSFUL DETECTION
            using (Graphics g = Graphics.FromImage(resultBmp))
            {
                PointF[] drawPoints = pts.Select(p => new PointF(p.X, p.Y)).ToArray();
                g.DrawPolygon(new Pen(Color.Lime, 4), drawPoints);

                string rxText = rxValid ? $"RX: {rx:F1}°" : "RX: N/A";

                string infoText = $"BOX DETECTED ✓\n" +
                                  $"Method: {bestMethod}\n" +
                                  $"W: {widthMM:F1} mm ({detectedWidthPx:F0}px)\n" +
                                  $"L: {lengthMM:F1} mm ({detectedLengthPx:F0}px)\n" +
                                  $"DX: {offsetX:F1} mm\n" +
                                  $"DY: {offsetY:F1} mm\n" +
                                  $"DZ: {offsetZ:F1} mm\n" +
                                  $"RZ: {angle:F1}°\n" +
                                  $"{rxText}\n" +
                                  $"Deviation: {avgDeviation:F1}%";

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
                HeightMM = referenceHeightMM,
                Angle = angle,
                ActualWidthMM = referenceWidthMM,
                ActualLengthMM = referenceLengthMM,
                ActualHeightMM = referenceHeightMM,
                ContourCount = totalContoursFound,
                DetectionSucceeded = true,
                DetectionMethod = bestMethod,
                SizeValidated = true,
                ValidationMessage = validationMsg,
                ReferenceWidthMM = referenceWidthMM,
                ReferenceLengthMM = referenceLengthMM,
                SizeDeviationPercent = avgDeviation,
                RX = rx,
                RXValid = rxValid
            };
        }

        /// <summary>
        /// Estimate RX (tilt around X-axis) from 2D image perspective distortion
        /// This is used when point cloud data is not available
        /// </summary>
        private double EstimateRXFrom2D(Point2f[] corners, double widthMM, double lengthMM)
        {
            try
            {
                // Order corners: Top-Left, Top-Right, Bottom-Right, Bottom-Left
                var sorted = corners.OrderBy(p => p.Y).ThenBy(p => p.X).ToList();

                if (sorted.Count < 4)
                    return 0;

                // Top edge length
                double topEdge = Math.Sqrt(
                    Math.Pow(sorted[0].X - sorted[1].X, 2) +
                    Math.Pow(sorted[0].Y - sorted[1].Y, 2));

                // Bottom edge length
                double bottomEdge = Math.Sqrt(
                    Math.Pow(sorted[2].X - sorted[3].X, 2) +
                    Math.Pow(sorted[2].Y - sorted[3].Y, 2));

                // If bottom edge is longer than top edge, box is tilted away
                // If top edge is longer than bottom edge, box is tilted toward camera
                double ratio = bottomEdge / topEdge;

                // Estimate RX based on perspective distortion
                double rx = 0;
                if (ratio > 1.0)
                {
                    // Tilted away (top closer to camera, bottom further)
                    rx = -Math.Min((ratio - 1.0) * 30.0, 30.0);
                }
                else if (ratio < 1.0)
                {
                    // Tilted toward (top further, bottom closer)
                    rx = Math.Min((1.0 - ratio) * 30.0, 30.0);
                }

                return Math.Clamp(rx, -45.0, 45.0);
            }
            catch
            {
                return 0;
            }
        }

        private void ProcessThreshold(Mat thresh, ref int totalContoursFound, ref OpenCvSharp.Point[] bestContour,
            ref RotatedRect bestBox, ref double bestScore, double expectedWidthPx, double expectedLengthPx,
            double expectedRatio, double frameCenterX, double frameCenterY, string methodName, ref string bestMethod,
            ref double bestWidthMM, ref double bestLengthMM, ref bool sizeValidated, ref string validationMessage)
        {
            OpenCvSharp.Point[][] contours;
            HierarchyIndex[] hierarchy;

            Cv2.FindContours(thresh, out contours, out hierarchy, RetrievalModes.List, ContourApproximationModes.ApproxSimple);

            totalContoursFound += contours.Length;

            foreach (var contour in contours)
            {
                double area = Cv2.ContourArea(contour);

                // Filter by area
                if (area < 500 || area > 500000)
                    continue;

                RotatedRect rect = Cv2.MinAreaRect(contour);

                double contourWidthPx = Math.Max(rect.Size.Width, rect.Size.Height);
                double contourLengthPx = Math.Min(rect.Size.Width, rect.Size.Height);

                if (contourWidthPx < 30 || contourLengthPx < 20)
                    continue;

                // Aspect ratio check with relaxed tolerance
                double ratio = contourWidthPx / contourLengthPx;
                double ratioTolerance = 0.8;

                if (Math.Abs(ratio - expectedRatio) > ratioTolerance)
                    continue;

                // Check physical size - but don't reject yet, just score lower
                double widthMM = contourWidthPx * mmPerPixel;
                double lengthMM = contourLengthPx * mmPerPixel;

                double widthDeviation = Math.Abs(widthMM - referenceWidthMM) / referenceWidthMM;
                double lengthDeviation = Math.Abs(lengthMM - referenceLengthMM) / referenceLengthMM;

                // If size is way off, heavily penalize
                double sizePenalty = 0;
                if (widthDeviation > 0.5 || lengthDeviation > 0.5)
                {
                    sizePenalty = (widthDeviation + lengthDeviation) * 1000;
                }

                // Calculate distance from center
                double distanceX = Math.Abs(rect.Center.X - frameCenterX);
                double distanceY = Math.Abs(rect.Center.Y - frameCenterY);

                // Score the contour
                double sizeMatchScore = 1.0 - (widthDeviation + lengthDeviation) / 2.0;
                double areaScore = Math.Min(area / 50000, 1.0);
                double centerScore = 1.0 - (distanceX + distanceY) / (frameCenterX + frameCenterY);
                double score = (sizeMatchScore * 5000) + (areaScore * 2000) + (centerScore * 1000) - sizePenalty;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestContour = contour;
                    bestBox = rect;
                    bestMethod = methodName;
                    bestWidthMM = widthMM;
                    bestLengthMM = lengthMM;

                    System.Diagnostics.Debug.WriteLine($"Candidate: {methodName}, Score={score:F0}, W={widthMM:F1}mm, L={lengthMM:F1}mm, Dev={((widthDeviation + lengthDeviation) / 2 * 100):F1}%");
                }
            }
        }

        // Method to manually set reference dimensions from UI
        public void SetReferenceDimensions(double widthMM, double lengthMM, double heightMM)
        {
            if (widthMM > 0) referenceWidthMM = widthMM;
            if (lengthMM > 0) referenceLengthMM = lengthMM;
            if (heightMM > 0) referenceHeightMM = heightMM;

            System.Diagnostics.Debug.WriteLine($"Reference dimensions updated: {referenceWidthMM}x{referenceLengthMM}x{referenceHeightMM}mm");
        }

        /// <summary>
        /// Update point cloud data for RX calculation
        /// </summary>
        public void UpdatePointCloud(float[] pointCloud, int width, int height)
        {
            latestPointCloud = pointCloud;
            latestWidth = width;
            latestHeight = height;
        }
    }
}