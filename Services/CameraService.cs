using Basler.Pylon;

namespace VisioNeo_3D.Services
{
    public class CameraService
    {
        private readonly LogService logger;

        public CameraService(LogService logService)
        {
            logger = logService;
        }

        public void StartGrab(Camera camera, EventHandler<ImageGrabbedEventArgs> grabHandler)
        {
            try
            {
                camera.StreamGrabber.ImageGrabbed += grabHandler;

                camera.StreamGrabber.Start(
                    GrabStrategy.LatestImages,
                    GrabLoop.ProvidedByStreamGrabber
                );

                logger.Log("Streaming started", Color.LimeGreen);
            }
            catch (Exception ex)
            {
                logger.Log("Grab start error: " + ex.Message, Color.Red);
            }
        }

        public void Disconnect(Camera camera)
        {
            try
            {
                if (camera != null)
                {
                    camera.StreamGrabber.Stop();
                    camera.Close();
                    camera.Dispose();
                }

                logger.Log("Camera disconnected", Color.Orange);
            }
            catch (Exception ex)
            {
                logger.Log("Disconnect error: " + ex.Message, Color.Red);
            }
        }
    }
}