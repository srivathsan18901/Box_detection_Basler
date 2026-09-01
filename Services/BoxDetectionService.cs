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

            // Convert to OpenCV Mat - process the FULL image without ROI restriction
            Mat src = BitmapConverter.ToMat(source);

            // Store original dimensions for coordinate calculations
            int originalWidth = src.Width;
            int originalHeight = src.Height;

            // Convert to grayscale
            Mat gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            // Apply Gaussian blur to reduce noise
            Mat blurred = new Mat();
            Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(5, 5), 0);

            // Apply adaptive threshold for better segmentation
            Mat thresh = new Mat();
            Cv2.AdaptiveThreshold(
                blurred,
                thresh,
                255,
                AdaptiveThresholdTypes.GaussianC,
                ThresholdTypes.BinaryInv,
                21,
                5);

            // Morphological operations to clean up the binary image
            Mat kernel = Cv2.GetStructuringElement(
                MorphShapes.Rect,
                new OpenCvSharp.Size(3, 3));

            Cv2.Dilate(thresh, thresh, kernel, iterations: 1);
            Cv2.MorphologyEx(thresh, thresh, MorphTypes.Close, kernel);

            // Find contours in the FULL image
            OpenCvSharp.Point[][] contours;
            HierarchyIndex[] hierarchy;

            Cv2.FindContours(
                thresh,
                out contours,
                out hierarchy,
                RetrievalModes.List,
                ContourApproximationModes.ApproxSimple);

            // Variables for best match
            RotatedRect bestBox = new RotatedRect();
            OpenCvSharp.Point[] bestContour = null;
            double bestScore = double.MinValue;

            // Calculate frame center (full image)
            double frameCenterX = src.Width / 2.0;
            double frameCenterY = src.Height / 2.0;

            // Expected box dimensions in pixels
            double expectedWidthPx = ACTUAL_WIDTH_MM / mmPerPixel;
            double expectedLengthPx = ACTUAL_LENGTH_MM / mmPerPixel;
            double expectedRatio = ACTUAL_WIDTH_MM / ACTUAL_LENGTH_MM;

            foreach (var contour in contours)
            {
                double area = Cv2.ContourArea(contour);

                // Minimum area threshold to filter out noise
                if (area < 3000)
                    continue;

                // Get minimum area rectangle
                RotatedRect rect = Cv2.MinAreaRect(contour);

                // Get width and height (normalize so width >= height)
                double contourWidthPx = Math.Max(rect.Size.Width, rect.Size.Height);
                double contourLengthPx = Math.Min(rect.Size.Width, rect.Size.Height);

                // Filter out very small rectangles
                if (contourWidthPx < 50 || contourLengthPx < 50)
                    continue;

                // Check aspect ratio with tolerance
                double ratio = contourWidthPx / contourLengthPx;
                if (Math.Abs(ratio - expectedRatio) > 0.20)
                    continue;

                // Check physical size with tolerance
                double widthDifference = Math.Abs(contourWidthPx - expectedWidthPx) / expectedWidthPx;
                double lengthDifference = Math.Abs(contourLengthPx - expectedLengthPx) / expectedLengthPx;

                if (widthDifference > 0.25 || lengthDifference > 0.25)
                    continue;

                // Calculate distance from center
                double distanceX = Math.Abs(rect.Center.X - frameCenterX);
                double distanceY = Math.Abs(rect.Center.Y - frameCenterY);

                // Score the contour
                double sizeScore = 1.0 - (widthDifference + lengthDifference) / 2.0;
                double centerPenalty = distanceX * 0.5 + distanceY * 0.5;
                double score = sizeScore * 10000 - centerPenalty;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestContour = contour;
                    bestBox = rect;
                }
            }

            // If no box detected, return default result
            if (bestContour == null)
            {
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
                    ActualHeightMM = ACTUAL_HEIGHT_MM
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

                g.DrawRectangle(new Pen(Color.Lime, 3), expectedRect);

                // Draw detected box outline (red)
                PointF[] drawPoints = pts.Select(p => new PointF(p.X, p.Y)).ToArray();
                g.DrawPolygon(new Pen(Color.Red, 3), drawPoints);

                // Draw crosshair at center
                g.DrawLine(Pens.Yellow, frameCenterX_int - 10, frameCenterY_int, frameCenterX_int + 10, frameCenterY_int);
                g.DrawLine(Pens.Yellow, frameCenterX_int, frameCenterY_int - 10, frameCenterX_int, frameCenterY_int + 10);

                // Draw information text
                string infoText = $"BOX DETECTED\n" +
                                  $"W: {widthMM:F1} mm\n" +
                                  $"L: {lengthMM:F1} mm\n" +
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
                ActualHeightMM = ACTUAL_HEIGHT_MM
            };
        }
    }
}