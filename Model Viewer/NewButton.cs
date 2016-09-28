using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Model_Viewer
{
    class NewButton : System.Windows.Forms.Button
    {
        public bool status = true;
        public System.Windows.Forms.NumericUpDown handler = null;

        public NewButton()
        {
            //this.Click += new System.EventHandler(this.PlayPause);
        }

        public void PlayPause(object sender ,System.EventArgs e)
        {
            Debug.WriteLine("Mother func");
            status = !status;
            this.Invalidate();
        }

        public override string Text
        {
            get
            {
                if (status==false)
                    return "Stop";
                else if (status==true)
                    return "Play";
                else
                   return "NewButton";
            }
            set
            {
                base.Text = value;
            }
        }
    }
}
