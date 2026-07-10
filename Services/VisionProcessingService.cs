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

                if (rotatedBag.Size.Width == 0)
                {
                    if ((DateTime.Now - lastNoBagLog).TotalSeconds > 2)
                    {
                        lastNoBagLog = DateTime.Now;
                        logger.Log("No bag detected in frame", DrawingColor.Orange);
                    }
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

            if (rotatedBag.Size.Width > 0)
            {
                int pixelIndex = bagCenter.Y * width + bagCenter.X;
                int pointIndex = pixelIndex * 3;

                X = pointCloud[pointIndex];
                Y = pointCloud[pointIndex + 1];
                Z = pointCloud[pointIndex + 2];
            }

            return (bitmap, bagCenter, rotatedBag, X, Y, Z);
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