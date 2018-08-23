using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
using System.IO;
using MVCore.GMDL;

namespace Model_Viewer
{
    public partial class AnimationSelectForm : Form { 
        private string animpath;
        private List<model> animScenes;

        public AnimationSelectForm(object parent)
        {
            InitializeComponent();
            //Get type of parent
            Type typ = parent.GetType();
            Debug.WriteLine(typ);
            CGLControl parent_control = (CGLControl) parent;
            animScenes = new List<model>();
            foreach (scene s in parent_control.animScenes)
                animScenes.Add(s);
        }

        private void AnimationSelectForm_Load(object sender, EventArgs e)
        {
            //Set up droplist
            foreach (model s in animScenes)
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
            scene activeScene = (scene) this.animScenes[this.listBox1.SelectedIndex];

            MVCore.Common.CallBacks.updateStatus("Loading Animation: " + animpath);
            MVCore.Common.CallBacks.openAnim(animpath, activeScene);
            MVCore.Common.CallBacks.updateStatus("Ready");
            
            this.Close();

        }

        private void button1_Click(object sender, EventArgs e)
        {
            //Select animation file
            openFileDialog1.Filter = "ANIM Files (*.ANIM.MBIN)|*.ANIM.MBIN;";
            DialogResult res = this.openFileDialog1.ShowDialog();

            if (res != DialogResult.OK)
                return;

            animpath = openFileDialog1.FileName;
            this.textBox1.Text = animpath;

        }

        private void AnimationSelectForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            //Cleanup
            animScenes.Clear();
        }
    }
}
