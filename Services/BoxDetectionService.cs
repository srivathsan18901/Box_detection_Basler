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

        // Add these
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
        private const double REFERENCE_DISTANCE_Z = 500.0;
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
            Bitmap resultBmp = (Bitmap)source.Clone();

            Mat src = BitmapConverter.ToMat(source);
            //double measuredHeight = REFERENCE_PLANE_Z - cameraZ;


            // Process only the upper 65% of the image
            Rect roi = new Rect(
                0,
                0,
                src.Width,
                (int)(src.Height * 0.80));

            src = new Mat(src, roi);

            Mat gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            Mat thresh = new Mat();

            Cv2.GaussianBlur(
                gray,
                gray,
                new OpenCvSharp.Size(5, 5),
                0);

            Cv2.AdaptiveThreshold(
                gray,
                thresh,
                255,
                AdaptiveThresholdTypes.GaussianC,
                ThresholdTypes.BinaryInv,
                21,
                5);

            Mat kernel =
                Cv2.GetStructuringElement(
                    MorphShapes.Rect,
                    new OpenCvSharp.Size(3, 3));

            Cv2.Dilate(
            thresh,
            thresh,
            kernel,
            iterations: 1);

            Cv2.MorphologyEx(
                thresh,
                thresh,
                MorphTypes.Close,
                kernel);

            OpenCvSharp.Point[][] contours;
            HierarchyIndex[] hierarchy;

            Cv2.FindContours(
                thresh,
                out contours,
                out hierarchy,
                RetrievalModes.List,
                ContourApproximationModes.ApproxSimple);

            Rect bestRect = new Rect();

            double bestScore = double.MinValue;
            OpenCvSharp.Point[] bestContour = null;
            RotatedRect bestBox = new RotatedRect();

            foreach (var contour in contours)
            {
                double area = Cv2.ContourArea(contour);

                if (area < 3000)
                    continue;

                RotatedRect rect = Cv2.MinAreaRect(contour);

                double w = rect.Size.Width;
                double h = rect.Size.Height;

                if (w < 50 || h < 50)
                    continue;

                double ratio = Math.Max(w, h) /
                               Math.Min(w, h);

                double expectedRatio = ACTUAL_WIDTH_MM / ACTUAL_LENGTH_MM;

                if (Math.Abs(ratio - expectedRatio) > 0.3)
                    continue;

                double frameCenterX = src.Width / 2.0;
                double frameCenterY = src.Height / 2.0;

                // distance from image center
                double distanceX = Math.Abs(rect.Center.X - frameCenterX);

                double distanceY =
                    Math.Abs(rect.Center.Y - frameCenterY);

                double score =
                      area
                    - distanceX * 500
                    - distanceY * 100;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestContour = contour;
                    bestBox = rect;
                }

            }

            if (bestContour == null)
            {
                return new BoxDetectionResult
                {
                    ResultImage = resultBmp,
                    OffsetX = 0,
                    OffsetY = 0,
                    OffsetZ = 0
                };
            }
            RotatedRect box = bestBox;

            double angle = box.Angle;

            // Normalize angle based on the longer dimension
            if (box.Size.Width < box.Size.Height)
            {
                angle += 90.0;
            }

            // Normalize to -90° to +90°
            while (angle >= 90.0)
                angle -= 180.0;

            while (angle < -90.0)
                angle += 180.0;

            Point2f[] pts = box.Points();

            //if (maxArea > 1000)
            {


                int frameCenterX = resultBmp.Width / 2;
                int frameCenterY = resultBmp.Height / 2;

                int boxCenterX = (int)(box.Center.X + roi.X);
                int boxCenterY = (int)(box.Center.Y + roi.Y);

                double widthPx = Math.Max(box.Size.Width, box.Size.Height);
                double lengthPx = Math.Min(box.Size.Width, box.Size.Height);

                // Use calibrated MM/PX
                double widthMM = widthPx * mmPerPixel;
                double lengthMM = lengthPx * mmPerPixel;

                // Position offsets using calibrated MM/PX
                double offsetX =
                    (boxCenterX - frameCenterX) * mmPerPixel;

                double offsetY =
                    (boxCenterY - frameCenterY) * mmPerPixel;

                double detectedDistanceZ = cameraZ;

                double offsetZ =
                   REFERENCE_DISTANCE_Z - detectedDistanceZ;


                using (Graphics g = Graphics.FromImage(resultBmp))
                {
                    int expectedWidthPx =
                        (int)(ACTUAL_WIDTH_MM / mmPerPixel);

                    int expectedLengthPx =
                        (int)(ACTUAL_LENGTH_MM / mmPerPixel);

                    Rectangle expectedRect =
                        new Rectangle(
                            frameCenterX - expectedWidthPx / 2,
                            frameCenterY - expectedLengthPx / 2,
                            expectedWidthPx,
                            expectedLengthPx);

                    g.DrawRectangle(
                        new Pen(Color.Lime, 3),
                        expectedRect);

                    g.DrawPolygon(
                        new Pen(Color.Red, 3),
                        pts.Select(p =>
                            new PointF(p.X, p.Y))
                        .ToArray());

                    g.DrawLine(
                        Pens.Yellow,
                        frameCenterX - 10,
                        frameCenterY,
                        frameCenterX + 10,
                        frameCenterY);

                    g.DrawLine(
                        Pens.Yellow,
                        frameCenterX,
                        frameCenterY - 10,
                        frameCenterX,
                        frameCenterY + 10);

                    g.DrawString(
                        $"BOX DETECTED\n" +
                        $"W: {ACTUAL_WIDTH_MM:F1} mm\n" +
                        $"L: {ACTUAL_LENGTH_MM:F1} mm\n" +
                        $"DX: {offsetX:F1} mm\n" +
                        $"DY: {offsetY:F1} mm\n" +
                        $"DZ: {offsetZ:F1} mm\n" +
                        $"ANGLE: {angle:F1}°",
                    SystemFonts.DefaultFont,
                        Brushes.Red,
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
                    ActualHeightMM = ACTUAL_HEIGHT_MM
                };
            }
            return new BoxDetectionResult
            {
                ResultImage = resultBmp,

                OffsetX = 0,
                OffsetY = 0,
                OffsetZ = 0,
                Angle = 0
            };
            if (bestContour == null)
            {
                return new BoxDetectionResult
                {
                    ResultImage = resultBmp,

                    OffsetX = 0,
                    OffsetY = 0,
                    OffsetZ = 0,
                    Angle = 0
                };
            }
        }
    }
}