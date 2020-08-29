using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.RightsManagement;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using MVCore.Common;
using MVCore.Utils;
using Newtonsoft.Json;
using MessageBox = System.Windows.MessageBox;


namespace WPFModelViewer
{
    /// <summary>
    /// Interaction logic for SettingsForm.xaml
    /// </summary>
    public partial class SettingsForm : Window
    {
        public SettingsForm()
        {
            InitializeComponent();

            //Load settings from environment
            SettingsContainer.Content = RenderState.settings;
            RenderSettingsContainer.Content = RenderState.renderSettings;
        }

        public static void loadSettingsStatic()
        {
            //Load jsonstring
            try
            {
                string jsonstring = File.ReadAllText("settings.json");
                JSONSettings lSettings = JsonConvert.DeserializeObject<JSONSettings>(jsonstring);
                saveSettingsToEnv(lSettings);
            }
            catch (FileNotFoundException)
            {
                //Generating new settings file

                string gamedir = NMSUtils.getGameInstallationDir();
                string unpackdir;

                if (gamedir == "" || gamedir is null)
                {
                    Util.showInfo("NMS Installation not found. Please choose your unpacked files folder...", "Info");
                    FolderBrowserDialog openFileDlg = new FolderBrowserDialog();
                    var res = openFileDlg.ShowDialog();

                    if (res == System.Windows.Forms.DialogResult.Cancel)
                        unpackdir = "";
                    else
                        unpackdir = openFileDlg.SelectedPath;
                    openFileDlg.Dispose();
                    //Store paths
                    RenderState.settings.GameDir = unpackdir;
                    RenderState.settings.UnpackDir = unpackdir;
                    return;
                }

                //Ask if the user has files unpacked
                MessageBoxResult result = MessageBox.Show("Do you have unpacked game files?", "", MessageBoxButton.YesNo);

                if (result == MessageBoxResult.No)
                {
                    unpackdir = gamedir;
                } else
                {
                    FolderBrowserDialog dialog = new FolderBrowserDialog();
                    dialog.Description = "Select the unpacked GAMEDATA folder";
                    DialogResult res = dialog.ShowDialog();

                    if (res == System.Windows.Forms.DialogResult.OK)
                    {
                        unpackdir = dialog.SelectedPath;
                    }
                    else
                        unpackdir = "";
                }

                //Save path settings to the environment
                RenderState.settings.GameDir = gamedir;
                RenderState.settings.UnpackDir = unpackdir;

                saveSettingsStatic(); //Save Settings right away
                
            }

        }

        public static void saveSettingsToEnv(JSONSettings settings)
        {
            //IF BINDINGS ARE CORRECT I DON"THAVE TO DO SHIT

            //Save values to the environment
            RenderState.settings.GameDir = settings.GameDir;
            RenderState.settings.UnpackDir = settings.UnpackDir;
            RenderState.settings.ProcGenWinNum = settings.ProcGenWinNum;
            RenderState.settings.ForceProcGen = settings.ForceProcGen;
            RenderState.renderSettings.UseVSYNC = (settings.UseVSYNC > 0);
            RenderState.renderSettings._HDRExposure = settings.HDRExposure;
            RenderState.renderSettings.animFPS = settings.AnimFPS;
        }

        public static void loadSettingsFromEnv(JSONSettings settings)
        {
            //IF BINDINGS ARE CORRECT I DON"THAVE TO DO SHIT

            //Load values from the environment
            settings.GameDir = RenderState.settings.GameDir;
            settings.UnpackDir = RenderState.settings.UnpackDir;
            settings.ProcGenWinNum = RenderState.settings.ProcGenWinNum;
            settings.ForceProcGen = RenderState.settings.ForceProcGen;
            settings.UseVSYNC = RenderState.renderSettings.UseVSYNC ? 1 : 0;
            settings.HDRExposure = RenderState.renderSettings._HDRExposure;
            settings.AnimFPS = RenderState.renderSettings.animFPS;
        }

        public static void saveSettingsStatic()
        {
            //Create JSONSettings
            JSONSettings settings = new JSONSettings();
            loadSettingsFromEnv(settings);

            //Serialize object
            string jsonstring = JsonConvert.SerializeObject(settings);
            File.WriteAllText("settings.json", jsonstring);
        }

        private void saveSettings(object sender, RoutedEventArgs e)
        {
            saveSettingsStatic();
            Util.showInfo(this, "Settings Saved", "Info");
            this.Focus(); //Bring focus back to the settings form
        }

        private void Dirpath_OnGotFocus(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.Button but = (System.Windows.Controls.Button) sender;
            
            FolderBrowserDialog openFileDlg = new FolderBrowserDialog();
            var res = openFileDlg.ShowDialog();

            string path = "";

            if (res == System.Windows.Forms.DialogResult.OK)
                path = openFileDlg.SelectedPath;
            openFileDlg.Dispose();

            if (but.Name == "GameDirSetButton")
            {
                RenderState.settings.GameDir = path;
                Util.showInfo(this, "Please restart the application to reload pak files.", "Info");
            }
            else
                RenderState.settings.UnpackDir = path;

        }
    }

    //Settings JSON Structure

    public class JSONSettings
    {
        public string GameDir;
        public string UnpackDir;
        public int ProcGenWinNum;
        public int ForceProcGen;
        public int UseVSYNC;
        public int AnimFPS;
        public float HDRExposure;
    }
   
}
