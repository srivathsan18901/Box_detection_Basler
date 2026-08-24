namespace VisioNeo_3D.Models
{
    public class PlcConfig
    {
        public string PlcIp { get; set; } = "192.168.0.99";
        public int PlcPort { get; set; } = 501;
        public string TriggerReg { get; set; } = "D101";
        public string XReg { get; set; } = "D100";
        public string YReg { get; set; } = "D102";
        public string ZReg { get; set; } = "D104";
        public string AngleReg { get; set; } = "D105";
    }
}