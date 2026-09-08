using Basler.Pylon;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using CvSize = OpenCvSharp.Size;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingColor = System.Drawing.Color;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;

namespace VisioNeo_3D.Services
{
    public class VisionProcessingService
    {
        private readonly LogService logger;
        private PixelDataConverter converter = new PixelDataConverter();

        private int blurSize = 5;
        private int thresholdValue = 60;
        private int minContourArea = 2000;
        private int maxContourArea = 500000;
        private double minBagRatio = 0.4;
        private double maxBagRatio = 2.5;

        // NEW: Box detection confidence threshold
        private const double MIN_BOX_CONFIDENCE = 0.60; // 60% minimum confidence
        private const double MIN_BOX_AREA_RATIO = 0.3;

        // Store the latest point cloud data for Z extraction
        private float[] latestPointCloud;
        private int latestWidth;
        private int latestHeight;

        // Expected box dimensions in mm
        private const double EXPECTED_WIDTH_MM = 331;
        private const double EXPECTED_LENGTH_MM = 175;

        public VisionProcessingService(LogService log)
        {
            logger = log;
        }

        public (DrawingBitmap bitmap, DrawingPoint center, RotatedRect rect, float X, float Y, float Z)
        ProcessFrame(IGrabResult grabResult, int selectedComponent)
        {
            using var container = grabResult.Container;
            using var rangeComponent = container[0];
            using var intensityComponent = container[1];
            using var confidenceComponent = container[2];

            int width = intensityComponent.Width;
            int height = intensityComponent.Height;

            float[] pointCloud = rangeComponent.PixelData as float[];

            // Store point cloud for later use
            latestPointCloud = pointCloud;
            latestWidth = width;
            latestHeight = height;

            DrawingBitmap bitmap = null;
            RotatedRect rotatedBag = new RotatedRect();
            DrawingPoint bagCenter = new DrawingPoint(0, 0);
            bool boxDetected = false;
            double boxConfidence = 0;

            if (selectedComponent == 1)
            {
                bitmap = new DrawingBitmap(width, height, PixelFormat.Format32bppRgb);

                BitmapData bmpData = bitmap.LockBits(
                    new DrawingRectangle(0, 0, width, height),
                    ImageLockMode.ReadWrite,
                    bitmap.PixelFormat);

                converter.OutputPixelFormat = PixelType.BGRA8packed;

                converter.Convert(
                    bmpData.Scan0,
                    bmpData.Stride * bitmap.Height,
                    intensityComponent);

                bitmap.UnlockBits(bmpData);

                var result = DetectWhiteBox(bitmap);

                rotatedBag = result.box;
                bagCenter = result.center;
                boxConfidence = result.confidence;

                // Only accept if confidence is high enough
                if (rotatedBag.Size.Width > 0 && boxConfidence >= MIN_BOX_CONFIDENCE)
                {
                    boxDetected = true;
                    //logger.Log($"Box detected with confidence: {boxConfidence:F2}% at center ({bagCenter.X}, {bagCenter.Y})", DrawingColor.Green);
                }
                else if (rotatedBag.Size.Width > 0)
                {
                    //logger.Log($"Box rejected - Low confidence: {boxConfidence:F2}% (threshold: {MIN_BOX_CONFIDENCE:F2}%)", DrawingColor.Orange);
                    rotatedBag = new RotatedRect();
                    bagCenter = new DrawingPoint(0, 0);
                }
                else
                {
                    //logger.Log("No box detected in frame", DrawingColor.Orange);
                }
            }
            else if (selectedComponent == 2)
            {
                bitmap = new DrawingBitmap(width, height, PixelFormat.Format32bppRgb);

                BitmapData bmpData = bitmap.LockBits(
                    new DrawingRectangle(0, 0, width, height),
                    ImageLockMode.ReadWrite,
                    bitmap.PixelFormat);

                converter.OutputPixelFormat = PixelType.BGRA8packed;

                converter.Convert(
                    bmpData.Scan0,
                    bmpData.Stride * bitmap.Height,
                    confidenceComponent);

                bitmap.UnlockBits(bmpData);
            }
            else if (selectedComponent == 0)
            {
                float[] depthZ = new float[width * height];

                for (int i = 0, p = 0; i < depthZ.Length; i++, p += 3)
                    depthZ[i] = pointCloud[p + 2];

                bitmap = ConvertDepthToBitmap(depthZ, width, height);
            }

            float X = 0;
            float Y = 0;
            float Z = 0;

            // Extract Z from the box region if box is detected
            if (boxDetected && rotatedBag.Size.Width > 0)
            {
                (X, Y, Z) = GetCenterPoint3D(
                    pointCloud,
                    width,
                    height,
                    bagCenter);

                if (Z > 0)
                {
                    //logger.Log($"Valid 3D point at center ({bagCenter.X}, {bagCenter.Y}) - Z: {Z:F2} mm", DrawingColor.Green);
                }
                else
                {
                    //logger.Log($"Invalid 3D point at center ({bagCenter.X}, {bagCenter.Y})", DrawingColor.Orange);
                }
            }
            else
            {
                // If no bag detected, get Z from image center as fallback
                DrawingPoint centerPoint = new DrawingPoint(width / 2, height / 2);
                (X, Y, Z) = ExtractZFromPointCloud(pointCloud, width, height, centerPoint);
            }

            return (bitmap, bagCenter, rotatedBag, X, Y, Z);
        }

        private (float X, float Y, float Z) GetCenterPoint3D(
            float[] pointCloud,
            int width,
            int height,
            DrawingPoint center)
        {
            if (pointCloud == null || pointCloud.Length == 0)
            {
                return (0, 0, 0);
            }

            // Validate center pixel
            if (center.X < 0 || center.X >= width ||
                center.Y < 0 || center.Y >= height)
            {
                return (0, 0, 0);
            }

            // Convert 2D pixel position to point-cloud index
            int pixelIndex = center.Y * width + center.X;
            int pointIndex = pixelIndex * 3;

            if (pointIndex + 2 >= pointCloud.Length)
            {
                return (0, 0, 0);
            }

            float X = pointCloud[pointIndex];
            float Y = pointCloud[pointIndex + 1];
            float Z = pointCloud[pointIndex + 2];

            // Validate values
            if (float.IsNaN(X) || float.IsInfinity(X) ||
                float.IsNaN(Y) || float.IsInfinity(Y) ||
                float.IsNaN(Z) || float.IsInfinity(Z) ||
                Z <= 0)
            {
                return (0, 0, 0);
            }

            return (X, Y, Z);
        }

        /// <summary>
        /// Extract X, Y, Z values from point cloud around a given center point
        /// </summary>
        private (float X, float Y, float Z) ExtractZFromPointCloud(float[] pointCloud, int width, int height, DrawingPoint center)
        {
            if (pointCloud == null || pointCloud.Length == 0)
            {
                return (0, 0, 0);
            }

            List<float> validXValues = new List<float>();
            List<float> validYValues = new List<float>();
            List<float> validZValues = new List<float>();

            // Use a larger radius for better sampling
            int radius = 15;

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int px = center.X + dx;
                    int py = center.Y + dy;

                    if (px < 0 || px >= width || py < 0 || py >= height)
                        continue;

                    int pixelIndex = py * width + px;
                    int pointIndex = pixelIndex * 3;

                    if (pointIndex + 2 >= pointCloud.Length)
                        continue;

                    float pxX = pointCloud[pointIndex];
                    float pxY = pointCloud[pointIndex + 1];
                    float pxZ = pointCloud[pointIndex + 2];

                    // Validate point data
                    if (float.IsNaN(pxZ) || float.IsInfinity(pxZ) || pxZ <= 0)
                        continue;

                    if (float.IsNaN(pxX) || float.IsInfinity(pxX))
                        continue;

                    if (float.IsNaN(pxY) || float.IsInfinity(pxY))
                        continue;

                    validXValues.Add(pxX);
                    validYValues.Add(pxY);
                    validZValues.Add(pxZ);
                }
            }

            if (validZValues.Count == 0)
            {
                return (0, 0, 0);
            }

            // Use median for stability
            float medianX = GetMedian(validXValues);
            float medianY = GetMedian(validYValues);
            float medianZ = GetMedian(validZValues);

            return (medianX, medianY, medianZ);
        }

        private float GetMedian(List<float> values)
        {
            if (values == null || values.Count == 0)
                return 0;

            values.Sort();

            int middle = values.Count / 2;

            if (values.Count % 2 == 0)
            {
                return (values[middle - 1] + values[middle]) / 2f;
            }

            return values[middle];
        }

        /// <summary>
        /// Detect WHITE box (bright object on dark background)
        /// </summary>
        private (RotatedRect box, DrawingPoint center, double confidence) DetectWhiteBox(DrawingBitmap bmp)
        {
            Mat img = BitmapConverter.ToMat(bmp);
            Mat gray = new Mat();
            Mat blur = new Mat();
            Mat thresh = new Mat();

            // Convert to grayscale
            Cv2.CvtColor(img, gray, ColorConversionCodes.BGR2GRAY);

            // Apply Gaussian blur to reduce noise
            Cv2.GaussianBlur(gray, blur, new CvSize(blurSize, blurSize), 0);

            // IMPORTANT: Use Binary (NOT BinaryInv) to detect WHITE objects on dark background
            // ThresholdTypes.Binary = white objects on dark background
            // ThresholdTypes.BinaryInv = black objects on bright background
            Cv2.Threshold(blur, thresh, thresholdValue, 255, ThresholdTypes.Binary);

            // Morphological operations to clean up
            Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new CvSize(5, 5));
            Cv2.MorphologyEx(thresh, thresh, MorphTypes.Close, kernel);
            Cv2.MorphologyEx(thresh, thresh, MorphTypes.Open, kernel);

            // Find contours
            Cv2.FindContours(
                thresh,
                out OpenCvSharp.Point[][] contours,
                out HierarchyIndex[] hierarchy,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple);

            double maxConfidence = 0;
            RotatedRect bestRect = new RotatedRect();

            // Calculate image area for reference
            double imageArea = img.Width * img.Height;

            // Get current MM/PX from the box detection service (passed through the system)
            double mmPerPixel = 0.6; // Default, will be updated from the service
            double expectedWidthPx = EXPECTED_WIDTH_MM / mmPerPixel;
            double expectedLengthPx = EXPECTED_LENGTH_MM / mmPerPixel;
            double expectedAreaPx = expectedWidthPx * expectedLengthPx;

            foreach (var c in contours)
            {
                double area = Cv2.ContourArea(c);

                if (area < minContourArea || area > maxContourArea)
                    continue;

                RotatedRect rect = Cv2.MinAreaRect(c);

                double width = rect.Size.Width;
                double height = rect.Size.Height;

                if (width == 0 || height == 0)
                    continue;

                double ratio = width / height;

                if (ratio < 1)
                    ratio = 1 / ratio;

                if (ratio < minBagRatio || ratio > maxBagRatio)
                    continue;

                // Calculate confidence score
                double confidence = CalculateBoxConfidence(rect, area, imageArea, expectedAreaPx);

                System.Diagnostics.Debug.WriteLine($"Contour: Area={area:F0}, Ratio={ratio:F2}, Confidence={confidence:F2}");

                if (confidence > maxConfidence)
                {
                    maxConfidence = confidence;
                    bestRect = rect;
                }
            }

            DrawingPoint center = new DrawingPoint(
                (int)bestRect.Center.X,
                (int)bestRect.Center.Y);

            System.Diagnostics.Debug.WriteLine($"Best detection: Confidence={maxConfidence:F2}");

            return (bestRect, center, maxConfidence);
        }

        /// <summary>
        /// Calculate confidence score for a detected box
        /// </summary>
        private double CalculateBoxConfidence(RotatedRect rect, double area, double imageArea, double expectedAreaPx)
        {
            double confidence = 0;

            // 1. Area score - should be close to expected area
            double areaRatio = area / expectedAreaPx;
            if (areaRatio > 0.3 && areaRatio < 2.0)
            {
                double areaScore = 1.0 - Math.Abs(1.0 - areaRatio);
                if (areaScore < 0) areaScore = 0;
                confidence += areaScore * 0.40; // 40% weight
            }

            // 2. Aspect ratio score
            double width = Math.Max(rect.Size.Width, rect.Size.Height);
            double height = Math.Min(rect.Size.Width, rect.Size.Height);
            double ratio = width / height;
            double expectedRatio = EXPECTED_WIDTH_MM / EXPECTED_LENGTH_MM;

            double ratioScore = 1.0 - Math.Abs(ratio - expectedRatio) / expectedRatio;
            if (ratioScore < 0) ratioScore = 0;
            confidence += ratioScore * 0.30; // 30% weight

            // 3. Rectangle regularity - box should not be too tilted
            double angle = rect.Angle;
            double angleScore = 1.0 - Math.Abs(angle) / 45.0;
            if (angleScore < 0) angleScore = 0;
            confidence += angleScore * 0.20; // 20% weight

            // 4. Position score - box should be somewhat centered
            double imageCenterX = 640 / 2.0;
            double imageCenterY = 480 / 2.0;
            double distFromCenter = Math.Sqrt(
                Math.Pow(rect.Center.X - imageCenterX, 2) +
                Math.Pow(rect.Center.Y - imageCenterY, 2)
            );
            double maxDist = Math.Sqrt(Math.Pow(imageCenterX, 2) + Math.Pow(imageCenterY, 2));
            double positionScore = 1.0 - (distFromCenter / maxDist);
            if (positionScore < 0) positionScore = 0;
            confidence += positionScore * 0.10; // 10% weight

            return confidence;
        }

        private DrawingBitmap ConvertDepthToBitmap(float[] depthData, int width, int height)
        {
            DrawingBitmap bmp = new DrawingBitmap(width, height, PixelFormat.Format24bppRgb);

            float min = float.MaxValue;
            float max = float.MinValue;

            for (int i = 0; i < depthData.Length; i++)
            {
                float v = depthData[i];
                if (v < min) min = v;
                if (v > max) max = v;
            }

            float range = max - min;
            if (range == 0) range = 1;

            BitmapData data = bmp.LockBits(
                new DrawingRectangle(0, 0, width, height),
                ImageLockMode.WriteOnly,
                bmp.PixelFormat);

            int stride = data.Stride;
            int bytes = stride * height;
            byte[] pixels = new byte[bytes];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    int index = y * stride + x * 3;

                    float normalized = (depthData[i] - min) / range;

                    var c = DepthToHeatmap(normalized);

                    pixels[index] = c.B;
                    pixels[index + 1] = c.G;
                    pixels[index + 2] = c.R;
                }
            }

            Marshal.Copy(pixels, 0, data.Scan0, bytes);
            bmp.UnlockBits(data);

            return bmp;
        }

        private DrawingColor DepthToHeatmap(float value)
        {
            value = Math.Clamp(value, 0f, 1f);

            float r = 0, g = 0, b = 0;

            if (value < 0.25f)
            {
                b = 1;
                g = value * 4;
            }
            else if (value < 0.5f)
            {
                b = 1 - (value - 0.25f) * 4;
                g = 1;
            }
            else if (value < 0.75f)
            {
                g = 1;
                r = (value - 0.5f) * 4;
            }
            else
            {
                g = 1 - (value - 0.75f) * 4;
                r = 1;
            }

            return DrawingColor.FromArgb(
                (int)(r * 255),
                (int)(g * 255),
                (int)(b * 255));
        }
    }
}