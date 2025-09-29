namespace DKVN
{
    partial class FormConfirmVision
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormConfirmVision));
            btnNG = new Button();
            lbAlarmMessage = new Label();
            timer1 = new System.Windows.Forms.Timer(components);
            btnOK = new Button();
            pictureBox1 = new PictureBox();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            SuspendLayout();
            // 
            // btnNG
            // 
            btnNG.BackColor = Color.Red;
            btnNG.Font = new Font("Century Gothic", 20.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            btnNG.ForeColor = Color.White;
            btnNG.Location = new Point(260, 106);
            btnNG.Name = "btnNG";
            btnNG.Size = new Size(123, 53);
            btnNG.TabIndex = 10;
            btnNG.Text = "Cancel";
            btnNG.UseVisualStyleBackColor = false;
            btnNG.Click += btnNG_Click;
            // 
            // lbAlarmMessage
            // 
            lbAlarmMessage.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lbAlarmMessage.BackColor = Color.IndianRed;
            lbAlarmMessage.Font = new Font("Century Gothic", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lbAlarmMessage.ForeColor = Color.Black;
            lbAlarmMessage.Location = new Point(127, 5);
            lbAlarmMessage.Name = "lbAlarmMessage";
            lbAlarmMessage.Size = new Size(256, 98);
            lbAlarmMessage.TabIndex = 9;
            lbAlarmMessage.Text = "Vision detection Warning. Please confirm the results below.";
            lbAlarmMessage.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // timer1
            // 
            timer1.Enabled = true;
            timer1.Interval = 1000;
            // 
            // btnOK
            // 
            btnOK.BackColor = Color.LimeGreen;
            btnOK.Font = new Font("Century Gothic", 20.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            btnOK.ForeColor = Color.White;
            btnOK.Location = new Point(125, 106);
            btnOK.Name = "btnOK";
            btnOK.Size = new Size(129, 53);
            btnOK.TabIndex = 10;
            btnOK.Text = "Confirm";
            btnOK.UseVisualStyleBackColor = false;
            btnOK.Click += btnOK_Click;
            // 
            // pictureBox1
            // 
            pictureBox1.Image = (Image)resources.GetObject("pictureBox1.Image");
            pictureBox1.Location = new Point(1, 5);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(120, 98);
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox1.TabIndex = 11;
            pictureBox1.TabStop = false;
            // 
            // FormConfirmVision
            // 
            AutoScaleDimensions = new SizeF(8F, 19F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(389, 168);
            Controls.Add(pictureBox1);
            Controls.Add(btnOK);
            Controls.Add(btnNG);
            Controls.Add(lbAlarmMessage);
            Font = new Font("Microsoft YaHei UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Margin = new Padding(4);
            TopMost = true;
            Name = "FormConfirmVision";
            StartPosition = FormStartPosition.Manual;
            Text = "FormConfirmVision";
            FormClosing += FormConfirmVision_FormClosing;
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnNG;
        private System.Windows.Forms.Label lbAlarmMessage;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.PictureBox pictureBox1;
    }
}
