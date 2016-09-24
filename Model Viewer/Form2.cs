using System;
using System.Windows.Forms;
using System.Xml;

namespace Model_Viewer
{
    public partial class SettingsForm : Form
    {
        public SettingsForm()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //Save Settings
            XmlDocument xml = new XmlDocument();
            XmlElement settings = xml.CreateElement("SETTINGS");
            XmlElement elem = xml.CreateElement("GAMEPATH");
            elem.InnerText = textBox1.Text;
            settings.AppendChild(elem);
            xml.AppendChild(settings);
            xml.Save("settings.xml");
        }

        public void loadSettings()
        {
            XmlDocument xml = new XmlDocument();
            try
            {
                xml.Load("settings.xml");
            }
            catch (System.IO.FileNotFoundException e)
            {
                System.Diagnostics.Debug.WriteLine("Settings File doesn't Exist");
                textBox1_MouseClick(this, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 1));
                return;
            }

            //If file exists then parse the settings
            
            
            XmlElement settings = (XmlElement) xml.SelectSingleNode("SETTINGS");
            XmlElement gamepath = (XmlElement) settings.SelectSingleNode("GAMEPATH");

            //Set Gamepath
            textBox1.Text = gamepath.InnerText;
            Util.dirpath = gamepath.InnerText;
            System.Diagnostics.Debug.WriteLine(gamepath.InnerText);



        }

        private void textBox1_MouseClick(object sender, MouseEventArgs e)
        {
            System.Windows.Forms.DialogResult res =  folderBrowserDialog1.ShowDialog();

            if (res == DialogResult.Cancel) return;

            //Set Gamepath
            textBox1.Text = folderBrowserDialog1.SelectedPath;
            Util.dirpath = folderBrowserDialog1.SelectedPath;
        }

        private void SettingsForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
        }
    }
}
