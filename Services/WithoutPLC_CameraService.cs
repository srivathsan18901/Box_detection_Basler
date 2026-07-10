using Basler.Pylon;
using Basler.Pylon.Blaze;

namespace VisioNeo_3D.Services
{
    public class WithoutPLC_CameraService
    {
        private readonly LogService logger;

        public Camera Camera { get; private set; }

        public WithoutPLC_CameraService(LogService logService)
        {
            logger = logService;
        }

        public bool ConnectCamera()
        {
            try
            {
                logger.Log("Searching Basler Blaze Camera...", Color.Blue);

                var filter = new Dictionary<string, string>
                {
                    { CameraInfoKey.DeviceType, DeviceType.BaslerGenTlBlazeDeviceClass }
                };

                Camera = new Camera(filter, CameraSelectionStrategy.FirstFound);

                Camera.CameraOpened += BlazeConfigurations.AcquirePointCloudsContinuously;

                Camera.Open();

                logger.Log("Camera Connected Successfully", Color.Green);

                ConfigureCamera();

                return true;
            }
            catch (Exception ex)
            {
                logger.Log("Camera connection failed: " + ex.Message, Color.Red);
                return false;
            }
        }

        private void ConfigureCamera()
        {
            Camera.Parameters[PLBlaze.OperatingMode].SetValue(PLBlaze.OperatingMode.LongRange);
            Camera.Parameters[PLBlaze.ExposureTime].SetValue(250);
            Camera.Parameters[PLBlaze.SpatialFilter].SetValue(true);
            Camera.Parameters[PLBlaze.TemporalFilter].SetValue(true);
            Camera.Parameters[PLBlaze.OutlierRemoval].SetValue(true);

            logger.Log("Camera parameters configured", Color.Purple);
        }
    }
}