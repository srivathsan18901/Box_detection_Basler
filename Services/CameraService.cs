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
                // Unsubscribe any existing handlers to prevent memory leaks
                camera.StreamGrabber.ImageGrabbed -= grabHandler;
                camera.StreamGrabber.ImageGrabbed += grabHandler;

                if (!camera.StreamGrabber.IsGrabbing)
                {
                    camera.StreamGrabber.Start(
                        GrabStrategy.LatestImages,
                        GrabLoop.ProvidedByStreamGrabber
                    );
                    logger.Log("Streaming started", Color.LimeGreen);
                }
                else
                {
                    logger.Log("Stream already running", Color.Orange);
                }
            }
            catch (Exception ex)
            {
                logger.Log("Grab start error: " + ex.Message, Color.Red);
            }
        }

        public void StopGrab(Camera camera, EventHandler<ImageGrabbedEventArgs> grabHandler = null)
        {
            try
            {
                if (camera != null && camera.StreamGrabber != null)
                {
                    if (camera.StreamGrabber.IsGrabbing)
                    {
                        camera.StreamGrabber.Stop();
                        logger.Log("Streaming stopped", Color.Orange);
                    }

                    // Unsubscribe event handler if provided
                    if (grabHandler != null)
                    {
                        camera.StreamGrabber.ImageGrabbed -= grabHandler;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Log("Stop grab error: " + ex.Message, Color.Red);
            }
        }

        public void Disconnect(Camera camera, EventHandler<ImageGrabbedEventArgs> grabHandler = null)
        {
            try
            {
                if (camera != null)
                {
                    // Stop grabbing first
                    if (camera.StreamGrabber != null)
                    {
                        if (camera.StreamGrabber.IsGrabbing)
                        {
                            camera.StreamGrabber.Stop();
                        }

                        // Unsubscribe event handler
                        if (grabHandler != null)
                        {
                            camera.StreamGrabber.ImageGrabbed -= grabHandler;
                        }
                    }

                    // Close and dispose
                    if (camera.IsOpen)
                    {
                        camera.Close();
                    }

                    camera.Dispose();
                    camera = null;

                    logger.Log("Camera disconnected", Color.Orange);
                }
            }
            catch (Exception ex)
            {
                logger.Log("Disconnect error: " + ex.Message, Color.Red);
            }
        }
    }
}