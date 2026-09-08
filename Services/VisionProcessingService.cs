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

        // REMOVED: minContourArea, maxContourArea, minBagRatio, maxBagRatio
        // These were causing false rejections

        // Store the latest point cloud data for Z extraction
        private float[] latestPointCloud;
        private int latestWidth;
        private int latestHeight;

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

                var result = DetectBox(bitmap);

                rotatedBag = result.box;
                bagCenter = result.center;
                boxConfidence = result.confidence;

                // Lower confidence threshold for better detection
                if (rotatedBag.Size.Width > 0 && boxConfidence >= 0.35) // Reduced from 0.70 to 0.35
                {
                    boxDetected = true;
                    logger.Log($"Box detected with confidence: {boxConfidence:P0} at center ({bagCenter.X}, {bagCenter.Y})", DrawingColor.Green);
                }
                else if (rotatedBag.Size.Width > 0)
                {
                    logger.Log($"Box rejected - Low confidence: {boxConfidence:P0} (threshold: 35%)", DrawingColor.Orange);
                    rotatedBag = new RotatedRect();
                    bagCenter = new DrawingPoint(0, 0);
                }
                else
                {
                    logger.Log("No box detected in frame", DrawingColor.Orange);
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

            if (boxDetected && rotatedBag.Size.Width > 0)
            {
                (X, Y, Z) = GetCenterPoint3D(
                    pointCloud,
                    width,
                    height,
                    bagCenter);

                if (Z > 0)
                {
                    logger.Log($"Valid 3D point at center ({bagCenter.X}, {bagCenter.Y}) - Z: {Z:F2} mm", DrawingColor.Green);
                }
                else
                {
                    logger.Log($"Invalid 3D point at center ({bagCenter.X}, {bagCenter.Y})", DrawingColor.Orange);
                }
            }
            else
            {
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

            if (center.X < 0 || center.X >= width ||
                center.Y < 0 || center.Y >= height)
            {
                return (0, 0, 0);
            }

            int pixelIndex = center.Y * width + center.X;
            int pointIndex = pixelIndex * 3;

            if (pointIndex + 2 >= pointCloud.Length)
            {
                return (0, 0, 0);
            }

            float X = pointCloud[pointIndex];
            float Y = pointCloud[pointIndex + 1];
            float Z = pointCloud[pointIndex + 2];

            if (float.IsNaN(X) || float.IsInfinity(X) ||
                float.IsNaN(Y) || float.IsInfinity(Y) ||
                float.IsNaN(Z) || float.IsInfinity(Z) ||
                Z <= 0)
            {
                return (0, 0, 0);
            }

            return (X, Y, Z);
        }

        private (float X, float Y, float Z) ExtractZFromPointCloud(float[] pointCloud, int width, int height, DrawingPoint center)
        {
            if (pointCloud == null || pointCloud.Length == 0)
            {
                return (0, 0, 0);
            }

            List<float> validXValues = new List<float>();
            List<float> validYValues = new List<float>();
            List<float> validZValues = new List<float>();

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
        /// IMPROVED: Detect box WITHOUT fixed size assumptions
        /// </summary>
        private (RotatedRect box, DrawingPoint center, double confidence) DetectBox(DrawingBitmap bmp)
        {
            Mat img = BitmapConverter.ToMat(bmp);
            Mat gray = new Mat();
            Mat blur = new Mat();
            Mat thresh = new Mat();

            // Convert to grayscale
            Cv2.CvtColor(img, gray, ColorConversionCodes.BGR2GRAY);

            // Apply Gaussian blur to reduce noise
            Cv2.GaussianBlur(gray, blur, new CvSize(5, 5), 0);

            // Try multiple threshold methods for better detection
            Mat thresh1 = new Mat();
            Mat thresh2 = new Mat();

            // Method 1: Adaptive Threshold
            Cv2.AdaptiveThreshold(blur, thresh1, 255,
                AdaptiveThresholdTypes.GaussianC,
                ThresholdTypes.Binary,
                21, 5);

            // Method 2: Otsu Threshold
            Cv2.Threshold(blur, thresh2, 0, 255, ThresholdTypes.Otsu | ThresholdTypes.Binary);

            // Combine both methods
            Cv2.BitwiseOr(thresh1, thresh2, thresh);

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
            double bestArea = 0;

            int imageWidth = img.Width;
            int imageHeight = img.Height;
            double imageCenterX = imageWidth / 2.0;
            double imageCenterY = imageHeight / 2.0;

            // Find the LARGEST contour that looks like a box
            List<(OpenCvSharp.Point[] contour, RotatedRect rect, double area, double ratio, double centerScore)> candidates
                = new List<(OpenCvSharp.Point[], RotatedRect, double, double, double)>();

            foreach (var c in contours)
            {
                double area = Cv2.ContourArea(c);

                // Skip extremely small or huge contours
                if (area < 1000 || area > 500000)
                    continue;

                RotatedRect rect = Cv2.MinAreaRect(c);

                double width = rect.Size.Width;
                double height = rect.Size.Height;

                if (width == 0 || height == 0)
                    continue;

                // Ensure width > height for consistent ratio
                if (width < height)
                {
                    double temp = width;
                    width = height;
                    height = temp;
                }

                double ratio = width / height;

                // A box should have aspect ratio between 1.1 and 4.0
                if (ratio < 1.1 || ratio > 4.0)
                    continue;

                // Check if contour is near the center
                double distFromCenter = Math.Sqrt(
                    Math.Pow(rect.Center.X - imageCenterX, 2) +
                    Math.Pow(rect.Center.Y - imageCenterY, 2)
                );
                double maxDist = Math.Sqrt(Math.Pow(imageCenterX, 2) + Math.Pow(imageCenterY, 2));
                double centerScore = 1.0 - Math.Min(distFromCenter / maxDist, 1.0);

                // Check if this could be a sticker (very small and off-center)
                if (area < 5000 && centerScore < 0.2)
                    continue;

                candidates.Add((c, rect, area, ratio, centerScore));

                System.Diagnostics.Debug.WriteLine($"Candidate: Area={area:F0}, Ratio={ratio:F2}, CenterScore={centerScore:F2}");
            }

            // If no candidates, return empty
            if (candidates.Count == 0)
            {
                return (new RotatedRect(), new DrawingPoint(0, 0), 0);
            }

            // Score candidates based on:
            // 1. Area (bigger is better - likely the box, not stickers)
            // 2. Center position (closer to center is better)
            // 3. Aspect ratio (1.5-2.5 is ideal for boxes)
            var scoredCandidates = candidates.Select(c =>
            {
                double areaScore = Math.Min(c.area / 50000, 1.0);
                double ratioScore = 1.0 - Math.Min(Math.Abs(c.ratio - 2.0) / 2.0, 1.0);
                double confidence = (areaScore * 0.5) + (c.centerScore * 0.3) + (ratioScore * 0.2);
                return new { c.contour, c.rect, c.area, c.ratio, c.centerScore, confidence };
            })
            .OrderByDescending(c => c.confidence)
            .ToList();

            // Pick the best candidate
            var best = scoredCandidates.First();

            RotatedRect bestRectResult = best.rect;
            double bestConfidence = best.confidence;
            double bestAreaResult = best.area;

            System.Diagnostics.Debug.WriteLine($"BEST: Area={bestAreaResult:F0}, Ratio={best.ratio:F2}, Confidence={bestConfidence:P0}");

            DrawingPoint center = new DrawingPoint(
                (int)bestRectResult.Center.X,
                (int)bestRectResult.Center.Y);

            return (bestRectResult, center, bestConfidence);
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