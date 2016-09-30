using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;

namespace Model_Viewer
{
    public partial class AnimationSelectForm : Form
    {
        private Form1 pform;
        private string animpath;
        public AnimationSelectForm(Form1 parent)
        {
            InitializeComponent();
            pform = parent;
        }

        private void AnimationSelectForm_Load(object sender, EventArgs e)
        {
            //Set up droplist
            foreach (GMDL.model s in this.pform.animScenes)
                this.listBox1.Items.Add(s.name);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            //Get scene selection
            Debug.WriteLine(this.listBox1.SelectedIndex);
            if (this.listBox1.SelectedIndex == -1)
            {
                MessageBox.Show("No Scene Selected", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (animpath == null)
            {
                MessageBox.Show("No ANIM File Selected", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            //Proceed to import
            //Select Scene
            GMDL.model activeScene = this.pform.animScenes[this.listBox1.SelectedIndex];
            FileStream fs = new FileStream(animpath, FileMode.Open);
            activeScene.animMeta = new GMDL.AnimeMetaData();
            activeScene.animMeta.Load(fs);

            this.Close();

        }

        private void button1_Click(object sender, EventArgs e)
        {
            //Select animation file
            DialogResult res = this.openFileDialog1.ShowDialog();

            if (res != DialogResult.OK)
                return;

            animpath = openFileDialog1.FileName;
            this.textBox1.Text = animpath;
            
        }
    }
}
