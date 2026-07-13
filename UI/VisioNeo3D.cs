namespace VisioNeo_3D
{
    using Basler.Pylon;
    using S7.Net;
    using System.Runtime.InteropServices;
    using VisioNeo_3D.Models;
    using VisioNeo_3D.Services;

    public enum PlcStatus
    {
        Disconnected,
        Retrying,
        Connected
    }

    public partial class VisioNeo3D : Form
    {
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        [DllImport("user32.dll")]

        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HTCAPTION = 0x2;
        private Plc _plc;
        private bool PlcConnected = false;
        private int retryCount = 0;
        private Camera mCamera;
        private int selectedComponent = 1;
        private bool isConnected = false;
        private LogService logger;
        private withPLC_ConnectionService WithPLCService;
        private WithoutPLC_CameraService withoutPLCService;
        private System.Windows.Forms.Timer plcTimer;
        private CameraService cameraService;
        private VisionProcessingService visionService;
        private BoxDetectionService boxDetectionService;
        private MitsubishiPLCService mitsubishiService;
        private PlcConfigService plcConfigService;
        private PlcConfig plcConfig;
        private Bitmap latestFrame;
        private float latestX;
        private float latestY;
        private float latestZ;
        public VisioNeo3D()
        {
            InitializeComponent();
            this.FormBorderStyle = FormBorderStyle.None;
            this.MouseDown += VisioNeo3D_MouseDown;
            logger = new LogService(toastbox);
            cameraService = new CameraService(logger);
            withoutPLCService = new WithoutPLC_CameraService(logger);
            WithPLCService = new withPLC_ConnectionService(logger);
            visionService = new VisionProcessingService(logger);
            boxDetectionService = new BoxDetectionService();
            plcTimer = new System.Windows.Forms.Timer();
            plcTimer.Interval = 3000; // 1 second
            plcTimer.Tick += PlcTimer_Tick;
            mitsubishiService = new MitsubishiPLCService();
            plcConfigService = new PlcConfigService();
        }

        private async void VisioNeo3D_Load(object sender, EventArgs e)
        {
            ImgModCB.Visible = false;
            //toastbox.Visible = false;
            loaderPic.Visible = false;

            ImgModCB.Items.Add("Intensity (Grayscale)");
            ImgModCB.Items.Add("Depth");
            ImgModCB.Items.Add("Confidence");

            ImgModCB.SelectedIndex = 0;

            // Load config FIRST
            plcConfig = plcConfigService.Load();

            PLC_IP_TB.Text = plcConfig.PlcIp;
            PLC_Port_TB.Text = plcConfig.PlcPort.ToString();
            Cam_Trigger_TB.Text = plcConfig.TriggerReg;
            X_Reg_TB.Text = plcConfig.XReg;
            Y_Reg_TB.Text = plcConfig.YReg;
            Z_Reg_TB.Text = plcConfig.ZReg;

            bool connected =
                await Task.Run(() =>
                    mitsubishiService.Connect(
                        plcConfig.PlcIp,
                        plcConfig.PlcPort));

            if (connected)
            {
                logger.Log(
                    "Mitsubishi PLC Connected",
                    Color.Green);
            }
            else
            {
                logger.Log(
                    "PLC Communication Failed",
                    Color.Red);
            }

            UpdatePLCStatus(
                connected
                    ? PlcStatus.Connected
                    : PlcStatus.Disconnected);

            logger.Log(
                connected
                    ? "Mitsubishi PLC Connected"
                    : "PLC Connection Failed",
                connected
                    ? Color.Green
                    : Color.Red);

            plcTimer.Start();
        }

        private async void PlcTimer_Tick(
            object sender,
            EventArgs e)
        {
            if (mitsubishiService.IsConnected())
            {
                retryCount = 0;
                UpdatePLCStatus(PlcStatus.Connected);
                return;
            }

            retryCount++;

            UpdatePLCStatus(
                PlcStatus.Retrying);

            PLC_status.Text =
                $"PLC Retrying... ({retryCount})";

            bool reconnect =
                await Task.Run(() =>
                {
                    return mitsubishiService.Connect(
                        plcConfig.PlcIp,
                        plcConfig.PlcPort);
                });

            if (reconnect)
            {
                retryCount = 0;
                UpdatePLCStatus(
                    PlcStatus.Connected);

                logger.Log(
                    "PLC Reconnected",
                    Color.Green);
            }
        }

        private void VisioNeo3D_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        }

        private void closeBtn_Click(object sender, EventArgs e)
        {
            plcTimer?.Stop();
            mitsubishiService?.Disconnect();
            Application.Exit();
        }

        private void minBtn_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private async void CnctBtn_Click(object sender, EventArgs e)
        {
            if (!isConnected)
            {
                ShowLoader(true);
                CnctBtn.Enabled = false;

                await Task.Run(() =>
                {
                    withoutPLC_Cam();
                });

                ShowLoader(false);
                CnctBtn.Enabled = true;

                if (mCamera != null && mCamera.IsOpen)
                {
                    isConnected = true;
                    CnctBtn.Text = "Disconnect";
                    CnctBtn.ForeColor = Color.Red;

                    ImgModCB.Visible = true;
                    toastbox.Visible = true;
                }
            }
            else
            {
                cameraService.Disconnect(mCamera);
            }
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }
        private void ImgModCB_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (ImgModCB.SelectedIndex)
            {
                case 0:
                    selectedComponent = 1; // Intensity
                    logger.Log("Image mode changed to Intensity", Color.Blue);
                    break;

                case 1:
                    selectedComponent = 0; // Depth
                    logger.Log("Image mode changed to Depth", Color.Orange);
                    break;

                case 2:
                    selectedComponent = 2; // Confidence
                    logger.Log("Image mode changed to Confidence", Color.Red);
                    break;
            }
        }

        private void withPLC_Cam()
        {
            bool ok = WithPLCService.StartSystem();

            if (ok)
            {
                _plc = WithPLCService.Plc;
                mCamera = WithPLCService.Camera;
                PlcConnected = WithPLCService.PlcConnected;
            }
        }

        private void withoutPLC_Cam()
        {
            bool ok = withoutPLCService.ConnectCamera();

            if (ok)
            {
                mCamera = withoutPLCService.Camera;
                cameraService.StartGrab(mCamera, ImageGrabbedHandler);
            }
        }

        private void ShowLoader(bool show)
        {
            if (loaderPic.InvokeRequired)
            {
                loaderPic.Invoke(new Action(() => ShowLoader(show)));
                return;
            }

            loaderPic.Visible = show;
        }

        private void ImageGrabbedHandler(object sender, ImageGrabbedEventArgs e)
        {
            IGrabResult grabResult = e.GrabResult;

            if (!grabResult.GrabSucceeded)
                return;

            var result = visionService.ProcessFrame(grabResult, selectedComponent);

            Bitmap bitmap = result.bitmap;

            latestFrame?.Dispose();
            latestFrame = (Bitmap)bitmap.Clone();

            // Save latest 3D coordinates
            latestX = result.X;
            latestY = result.Y;
            latestZ = result.Z;

            BeginInvoke(new Action(() =>
            {
                pictureBox1.Image?.Dispose();
                pictureBox1.Image = bitmap;
            }));
        }

        private void Cap_Btn_Click(object sender, EventArgs e)
        {

            if (latestFrame == null)
            {
                logger.Log("No image available", Color.Red);
                return;
            }

            var boxResult = boxDetectionService.DetectBox(latestFrame, latestZ);

            logger.Log(
                $"Actual Box Size    : W={boxResult.ActualWidthMM:F1} mm  " +
                $"L={boxResult.ActualLengthMM:F1} mm  " +
                $"H={boxResult.ActualHeightMM:F1} mm",
                Color.Blue);

            logger.Log(
                $"Detected Box Size : W={boxResult.WidthMM:F1} mm  " +
                $"L={boxResult.LengthMM:F1} mm  " +
                $"H={boxResult.HeightMM:F1} mm",
                Color.DarkGreen);

            label1.Text = $"ΔX : {boxResult.OffsetX:F2} mm";
            label2.Text = $"ΔY : {boxResult.OffsetY:F2} mm";
            label3.Text = $"ΔZ : {boxResult.OffsetZ:F2} mm";

            Res_PB.Image?.Dispose();
            Res_PB.Image = boxResult.ResultImage;

            // Send XYZ to PLC
            bool sent = mitsubishiService.SendXYZ(
                plcConfig.XReg,
                plcConfig.YReg,
                plcConfig.ZReg,
                boxResult.OffsetX,
                boxResult.OffsetY,
                boxResult.OffsetZ);

            if (sent)
            {
                logger.Log(
                    $"XYZ Sent -> X:{boxResult.OffsetX:F2} " +
                    $"Y:{boxResult.OffsetY:F2} " +
                    $"Z:{boxResult.OffsetZ:F2}",
                    Color.Green);
            }
            else
            {
                logger.Log("Failed to send XYZ to PLC", Color.Red);
            }
        }

        private void Res_PB_Click(object sender, EventArgs e)
        {

        }

        private void loaderPic_Click(object sender, EventArgs e)
        {

        }

        private void UpdatePLCStatus(PlcStatus status)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                    UpdatePLCStatus(status)));
                return;
            }

            switch (status)
            {
                case PlcStatus.Connected:
                    PLC_status.Text = "PLC Connected";
                    PLC_status.ForeColor = Color.Green;
                    break;

                case PlcStatus.Retrying:
                    PLC_status.Text =
                        $"PLC Retrying... ({retryCount})";
                    PLC_status.ForeColor = Color.Orange;
                    break;

                default:
                    PLC_status.Text = "PLC Disconnected";
                    PLC_status.ForeColor = Color.Red;
                    break;
            }
        }

        private async void SavePLC_Btn_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(PLC_Port_TB.Text.Trim(), out int port))
            {
                logger.Log("Invalid PLC Port", Color.Red);
                return;
            }

            plcConfig.PlcIp = PLC_IP_TB.Text.Trim();
            plcConfig.PlcPort = port;
            plcConfig.TriggerReg = Cam_Trigger_TB.Text.Trim();
            plcConfig.XReg = X_Reg_TB.Text.Trim();
            plcConfig.YReg = Y_Reg_TB.Text.Trim();
            plcConfig.ZReg = Z_Reg_TB.Text.Trim();

            plcConfigService.Save(plcConfig);

            logger.Log("PLC Configuration Saved", Color.Green);

            // Disconnect current PLC
            mitsubishiService.Disconnect();

            UpdatePLCStatus(PlcStatus.Disconnected);

            // Connect using new settings
            bool connected = await Task.Run(() =>
            {
                return mitsubishiService.Connect(
                    plcConfig.PlcIp,
                    plcConfig.PlcPort);
            });

            if (connected)
            {
                UpdatePLCStatus(PlcStatus.Connected);

                logger.Log(
                    $"Connected to PLC ({plcConfig.PlcIp}:{plcConfig.PlcPort})",
                    Color.Green);
            }
            else
            {
                UpdatePLCStatus(PlcStatus.Disconnected);

                logger.Log(
                    $"Failed to connect to PLC ({plcConfig.PlcIp}:{plcConfig.PlcPort})",
                    Color.Red);
            }
        }

        private async void ReadPlc_Btn_Click(object sender, EventArgs e)
        {
            string address = PLCAddr_TB.Text.Trim();

            if (string.IsNullOrEmpty(address))
            {
                logger.Log("Please enter an address", Color.Red);
                return;
            }

            if (!mitsubishiService.IsConnected())
            {
                logger.Log("PLC is not connected", Color.Red);
                return;
            }

            ReadPlc_Btn.Enabled = false;

            try
            {
                string value = await Task.Run(() =>
                {
                    return mitsubishiService.ReadValue(address);
                });

                if (string.IsNullOrEmpty(value))
                {
                    logger.Log($"Read Failed: {address}", Color.Red);
                }
                else if (value.StartsWith("ERR:"))
                {
                    logger.Log(value, Color.Red);
                }
                else
                {
                    DataPlc_TB.Text = value;
                    logger.Log($"Read Success: {address} = {value}", Color.Green);
                }
            }
            catch (Exception ex)
            {
                logger.Log($"Error reading from PLC: {ex.Message}", Color.Red);
            }
            finally
            {
                ReadPlc_Btn.Enabled = true;
            }
        }

        private async void writePLC_Btn_Click_1(object sender, EventArgs e)
        {
            string address = PLCAddr_TB.Text.Trim();
            string value = DataPlc_TB.Text.Trim();

            if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(value))
            {
                logger.Log("Please enter both address and value", Color.Red);
                return;
            }

            if (!mitsubishiService.IsConnected())
            {
                logger.Log("PLC is not connected", Color.Red);
                return;
            }

            writePLC_Btn.Enabled = false;

            try
            {
                bool ok = await Task.Run(() =>
                {
                    return mitsubishiService.WriteValue(address, value);
                });

                if (ok)
                {
                    logger.Log($"Write Success: {address} = {value}", Color.Green);
                }
                else
                {
                    logger.Log($"Write Failed: {address}", Color.Red);
                }
            }
            catch (Exception ex)
            {
                logger.Log($"Error writing to PLC: {ex.Message}", Color.Red);
            }
            finally
            {
                writePLC_Btn.Enabled = true;
            }
        }

        //private void ImageGrabbedHandler(object sender, ImageGrabbedEventArgs e)
        //{
        //    IGrabResult grabResult = e.GrabResult;

        //    if (!grabResult.GrabSucceeded)
        //        return;

        //    var result = visionService.ProcessFrame(grabResult, selectedComponent);

        //    Bitmap bitmap = result.bitmap;

        //    BeginInvoke(new Action(() =>
        //    {
        //        label1.Text = $"X : {result.X:F2} mm";
        //        label2.Text = $"Y : {result.Y:F2} mm";
        //        label3.Text = $"Z : {result.Z:F2} mm";

        //        pictureBox1.Image?.Dispose();
        //        pictureBox1.Image = bitmap;
        //    }));
        //}

    }
}
