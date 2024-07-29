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
using System.Windows.Media.Media3D;
using Microsoft.WindowsAPICodePack.Dialogs;
using MVCore.Common;
using MVCore.GMDL;
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
                    Util.showInfo("NMS Installation not found. Please choose your NMS Installation folder (root).", "Info");
                    var openFileDlg = new CommonOpenFileDialog()
                    {
                        Title = "Select NMS Installation Folder",
                        IsFolderPicker = true
                    };

                    if (openFileDlg.ShowDialog() == CommonFileDialogResult.Cancel)
                        unpackdir = "";
                    else
                        unpackdir = openFileDlg.FileName;
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
                    var dialog = new CommonOpenFileDialog()
                    {
                        Title = "Select the unpacked GAMEDATA folder",
                        IsFolderPicker = true
                    };

                    if (dialog.ShowDialog() == CommonFileDialogResult.Cancel)
                        unpackdir = "";
                    else
                        unpackdir = dialog.FileName;

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
            RenderState.camSettings = cam_settings;
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
            if (RenderState.activeCam != null)
                writer.WriteRawValue(JsonConvert.SerializeObject(new CameraJSONSettings(RenderState.activeCam)));
            else 
                writer.WriteRawValue(JsonConvert.SerializeObject(new CameraJSONSettings()));
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

            CommonOpenFileDialog openFileDlg = new()
            {
                IsFolderPicker = true,
                Multiselect = false
            };
            
            var res = openFileDlg.ShowDialog();

            string path = "";

            if (res == CommonFileDialogResult.Ok)
                path = openFileDlg.FileName;
            openFileDlg.Dispose();

            if (but.Name == "GameDirSetButton")
            {
                RenderState.settings.GameDir = path;
                Util.showInfo(this, "Please restart the application to reload pak files.", "Info");
            }
            else
                RenderState.settings.UnpackDir = path;

        }

        private void TextBox_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            System.Windows.Controls.TextBox tb = (System.Windows.Controls.TextBox)sender;
            
            if ((string)tb.Tag == "KeyDownTag")
                RenderState.settings.KeyDownProp = e.Key.ToString();
            else if ((string)tb.Tag == "KeyUpTag")
                RenderState.settings.KeyUpProp = e.Key.ToString();
            else if ((string)tb.Tag == "KeyRightTag")
                RenderState.settings.KeyRightProp = e.Key.ToString();
            else if ((string)tb.Tag == "KeyLeftTag")
                RenderState.settings.KeyLeftProp = e.Key.ToString();
        }

    }

}
