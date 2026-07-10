
using Basler.Pylon;
using Basler.Pylon.Blaze;
using S7.Net;
using Toastr.Winforms;

namespace VisioNeo_3D.Services
{
    public class withPLC_ConnectionService
    {
        private readonly LogService logger;

        public Plc Plc { get; private set; }
        public Camera Camera { get; private set; }
        public bool PlcConnected { get; private set; }

        public withPLC_ConnectionService(LogService logService)
        {
            logger = logService;
        }

        public bool StartSystem()
        {
            logger.Log("System Connection Started...", Color.Purple);

            bool plcOk = ConnectPLC();

            if (!plcOk)
            {
                logger.Log("System stopped because PLC not connected", Color.Red);
                return false;
            }

            bool camOk = ConnectCamera();

            if (!camOk)
            {
                logger.Log("System stopped because Camera not connected", Color.Red);
                return false;
            }

            logger.Log("System Ready ✔", Color.LimeGreen);
            return true;
        }

        private bool ConnectPLC()
        {
            try
            {
                logger.Log("Trying PLC connection...", Color.Blue);

                Plc = new Plc(CpuType.S71500, "192.168.0.1", 0, 0);
                Plc.Open();

                if (Plc.IsConnected)
                {
                    PlcConnected = true;
                    logger.Log("PLC Connected Successfully", Color.Green);
                    return true;
                }

                logger.Log("PLC Connection Failed", Color.Red);
                return false;
            }
            catch (Exception ex)
            {
                logger.Log("PLC Error: " + ex.Message, Color.Red);
                return false;
            }
        }

        private bool ConnectCamera()
        {
            try
            {
                if (Camera != null)
                {
                    Camera.Close();
                    Camera.Dispose();
                    Camera = null;
                }

                var toast = new Toast();
                toast.ShowSuccess("Searching for Basler Blaze Camera...");

                logger.Log("Searching for Basler Blaze Camera...", Color.Blue);

                List<ICameraInfo> cameraList = CameraFinder
                    .Enumerate()
                    .Where(c => c[CameraInfoKey.DeviceType] == DeviceType.BaslerGenTlBlazeDeviceClass)
                    .ToList();

                if (cameraList.Count == 0)
                {
                    logger.Log("No Basler Blaze camera detected.", Color.Red);
                    return false;
                }

                ICameraInfo cameraInfo = cameraList[0];
                string cameraName = cameraInfo[CameraInfoKey.FriendlyName];

                logger.Log($"Camera found: {cameraName}. Attempting connection...", Color.Blue);

                Camera = new Camera(cameraInfo);

                Camera.CameraOpened += BlazeConfigurations.AcquirePointCloudsContinuously;

                Camera.Open();

                Camera.Parameters[PLBlaze.OperatingMode].SetValue(PLBlaze.OperatingMode.LongRange);
                Camera.Parameters[PLBlaze.ExposureTime].SetValue(250);

                logger.Log($"Successfully connected to: {cameraName}", Color.Green);

                return true;
            }
            catch (Exception ex)
            {
                if (Camera != null)
                {
                    Camera.Dispose();
                    Camera = null;
                }

                string errorDetails = $"Message: {ex.Message} | StackTrace: {ex.StackTrace}";

                if (ex.InnerException != null)
                    errorDetails += $" | Inner: {ex.InnerException.Message}";

                logger.Log("Camera Connection Failed: " + errorDetails, Color.Red);

                return false;
            }
        }
    }
}