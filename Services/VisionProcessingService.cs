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

        private DateTime lastNoBagLog = DateTime.MinValue;

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

            // Store point cloud for later use
            latestPointCloud = pointCloud;
            latestWidth = width;
            latestHeight = height;

            DrawingBitmap bitmap = null;
            RotatedRect rotatedBag = new RotatedRect();
            DrawingPoint bagCenter = new DrawingPoint(0, 0);

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

                var result = DetectSugarBag(bitmap);

                rotatedBag = result.box;
                bagCenter = result.center;
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

            // ALWAYS extract Z value from point cloud, even without bag detection
            float X = 0;
            float Y = 0;
            float Z = 0;

            // Try to get Z from bag center if bag detected
            if (rotatedBag.Size.Width > 0)
            {
                (X, Y, Z) = GetCenterPoint3D(
                    pointCloud,
                    width,
                    height,
                    bagCenter);
            }
            else
            {
                // If no bag detected, get Z from image center
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
                //logger.Log("Point cloud is null or empty", DrawingColor.Red);
                return (0, 0, 0);
            }

            // Validate center pixel
            if (center.X < 0 || center.X >= width ||
                center.Y < 0 || center.Y >= height)
            {
                //logger.Log(
                //    $"Center point ({center.X}, {center.Y}) is outside image",
                //    DrawingColor.Red);

                return (0, 0, 0);
            }

            // Convert 2D pixel position to point-cloud index
            int pixelIndex = center.Y * width + center.X;
            int pointIndex = pixelIndex * 3;

            if (pointIndex + 2 >= pointCloud.Length)
            {
                //logger.Log("Point cloud index is outside range", DrawingColor.Red);
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
                //logger.Log(
                //    $"Invalid 3D point at center ({center.X}, {center.Y})",
                //    DrawingColor.Orange);

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
                //logger.Log("Point cloud is null or empty", DrawingColor.Red);
                //return (0, 0, 0);
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
                //logger.Log($"No valid Z points found around center ({center.X}, {center.Y})", DrawingColor.Orange);
                //return (0, 0, 0);
            }

            // Use median for stability
            float medianX = GetMedian(validXValues);
            float medianY = GetMedian(validYValues);
            float medianZ = GetMedian(validZValues);

            //logger.Log($"3D Measurement -> X:{medianX:F2} mm Y:{medianY:F2} mm Z:{medianZ:F2} mm Valid Points:{validZValues.Count}",
            //    DrawingColor.Green);

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

        private (RotatedRect box, DrawingPoint center) DetectSugarBag(DrawingBitmap bmp)
        {
            Mat img = BitmapConverter.ToMat(bmp);
            Mat gray = new Mat();
            Mat blur = new Mat();
            Mat thresh = new Mat();

            Cv2.CvtColor(img, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.GaussianBlur(gray, blur, new CvSize(blurSize, blurSize), 0);
            Cv2.Threshold(blur, thresh, thresholdValue, 255, ThresholdTypes.BinaryInv);

            Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new CvSize(5, 5));
            Cv2.MorphologyEx(thresh, thresh, MorphTypes.Close, kernel);

            Cv2.FindContours(
                thresh,
                out OpenCvSharp.Point[][] contours,
                out HierarchyIndex[] hierarchy,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple);

            double maxArea = 0;
            RotatedRect bestRect = new RotatedRect();

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

                if (area > maxArea)
                {
                    maxArea = area;
                    bestRect = rect;
                }
            }

            DrawingPoint center = new DrawingPoint(
                (int)bestRect.Center.X,
                (int)bestRect.Center.Y);

            return (bestRect, center);
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