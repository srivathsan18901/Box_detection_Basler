using System.Text.Json;
using VisioNeo_3D.Models;

namespace VisioNeo_3D.Services
{
    public class PlcConfigService
    {
        private readonly string filePath =
            Path.Combine(
                Application.StartupPath,
                "PlcConfig.json");

        public PlcConfig Load()
        {
            if (!File.Exists(filePath))
            {
                var config = new PlcConfig();
                Save(config);
                return config;
            }

            string json =
                File.ReadAllText(filePath);

            return JsonSerializer
                .Deserialize<PlcConfig>(json);
        }

        public void Save(PlcConfig config)
        {
            string json =
                JsonSerializer.Serialize(
                    config,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

            File.WriteAllText(
                filePath,
                json);
        }

    }
}