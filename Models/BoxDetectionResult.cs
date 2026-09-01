namespace VisioNeo_3D.Models
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

        // ADD THIS - Actual detected Z from point cloud
        public double DetectedZ { get; set; }

        // ADD THIS - Center point in original image coordinates
        public System.Drawing.Point CenterPoint { get; set; }
    }
}
