namespace TextureMixer
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
            System.Windows.Forms.ListViewItem listViewItem1 = new System.Windows.Forms.ListViewItem("0 Diffuse");
            System.Windows.Forms.ListViewItem listViewItem2 = new System.Windows.Forms.ListViewItem("1 Diffuse");
            System.Windows.Forms.ListViewItem listViewItem3 = new System.Windows.Forms.ListViewItem("2 Diffuse");
            System.Windows.Forms.ListViewItem listViewItem4 = new System.Windows.Forms.ListViewItem("3 Diffuse");
            System.Windows.Forms.ListViewItem listViewItem5 = new System.Windows.Forms.ListViewItem("4 Diffuse");
            System.Windows.Forms.ListViewItem listViewItem6 = new System.Windows.Forms.ListViewItem("5 Diffuse");
            System.Windows.Forms.ListViewItem listViewItem7 = new System.Windows.Forms.ListViewItem("6 Diffuse");
            System.Windows.Forms.ListViewItem listViewItem8 = new System.Windows.Forms.ListViewItem("7 Diffuse");
            System.Windows.Forms.ListViewItem listViewItem9 = new System.Windows.Forms.ListViewItem("0 Alpha");
            System.Windows.Forms.ListViewItem listViewItem10 = new System.Windows.Forms.ListViewItem("1 Alpha");
            System.Windows.Forms.ListViewItem listViewItem11 = new System.Windows.Forms.ListViewItem("2 Alpha");
            System.Windows.Forms.ListViewItem listViewItem12 = new System.Windows.Forms.ListViewItem("3 Alpha");
            System.Windows.Forms.ListViewItem listViewItem13 = new System.Windows.Forms.ListViewItem("4 Alpha");
            System.Windows.Forms.ListViewItem listViewItem14 = new System.Windows.Forms.ListViewItem("5 Alpha");
            System.Windows.Forms.ListViewItem listViewItem15 = new System.Windows.Forms.ListViewItem("6 Alpha");
            System.Windows.Forms.ListViewItem listViewItem16 = new System.Windows.Forms.ListViewItem("7 Alpha");
            this.glControl1 = new OpenTK.GLControl(OpenTK.Graphics.GraphicsMode.Default);
            this.mainSplitter = new System.Windows.Forms.SplitContainer();
            this.secondarySplitter = new System.Windows.Forms.SplitContainer();
            this.diffuseList = new System.Windows.Forms.ListView();
            this.thirdSplitter = new System.Windows.Forms.SplitContainer();
            this.alphaList = new System.Windows.Forms.ListView();
            this.checkBox2 = new System.Windows.Forms.CheckBox();
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.contextMenuStrip1 = new TextureMixer.customStrip(this.components);
            this.importTexturesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.setReColourToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)(this.mainSplitter)).BeginInit();
            this.mainSplitter.Panel1.SuspendLayout();
            this.mainSplitter.Panel2.SuspendLayout();
            this.mainSplitter.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.secondarySplitter)).BeginInit();
            this.secondarySplitter.Panel1.SuspendLayout();
            this.secondarySplitter.Panel2.SuspendLayout();
            this.secondarySplitter.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.thirdSplitter)).BeginInit();
            this.thirdSplitter.Panel1.SuspendLayout();
            this.thirdSplitter.Panel2.SuspendLayout();
            this.thirdSplitter.SuspendLayout();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // glControl1
            // 
            this.glControl1.BackColor = System.Drawing.Color.Black;
            this.glControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.glControl1.Location = new System.Drawing.Point(0, 0);
            this.glControl1.Name = "glControl1";
            this.glControl1.Size = new System.Drawing.Size(669, 621);
            this.glControl1.TabIndex = 0;
            this.glControl1.VSync = false;
            // 
            // mainSplitter
            // 
            this.mainSplitter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainSplitter.Location = new System.Drawing.Point(0, 0);
            this.mainSplitter.Name = "mainSplitter";
            // 
            // mainSplitter.Panel1
            // 
            this.mainSplitter.Panel1.Controls.Add(this.glControl1);
            // 
            // mainSplitter.Panel2
            // 
            this.mainSplitter.Panel2.Controls.Add(this.secondarySplitter);
            this.mainSplitter.Size = new System.Drawing.Size(980, 621);
            this.mainSplitter.SplitterDistance = 669;
            this.mainSplitter.TabIndex = 1;
            // 
            // secondarySplitter
            // 
            this.secondarySplitter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.secondarySplitter.Location = new System.Drawing.Point(0, 0);
            this.secondarySplitter.Name = "secondarySplitter";
            this.secondarySplitter.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // secondarySplitter.Panel1
            // 
            this.secondarySplitter.Panel1.Controls.Add(this.diffuseList);
            // 
            // secondarySplitter.Panel2
            // 
            this.secondarySplitter.Panel2.Controls.Add(this.thirdSplitter);
            this.secondarySplitter.Size = new System.Drawing.Size(307, 621);
            this.secondarySplitter.SplitterDistance = 252;
            this.secondarySplitter.TabIndex = 0;
            // 
            // diffuseList
            // 
            this.diffuseList.CheckBoxes = true;
            this.diffuseList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.diffuseList.FullRowSelect = true;
            listViewItem1.StateImageIndex = 0;
            listViewItem2.StateImageIndex = 0;
            listViewItem3.StateImageIndex = 0;
            listViewItem4.StateImageIndex = 0;
            listViewItem5.StateImageIndex = 0;
            listViewItem6.StateImageIndex = 0;
            listViewItem7.StateImageIndex = 0;
            listViewItem8.StateImageIndex = 0;
            this.diffuseList.Items.AddRange(new System.Windows.Forms.ListViewItem[] {
            listViewItem1,
            listViewItem2,
            listViewItem3,
            listViewItem4,
            listViewItem5,
            listViewItem6,
            listViewItem7,
            listViewItem8});
            this.diffuseList.Location = new System.Drawing.Point(0, 0);
            this.diffuseList.Name = "diffuseList";
            this.diffuseList.Size = new System.Drawing.Size(307, 252);
            this.diffuseList.TabIndex = 0;
            this.diffuseList.UseCompatibleStateImageBehavior = false;
            this.diffuseList.View = System.Windows.Forms.View.List;
            // 
            // thirdSplitter
            // 
            this.thirdSplitter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.thirdSplitter.Location = new System.Drawing.Point(0, 0);
            this.thirdSplitter.Name = "thirdSplitter";
            this.thirdSplitter.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // thirdSplitter.Panel1
            // 
            this.thirdSplitter.Panel1.Controls.Add(this.alphaList);
            // 
            // thirdSplitter.Panel2
            // 
            this.thirdSplitter.Panel2.Controls.Add(this.checkBox2);
            this.thirdSplitter.Panel2.Controls.Add(this.checkBox1);
            this.thirdSplitter.Size = new System.Drawing.Size(307, 365);
            this.thirdSplitter.SplitterDistance = 203;
            this.thirdSplitter.TabIndex = 0;
            // 
            // alphaList
            // 
            this.alphaList.CheckBoxes = true;
            this.alphaList.Dock = System.Windows.Forms.DockStyle.Fill;
            listViewItem9.StateImageIndex = 0;
            listViewItem10.StateImageIndex = 0;
            listViewItem11.StateImageIndex = 0;
            listViewItem12.StateImageIndex = 0;
            listViewItem13.StateImageIndex = 0;
            listViewItem14.StateImageIndex = 0;
            listViewItem15.StateImageIndex = 0;
            listViewItem16.StateImageIndex = 0;
            this.alphaList.Items.AddRange(new System.Windows.Forms.ListViewItem[] {
            listViewItem9,
            listViewItem10,
            listViewItem11,
            listViewItem12,
            listViewItem13,
            listViewItem14,
            listViewItem15,
            listViewItem16});
            this.alphaList.Location = new System.Drawing.Point(0, 0);
            this.alphaList.Name = "alphaList";
            this.alphaList.Size = new System.Drawing.Size(307, 203);
            this.alphaList.TabIndex = 1;
            this.alphaList.UseCompatibleStateImageBehavior = false;
            this.alphaList.View = System.Windows.Forms.View.List;
            // 
            // checkBox2
            // 
            this.checkBox2.AutoSize = true;
            this.checkBox2.Location = new System.Drawing.Point(3, 26);
            this.checkBox2.Name = "checkBox2";
            this.checkBox2.Size = new System.Drawing.Size(83, 17);
            this.checkBox2.TabIndex = 1;
            this.checkBox2.Text = "renderMode";
            this.checkBox2.UseVisualStyleBackColor = true;
            this.checkBox2.CheckedChanged += new System.EventHandler(this.checkBox2_CheckedChanged);
            // 
            // checkBox1
            // 
            this.checkBox1.AutoSize = true;
            this.checkBox1.Location = new System.Drawing.Point(3, 3);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(70, 17);
            this.checkBox1.TabIndex = 0;
            this.checkBox1.Text = "hasAlpha";
            this.checkBox1.UseVisualStyleBackColor = true;
            this.checkBox1.CheckedChanged += new System.EventHandler(this.checkBox1_CheckedChanged);
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.importTexturesToolStripMenuItem,
            this.setReColourToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(157, 48);
            // 
            // importTexturesToolStripMenuItem
            // 
            this.importTexturesToolStripMenuItem.Name = "importTexturesToolStripMenuItem";
            this.importTexturesToolStripMenuItem.Size = new System.Drawing.Size(156, 22);
            this.importTexturesToolStripMenuItem.Text = "Import Textures";
            this.importTexturesToolStripMenuItem.Click += new System.EventHandler(this.importTexturesToolStripMenuItem_Click);
            // 
            // setReColourToolStripMenuItem
            // 
            this.setReColourToolStripMenuItem.Name = "setReColourToolStripMenuItem";
            this.setReColourToolStripMenuItem.Size = new System.Drawing.Size(156, 22);
            this.setReColourToolStripMenuItem.Text = "Set ReColour";
            this.setReColourToolStripMenuItem.Click += new System.EventHandler(this.setReColourToolStripMenuItem_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(980, 621);
            this.Controls.Add(this.mainSplitter);
            this.Name = "Form1";
            this.Text = "Texture Mixer";
            this.mainSplitter.Panel1.ResumeLayout(false);
            this.mainSplitter.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.mainSplitter)).EndInit();
            this.mainSplitter.ResumeLayout(false);
            this.secondarySplitter.Panel1.ResumeLayout(false);
            this.secondarySplitter.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.secondarySplitter)).EndInit();
            this.secondarySplitter.ResumeLayout(false);
            this.thirdSplitter.Panel1.ResumeLayout(false);
            this.thirdSplitter.Panel2.ResumeLayout(false);
            this.thirdSplitter.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.thirdSplitter)).EndInit();
            this.thirdSplitter.ResumeLayout(false);
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private OpenTK.GLControl glControl1;
        private System.Windows.Forms.SplitContainer mainSplitter;
        private System.Windows.Forms.SplitContainer secondarySplitter;
        private System.Windows.Forms.ListView diffuseList;
        private System.Windows.Forms.SplitContainer thirdSplitter;
        private System.Windows.Forms.ListView alphaList;
        private customStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem importTexturesToolStripMenuItem;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.ToolStripMenuItem setReColourToolStripMenuItem;
        private System.Windows.Forms.CheckBox checkBox1;
        private System.Windows.Forms.CheckBox checkBox2;
    }
}

