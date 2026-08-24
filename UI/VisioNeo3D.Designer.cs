namespace VisioNeo_3D
{
    partial class VisioNeo3D
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        private System.ComponentModel.ComponentResourceManager resources =
    new System.ComponentModel.ComponentResourceManager(
        typeof(VisioNeo3D));

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VisioNeo3D));
            pictureBox1 = new PictureBox();
            pictureBox2 = new PictureBox();
            pictureBox3 = new PictureBox();
            closeBtn = new PictureBox();
            minBtn = new PictureBox();
            CnctBtn = new Button();
            toastbox = new RichTextBox();
            ImgModCB = new ComboBox();
            loaderPic = new PictureBox();
            label1 = new Label();
            label2 = new Label();
            label3 = new Label();
            label4 = new Label();
            Res_PB = new PictureBox();
            Cap_Btn = new Button();
            PLC_status = new Label();
            panel1 = new Panel();
            Angle_Reg_TB = new TextBox();
            label12 = new Label();
            SavePLC_Btn = new Button();
            PLC_Port_TB = new TextBox();
            Z_Reg_TB = new TextBox();
            Y_Reg_TB = new TextBox();
            X_Reg_TB = new TextBox();
            Cam_Trigger_TB = new TextBox();
            PLC_IP_TB = new TextBox();
            label9 = new Label();
            label8 = new Label();
            label7 = new Label();
            label6 = new Label();
            label5 = new Label();
            PLCAddr_TB = new TextBox();
            DataPlc_TB = new TextBox();
            label10 = new Label();
            label11 = new Label();
            ReadPlc_Btn = new Button();
            writePLC_Btn = new Button();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox2).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox3).BeginInit();
            ((System.ComponentModel.ISupportInitialize)closeBtn).BeginInit();
            ((System.ComponentModel.ISupportInitialize)minBtn).BeginInit();
            ((System.ComponentModel.ISupportInitialize)loaderPic).BeginInit();
            ((System.ComponentModel.ISupportInitialize)Res_PB).BeginInit();
            panel1.SuspendLayout();
            SuspendLayout();
            // 
            // pictureBox1
            // 
            pictureBox1.BackColor = Color.Transparent;
            pictureBox1.Location = new Point(12, 100);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(538, 526);
            pictureBox1.TabIndex = 1;
            pictureBox1.TabStop = false;
            // 
            // pictureBox2
            // 
            pictureBox2.BackColor = Color.Transparent;
            pictureBox2.Image = (Image)resources.GetObject("pictureBox2.Image");
            pictureBox2.Location = new Point(12, 9);
            pictureBox2.Name = "pictureBox2";
            pictureBox2.Size = new Size(87, 50);
            pictureBox2.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox2.TabIndex = 2;
            pictureBox2.TabStop = false;
            // 
            // pictureBox3
            // 
            pictureBox3.BackColor = Color.Transparent;
            pictureBox3.Image = (Image)resources.GetObject("pictureBox3.Image");
            pictureBox3.Location = new Point(102, 19);
            pictureBox3.Name = "pictureBox3";
            pictureBox3.Size = new Size(121, 35);
            pictureBox3.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox3.TabIndex = 3;
            pictureBox3.TabStop = false;
            // 
            // closeBtn
            // 
            closeBtn.BackColor = Color.Transparent;
            closeBtn.Image = (Image)resources.GetObject("closeBtn.Image");
            closeBtn.Location = new Point(1585, 13);
            closeBtn.Name = "closeBtn";
            closeBtn.Size = new Size(23, 23);
            closeBtn.SizeMode = PictureBoxSizeMode.StretchImage;
            closeBtn.TabIndex = 4;
            closeBtn.TabStop = false;
            closeBtn.Click += closeBtn_Click;
            // 
            // minBtn
            // 
            minBtn.BackColor = Color.Transparent;
            minBtn.Image = (Image)resources.GetObject("minBtn.Image");
            minBtn.Location = new Point(1554, 13);
            minBtn.Name = "minBtn";
            minBtn.Size = new Size(23, 23);
            minBtn.SizeMode = PictureBoxSizeMode.StretchImage;
            minBtn.TabIndex = 5;
            minBtn.TabStop = false;
            minBtn.Click += minBtn_Click;
            // 
            // CnctBtn
            // 
            CnctBtn.BackColor = Color.Transparent;
            CnctBtn.BackgroundImageLayout = ImageLayout.Center;
            CnctBtn.Font = new Font("Noto Sans SC", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
            CnctBtn.ForeColor = Color.DodgerBlue;
            CnctBtn.Location = new Point(229, 20);
            CnctBtn.Name = "CnctBtn";
            CnctBtn.Size = new Size(112, 34);
            CnctBtn.TabIndex = 6;
            CnctBtn.Text = "Connect";
            CnctBtn.UseVisualStyleBackColor = false;
            CnctBtn.Click += CnctBtn_Click;
            // 
            // toastbox
            // 
            toastbox.BorderStyle = BorderStyle.None;
            toastbox.Location = new Point(1275, 98);
            toastbox.Name = "toastbox";
            toastbox.Size = new Size(335, 219);
            toastbox.TabIndex = 7;
            toastbox.Text = "";
            // 
            // ImgModCB
            // 
            ImgModCB.DropDownHeight = 102;
            ImgModCB.IntegralHeight = false;
            ImgModCB.ItemHeight = 15;
            ImgModCB.Location = new Point(12, 71);
            ImgModCB.Name = "ImgModCB";
            ImgModCB.Size = new Size(170, 23);
            ImgModCB.TabIndex = 8;
            ImgModCB.SelectedIndexChanged += ImgModCB_SelectedIndexChanged;
            // 
            // loaderPic
            // 
            loaderPic.BackColor = Color.Transparent;
            loaderPic.BackgroundImage = (Image)resources.GetObject("loaderPic.BackgroundImage");
            loaderPic.Image = (Image)resources.GetObject("loaderPic.Image");
            loaderPic.Location = new Point(1491, 2);
            loaderPic.Name = "loaderPic";
            loaderPic.Size = new Size(46, 46);
            loaderPic.SizeMode = PictureBoxSizeMode.Zoom;
            loaderPic.TabIndex = 9;
            loaderPic.TabStop = false;
            loaderPic.Click += loaderPic_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.BackColor = Color.Transparent;
            label1.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            label1.Location = new Point(198, 70);
            label1.Name = "label1";
            label1.Size = new Size(0, 19);
            label1.TabIndex = 10;
            label1.Click += label1_Click;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.BackColor = Color.Transparent;
            label2.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            label2.Location = new Point(300, 71);
            label2.Name = "label2";
            label2.Size = new Size(0, 19);
            label2.TabIndex = 11;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.BackColor = Color.Transparent;
            label3.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            label3.Location = new Point(417, 71);
            label3.Name = "label3";
            label3.Size = new Size(0, 19);
            label3.TabIndex = 12;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.BackColor = Color.Transparent;
            label4.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            label4.Location = new Point(550, 71);
            label4.Name = "label4";
            label4.Size = new Size(0, 19);
            label4.TabIndex = 13;
            // 
            // Res_PB
            // 
            Res_PB.BackColor = Color.Transparent;
            Res_PB.Location = new Point(566, 98);
            Res_PB.Name = "Res_PB";
            Res_PB.Size = new Size(703, 528);
            Res_PB.TabIndex = 14;
            Res_PB.TabStop = false;
            Res_PB.Click += Res_PB_Click;
            // 
            // Cap_Btn
            // 
            Cap_Btn.BackColor = Color.Transparent;
            Cap_Btn.BackgroundImageLayout = ImageLayout.Center;
            Cap_Btn.Font = new Font("Noto Sans SC", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
            Cap_Btn.ForeColor = Color.DodgerBlue;
            Cap_Btn.Location = new Point(344, 21);
            Cap_Btn.Name = "Cap_Btn";
            Cap_Btn.Size = new Size(112, 34);
            Cap_Btn.TabIndex = 15;
            Cap_Btn.Text = "Capture";
            Cap_Btn.UseVisualStyleBackColor = false;
            Cap_Btn.Click += Cap_Btn_Click;
            // 
            // PLC_status
            // 
            PLC_status.AutoSize = true;
            PLC_status.BackColor = Color.Transparent;
            PLC_status.Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point, 0);
            PLC_status.ForeColor = SystemColors.Highlight;
            PLC_status.Location = new Point(462, 27);
            PLC_status.Name = "PLC_status";
            PLC_status.Size = new Size(0, 21);
            PLC_status.TabIndex = 16;
            // 
            // panel1
            // 
            panel1.BackColor = Color.Transparent;
            panel1.Controls.Add(Angle_Reg_TB);
            panel1.Controls.Add(label12);
            panel1.Controls.Add(SavePLC_Btn);
            panel1.Controls.Add(PLC_Port_TB);
            panel1.Controls.Add(Z_Reg_TB);
            panel1.Controls.Add(Y_Reg_TB);
            panel1.Controls.Add(X_Reg_TB);
            panel1.Controls.Add(Cam_Trigger_TB);
            panel1.Controls.Add(PLC_IP_TB);
            panel1.Controls.Add(label9);
            panel1.Controls.Add(label8);
            panel1.Controls.Add(label7);
            panel1.Controls.Add(label6);
            panel1.Controls.Add(label5);
            panel1.Location = new Point(1275, 323);
            panel1.Name = "panel1";
            panel1.Size = new Size(335, 303);
            panel1.TabIndex = 17;
            // 
            // Angle_Reg_TB
            // 
            Angle_Reg_TB.BorderStyle = BorderStyle.FixedSingle;
            Angle_Reg_TB.Location = new Point(167, 188);
            Angle_Reg_TB.Name = "Angle_Reg_TB";
            Angle_Reg_TB.Size = new Size(157, 23);
            Angle_Reg_TB.TabIndex = 31;
            // 
            // label12
            // 
            label12.AutoSize = true;
            label12.BackColor = Color.Transparent;
            label12.Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point, 0);
            label12.ForeColor = SystemColors.Highlight;
            label12.Location = new Point(18, 186);
            label12.Name = "label12";
            label12.Size = new Size(52, 21);
            label12.TabIndex = 30;
            label12.Text = "Angle";
            // 
            // SavePLC_Btn
            // 
            SavePLC_Btn.FlatStyle = FlatStyle.Flat;
            SavePLC_Btn.Font = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point, 0);
            SavePLC_Btn.ForeColor = SystemColors.Highlight;
            SavePLC_Btn.Location = new Point(255, 258);
            SavePLC_Btn.Name = "SavePLC_Btn";
            SavePLC_Btn.Size = new Size(75, 32);
            SavePLC_Btn.TabIndex = 29;
            SavePLC_Btn.Text = "Save";
            SavePLC_Btn.UseVisualStyleBackColor = true;
            SavePLC_Btn.Click += SavePLC_Btn_Click;
            // 
            // PLC_Port_TB
            // 
            PLC_Port_TB.BorderStyle = BorderStyle.FixedSingle;
            PLC_Port_TB.Location = new Point(243, 8);
            PLC_Port_TB.Name = "PLC_Port_TB";
            PLC_Port_TB.Size = new Size(87, 23);
            PLC_Port_TB.TabIndex = 28;
            // 
            // Z_Reg_TB
            // 
            Z_Reg_TB.BorderStyle = BorderStyle.FixedSingle;
            Z_Reg_TB.Location = new Point(168, 159);
            Z_Reg_TB.Name = "Z_Reg_TB";
            Z_Reg_TB.Size = new Size(157, 23);
            Z_Reg_TB.TabIndex = 27;
            // 
            // Y_Reg_TB
            // 
            Y_Reg_TB.BorderStyle = BorderStyle.FixedSingle;
            Y_Reg_TB.Location = new Point(168, 129);
            Y_Reg_TB.Name = "Y_Reg_TB";
            Y_Reg_TB.Size = new Size(157, 23);
            Y_Reg_TB.TabIndex = 26;
            // 
            // X_Reg_TB
            // 
            X_Reg_TB.BorderStyle = BorderStyle.FixedSingle;
            X_Reg_TB.Location = new Point(168, 101);
            X_Reg_TB.Name = "X_Reg_TB";
            X_Reg_TB.Size = new Size(157, 23);
            X_Reg_TB.TabIndex = 25;
            // 
            // Cam_Trigger_TB
            // 
            Cam_Trigger_TB.BorderStyle = BorderStyle.FixedSingle;
            Cam_Trigger_TB.Location = new Point(168, 72);
            Cam_Trigger_TB.Name = "Cam_Trigger_TB";
            Cam_Trigger_TB.Size = new Size(157, 23);
            Cam_Trigger_TB.TabIndex = 24;
            // 
            // PLC_IP_TB
            // 
            PLC_IP_TB.BorderStyle = BorderStyle.FixedSingle;
            PLC_IP_TB.Location = new Point(93, 8);
            PLC_IP_TB.Name = "PLC_IP_TB";
            PLC_IP_TB.Size = new Size(144, 23);
            PLC_IP_TB.TabIndex = 23;
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.BackColor = Color.Transparent;
            label9.Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point, 0);
            label9.ForeColor = SystemColors.Highlight;
            label9.Location = new Point(19, 157);
            label9.Name = "label9";
            label9.Size = new Size(19, 21);
            label9.TabIndex = 22;
            label9.Text = "Z";
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.BackColor = Color.Transparent;
            label8.Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point, 0);
            label8.ForeColor = SystemColors.Highlight;
            label8.Location = new Point(19, 131);
            label8.Name = "label8";
            label8.Size = new Size(19, 21);
            label8.TabIndex = 21;
            label8.Text = "Y";
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.BackColor = Color.Transparent;
            label7.Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point, 0);
            label7.ForeColor = SystemColors.Highlight;
            label7.Location = new Point(19, 99);
            label7.Name = "label7";
            label7.Size = new Size(20, 21);
            label7.TabIndex = 20;
            label7.Text = "X";
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.BackColor = Color.Transparent;
            label6.Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point, 0);
            label6.ForeColor = SystemColors.Highlight;
            label6.Location = new Point(19, 70);
            label6.Name = "label6";
            label6.Size = new Size(123, 21);
            label6.TabIndex = 19;
            label6.Text = "Capture Trigger";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.BackColor = Color.Transparent;
            label5.Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point, 0);
            label5.ForeColor = SystemColors.Highlight;
            label5.Location = new Point(10, 10);
            label5.Name = "label5";
            label5.Size = new Size(57, 21);
            label5.TabIndex = 18;
            label5.Text = "PLC IP";
            // 
            // PLCAddr_TB
            // 
            PLCAddr_TB.BorderStyle = BorderStyle.FixedSingle;
            PLCAddr_TB.Location = new Point(1129, 15);
            PLCAddr_TB.Name = "PLCAddr_TB";
            PLCAddr_TB.Size = new Size(100, 23);
            PLCAddr_TB.TabIndex = 18;
            // 
            // DataPlc_TB
            // 
            DataPlc_TB.BorderStyle = BorderStyle.FixedSingle;
            DataPlc_TB.Location = new Point(1129, 59);
            DataPlc_TB.Name = "DataPlc_TB";
            DataPlc_TB.Size = new Size(100, 23);
            DataPlc_TB.TabIndex = 19;
            // 
            // label10
            // 
            label10.AutoSize = true;
            label10.BackColor = Color.Transparent;
            label10.Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point, 0);
            label10.ForeColor = SystemColors.Highlight;
            label10.Location = new Point(1057, 15);
            label10.Name = "label10";
            label10.Size = new Size(67, 21);
            label10.TabIndex = 30;
            label10.Text = "Address";
            // 
            // label11
            // 
            label11.AutoSize = true;
            label11.BackColor = Color.Transparent;
            label11.Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point, 0);
            label11.ForeColor = SystemColors.Highlight;
            label11.Location = new Point(1078, 60);
            label11.Name = "label11";
            label11.Size = new Size(46, 21);
            label11.TabIndex = 31;
            label11.Text = "Data";
            // 
            // ReadPlc_Btn
            // 
            ReadPlc_Btn.BackColor = Color.Transparent;
            ReadPlc_Btn.FlatStyle = FlatStyle.Flat;
            ReadPlc_Btn.Font = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point, 0);
            ReadPlc_Btn.ForeColor = SystemColors.Highlight;
            ReadPlc_Btn.Location = new Point(1234, 9);
            ReadPlc_Btn.Name = "ReadPlc_Btn";
            ReadPlc_Btn.Size = new Size(75, 32);
            ReadPlc_Btn.TabIndex = 30;
            ReadPlc_Btn.Text = "Read";
            ReadPlc_Btn.UseVisualStyleBackColor = false;
            ReadPlc_Btn.Click += ReadPlc_Btn_Click;
            // 
            // writePLC_Btn
            // 
            writePLC_Btn.BackColor = Color.Transparent;
            writePLC_Btn.FlatStyle = FlatStyle.Flat;
            writePLC_Btn.Font = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point, 0);
            writePLC_Btn.ForeColor = SystemColors.Highlight;
            writePLC_Btn.Location = new Point(1235, 52);
            writePLC_Btn.Name = "writePLC_Btn";
            writePLC_Btn.Size = new Size(75, 32);
            writePLC_Btn.TabIndex = 32;
            writePLC_Btn.Text = "Write";
            writePLC_Btn.UseVisualStyleBackColor = false;
            writePLC_Btn.Click += writePLC_Btn_Click_1;
            // 
            // VisioNeo3D
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackgroundImage = (Image)resources.GetObject("$this.BackgroundImage");
            BackgroundImageLayout = ImageLayout.Stretch;
            ClientSize = new Size(1622, 644);
            ControlBox = false;
            Controls.Add(writePLC_Btn);
            Controls.Add(ReadPlc_Btn);
            Controls.Add(label11);
            Controls.Add(label10);
            Controls.Add(DataPlc_TB);
            Controls.Add(PLCAddr_TB);
            Controls.Add(panel1);
            Controls.Add(PLC_status);
            Controls.Add(Cap_Btn);
            Controls.Add(Res_PB);
            Controls.Add(label4);
            Controls.Add(label3);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(loaderPic);
            Controls.Add(ImgModCB);
            Controls.Add(toastbox);
            Controls.Add(CnctBtn);
            Controls.Add(minBtn);
            Controls.Add(closeBtn);
            Controls.Add(pictureBox3);
            Controls.Add(pictureBox2);
            Controls.Add(pictureBox1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "VisioNeo3D";
            Text = "VisioNeo3D";
            Load += VisioNeo3D_Load;
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox2).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox3).EndInit();
            ((System.ComponentModel.ISupportInitialize)closeBtn).EndInit();
            ((System.ComponentModel.ISupportInitialize)minBtn).EndInit();
            ((System.ComponentModel.ISupportInitialize)loaderPic).EndInit();
            ((System.ComponentModel.ISupportInitialize)Res_PB).EndInit();
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private PictureBox pictureBox1;
        private PictureBox pictureBox2;
        private PictureBox pictureBox3;
        private PictureBox closeBtn;
        private PictureBox minBtn;
        private Button CnctBtn;
        private RichTextBox toastbox;
        private ComboBox ImgModCB;
        private PictureBox loaderPic;
        private Label label1;
        private Label label2;
        private Label label3;
        private Label label4;
        private PictureBox Res_PB;
        private Button Cap_Btn;
        private Label PLC_status;
        private Panel panel1;
        private TextBox Cam_Trigger_TB;
        private TextBox PLC_IP_TB;
        private Label label9;
        private Label label8;
        private Label label7;
        private Label label6;
        private Label label5;
        private TextBox Z_Reg_TB;
        private TextBox Y_Reg_TB;
        private TextBox X_Reg_TB;
        private TextBox PLC_Port_TB;
        private Button SavePLC_Btn;
        private TextBox PLCAddr_TB;
        private TextBox DataPlc_TB;
        private Label label10;
        private Label label11;
        private Button ReadPlc_Btn;
        private Button writePLC_Btn;
        private TextBox Angle_Reg_TB;
        private Label label12;
    }
}
