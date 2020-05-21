using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using libMBIN.NMS.Toolkit;
using MVCore;
using MVCore.Common;
using Newtonsoft.Json;
using MessageBox = System.Windows.MessageBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace WPFModelViewer
{
    /// <summary>
    /// Interaction logic for SettingsForm.xaml
    /// </summary>
    public partial class SettingsForm : Window
    {
        private Settings settings;
        public SettingsForm()
        {
            InitializeComponent();

            //Load settings from environment
            settings = new Settings();
            settings.dirpath = MVCore.FileUtils.dirpath;
            settings.forceProcGen = MVCore.Common.RenderState.forceProcGen? 1: 0;
            settings.procGenWinNum = Util.procGenNum;
            loadSettings(settings);
        }

        public SettingsForm(Settings settings)
        {
            InitializeComponent();
            //Load settings from input
            this.settings = settings;
            loadSettings(settings);
        }

        public static Settings loadSettingsStatic()
        {
            Settings lSettings = new Settings();
            //Load jsonstring
            try
            {
                string jsonstring = File.ReadAllText("settings.json");
                lSettings = JsonConvert.DeserializeObject<Settings>(jsonstring);
            }
            catch (FileNotFoundException)
            {
                //Generating new settings file

                string gamedir = NMSUtils.getGameInstallationDir();

                if (gamedir == "")
                {
                    MessageBox.Show("NMS Installation not found. Please choose your unpacked files folder...", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    FolderBrowserDialog openFileDlg = new FolderBrowserDialog();
                    var res = openFileDlg.ShowDialog();

                    if (res == System.Windows.Forms.DialogResult.Cancel)
                        gamedir = "";
                    else
                        gamedir = openFileDlg.SelectedPath;
                    openFileDlg.Dispose();
                }

                lSettings.dirpath = gamedir;
                lSettings.forceProcGen = 1;
                lSettings.procGenWinNum = 15;
                lSettings.animFPS = 60;
                lSettings.HDRExposure = 0.15f;
                lSettings.useVSYNC = 0;
                
                saveSettingsStatic(lSettings); //Save Settings right away
                
            }

            return lSettings;
        }

        public void loadSettings(Settings settings)
        {
            //Load settings to the Control
            dirpath.Text = settings.dirpath;
            forceProcGen.Text = settings.forceProcGen.ToString();
            procGenWinNum.Text = settings.procGenWinNum.ToString();
            AnimFPS.Text = settings.animFPS.ToString();
            HDRExposure.Text = settings.HDRExposure.ToString();
            VSYNC.Text = settings.useVSYNC.ToString();
        }

        public static void saveSettingsToEnv(Settings settings)
        {
            //Load values to the environment
            FileUtils.dirpath = settings.dirpath;
            Util.procGenNum = settings.procGenWinNum;
            RenderState.forceProcGen = (settings.forceProcGen > 0) ? true : false;
            MVCore.Common.RenderOptions._HDRExposure = settings.HDRExposure;
            MVCore.Common.RenderOptions.animFPS = settings.animFPS;
            MVCore.Common.RenderOptions.UseVSYNC = (settings.useVSYNC > 0) ? true : false;
        }

        public static void saveSettingsStatic(Settings settings)
        {
            saveSettingsToEnv(settings);
            //Serialize object
            string jsonstring = JsonConvert.SerializeObject(settings);
            File.WriteAllText("settings.json", jsonstring);
        }

        private void saveSettings(object sender, RoutedEventArgs e)
        {
            //Read values from control
            settings.dirpath = dirpath.Text;
            settings.forceProcGen = int.Parse(forceProcGen.Text.ToString());
            settings.procGenWinNum = int.Parse(procGenWinNum.Text.ToString());
            settings.HDRExposure = float.Parse(HDRExposure.Text.ToString());
            settings.animFPS = int.Parse(AnimFPS.Text.ToString());
            
            saveSettingsStatic(settings);

            MessageBox.Show("Settings Saved", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Dirpath_OnGotFocus(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog openFileDlg = new FolderBrowserDialog();
            var res = openFileDlg.ShowDialog();

            if (res == System.Windows.Forms.DialogResult.Cancel)
                dirpath.Text = "";
            else
                dirpath.Text = openFileDlg.SelectedPath;

            openFileDlg.Dispose();
        }
    }

    //Settings Structure
    public class Settings
    {
        public string dirpath;
        public int procGenWinNum;
        public int forceProcGen;
        public int useVSYNC;
        public int animFPS;
        public float HDRExposure;
    }
}
