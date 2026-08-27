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
        private bool _lastTriggerState = false;
        private bool _triggerReadBusy = false;
        private bool _captureInProgress = false;

        // MM/PX Calibration
        private bool calibrationMode = false;
        private bool calibrationPoint1Selected = false;

        private Point calibrationPoint1;
        private Point calibrationPoint2;
        private Point calibrationMousePoint;

        private double calibratedMmPerPixel = 0.60;
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
            plcTimer = new System.Windows.Forms.Timer();
            plcTimer.Interval = 100; // 1 second

            boxDetectionService = new BoxDetectionService();
            calibratedMmPerPixel = boxDetectionService.MmPerPixel;

            mmpp_Lbl.Text = $"MM/PX : {calibratedMmPerPixel:F4}";

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
            Angle_Reg_TB.Text = plcConfig.AngleReg;

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

        private async void PlcTimer_Tick(object sender, EventArgs e)
        {
            // Prevent overlapping PLC reads
            if (_triggerReadBusy)
                return;

            if (!mitsubishiService.IsConnected())
            {
                retryCount++;

                UpdatePLCStatus(PlcStatus.Retrying);

                PLC_status.Text =
                    $"PLC Retrying... ({retryCount})";

                bool reconnect = await Task.Run(() =>
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

                return;
            }

            retryCount = 0;

            UpdatePLCStatus(
                PlcStatus.Connected);

            // Check PLC trigger
            await CheckPLCTriggerAsync();
        }
        private async Task CheckPLCTriggerAsync()
        {
            if (_triggerReadBusy)
                return;

            _triggerReadBusy = true;

            try
            {
                string triggerAddress = plcConfig.TriggerReg;

                string value = await Task.Run(() =>
                {
                    return mitsubishiService.ReadValue(triggerAddress);
                });

                if (string.IsNullOrEmpty(value))
                    return;

                if (value.StartsWith("ERR:"))
                {
                    logger.Log(
                        $"Trigger Read Error: {value}",
                        Color.Red);

                    return;
                }

                bool currentTriggerState = value.Trim() == "1";

                // Detect ONLY rising edge: 0 -> 1
                bool risingEdge =
                    currentTriggerState && !_lastTriggerState;

                // IMPORTANT:
                // Update state immediately so that keeping D101 = 1
                // will NOT generate another trigger.
                _lastTriggerState = currentTriggerState;

                if (!risingEdge)
                    return;

                logger.Log(
                    $"PLC Rising Edge Detected: {triggerAddress} = 1",
                    Color.Blue);

                // Prevent another capture while current capture is running
                if (_captureInProgress)
                {
                    logger.Log(
                        "Capture already in progress. Trigger ignored.",
                        Color.Orange);

                    return;
                }

                await Task.Run(() =>
                {
                    CaptureAndProcess();
                });
            }
            catch (Exception ex)
            {
                logger.Log(
                    $"PLC Trigger Error: {ex.Message}",
                    Color.Red);
            }
            finally
            {
                _triggerReadBusy = false;
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

        private void CaptureAndProcess()
        {
            if (_captureInProgress)
                return;

            _captureInProgress = true;

            try
            {
                if (latestFrame == null)
                {
                    logger.Log(
                        "No image available for capture",
                        Color.Red);

                    return;
                }

                logger.Log(
                    "Capture triggered - Processing image...",
                    Color.Blue);

                // Clone latest frame so the camera thread
                // cannot modify it while processing
                using Bitmap captureFrame =
                    (Bitmap)latestFrame.Clone();

                var boxResult =
                    boxDetectionService.DetectBox(
                        captureFrame,
                        latestZ);

                logger.Log(
                    $"Actual Box Size : " +
                    $"W={boxResult.ActualWidthMM:F1} mm  " +
                    $"L={boxResult.ActualLengthMM:F1} mm  " +
                    $"H={boxResult.ActualHeightMM:F1} mm " +
                    $"Angle:{boxResult.Angle:F1}°",
                    Color.Blue);

                logger.Log(
                    $"Detected Box Size : " +
                    $"W={boxResult.WidthMM:F2} mm  " +
                    $"L={boxResult.LengthMM:F2} mm  " +
                    $"H={boxResult.HeightMM:F2} mm " +
                    $"Angle:{boxResult.Angle:F2}°",
                    Color.DarkGreen);

                logger.Log(
                    $"POSITION -> " +
                    $"X:{boxResult.OffsetX:F2} mm  " +
                    $"Y:{boxResult.OffsetY:F2} mm  " +
                    $"Z:{boxResult.OffsetZ:F2} mm  " +
                    $"ANGLE:{boxResult.Angle:F2}°",
                    Color.Green);

                // Update UI
                BeginInvoke(new Action(() =>
                {
                    label1.Text =
                        $"ΔX : {boxResult.OffsetX:F2} mm";

                    label2.Text =
                        $"ΔY : {boxResult.OffsetY:F2} mm";

                    mm.Text =
                        $"ΔZ : {boxResult.OffsetZ:F2} mm";

                    Res_PB.Image?.Dispose();

                    Res_PB.Image =
                        boxResult.ResultImage;
                }));

                // Send XYZ to PLC
                bool sent = mitsubishiService.SendXYZ(
    plcConfig.XReg,
    plcConfig.YReg,
    plcConfig.ZReg,
    plcConfig.AngleReg,
    boxResult.OffsetX,
    boxResult.OffsetY,
    boxResult.OffsetZ,
    boxResult.Angle);

                if (sent)
                {
                    logger.Log(
                        $"XYZ Sent -> " +
                        $"X:{boxResult.OffsetX:F2}  " +
                        $"Y:{boxResult.OffsetY:F2}  " +
                        $"Z:{boxResult.OffsetZ:F2}" + $"Angle:{boxResult.Angle:F1}°",
                        Color.Green);
                }
                else
                {
                    logger.Log(
                        "Failed to send XYZ to PLC",
                        Color.Red);
                }
            }
            catch (Exception ex)
            {
                logger.Log(
                    $"Capture Processing Error: {ex.Message}",
                    Color.Red);
            }
            finally
            {
                _captureInProgress = false;
            }
        }

        private void Cap_Btn_Click(object sender, EventArgs e)
        {
            CaptureAndProcess();
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
            plcConfig.AngleReg = Angle_Reg_TB.Text.Trim();

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

        private void Res_BTN_Click(object sender, EventArgs e)
        {
            StartCalibration();
        }

        private void StartCalibration()
        {
            if (Res_PB.Image == null)
            {
                MessageBox.Show(
                    "No image available for calibration.",
                    "Calibration",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            calibrationMode = true;
            calibrationPoint1Selected = false;

            calibrationPoint1 = Point.Empty;
            calibrationPoint2 = Point.Empty;
            calibrationMousePoint = Point.Empty;

            Res_PB.Cursor = Cursors.Cross;

            logger.Log(
                "MM/PX Calibration started. Click first point.",
                Color.Blue);

            Res_PB.Invalidate();
        }

        private void Res_PB_MouseMove(object sender, MouseEventArgs e)
        {
            if (!calibrationMode)
                return;

            calibrationMousePoint = e.Location;

            Res_PB.Invalidate();
        }

        private void Res_PB_Click(object sender, EventArgs e)
        {
            if (!calibrationMode)
                return;

            if (Res_PB.Image == null)
                return;

            MouseEventArgs mouseEvent = e as MouseEventArgs;

            if (mouseEvent == null)
                return;

            Point clickedPoint = mouseEvent.Location;

            // First point
            if (!calibrationPoint1Selected)
            {
                calibrationPoint1 = clickedPoint;

                calibrationPoint1Selected = true;

                calibrationMousePoint = clickedPoint;

                logger.Log(
                    $"Calibration Point 1 selected: " +
                    $"X={clickedPoint.X}, Y={clickedPoint.Y}",
                    Color.Blue);

                Res_PB.Invalidate();

                return;
            }

            // Second point
            calibrationPoint2 = clickedPoint;

            logger.Log(
                $"Calibration Point 2 selected: " +
                $"X={clickedPoint.X}, Y={clickedPoint.Y}",
                Color.Blue);

            // Calculate pixel distance
            double dx =
                calibrationPoint2.X -
                calibrationPoint1.X;

            double dy =
                calibrationPoint2.Y -
                calibrationPoint1.Y;

            double pixelDistance =
                Math.Sqrt(
                    (dx * dx) +
                    (dy * dy));

            if (pixelDistance <= 0)
            {
                MessageBox.Show(
                    "Invalid calibration distance.",
                    "Calibration",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                calibrationPoint1Selected = false;
                Res_PB.Invalidate();

                return;
            }

            // Ask user for actual distance
            string input =
                Microsoft.VisualBasic.Interaction.InputBox(
                    $"Measured pixel distance: {pixelDistance:F2} pixels\n\n" +
                    "Enter the actual distance in mm:",
                    "MM/PX Calibration",
                    "");

            if (string.IsNullOrWhiteSpace(input))
            {
                logger.Log(
                    "Calibration cancelled.",
                    Color.Orange);

                calibrationMode = false;
                calibrationPoint1Selected = false;

                Res_PB.Cursor = Cursors.Default;
                Res_PB.Invalidate();

                return;
            }

            if (!double.TryParse(
                input,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double actualDistanceMM))
            {
                MessageBox.Show(
                    "Please enter a valid numeric distance.",
                    "Calibration Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                calibrationMode = false;
                calibrationPoint1Selected = false;

                Res_PB.Cursor = Cursors.Default;
                Res_PB.Invalidate();

                return;
            }

            if (actualDistanceMM <= 0)
            {
                MessageBox.Show(
                    "Distance must be greater than zero.",
                    "Calibration Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                calibrationMode = false;
                calibrationPoint1Selected = false;

                Res_PB.Cursor = Cursors.Default;
                Res_PB.Invalidate();

                return;
            }

            // Calculate MM/PX
            double newMmPerPixel =
                actualDistanceMM / pixelDistance;

            // Update service
            boxDetectionService.SetMmPerPixel(
                newMmPerPixel);

            calibratedMmPerPixel =
                newMmPerPixel;

            // Update label
            mmpp_Lbl.Text =
                $"MM/PX : {calibratedMmPerPixel:F4}";

            logger.Log(
                $"Calibration completed -> " +
                $"Pixel Distance: {pixelDistance:F2}px, " +
                $"Actual Distance: {actualDistanceMM:F2}mm, " +
                $"MM/PX: {newMmPerPixel:F4}",
                Color.Green);

            MessageBox.Show(
                $"Calibration completed successfully.\n\n" +
                $"Pixel Distance : {pixelDistance:F2} px\n" +
                $"Actual Distance: {actualDistanceMM:F2} mm\n\n" +
                $"MM/PX : {newMmPerPixel:F4}",
                "Calibration Complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            calibrationMode = false;
            calibrationPoint1Selected = false;

            Res_PB.Cursor = Cursors.Default;

            Res_PB.Invalidate();
        }

        private void Res_PB_Paint(object sender, PaintEventArgs e)
        {

            if (!calibrationMode)
                return;

            if (!calibrationPoint1Selected)
            {
                using Pen pointPen =
                    new Pen(Color.Yellow, 3);

                e.Graphics.DrawEllipse(
                    pointPen,
                    calibrationMousePoint.X - 5,
                    calibrationMousePoint.Y - 5,
                    10,
                    10);

                return;
            }

            Point endPoint =
                calibrationMousePoint;

            using Pen linePen =
                new Pen(Color.Yellow, 3);

            e.Graphics.DrawLine(
                linePen,
                calibrationPoint1,
                endPoint);

            // Calculate realtime pixel distance
            double dx =
                endPoint.X -
                calibrationPoint1.X;

            double dy =
                endPoint.Y -
                calibrationPoint1.Y;

            double pixelDistance =
                Math.Sqrt(
                    dx * dx +
                    dy * dy);

            string text =
                $"Distance: {pixelDistance:F1} px";

            if (pixelDistance > 0)
            {
                double estimatedMM =
                    pixelDistance *
                    calibratedMmPerPixel;

                text +=
                    $"\nEstimated: {estimatedMM:F2} mm";
            }

            using Font font =
                new Font("Arial", 11, FontStyle.Bold);

            using Brush brush =
                new SolidBrush(Color.Yellow);

            Point textPosition =
                new Point(
                    endPoint.X + 10,
                    endPoint.Y + 10);

            e.Graphics.DrawString(
                text,
                font,
                brush,
                textPosition);
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
