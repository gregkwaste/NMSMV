using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Model_Viewer
{
    public partial class AnimationSelectForm : Form
    {
        private Form1 pform;
        private List<GMDL.scene> scenes = new List<GMDL.scene>();
        private string animpath;
        public AnimationSelectForm(Form1 parent)
        {
            InitializeComponent();
            pform = parent;
        }

        private void AnimationSelectForm_Load(object sender, EventArgs e)
        {
            //This should fetch the available scenes
            
            fetchScenes(ref scenes, pform.mainScene);
            //Set up droplist
            foreach (GMDL.scene s in scenes)
                this.listBox1.Items.Add(s.name);
        }

        private void fetchScenes(ref List<GMDL.scene> scenes, GMDL.model root)
        {
            if (root.type == TYPES.SCENE)
                if (((GMDL.scene) root).jointModel != null)
                    scenes.Add((GMDL.scene) root);

            foreach (GMDL.model c in root.children)
            {
                fetchScenes(ref scenes, c);
            }
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
