namespace CameraManager
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            DisplayTimer = new System.Windows.Forms.Timer(components);
            tableLayoutPanel1 = new TableLayoutPanel();
            panelHeader = new Panel();
            btnHideSetting = new Button();
            btnMinimize = new Button();
            btnMaximize = new Button();
            btnExit = new Button();
            label1 = new Label();
            panel2 = new Panel();
            panelView = new Panel();
            tableLayoutPanel2 = new TableLayoutPanel();
            panelMain = new Panel();
            panelLog = new Panel();
            dgviewLog = new DataGridView();
            panelSetting = new Panel();
            btnStopCamera = new Button();
            btnStartCamera = new Button();
            btnAlarm = new Button();
            btnSetting = new Button();
            btnLogView = new Button();
            tableLayoutPanel1.SuspendLayout();
            panelHeader.SuspendLayout();
            panel2.SuspendLayout();
            panelView.SuspendLayout();
            tableLayoutPanel2.SuspendLayout();
            panelLog.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgviewLog).BeginInit();
            panelSetting.SuspendLayout();
            SuspendLayout();
            // 
            // DisplayTimer
            // 
            DisplayTimer.Interval = 33;
            DisplayTimer.Tick += DisplayTimer_Tick;
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 1;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Controls.Add(panelHeader, 0, 0);
            tableLayoutPanel1.Controls.Add(panel2, 0, 1);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(0, 0);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 2;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Size = new Size(1046, 581);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // panelHeader
            // 
            panelHeader.BackColor = Color.FromArgb(134, 175, 215);
            panelHeader.Controls.Add(btnHideSetting);
            panelHeader.Controls.Add(btnMinimize);
            panelHeader.Controls.Add(btnMaximize);
            panelHeader.Controls.Add(btnExit);
            panelHeader.Controls.Add(label1);
            panelHeader.Dock = DockStyle.Fill;
            panelHeader.Location = new Point(3, 3);
            panelHeader.Name = "panelHeader";
            panelHeader.Size = new Size(1040, 24);
            panelHeader.TabIndex = 0;
            // 
            // btnHideSetting
            // 
            btnHideSetting.Dock = DockStyle.Right;
            btnHideSetting.FlatAppearance.BorderSize = 0;
            btnHideSetting.FlatStyle = FlatStyle.Flat;
            btnHideSetting.Image = (Image)resources.GetObject("btnHideSetting.Image");
            btnHideSetting.Location = new Point(937, 0);
            btnHideSetting.Name = "btnHideSetting";
            btnHideSetting.Size = new Size(24, 24);
            btnHideSetting.TabIndex = 4;
            btnHideSetting.UseVisualStyleBackColor = true;
            btnHideSetting.Click += btnHideSetting_Click;
            // 
            // btnMinimize
            // 
            btnMinimize.Dock = DockStyle.Right;
            btnMinimize.FlatAppearance.BorderSize = 0;
            btnMinimize.FlatStyle = FlatStyle.Flat;
            btnMinimize.Image = (Image)resources.GetObject("btnMinimize.Image");
            btnMinimize.Location = new Point(961, 0);
            btnMinimize.Name = "btnMinimize";
            btnMinimize.Size = new Size(24, 24);
            btnMinimize.TabIndex = 3;
            btnMinimize.UseVisualStyleBackColor = true;
            btnMinimize.Click += btnMinimize_Click;
            // 
            // btnMaximize
            // 
            btnMaximize.Dock = DockStyle.Right;
            btnMaximize.FlatAppearance.BorderSize = 0;
            btnMaximize.FlatStyle = FlatStyle.Flat;
            btnMaximize.Image = Properties.Resources.icons8_rectangle_28;
            btnMaximize.Location = new Point(985, 0);
            btnMaximize.Name = "btnMaximize";
            btnMaximize.Size = new Size(27, 24);
            btnMaximize.TabIndex = 2;
            btnMaximize.UseVisualStyleBackColor = true;
            btnMaximize.Click += btnMaximize_Click;
            // 
            // btnExit
            // 
            btnExit.Dock = DockStyle.Right;
            btnExit.FlatAppearance.BorderSize = 0;
            btnExit.FlatStyle = FlatStyle.Flat;
            btnExit.Image = (Image)resources.GetObject("btnExit.Image");
            btnExit.Location = new Point(1012, 0);
            btnExit.Name = "btnExit";
            btnExit.Size = new Size(28, 24);
            btnExit.TabIndex = 1;
            btnExit.UseVisualStyleBackColor = true;
            btnExit.Click += btnExit_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Dock = DockStyle.Left;
            label1.Font = new Font("Microsoft YaHei", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label1.ForeColor = Color.MediumBlue;
            label1.Location = new Point(0, 0);
            label1.Name = "label1";
            label1.Size = new Size(40, 19);
            label1.TabIndex = 0;
            label1.Text = "Hello";
            // 
            // panel2
            // 
            panel2.Controls.Add(panelView);
            panel2.Controls.Add(panelSetting);
            panel2.Dock = DockStyle.Fill;
            panel2.Location = new Point(3, 33);
            panel2.Name = "panel2";
            panel2.Size = new Size(1040, 545);
            panel2.TabIndex = 1;
            // 
            // panelView
            // 
            panelView.Controls.Add(tableLayoutPanel2);
            panelView.Dock = DockStyle.Fill;
            panelView.Location = new Point(0, 64);
            panelView.Name = "panelView";
            panelView.Size = new Size(1040, 481);
            panelView.TabIndex = 1;
            // 
            // tableLayoutPanel2
            // 
            tableLayoutPanel2.ColumnCount = 2;
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 80F));
            tableLayoutPanel2.Controls.Add(panelMain, 1, 0);
            tableLayoutPanel2.Controls.Add(panelLog, 0, 0);
            tableLayoutPanel2.Dock = DockStyle.Fill;
            tableLayoutPanel2.Location = new Point(0, 0);
            tableLayoutPanel2.Name = "tableLayoutPanel2";
            tableLayoutPanel2.RowCount = 1;
            tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel2.Size = new Size(1040, 481);
            tableLayoutPanel2.TabIndex = 3;
            // 
            // panelMain
            // 
            panelMain.BackColor = Color.Gainsboro;
            panelMain.Dock = DockStyle.Fill;
            panelMain.Location = new Point(211, 3);
            panelMain.Name = "panelMain";
            panelMain.Size = new Size(826, 475);
            panelMain.TabIndex = 0;
            // 
            // panelLog
            // 
            panelLog.Controls.Add(dgviewLog);
            panelLog.Dock = DockStyle.Fill;
            panelLog.Location = new Point(3, 3);
            panelLog.Name = "panelLog";
            panelLog.Size = new Size(202, 475);
            panelLog.TabIndex = 1;
            // 
            // dgviewLog
            // 
            dgviewLog.BackgroundColor = Color.Gainsboro;
            dgviewLog.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgviewLog.Dock = DockStyle.Fill;
            dgviewLog.Location = new Point(0, 0);
            dgviewLog.Name = "dgviewLog";
            dgviewLog.RowHeadersVisible = false;
            dgviewLog.Size = new Size(202, 475);
            dgviewLog.TabIndex = 0;
            // 
            // panelSetting
            // 
            panelSetting.Controls.Add(btnStopCamera);
            panelSetting.Controls.Add(btnStartCamera);
            panelSetting.Controls.Add(btnAlarm);
            panelSetting.Controls.Add(btnSetting);
            panelSetting.Controls.Add(btnLogView);
            panelSetting.Dock = DockStyle.Top;
            panelSetting.Location = new Point(0, 0);
            panelSetting.Name = "panelSetting";
            panelSetting.Size = new Size(1040, 64);
            panelSetting.TabIndex = 0;
            panelSetting.Visible = false;
            // 
            // btnStopCamera
            // 
            btnStopCamera.BackColor = Color.Aquamarine;
            btnStopCamera.Dock = DockStyle.Left;
            btnStopCamera.FlatAppearance.BorderColor = Color.White;
            btnStopCamera.FlatStyle = FlatStyle.Flat;
            btnStopCamera.Font = new Font("Microsoft YaHei", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnStopCamera.ForeColor = SystemColors.ButtonHighlight;
            btnStopCamera.Image = (Image)resources.GetObject("btnStopCamera.Image");
            btnStopCamera.ImageAlign = ContentAlignment.TopCenter;
            btnStopCamera.Location = new Point(63, 0);
            btnStopCamera.Name = "btnStopCamera";
            btnStopCamera.Size = new Size(63, 64);
            btnStopCamera.TabIndex = 4;
            btnStopCamera.Text = "STOP";
            btnStopCamera.TextAlign = ContentAlignment.BottomCenter;
            btnStopCamera.TextImageRelation = TextImageRelation.ImageAboveText;
            btnStopCamera.UseVisualStyleBackColor = false;
            btnStopCamera.Click += btnStopCamera_Click;
            // 
            // btnStartCamera
            // 
            btnStartCamera.BackColor = Color.Aquamarine;
            btnStartCamera.Dock = DockStyle.Left;
            btnStartCamera.FlatAppearance.BorderColor = Color.White;
            btnStartCamera.FlatStyle = FlatStyle.Flat;
            btnStartCamera.Font = new Font("Microsoft YaHei", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnStartCamera.ForeColor = SystemColors.ButtonHighlight;
            btnStartCamera.Image = (Image)resources.GetObject("btnStartCamera.Image");
            btnStartCamera.ImageAlign = ContentAlignment.TopCenter;
            btnStartCamera.Location = new Point(0, 0);
            btnStartCamera.Name = "btnStartCamera";
            btnStartCamera.Size = new Size(63, 64);
            btnStartCamera.TabIndex = 3;
            btnStartCamera.Text = "START";
            btnStartCamera.TextAlign = ContentAlignment.BottomCenter;
            btnStartCamera.TextImageRelation = TextImageRelation.ImageAboveText;
            btnStartCamera.UseVisualStyleBackColor = false;
            btnStartCamera.Click += btnStartCamera_Click;
            // 
            // btnAlarm
            // 
            btnAlarm.BackColor = Color.Aquamarine;
            btnAlarm.Dock = DockStyle.Right;
            btnAlarm.FlatAppearance.BorderSize = 0;
            btnAlarm.FlatStyle = FlatStyle.Flat;
            btnAlarm.Font = new Font("Microsoft YaHei", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnAlarm.ForeColor = SystemColors.ButtonHighlight;
            btnAlarm.Image = Properties.Resources.imac_settings_32px;
            btnAlarm.ImageAlign = ContentAlignment.TopCenter;
            btnAlarm.Location = new Point(770, 0);
            btnAlarm.Name = "btnAlarm";
            btnAlarm.Size = new Size(90, 64);
            btnAlarm.TabIndex = 2;
            btnAlarm.Text = "ALARM";
            btnAlarm.TextAlign = ContentAlignment.BottomCenter;
            btnAlarm.TextImageRelation = TextImageRelation.ImageAboveText;
            btnAlarm.UseVisualStyleBackColor = false;
            btnAlarm.Click += btnAlarm_Click;
            // 
            // btnSetting
            // 
            btnSetting.BackColor = Color.Aquamarine;
            btnSetting.Dock = DockStyle.Right;
            btnSetting.FlatAppearance.BorderSize = 0;
            btnSetting.FlatStyle = FlatStyle.Flat;
            btnSetting.Font = new Font("Microsoft YaHei", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnSetting.ForeColor = SystemColors.ButtonHighlight;
            btnSetting.Image = (Image)resources.GetObject("btnSetting.Image");
            btnSetting.ImageAlign = ContentAlignment.TopCenter;
            btnSetting.Location = new Point(860, 0);
            btnSetting.Name = "btnSetting";
            btnSetting.Size = new Size(90, 64);
            btnSetting.TabIndex = 1;
            btnSetting.Text = "SETTING";
            btnSetting.TextAlign = ContentAlignment.BottomCenter;
            btnSetting.TextImageRelation = TextImageRelation.ImageAboveText;
            btnSetting.UseVisualStyleBackColor = false;
            btnSetting.Click += btnSetting_Click;
            // 
            // btnLogView
            // 
            btnLogView.BackColor = Color.Aquamarine;
            btnLogView.Dock = DockStyle.Right;
            btnLogView.FlatAppearance.BorderSize = 0;
            btnLogView.FlatStyle = FlatStyle.Flat;
            btnLogView.Font = new Font("Microsoft YaHei", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnLogView.ForeColor = SystemColors.ButtonHighlight;
            btnLogView.Image = Properties.Resources.imac_settings_32px;
            btnLogView.ImageAlign = ContentAlignment.TopCenter;
            btnLogView.Location = new Point(950, 0);
            btnLogView.Name = "btnLogView";
            btnLogView.Size = new Size(90, 64);
            btnLogView.TabIndex = 0;
            btnLogView.Text = "LOG";
            btnLogView.TextAlign = ContentAlignment.BottomCenter;
            btnLogView.TextImageRelation = TextImageRelation.ImageAboveText;
            btnLogView.UseVisualStyleBackColor = false;
            btnLogView.Click += btnLogView_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1046, 581);
            Controls.Add(tableLayoutPanel1);
            FormBorderStyle = FormBorderStyle.None;
            Name = "Form1";
            Text = "Form1";
            FormClosing += Form1_FormClosing;
            Load += Form1_Load;
            KeyDown += Form1_KeyDown;
            tableLayoutPanel1.ResumeLayout(false);
            panelHeader.ResumeLayout(false);
            panelHeader.PerformLayout();
            panel2.ResumeLayout(false);
            panelView.ResumeLayout(false);
            tableLayoutPanel2.ResumeLayout(false);
            panelLog.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgviewLog).EndInit();
            panelSetting.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion
        private System.Windows.Forms.Timer DisplayTimer;
        private TableLayoutPanel tableLayoutPanel1;
        private Panel panelHeader;
        private Label label1;
        private Panel panel2;
        private Button btnMinimize;
        private Button btnMaximize;
        private Button btnExit;
        private Panel panelView;
        private TableLayoutPanel tableLayoutPanel2;
        private Panel panelMain;
        private Panel panelLog;
        private DataGridView dgviewLog;
        private Panel panelSetting;
        private Button btnAlarm;
        private Button btnSetting;
        private Button btnLogView;
        private Button btnHideSetting;
        private Button btnStartCamera;
        private Button btnStopCamera;
    }
}
