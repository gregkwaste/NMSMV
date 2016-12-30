using System.Windows.Forms;
using OpenTK;

namespace Model_Viewer
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
            this.components = new System.ComponentModel.Container();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openAnimationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.optionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.settingsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
            this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusLabel3 = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabel2 = new System.Windows.Forms.ToolStripStatusLabel();
            this.glControl1 = new OpenTK.GLControl();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.camSettings = new System.Windows.Forms.TableLayoutPanel();
            this.zFar_Label = new System.Windows.Forms.Label();
            this.numericUpDown5 = new System.Windows.Forms.NumericUpDown();
            this.zNear_Label = new System.Windows.Forms.Label();
            this.numericUpDown4 = new System.Windows.Forms.NumericUpDown();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.numericUpDown1 = new System.Windows.Forms.NumericUpDown();
            this.label2 = new System.Windows.Forms.Label();
            this.numericUpDown2 = new System.Windows.Forms.NumericUpDown();
            this.numericUpDown3 = new System.Windows.Forms.NumericUpDown();
            this.l_intensity_nud = new System.Windows.Forms.NumericUpDown();
            this.splitContainer3 = new System.Windows.Forms.SplitContainer();
            this.sceneGraphGroup = new System.Windows.Forms.GroupBox();
            this.treeView1 = new Model_Viewer.NoClickTree();
            this.splitContainer4 = new System.Windows.Forms.SplitContainer();
            this.ProcGenGroup = new System.Windows.Forms.GroupBox();
            this.randomgenerator = new System.Windows.Forms.Button();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.AnimTable = new System.Windows.Forms.TableLayoutPanel();
            this.newButton1 = new Model_Viewer.NewButton();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.RightSplitter = new System.Windows.Forms.SplitContainer();
            this.selObjectBox = new System.Windows.Forms.GroupBox();
            this.rightFlowPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.selMatInfo = new System.Windows.Forms.GroupBox();
            this.selMatName = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.mainglcontrolContext = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.getAltIDToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportToObjToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.getObjectTexturesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menuStrip1.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.camSettings.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown5)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown4)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown2)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown3)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.l_intensity_nud)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).BeginInit();
            this.splitContainer3.Panel1.SuspendLayout();
            this.splitContainer3.Panel2.SuspendLayout();
            this.splitContainer3.SuspendLayout();
            this.sceneGraphGroup.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer4)).BeginInit();
            this.splitContainer4.Panel1.SuspendLayout();
            this.splitContainer4.Panel2.SuspendLayout();
            this.splitContainer4.SuspendLayout();
            this.ProcGenGroup.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.AnimTable.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.RightSplitter)).BeginInit();
            this.RightSplitter.Panel1.SuspendLayout();
            this.RightSplitter.Panel2.SuspendLayout();
            this.RightSplitter.SuspendLayout();
            this.selObjectBox.SuspendLayout();
            this.rightFlowPanel.SuspendLayout();
            this.selMatInfo.SuspendLayout();
            this.mainglcontrolContext.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.optionsToolStripMenuItem,
            this.aboutToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(1240, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openToolStripMenuItem,
            this.openAnimationToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // openToolStripMenuItem
            // 
            this.openToolStripMenuItem.Name = "openToolStripMenuItem";
            this.openToolStripMenuItem.Size = new System.Drawing.Size(162, 22);
            this.openToolStripMenuItem.Text = "Open";
            this.openToolStripMenuItem.Click += new System.EventHandler(this.openToolStripMenuItem_Click);
            // 
            // openAnimationToolStripMenuItem
            // 
            this.openAnimationToolStripMenuItem.Name = "openAnimationToolStripMenuItem";
            this.openAnimationToolStripMenuItem.Size = new System.Drawing.Size(162, 22);
            this.openAnimationToolStripMenuItem.Text = "Open Animation";
            this.openAnimationToolStripMenuItem.Click += new System.EventHandler(this.openAnimationToolStripMenuItem_Click);
            // 
            // optionsToolStripMenuItem
            // 
            this.optionsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.settingsToolStripMenuItem});
            this.optionsToolStripMenuItem.Name = "optionsToolStripMenuItem";
            this.optionsToolStripMenuItem.Size = new System.Drawing.Size(61, 20);
            this.optionsToolStripMenuItem.Text = "Options";
            // 
            // settingsToolStripMenuItem
            // 
            this.settingsToolStripMenuItem.Name = "settingsToolStripMenuItem";
            this.settingsToolStripMenuItem.Size = new System.Drawing.Size(116, 22);
            this.settingsToolStripMenuItem.Text = "Settings";
            this.settingsToolStripMenuItem.Click += new System.EventHandler(this.settingsToolStripMenuItem_Click);
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size(52, 20);
            this.aboutToolStripMenuItem.Text = "About";
            this.aboutToolStripMenuItem.Click += new System.EventHandler(this.aboutToolStripMenuItem_Click);
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            // 
            // backgroundWorker1
            // 
            this.backgroundWorker1.WorkerReportsProgress = true;
            this.backgroundWorker1.WorkerSupportsCancellation = true;
            this.backgroundWorker1.DoWork += new System.ComponentModel.DoWorkEventHandler(this.backgroundWorker1_DoWork);
            this.backgroundWorker1.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.backgroundWorker1_ProgressChanged);
            // 
            // toolStripStatusLabel1
            // 
            this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            this.toolStripStatusLabel1.Size = new System.Drawing.Size(39, 17);
            this.toolStripStatusLabel1.Text = "Ready";
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabel1,
            this.toolStripStatusLabel3,
            this.toolStripStatusLabel2});
            this.statusStrip1.Location = new System.Drawing.Point(0, 647);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(1240, 22);
            this.statusStrip1.TabIndex = 3;
            this.statusStrip1.Text = "status";
            // 
            // toolStripStatusLabel3
            // 
            this.toolStripStatusLabel3.Name = "toolStripStatusLabel3";
            this.toolStripStatusLabel3.Size = new System.Drawing.Size(1059, 17);
            this.toolStripStatusLabel3.Spring = true;
            // 
            // toolStripStatusLabel2
            // 
            this.toolStripStatusLabel2.Name = "toolStripStatusLabel2";
            this.toolStripStatusLabel2.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.toolStripStatusLabel2.Size = new System.Drawing.Size(127, 17);
            this.toolStripStatusLabel2.Text = "Created by gregkwaste";
            // 
            // glControl1
            // 
            this.glControl1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.glControl1.BackColor = System.Drawing.Color.Black;
            this.glControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.glControl1.Location = new System.Drawing.Point(0, 0);
            this.glControl1.MinimumSize = new System.Drawing.Size(256, 256);
            this.glControl1.Name = "glControl1";
            this.glControl1.Size = new System.Drawing.Size(661, 623);
            this.glControl1.TabIndex = 1;
            this.glControl1.VSync = true;
            this.glControl1.Load += new System.EventHandler(this.glControl_Load);
            this.glControl1.Paint += new System.Windows.Forms.PaintEventHandler(this.glControl1_Paint);
            this.glControl1.Enter += new System.EventHandler(this.glControl1_Enter);
            this.glControl1.Leave += new System.EventHandler(this.glControl1_Leave);
            this.glControl1.MouseClick += new System.Windows.Forms.MouseEventHandler(this.glControl1_MouseClick);
            this.glControl1.MouseHover += new System.EventHandler(this.glControl1_MouseHover);
            this.glControl1.MouseMove += new System.Windows.Forms.MouseEventHandler(this.glControl1_MouseMove);
            this.glControl1.MouseWheel += new System.Windows.Forms.MouseEventHandler(this.glControl1_Scroll);
            this.glControl1.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(this.glControl1_KeyDown);
            this.glControl1.Resize += new System.EventHandler(this.glControl1_Resize);
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Name = "splitContainer2";
            this.splitContainer2.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.groupBox1);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.splitContainer3);
            this.splitContainer2.Size = new System.Drawing.Size(257, 623);
            this.splitContainer2.SplitterDistance = 170;
            this.splitContainer2.TabIndex = 0;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.camSettings);
            this.groupBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox1.Location = new System.Drawing.Point(0, 0);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(257, 170);
            this.groupBox1.TabIndex = 3;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Camera Options";
            // 
            // camSettings
            // 
            this.camSettings.ColumnCount = 2;
            this.camSettings.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 47.05882F));
            this.camSettings.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 52.94118F));
            this.camSettings.Controls.Add(this.zFar_Label, 0, 5);
            this.camSettings.Controls.Add(this.numericUpDown5, 1, 5);
            this.camSettings.Controls.Add(this.zNear_Label, 0, 4);
            this.camSettings.Controls.Add(this.numericUpDown4, 1, 4);
            this.camSettings.Controls.Add(this.label3, 0, 3);
            this.camSettings.Controls.Add(this.label4, 0, 2);
            this.camSettings.Controls.Add(this.label1, 0, 0);
            this.camSettings.Controls.Add(this.numericUpDown1, 1, 0);
            this.camSettings.Controls.Add(this.label2, 0, 1);
            this.camSettings.Controls.Add(this.numericUpDown2, 1, 1);
            this.camSettings.Controls.Add(this.numericUpDown3, 1, 2);
            this.camSettings.Controls.Add(this.l_intensity_nud, 1, 3);
            this.camSettings.Dock = System.Windows.Forms.DockStyle.Fill;
            this.camSettings.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.FixedSize;
            this.camSettings.Location = new System.Drawing.Point(3, 16);
            this.camSettings.Name = "camSettings";
            this.camSettings.RowCount = 6;
            this.camSettings.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.camSettings.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.camSettings.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.camSettings.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.camSettings.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.camSettings.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.camSettings.Size = new System.Drawing.Size(251, 151);
            this.camSettings.TabIndex = 0;
            // 
            // zFar_Label
            // 
            this.zFar_Label.AutoSize = true;
            this.zFar_Label.Dock = System.Windows.Forms.DockStyle.Fill;
            this.zFar_Label.Location = new System.Drawing.Point(5, 135);
            this.zFar_Label.Margin = new System.Windows.Forms.Padding(5);
            this.zFar_Label.Name = "zFar_Label";
            this.zFar_Label.Size = new System.Drawing.Size(108, 11);
            this.zFar_Label.TabIndex = 14;
            this.zFar_Label.Text = "zFar";
            this.zFar_Label.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // numericUpDown5
            // 
            this.numericUpDown5.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.numericUpDown5.DecimalPlaces = 1;
            this.numericUpDown5.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.numericUpDown5.Location = new System.Drawing.Point(121, 133);
            this.numericUpDown5.Maximum = new decimal(new int[] {
            5000,
            0,
            0,
            0});
            this.numericUpDown5.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericUpDown5.Name = "numericUpDown5";
            this.numericUpDown5.Size = new System.Drawing.Size(127, 20);
            this.numericUpDown5.TabIndex = 15;
            this.numericUpDown5.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.numericUpDown5.Value = new decimal(new int[] {
            3000,
            0,
            0,
            65536});
            this.numericUpDown5.ValueChanged += new System.EventHandler(this.numericUpDown5_ValueChanged);
            // 
            // zNear_Label
            // 
            this.zNear_Label.AutoSize = true;
            this.zNear_Label.Dock = System.Windows.Forms.DockStyle.Fill;
            this.zNear_Label.Location = new System.Drawing.Point(5, 109);
            this.zNear_Label.Margin = new System.Windows.Forms.Padding(5);
            this.zNear_Label.Name = "zNear_Label";
            this.zNear_Label.Size = new System.Drawing.Size(108, 16);
            this.zNear_Label.TabIndex = 12;
            this.zNear_Label.Text = "zNear";
            this.zNear_Label.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // numericUpDown4
            // 
            this.numericUpDown4.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.numericUpDown4.DecimalPlaces = 1;
            this.numericUpDown4.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.numericUpDown4.Location = new System.Drawing.Point(121, 107);
            this.numericUpDown4.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.numericUpDown4.Name = "numericUpDown4";
            this.numericUpDown4.Size = new System.Drawing.Size(127, 20);
            this.numericUpDown4.TabIndex = 13;
            this.numericUpDown4.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.numericUpDown4.Value = new decimal(new int[] {
            10,
            0,
            0,
            65536});
            this.numericUpDown4.ValueChanged += new System.EventHandler(this.numericUpDown4_ValueChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label3.Location = new System.Drawing.Point(5, 83);
            this.label3.Margin = new System.Windows.Forms.Padding(5);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(108, 16);
            this.label3.TabIndex = 10;
            this.label3.Text = "Light Intensity";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label4.Location = new System.Drawing.Point(5, 57);
            this.label4.Margin = new System.Windows.Forms.Padding(5);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(108, 16);
            this.label4.TabIndex = 8;
            this.label4.Text = "Movement Speed";
            this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(5, 5);
            this.label1.Margin = new System.Windows.Forms.Padding(5);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(108, 16);
            this.label1.TabIndex = 3;
            this.label1.Text = "FOV";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // numericUpDown1
            // 
            this.numericUpDown1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.numericUpDown1.Location = new System.Drawing.Point(121, 3);
            this.numericUpDown1.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericUpDown1.Name = "numericUpDown1";
            this.numericUpDown1.Size = new System.Drawing.Size(127, 20);
            this.numericUpDown1.TabIndex = 5;
            this.numericUpDown1.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.numericUpDown1.Value = new decimal(new int[] {
            35,
            0,
            0,
            0});
            this.numericUpDown1.ValueChanged += new System.EventHandler(this.numericUpDown1_ValueChanged);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label2.Location = new System.Drawing.Point(5, 31);
            this.label2.Margin = new System.Windows.Forms.Padding(5);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(108, 16);
            this.label2.TabIndex = 6;
            this.label2.Text = "Light Distance";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // numericUpDown2
            // 
            this.numericUpDown2.DecimalPlaces = 1;
            this.numericUpDown2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.numericUpDown2.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.numericUpDown2.Location = new System.Drawing.Point(121, 29);
            this.numericUpDown2.Maximum = new decimal(new int[] {
            50,
            0,
            0,
            0});
            this.numericUpDown2.Name = "numericUpDown2";
            this.numericUpDown2.Size = new System.Drawing.Size(127, 20);
            this.numericUpDown2.TabIndex = 7;
            this.numericUpDown2.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.numericUpDown2.Value = new decimal(new int[] {
            50,
            0,
            0,
            65536});
            this.numericUpDown2.ValueChanged += new System.EventHandler(this.numericUpDown2_ValueChanged);
            // 
            // numericUpDown3
            // 
            this.numericUpDown3.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.numericUpDown3.Location = new System.Drawing.Point(121, 55);
            this.numericUpDown3.Maximum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.numericUpDown3.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericUpDown3.Name = "numericUpDown3";
            this.numericUpDown3.Size = new System.Drawing.Size(127, 20);
            this.numericUpDown3.TabIndex = 9;
            this.numericUpDown3.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.numericUpDown3.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericUpDown3.ValueChanged += new System.EventHandler(this.numericUpDown3_ValueChanged);
            // 
            // l_intensity_nud
            // 
            this.l_intensity_nud.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.l_intensity_nud.DecimalPlaces = 1;
            this.l_intensity_nud.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.l_intensity_nud.Location = new System.Drawing.Point(121, 81);
            this.l_intensity_nud.Maximum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.l_intensity_nud.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.l_intensity_nud.Name = "l_intensity_nud";
            this.l_intensity_nud.Size = new System.Drawing.Size(127, 20);
            this.l_intensity_nud.TabIndex = 11;
            this.l_intensity_nud.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.l_intensity_nud.Value = new decimal(new int[] {
            20,
            0,
            0,
            65536});
            this.l_intensity_nud.ValueChanged += new System.EventHandler(this.l_intensity_nud_ValueChanged);
            // 
            // splitContainer3
            // 
            this.splitContainer3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer3.Location = new System.Drawing.Point(0, 0);
            this.splitContainer3.Name = "splitContainer3";
            this.splitContainer3.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer3.Panel1
            // 
            this.splitContainer3.Panel1.Controls.Add(this.sceneGraphGroup);
            // 
            // splitContainer3.Panel2
            // 
            this.splitContainer3.Panel2.Controls.Add(this.splitContainer4);
            this.splitContainer3.Size = new System.Drawing.Size(257, 449);
            this.splitContainer3.SplitterDistance = 279;
            this.splitContainer3.TabIndex = 2;
            // 
            // sceneGraphGroup
            // 
            this.sceneGraphGroup.Controls.Add(this.treeView1);
            this.sceneGraphGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this.sceneGraphGroup.Location = new System.Drawing.Point(0, 0);
            this.sceneGraphGroup.Name = "sceneGraphGroup";
            this.sceneGraphGroup.Size = new System.Drawing.Size(257, 279);
            this.sceneGraphGroup.TabIndex = 1;
            this.sceneGraphGroup.TabStop = false;
            this.sceneGraphGroup.Text = "SceneGraph";
            // 
            // treeView1
            // 
            this.treeView1.CheckBoxes = true;
            this.treeView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeView1.Location = new System.Drawing.Point(3, 16);
            this.treeView1.Name = "treeView1";
            this.treeView1.Size = new System.Drawing.Size(251, 260);
            this.treeView1.TabIndex = 0;
            this.treeView1.AfterCheck += new System.Windows.Forms.TreeViewEventHandler(this.treeView1_AfterCheck);
            this.treeView1.NodeMouseClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.treeView1_NodeMouseClick);
            // 
            // splitContainer4
            // 
            this.splitContainer4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer4.Location = new System.Drawing.Point(0, 0);
            this.splitContainer4.Name = "splitContainer4";
            this.splitContainer4.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer4.Panel1
            // 
            this.splitContainer4.Panel1.Controls.Add(this.ProcGenGroup);
            // 
            // splitContainer4.Panel2
            // 
            this.splitContainer4.Panel2.Controls.Add(this.groupBox2);
            this.splitContainer4.Size = new System.Drawing.Size(257, 166);
            this.splitContainer4.SplitterDistance = 89;
            this.splitContainer4.TabIndex = 1;
            // 
            // ProcGenGroup
            // 
            this.ProcGenGroup.Controls.Add(this.randomgenerator);
            this.ProcGenGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ProcGenGroup.Location = new System.Drawing.Point(0, 0);
            this.ProcGenGroup.Name = "ProcGenGroup";
            this.ProcGenGroup.Size = new System.Drawing.Size(257, 89);
            this.ProcGenGroup.TabIndex = 0;
            this.ProcGenGroup.TabStop = false;
            this.ProcGenGroup.Text = "ProcGenTools";
            // 
            // randomgenerator
            // 
            this.randomgenerator.Dock = System.Windows.Forms.DockStyle.Fill;
            this.randomgenerator.Location = new System.Drawing.Point(3, 16);
            this.randomgenerator.Name = "randomgenerator";
            this.randomgenerator.Size = new System.Drawing.Size(251, 70);
            this.randomgenerator.TabIndex = 9;
            this.randomgenerator.Text = "ProcGen";
            this.randomgenerator.UseVisualStyleBackColor = true;
            this.randomgenerator.Click += new System.EventHandler(this.randgenClickNew);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.AnimTable);
            this.groupBox2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox2.Location = new System.Drawing.Point(0, 0);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(257, 73);
            this.groupBox2.TabIndex = 0;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "AnimControls";
            // 
            // AnimTable
            // 
            this.AnimTable.ColumnCount = 2;
            this.AnimTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.AnimTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.AnimTable.Controls.Add(this.newButton1, 0, 0);
            this.AnimTable.Dock = System.Windows.Forms.DockStyle.Fill;
            this.AnimTable.Location = new System.Drawing.Point(3, 16);
            this.AnimTable.Name = "AnimTable";
            this.AnimTable.RowCount = 1;
            this.AnimTable.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.AnimTable.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.AnimTable.Size = new System.Drawing.Size(251, 54);
            this.AnimTable.TabIndex = 0;
            // 
            // newButton1
            // 
            this.AnimTable.SetColumnSpan(this.newButton1, 2);
            this.newButton1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.newButton1.Location = new System.Drawing.Point(3, 3);
            this.newButton1.Name = "newButton1";
            this.newButton1.Size = new System.Drawing.Size(245, 48);
            this.newButton1.TabIndex = 14;
            this.newButton1.Text = "Play";
            this.newButton1.UseVisualStyleBackColor = true;
            this.newButton1.Click += new System.EventHandler(this.newButton1_Click);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 24);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.splitContainer2);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.RightSplitter);
            this.splitContainer1.Size = new System.Drawing.Size(1240, 623);
            this.splitContainer1.SplitterDistance = 257;
            this.splitContainer1.TabIndex = 2;
            // 
            // RightSplitter
            // 
            this.RightSplitter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.RightSplitter.Location = new System.Drawing.Point(0, 0);
            this.RightSplitter.Name = "RightSplitter";
            // 
            // RightSplitter.Panel1
            // 
            this.RightSplitter.Panel1.Controls.Add(this.glControl1);
            // 
            // RightSplitter.Panel2
            // 
            this.RightSplitter.Panel2.Controls.Add(this.selObjectBox);
            this.RightSplitter.Size = new System.Drawing.Size(979, 623);
            this.RightSplitter.SplitterDistance = 661;
            this.RightSplitter.TabIndex = 2;
            // 
            // selObjectBox
            // 
            this.selObjectBox.Controls.Add(this.rightFlowPanel);
            this.selObjectBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.selObjectBox.Location = new System.Drawing.Point(0, 0);
            this.selObjectBox.Name = "selObjectBox";
            this.selObjectBox.Size = new System.Drawing.Size(314, 623);
            this.selObjectBox.TabIndex = 0;
            this.selObjectBox.TabStop = false;
            this.selObjectBox.Text = "Selected Object Info";
            // 
            // rightFlowPanel
            // 
            this.rightFlowPanel.Controls.Add(this.selMatInfo);
            this.rightFlowPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rightFlowPanel.Location = new System.Drawing.Point(3, 16);
            this.rightFlowPanel.Name = "rightFlowPanel";
            this.rightFlowPanel.Size = new System.Drawing.Size(308, 604);
            this.rightFlowPanel.TabIndex = 3;
            // 
            // selMatInfo
            // 
            this.selMatInfo.Controls.Add(this.selMatName);
            this.selMatInfo.Controls.Add(this.label5);
            this.selMatInfo.Location = new System.Drawing.Point(3, 3);
            this.selMatInfo.Name = "selMatInfo";
            this.selMatInfo.Size = new System.Drawing.Size(292, 67);
            this.selMatInfo.TabIndex = 3;
            this.selMatInfo.TabStop = false;
            this.selMatInfo.Text = "MaterialInfo";
            // 
            // selMatName
            // 
            this.selMatName.Location = new System.Drawing.Point(148, 13);
            this.selMatName.Name = "selMatName";
            this.selMatName.Size = new System.Drawing.Size(138, 20);
            this.selMatName.TabIndex = 1;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(6, 16);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(75, 13);
            this.label5.TabIndex = 0;
            this.label5.Text = "Material Name";
            // 
            // mainglcontrolContext
            // 
            this.mainglcontrolContext.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.getAltIDToolStripMenuItem,
            this.exportToObjToolStripMenuItem,
            this.getObjectTexturesToolStripMenuItem});
            this.mainglcontrolContext.Name = "mainglcontrolContext";
            this.mainglcontrolContext.Size = new System.Drawing.Size(177, 92);
            // 
            // getAltIDToolStripMenuItem
            // 
            this.getAltIDToolStripMenuItem.Name = "getAltIDToolStripMenuItem";
            this.getAltIDToolStripMenuItem.Size = new System.Drawing.Size(176, 22);
            this.getAltIDToolStripMenuItem.Text = "Get AltID";
            this.getAltIDToolStripMenuItem.Click += new System.EventHandler(this.getAltIDToolStripMenuItem_Click);
            // 
            // exportToObjToolStripMenuItem
            // 
            this.exportToObjToolStripMenuItem.Name = "exportToObjToolStripMenuItem";
            this.exportToObjToolStripMenuItem.Size = new System.Drawing.Size(176, 22);
            this.exportToObjToolStripMenuItem.Text = "Export to Obj";
            this.exportToObjToolStripMenuItem.Click += new System.EventHandler(this.exportToObjToolStripMenuItem_Click);
            // 
            // getObjectTexturesToolStripMenuItem
            // 
            this.getObjectTexturesToolStripMenuItem.Name = "getObjectTexturesToolStripMenuItem";
            this.getObjectTexturesToolStripMenuItem.Size = new System.Drawing.Size(176, 22);
            this.getObjectTexturesToolStripMenuItem.Text = "Get Object Textures";
            this.getObjectTexturesToolStripMenuItem.Click += new System.EventHandler(this.getObjectTexturesToolStripMenuItem_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1240, 669);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "Form1";
            this.Text = "No Man\'s Model Viewer v0.70";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.Form1_FormClosed);
            this.Load += new System.EventHandler(this.Form1_Load);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            this.groupBox1.ResumeLayout(false);
            this.camSettings.ResumeLayout(false);
            this.camSettings.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown5)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown4)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown2)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown3)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.l_intensity_nud)).EndInit();
            this.splitContainer3.Panel1.ResumeLayout(false);
            this.splitContainer3.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).EndInit();
            this.splitContainer3.ResumeLayout(false);
            this.sceneGraphGroup.ResumeLayout(false);
            this.splitContainer4.Panel1.ResumeLayout(false);
            this.splitContainer4.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer4)).EndInit();
            this.splitContainer4.ResumeLayout(false);
            this.ProcGenGroup.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.AnimTable.ResumeLayout(false);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.RightSplitter.Panel1.ResumeLayout(false);
            this.RightSplitter.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.RightSplitter)).EndInit();
            this.RightSplitter.ResumeLayout(false);
            this.selObjectBox.ResumeLayout(false);
            this.rightFlowPanel.ResumeLayout(false);
            this.selMatInfo.ResumeLayout(false);
            this.selMatInfo.PerformLayout();
            this.mainglcontrolContext.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.ToolStripMenuItem openAnimationToolStripMenuItem;
        private System.ComponentModel.BackgroundWorker backgroundWorker1;
        private System.Windows.Forms.ToolStripMenuItem optionsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem settingsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private OpenTK.GLControl glControl1;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TableLayoutPanel camSettings;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.NumericUpDown numericUpDown1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.NumericUpDown numericUpDown2;
        private System.Windows.Forms.NumericUpDown numericUpDown3;
        private System.Windows.Forms.SplitContainer splitContainer3;
        private System.Windows.Forms.GroupBox sceneGraphGroup;
        private System.Windows.Forms.SplitContainer splitContainer4;
        private System.Windows.Forms.GroupBox ProcGenGroup;
        private System.Windows.Forms.Button randomgenerator;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.TableLayoutPanel AnimTable;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private NoClickTree treeView1;
        private NewButton newButton1;
        private ToolStripStatusLabel toolStripStatusLabel3;
        private ToolStripStatusLabel toolStripStatusLabel2;
        private ContextMenuStrip mainglcontrolContext;
        private ToolStripMenuItem getAltIDToolStripMenuItem;
        private ToolStripMenuItem exportToObjToolStripMenuItem;
        private Label label3;
        private NumericUpDown l_intensity_nud;
        private Label zFar_Label;
        private NumericUpDown numericUpDown5;
        private Label zNear_Label;
        private NumericUpDown numericUpDown4;
        private SplitContainer RightSplitter;
        private GroupBox selObjectBox;
        private XYZControl xyzControl2;
        private XYZControl xyzControl1;
        private FlowLayoutPanel rightFlowPanel;
        private GroupBox selMatInfo;
        private TextBox selMatName;
        private Label label5;
        private ToolStripMenuItem getObjectTexturesToolStripMenuItem;
    }
}

