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
                
                Newtonsoft.Json.Linq.JObject tkn = Newtonsoft.Json.Linq.JObject.Parse(jsonstring);


                AppSettings app_settings = JsonConvert.DeserializeObject<AppSettings>(tkn.GetValue("AppSettings").ToString());
                RenderSettings render_settings = JsonConvert.DeserializeObject<RenderSettings>(tkn.GetValue("RenderSettings").ToString());
                RenderViewSettings view_settings = JsonConvert.DeserializeObject<RenderViewSettings>(tkn.GetValue("ViewSettings").ToString());
                MVCore.GMDL.CameraJSONSettings cam_settings = JsonConvert.DeserializeObject<MVCore.GMDL.CameraJSONSettings>(tkn.GetValue("CameraSettings").ToString());
                
                saveSettingsToEnv(app_settings, render_settings, view_settings, cam_settings);
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

        public static void saveSettingsToEnv(AppSettings app_settings, 
                                                RenderSettings render_settings,
                                                RenderViewSettings view_settings,
                                                MVCore.GMDL.CameraJSONSettings cam_settings)
        {
            //Save values to the environment
            RenderState.settings = app_settings;
            RenderState.renderSettings = render_settings;
            RenderState.renderViewSettings = view_settings;
            MVCore.GMDL.Camera.SetCameraSettings(ref RenderState.activeCam, cam_settings.settings);
            MVCore.GMDL.Camera.SetCameraPosition(ref RenderState.activeCam, 
                new OpenTK.Mathematics.Vector3(cam_settings.PosX, cam_settings.PosY, cam_settings.PosZ));
            MVCore.GMDL.Camera.SetCameraDirection(ref RenderState.activeCam,
                new OpenTK.Mathematics.Quaternion(cam_settings.DirX, cam_settings.DirY, cam_settings.DirZ, cam_settings.DirW));
        }

        public static void saveSettingsStatic()
        {
            StreamWriter stream = new StreamWriter("settings.json");
            JsonTextWriter writer = new JsonTextWriter(stream);
            writer.Formatting = Formatting.Indented;

            writer.WriteStartObject();
            writer.WritePropertyName("AppSettings");
            writer.WriteRawValue(JsonConvert.SerializeObject(RenderState.settings));
            writer.WritePropertyName("RenderSettings");
            writer.WriteRawValue(JsonConvert.SerializeObject(RenderState.renderSettings));
            writer.WritePropertyName("ViewSettings");
            writer.WriteRawValue(JsonConvert.SerializeObject(RenderState.renderViewSettings));
            writer.WritePropertyName("CameraSettings");
            writer.WriteRawValue(JsonConvert.SerializeObject(RenderState.activeCam.GetSettings()));
            writer.WriteEndObject();
            writer.Close();
        }

        private void saveSettings(object sender, RoutedEventArgs e)
        {
            saveSettingsStatic();
            Util.showInfo(this, "Settings Saved", "Info");
            Focus(); //Bring focus back to the settings form
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

}
