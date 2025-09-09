namespace CameraManager
{
    partial class MeasureRecipe2
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            panelPage1Setting = new Panel();
            tableLayoutPanel2 = new TableLayoutPanel();
            panel3 = new Panel();
            panelBaseTool = new Panel();
            dgviewCamera = new DataGridView();
            panel4 = new Panel();
            btnRefreshRecipe = new Button();
            btnAddToolBase = new Button();
            lbTitleName = new Label();
            tableLayoutPanel1 = new TableLayoutPanel();
            panel1 = new Panel();
            groupBox1 = new GroupBox();
            btnApply = new Button();
            num_y1 = new NumericUpDown();
            label3 = new Label();
            num_x1 = new NumericUpDown();
            label2 = new Label();
            numInterval = new NumericUpDown();
            label1 = new Label();
            panel2 = new Panel();
            panelMain = new Panel();
            pictureBox1 = new PictureBox();
            toolStrip1 = new ToolStrip();
            toolStripRunImage = new ToolStripButton();
            toolStripSeparator1 = new ToolStripSeparator();
            toolStripOpenImage = new ToolStripButton();
            toolStripSeparator3 = new ToolStripSeparator();
            toolStripProcessTime = new ToolStripLabel();
            toolStripSeparator6 = new ToolStripSeparator();
            toolStripSeparator7 = new ToolStripSeparator();
            toolStripStatus = new ToolStripLabel();
            toolStripLabel2 = new ToolStripLabel();
            toolStripDropDownButton1 = new ToolStripDropDownButton();
            commonGraphicToolStripMenuItem = new ToolStripMenuItem();
            showResultGraphicToolStripMenuItem = new ToolStripMenuItem();
            showTextGraphicToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator8 = new ToolStripSeparator();
            toolStripLabel3 = new ToolStripLabel();
            toolStripRuntime = new ToolStripButton();
            mySqlDataAdapter1 = new MySql.Data.MySqlClient.MySqlDataAdapter();
            btnClear = new Button();
            btnDraw4Point = new Button();
            num_y2 = new NumericUpDown();
            label4 = new Label();
            num_x2 = new NumericUpDown();
            label5 = new Label();
            numericUpDown3 = new NumericUpDown();
            label6 = new Label();
            num_x3 = new NumericUpDown();
            label7 = new Label();
            numericUpDown5 = new NumericUpDown();
            label8 = new Label();
            numericUpDown6 = new NumericUpDown();
            label9 = new Label();
            panelPage1Setting.SuspendLayout();
            tableLayoutPanel2.SuspendLayout();
            panel3.SuspendLayout();
            panelBaseTool.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgviewCamera).BeginInit();
            panel4.SuspendLayout();
            tableLayoutPanel1.SuspendLayout();
            panel1.SuspendLayout();
            groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)num_y1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)num_x1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numInterval).BeginInit();
            panel2.SuspendLayout();
            panelMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)num_y2).BeginInit();
            ((System.ComponentModel.ISupportInitialize)num_x2).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDown3).BeginInit();
            ((System.ComponentModel.ISupportInitialize)num_x3).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDown5).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDown6).BeginInit();
            SuspendLayout();
            // 
            // panelPage1Setting
            // 
            panelPage1Setting.AutoScroll = true;
            panelPage1Setting.BackColor = SystemColors.ControlLightLight;
            panelPage1Setting.BorderStyle = BorderStyle.FixedSingle;
            panelPage1Setting.Controls.Add(tableLayoutPanel2);
            panelPage1Setting.Controls.Add(lbTitleName);
            panelPage1Setting.Dock = DockStyle.Fill;
            panelPage1Setting.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            panelPage1Setting.Location = new Point(3, 3);
            panelPage1Setting.Name = "panelPage1Setting";
            panelPage1Setting.Size = new Size(294, 817);
            panelPage1Setting.TabIndex = 3;
            // 
            // tableLayoutPanel2
            // 
            tableLayoutPanel2.ColumnCount = 1;
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel2.Controls.Add(panel3, 0, 0);
            tableLayoutPanel2.Dock = DockStyle.Fill;
            tableLayoutPanel2.Location = new Point(0, 32);
            tableLayoutPanel2.Name = "tableLayoutPanel2";
            tableLayoutPanel2.RowCount = 1;
            tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            tableLayoutPanel2.Size = new Size(292, 783);
            tableLayoutPanel2.TabIndex = 1;
            // 
            // panel3
            // 
            panel3.Controls.Add(panelBaseTool);
            panel3.Dock = DockStyle.Fill;
            panel3.Location = new Point(3, 3);
            panel3.Name = "panel3";
            panel3.Size = new Size(286, 777);
            panel3.TabIndex = 0;
            // 
            // panelBaseTool
            // 
            panelBaseTool.Controls.Add(dgviewCamera);
            panelBaseTool.Controls.Add(panel4);
            panelBaseTool.Dock = DockStyle.Fill;
            panelBaseTool.Location = new Point(0, 0);
            panelBaseTool.Name = "panelBaseTool";
            panelBaseTool.Size = new Size(286, 777);
            panelBaseTool.TabIndex = 20;
            // 
            // dgviewCamera
            // 
            dgviewCamera.AllowUserToAddRows = false;
            dgviewCamera.AllowUserToDeleteRows = false;
            dgviewCamera.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgviewCamera.Dock = DockStyle.Fill;
            dgviewCamera.Location = new Point(0, 40);
            dgviewCamera.Margin = new Padding(0);
            dgviewCamera.Name = "dgviewCamera";
            dgviewCamera.ReadOnly = true;
            dgviewCamera.RowHeadersVisible = false;
            dgviewCamera.RowHeadersWidth = 51;
            dgviewCamera.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgviewCamera.Size = new Size(286, 737);
            dgviewCamera.TabIndex = 1;
            dgviewCamera.SelectionChanged += dgviewCamera_SelectionChanged;
            // 
            // panel4
            // 
            panel4.BorderStyle = BorderStyle.FixedSingle;
            panel4.Controls.Add(btnRefreshRecipe);
            panel4.Controls.Add(btnAddToolBase);
            panel4.Dock = DockStyle.Top;
            panel4.Location = new Point(0, 0);
            panel4.Name = "panel4";
            panel4.Size = new Size(286, 40);
            panel4.TabIndex = 0;
            // 
            // btnRefreshRecipe
            // 
            btnRefreshRecipe.BackColor = SystemColors.ControlLightLight;
            btnRefreshRecipe.BackgroundImageLayout = ImageLayout.Zoom;
            btnRefreshRecipe.Dock = DockStyle.Left;
            btnRefreshRecipe.FlatAppearance.BorderSize = 0;
            btnRefreshRecipe.FlatAppearance.MouseDownBackColor = Color.Gray;
            btnRefreshRecipe.FlatAppearance.MouseOverBackColor = Color.Gray;
            btnRefreshRecipe.FlatStyle = FlatStyle.Flat;
            btnRefreshRecipe.Image = Properties.Resources.synchronize_28px;
            btnRefreshRecipe.Location = new Point(0, 0);
            btnRefreshRecipe.Name = "btnRefreshRecipe";
            btnRefreshRecipe.Size = new Size(50, 38);
            btnRefreshRecipe.TabIndex = 16;
            btnRefreshRecipe.UseVisualStyleBackColor = false;
            btnRefreshRecipe.Click += btnRefreshRecipe_Click;
            // 
            // btnAddToolBase
            // 
            btnAddToolBase.Dock = DockStyle.Right;
            btnAddToolBase.FlatAppearance.BorderSize = 0;
            btnAddToolBase.FlatStyle = FlatStyle.Flat;
            btnAddToolBase.Image = Properties.Resources.add_new_28px;
            btnAddToolBase.Location = new Point(68, 0);
            btnAddToolBase.Name = "btnAddToolBase";
            btnAddToolBase.Size = new Size(216, 38);
            btnAddToolBase.TabIndex = 1;
            btnAddToolBase.Text = "Manager Camera";
            btnAddToolBase.TextImageRelation = TextImageRelation.TextBeforeImage;
            btnAddToolBase.UseVisualStyleBackColor = true;
            btnAddToolBase.Click += btnAddToolBase_Click;
            // 
            // lbTitleName
            // 
            lbTitleName.BackColor = Color.Lavender;
            lbTitleName.Dock = DockStyle.Top;
            lbTitleName.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lbTitleName.Location = new Point(0, 0);
            lbTitleName.Name = "lbTitleName";
            lbTitleName.Size = new Size(292, 32);
            lbTitleName.TabIndex = 0;
            lbTitleName.Text = "CAMERA";
            lbTitleName.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 3;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Controls.Add(panelPage1Setting, 0, 0);
            tableLayoutPanel1.Controls.Add(panel1, 1, 0);
            tableLayoutPanel1.Controls.Add(panel2, 2, 0);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(0, 0);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 1;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Size = new Size(1110, 823);
            tableLayoutPanel1.TabIndex = 4;
            // 
            // panel1
            // 
            panel1.Controls.Add(groupBox1);
            panel1.Dock = DockStyle.Fill;
            panel1.Location = new Point(303, 3);
            panel1.Name = "panel1";
            panel1.Size = new Size(294, 817);
            panel1.TabIndex = 4;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(numericUpDown5);
            groupBox1.Controls.Add(label8);
            groupBox1.Controls.Add(numericUpDown6);
            groupBox1.Controls.Add(label9);
            groupBox1.Controls.Add(numericUpDown3);
            groupBox1.Controls.Add(label6);
            groupBox1.Controls.Add(num_x3);
            groupBox1.Controls.Add(label7);
            groupBox1.Controls.Add(num_y2);
            groupBox1.Controls.Add(label4);
            groupBox1.Controls.Add(num_x2);
            groupBox1.Controls.Add(label5);
            groupBox1.Controls.Add(btnDraw4Point);
            groupBox1.Controls.Add(btnClear);
            groupBox1.Controls.Add(btnApply);
            groupBox1.Controls.Add(num_y1);
            groupBox1.Controls.Add(label3);
            groupBox1.Controls.Add(num_x1);
            groupBox1.Controls.Add(label2);
            groupBox1.Controls.Add(numInterval);
            groupBox1.Controls.Add(label1);
            groupBox1.Font = new Font("Microsoft YaHei", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            groupBox1.Location = new Point(3, 3);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(288, 436);
            groupBox1.TabIndex = 0;
            groupBox1.TabStop = false;
            groupBox1.Text = "Parametter";
            // 
            // btnApply
            // 
            btnApply.BackColor = Color.Gainsboro;
            btnApply.FlatAppearance.BorderSize = 0;
            btnApply.Location = new Point(0, 394);
            btnApply.Name = "btnApply";
            btnApply.Size = new Size(288, 32);
            btnApply.TabIndex = 7;
            btnApply.Text = "Apply";
            btnApply.TextImageRelation = TextImageRelation.TextBeforeImage;
            btnApply.UseVisualStyleBackColor = false;
            btnApply.Click += btnApply_Click;
            // 
            // num_y1
            // 
            num_y1.DecimalPlaces = 2;
            num_y1.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            num_y1.Location = new Point(168, 101);
            num_y1.Maximum = new decimal(new int[] { 1000000, 0, 0, 0 });
            num_y1.Minimum = new decimal(new int[] { 1, 0, 0, 65536 });
            num_y1.Name = "num_y1";
            num_y1.Size = new Size(114, 25);
            num_y1.TabIndex = 6;
            num_y1.Value = new decimal(new int[] { 1, 0, 0, 65536 });
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Font = new Font("Microsoft YaHei", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label3.Location = new Point(6, 103);
            label3.Name = "label3";
            label3.Size = new Size(27, 19);
            label3.TabIndex = 5;
            label3.Text = "y1:";
            // 
            // num_x1
            // 
            num_x1.DecimalPlaces = 2;
            num_x1.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            num_x1.Location = new Point(168, 68);
            num_x1.Maximum = new decimal(new int[] { 1000000, 0, 0, 0 });
            num_x1.Minimum = new decimal(new int[] { 1, 0, 0, 65536 });
            num_x1.Name = "num_x1";
            num_x1.Size = new Size(114, 25);
            num_x1.TabIndex = 4;
            num_x1.Value = new decimal(new int[] { 1, 0, 0, 65536 });
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new Font("Microsoft YaHei", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label2.Location = new Point(6, 70);
            label2.Name = "label2";
            label2.Size = new Size(27, 19);
            label2.TabIndex = 3;
            label2.Text = "x1:";
            // 
            // numInterval
            // 
            numInterval.Increment = new decimal(new int[] { 5, 0, 0, 0 });
            numInterval.Location = new Point(168, 33);
            numInterval.Maximum = new decimal(new int[] { 30, 0, 0, 0 });
            numInterval.Minimum = new decimal(new int[] { 5, 0, 0, 0 });
            numInterval.Name = "numInterval";
            numInterval.Size = new Size(114, 25);
            numInterval.TabIndex = 2;
            numInterval.Value = new decimal(new int[] { 5, 0, 0, 0 });
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Microsoft YaHei", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label1.Location = new Point(6, 35);
            label1.Name = "label1";
            label1.Size = new Size(58, 19);
            label1.TabIndex = 1;
            label1.Text = "Interval:";
            // 
            // panel2
            // 
            panel2.Controls.Add(panelMain);
            panel2.Controls.Add(toolStrip1);
            panel2.Dock = DockStyle.Fill;
            panel2.Location = new Point(603, 3);
            panel2.Name = "panel2";
            panel2.Size = new Size(504, 817);
            panel2.TabIndex = 5;
            // 
            // panelMain
            // 
            panelMain.Controls.Add(pictureBox1);
            panelMain.Dock = DockStyle.Fill;
            panelMain.Location = new Point(0, 35);
            panelMain.Name = "panelMain";
            panelMain.Size = new Size(504, 782);
            panelMain.TabIndex = 11;
            // 
            // pictureBox1
            // 
            pictureBox1.BorderStyle = BorderStyle.FixedSingle;
            pictureBox1.Dock = DockStyle.Fill;
            pictureBox1.Location = new Point(0, 0);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(504, 782);
            pictureBox1.TabIndex = 0;
            pictureBox1.TabStop = false;
            // 
            // toolStrip1
            // 
            toolStrip1.ImageScalingSize = new Size(24, 24);
            toolStrip1.Items.AddRange(new ToolStripItem[] { toolStripRunImage, toolStripSeparator1, toolStripOpenImage, toolStripSeparator3, toolStripProcessTime, toolStripSeparator6, toolStripSeparator7, toolStripStatus, toolStripLabel2, toolStripDropDownButton1, toolStripSeparator8, toolStripLabel3, toolStripRuntime });
            toolStrip1.Location = new Point(0, 0);
            toolStrip1.Name = "toolStrip1";
            toolStrip1.Size = new Size(504, 35);
            toolStrip1.TabIndex = 10;
            toolStrip1.Text = "toolStrip1";
            // 
            // toolStripRunImage
            // 
            toolStripRunImage.DisplayStyle = ToolStripItemDisplayStyle.Image;
            toolStripRunImage.Image = Properties.Resources.next_28px;
            toolStripRunImage.ImageScaling = ToolStripItemImageScaling.None;
            toolStripRunImage.ImageTransparentColor = Color.Magenta;
            toolStripRunImage.Name = "toolStripRunImage";
            toolStripRunImage.Size = new Size(32, 32);
            toolStripRunImage.Text = "Run Image";
            toolStripRunImage.Click += toolStripRun_Click;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Margin = new Padding(10, 0, 10, 0);
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(6, 35);
            // 
            // toolStripOpenImage
            // 
            toolStripOpenImage.DisplayStyle = ToolStripItemDisplayStyle.Image;
            toolStripOpenImage.Image = Properties.Resources.image_28px;
            toolStripOpenImage.ImageScaling = ToolStripItemImageScaling.None;
            toolStripOpenImage.ImageTransparentColor = Color.Magenta;
            toolStripOpenImage.Name = "toolStripOpenImage";
            toolStripOpenImage.Size = new Size(32, 32);
            toolStripOpenImage.Text = "Open File";
            toolStripOpenImage.Click += toolStripOpenImage_Click;
            // 
            // toolStripSeparator3
            // 
            toolStripSeparator3.Margin = new Padding(5, 0, 5, 0);
            toolStripSeparator3.Name = "toolStripSeparator3";
            toolStripSeparator3.Size = new Size(6, 35);
            // 
            // toolStripProcessTime
            // 
            toolStripProcessTime.Alignment = ToolStripItemAlignment.Right;
            toolStripProcessTime.Name = "toolStripProcessTime";
            toolStripProcessTime.Size = new Size(32, 32);
            toolStripProcessTime.Text = "0 ms";
            // 
            // toolStripSeparator6
            // 
            toolStripSeparator6.Name = "toolStripSeparator6";
            toolStripSeparator6.Size = new Size(6, 35);
            // 
            // toolStripSeparator7
            // 
            toolStripSeparator7.Alignment = ToolStripItemAlignment.Right;
            toolStripSeparator7.Name = "toolStripSeparator7";
            toolStripSeparator7.Size = new Size(6, 35);
            // 
            // toolStripStatus
            // 
            toolStripStatus.Alignment = ToolStripItemAlignment.Right;
            toolStripStatus.Name = "toolStripStatus";
            toolStripStatus.Size = new Size(39, 32);
            toolStripStatus.Text = "Status";
            // 
            // toolStripLabel2
            // 
            toolStripLabel2.Name = "toolStripLabel2";
            toolStripLabel2.Size = new Size(48, 32);
            toolStripLabel2.Text = "Graphic";
            // 
            // toolStripDropDownButton1
            // 
            toolStripDropDownButton1.DisplayStyle = ToolStripItemDisplayStyle.Image;
            toolStripDropDownButton1.DropDownItems.AddRange(new ToolStripItem[] { commonGraphicToolStripMenuItem, showResultGraphicToolStripMenuItem, showTextGraphicToolStripMenuItem });
            toolStripDropDownButton1.ImageTransparentColor = Color.Magenta;
            toolStripDropDownButton1.Name = "toolStripDropDownButton1";
            toolStripDropDownButton1.Size = new Size(13, 32);
            toolStripDropDownButton1.Text = "Graphic Option";
            // 
            // commonGraphicToolStripMenuItem
            // 
            commonGraphicToolStripMenuItem.CheckOnClick = true;
            commonGraphicToolStripMenuItem.Name = "commonGraphicToolStripMenuItem";
            commonGraphicToolStripMenuItem.Size = new Size(182, 22);
            commonGraphicToolStripMenuItem.Text = "Common Graphic";
            // 
            // showResultGraphicToolStripMenuItem
            // 
            showResultGraphicToolStripMenuItem.CheckOnClick = true;
            showResultGraphicToolStripMenuItem.Name = "showResultGraphicToolStripMenuItem";
            showResultGraphicToolStripMenuItem.Size = new Size(182, 22);
            showResultGraphicToolStripMenuItem.Text = "Show Result Graphic";
            // 
            // showTextGraphicToolStripMenuItem
            // 
            showTextGraphicToolStripMenuItem.CheckOnClick = true;
            showTextGraphicToolStripMenuItem.Name = "showTextGraphicToolStripMenuItem";
            showTextGraphicToolStripMenuItem.Size = new Size(182, 22);
            showTextGraphicToolStripMenuItem.Text = "Show Text Graphic";
            // 
            // toolStripSeparator8
            // 
            toolStripSeparator8.Margin = new Padding(0, 0, 5, 0);
            toolStripSeparator8.Name = "toolStripSeparator8";
            toolStripSeparator8.Size = new Size(6, 35);
            // 
            // toolStripLabel3
            // 
            toolStripLabel3.Name = "toolStripLabel3";
            toolStripLabel3.Size = new Size(86, 32);
            toolStripLabel3.Text = "Runtime Mode";
            // 
            // toolStripRuntime
            // 
            toolStripRuntime.AutoSize = false;
            toolStripRuntime.BackColor = SystemColors.ControlLight;
            toolStripRuntime.CheckOnClick = true;
            toolStripRuntime.DisplayStyle = ToolStripItemDisplayStyle.Image;
            toolStripRuntime.ImageTransparentColor = Color.Magenta;
            toolStripRuntime.Name = "toolStripRuntime";
            toolStripRuntime.Size = new Size(23, 23);
            toolStripRuntime.Text = "OFF";
            // 
            // mySqlDataAdapter1
            // 
            mySqlDataAdapter1.DeleteCommand = null;
            mySqlDataAdapter1.InsertCommand = null;
            mySqlDataAdapter1.SelectCommand = null;
            mySqlDataAdapter1.UpdateCommand = null;
            // 
            // btnClear
            // 
            btnClear.BackColor = Color.Gainsboro;
            btnClear.FlatAppearance.BorderSize = 0;
            btnClear.Location = new Point(0, 356);
            btnClear.Name = "btnClear";
            btnClear.Size = new Size(142, 32);
            btnClear.TabIndex = 8;
            btnClear.Text = "Clear";
            btnClear.TextImageRelation = TextImageRelation.TextBeforeImage;
            btnClear.UseVisualStyleBackColor = false;
            // 
            // btnDraw4Point
            // 
            btnDraw4Point.BackColor = Color.Gainsboro;
            btnDraw4Point.FlatAppearance.BorderSize = 0;
            btnDraw4Point.Location = new Point(146, 356);
            btnDraw4Point.Name = "btnDraw4Point";
            btnDraw4Point.Size = new Size(142, 32);
            btnDraw4Point.TabIndex = 9;
            btnDraw4Point.Text = "Draw Bbox";
            btnDraw4Point.TextImageRelation = TextImageRelation.TextBeforeImage;
            btnDraw4Point.UseVisualStyleBackColor = false;
            btnDraw4Point.Click += btnDraw4Point_Click;
            // 
            // num_y2
            // 
            num_y2.DecimalPlaces = 2;
            num_y2.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            num_y2.Location = new Point(168, 165);
            num_y2.Maximum = new decimal(new int[] { 1000000, 0, 0, 0 });
            num_y2.Minimum = new decimal(new int[] { 1, 0, 0, 65536 });
            num_y2.Name = "num_y2";
            num_y2.Size = new Size(114, 25);
            num_y2.TabIndex = 13;
            num_y2.Value = new decimal(new int[] { 1, 0, 0, 65536 });
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Font = new Font("Microsoft YaHei", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label4.Location = new Point(6, 167);
            label4.Name = "label4";
            label4.Size = new Size(27, 19);
            label4.TabIndex = 12;
            label4.Text = "y2:";
            // 
            // num_x2
            // 
            num_x2.DecimalPlaces = 2;
            num_x2.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            num_x2.Location = new Point(168, 132);
            num_x2.Maximum = new decimal(new int[] { 1000000, 0, 0, 0 });
            num_x2.Minimum = new decimal(new int[] { 1, 0, 0, 65536 });
            num_x2.Name = "num_x2";
            num_x2.Size = new Size(114, 25);
            num_x2.TabIndex = 11;
            num_x2.Value = new decimal(new int[] { 1, 0, 0, 65536 });
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Font = new Font("Microsoft YaHei", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label5.Location = new Point(6, 134);
            label5.Name = "label5";
            label5.Size = new Size(27, 19);
            label5.TabIndex = 10;
            label5.Text = "x2:";
            // 
            // numericUpDown3
            // 
            numericUpDown3.DecimalPlaces = 2;
            numericUpDown3.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            numericUpDown3.Location = new Point(168, 229);
            numericUpDown3.Maximum = new decimal(new int[] { 1000000, 0, 0, 0 });
            numericUpDown3.Minimum = new decimal(new int[] { 1, 0, 0, 65536 });
            numericUpDown3.Name = "numericUpDown3";
            numericUpDown3.Size = new Size(114, 25);
            numericUpDown3.TabIndex = 17;
            numericUpDown3.Value = new decimal(new int[] { 1, 0, 0, 65536 });
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Font = new Font("Microsoft YaHei", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label6.Location = new Point(6, 231);
            label6.Name = "label6";
            label6.Size = new Size(27, 19);
            label6.TabIndex = 16;
            label6.Text = "y3:";
            // 
            // num_x3
            // 
            num_x3.DecimalPlaces = 2;
            num_x3.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            num_x3.Location = new Point(168, 196);
            num_x3.Maximum = new decimal(new int[] { 1000000, 0, 0, 0 });
            num_x3.Minimum = new decimal(new int[] { 1, 0, 0, 65536 });
            num_x3.Name = "num_x3";
            num_x3.Size = new Size(114, 25);
            num_x3.TabIndex = 15;
            num_x3.Value = new decimal(new int[] { 1, 0, 0, 65536 });
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Font = new Font("Microsoft YaHei", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label7.Location = new Point(6, 198);
            label7.Name = "label7";
            label7.Size = new Size(27, 19);
            label7.TabIndex = 14;
            label7.Text = "x3:";
            // 
            // numericUpDown5
            // 
            numericUpDown5.DecimalPlaces = 2;
            numericUpDown5.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            numericUpDown5.Location = new Point(168, 293);
            numericUpDown5.Maximum = new decimal(new int[] { 1000000, 0, 0, 0 });
            numericUpDown5.Minimum = new decimal(new int[] { 1, 0, 0, 65536 });
            numericUpDown5.Name = "numericUpDown5";
            numericUpDown5.Size = new Size(114, 25);
            numericUpDown5.TabIndex = 21;
            numericUpDown5.Value = new decimal(new int[] { 1, 0, 0, 65536 });
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Font = new Font("Microsoft YaHei", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label8.Location = new Point(6, 295);
            label8.Name = "label8";
            label8.Size = new Size(27, 19);
            label8.TabIndex = 20;
            label8.Text = "y4:";
            // 
            // numericUpDown6
            // 
            numericUpDown6.DecimalPlaces = 2;
            numericUpDown6.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            numericUpDown6.Location = new Point(168, 260);
            numericUpDown6.Maximum = new decimal(new int[] { 1000000, 0, 0, 0 });
            numericUpDown6.Minimum = new decimal(new int[] { 1, 0, 0, 65536 });
            numericUpDown6.Name = "numericUpDown6";
            numericUpDown6.Size = new Size(114, 25);
            numericUpDown6.TabIndex = 19;
            numericUpDown6.Value = new decimal(new int[] { 1, 0, 0, 65536 });
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Font = new Font("Microsoft YaHei", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label9.Location = new Point(6, 262);
            label9.Name = "label9";
            label9.Size = new Size(27, 19);
            label9.TabIndex = 18;
            label9.Text = "x4:";
            // 
            // MeasureRecipe2
            // 
            AutoScaleDimensions = new SizeF(6F, 13F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(tableLayoutPanel1);
            Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            Margin = new Padding(3, 4, 3, 4);
            Name = "MeasureRecipe2";
            Size = new Size(1110, 823);
            Load += MeasureRecipe2_Load;
            panelPage1Setting.ResumeLayout(false);
            tableLayoutPanel2.ResumeLayout(false);
            panel3.ResumeLayout(false);
            panelBaseTool.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgviewCamera).EndInit();
            panel4.ResumeLayout(false);
            tableLayoutPanel1.ResumeLayout(false);
            panel1.ResumeLayout(false);
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)num_y1).EndInit();
            ((System.ComponentModel.ISupportInitialize)num_x1).EndInit();
            ((System.ComponentModel.ISupportInitialize)numInterval).EndInit();
            panel2.ResumeLayout(false);
            panel2.PerformLayout();
            panelMain.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            toolStrip1.ResumeLayout(false);
            toolStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)num_y2).EndInit();
            ((System.ComponentModel.ISupportInitialize)num_x2).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDown3).EndInit();
            ((System.ComponentModel.ISupportInitialize)num_x3).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDown5).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDown6).EndInit();
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panelPage1Setting;
        private System.Windows.Forms.Label lbTitleName;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.GroupBox groupBox1;
        private MySql.Data.MySqlClient.MySqlDataAdapter mySqlDataAdapter1;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Panel panelMain;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton toolStripRunImage;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripButton toolStripOpenImage;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripLabel toolStripProcessTime;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator6;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator7;
        private System.Windows.Forms.ToolStripLabel toolStripStatus;
        private System.Windows.Forms.ToolStripLabel toolStripLabel2;
        private System.Windows.Forms.ToolStripDropDownButton toolStripDropDownButton1;
        private System.Windows.Forms.ToolStripMenuItem commonGraphicToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showResultGraphicToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showTextGraphicToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator8;
        private System.Windows.Forms.ToolStripLabel toolStripLabel3;
        private System.Windows.Forms.ToolStripButton toolStripRuntime;
        private System.Windows.Forms.NumericUpDown num_y1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.NumericUpDown num_x1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.NumericUpDown numInterval;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnApply;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.Panel panelBaseTool;
        private System.Windows.Forms.DataGridView dgviewCamera;
        private System.Windows.Forms.Panel panel4;
        private System.Windows.Forms.Button btnRefreshRecipe;
        private System.Windows.Forms.Button btnAddToolBase;
        private System.Windows.Forms.Panel panel5;
        private System.Windows.Forms.Panel panelPCInfo;
        private System.Windows.Forms.Button btnConfigScreen;
        private System.Windows.Forms.NumericUpDown numRowCam;
        private System.Windows.Forms.NumericUpDown numColCam;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private PictureBox pictureBox1;
        private Button btnDraw4Point;
        private Button btnClear;
        private NumericUpDown numericUpDown5;
        private Label label8;
        private NumericUpDown numericUpDown6;
        private Label label9;
        private NumericUpDown numericUpDown3;
        private Label label6;
        private NumericUpDown num_x3;
        private Label label7;
        private NumericUpDown num_y2;
        private NumericUpDown num_x2;
    }
}
