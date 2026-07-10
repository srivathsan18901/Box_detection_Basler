namespace VisioNeo_3D.Models
{
    public class PlcConfig
    {
        public string PlcIp { get; set; } = "192.168.3.1";
        public int PlcPort { get; set; } = 502;
        public string TriggerReg { get; set; } = "M100";
        public string XReg { get; set; } = "D100";
        public string YReg { get; set; } = "D102";
        public string ZReg { get; set; } = "D104";
    }
}