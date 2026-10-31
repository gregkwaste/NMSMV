using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
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
        }

        public static void loadSettingsStatic()
        {
            if (File.Exists("settings.json"))
            {
                try
                {
                    string jsonstring = File.ReadAllText("settings.json");
                    Newtonsoft.Json.Linq.JObject tkn = Newtonsoft.Json.Linq.JObject.Parse(jsonstring);

                    //A missing or broken section keeps its defaults instead of failing the whole file
                    AppSettings app_settings = readSettingsSection(tkn, "AppSettings", RenderState.settings);
                    RenderSettings render_settings = readSettingsSection(tkn, "RenderSettings", RenderState.renderSettings);
                    RenderViewSettings view_settings = readSettingsSection(tkn, "ViewSettings", RenderState.renderViewSettings);
                    MVCore.GMDL.CameraJSONSettings cam_settings = readSettingsSection(tkn, "CameraSettings", RenderState.camSettings);

                    saveSettingsToEnv(app_settings, render_settings, view_settings, cam_settings);
                    return;
                }
                catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException)
                {
                    //Keep the unreadable file for reference and start over with new settings
                    //and overwrite it with the defaults, so the next start doesn't hit it again
                    try
                    {
                        File.Copy("settings.json", "settings.json.bak", true);
                        saveSettingsStatic();
                    }
                    catch (Exception) { }

                    Util.showInfo("settings.json could not be read, so new settings will be created. " +
                                  "The old file was kept as settings.json.bak.\n\n" + ex.Message, "Info");
                }
            }

            createSettings();
        }

        private static T readSettingsSection<T>(Newtonsoft.Json.Linq.JObject tkn, string name, T fallback) where T : class
        {
            Newtonsoft.Json.Linq.JToken section = tkn[name];
            if (section == null || section.Type == Newtonsoft.Json.Linq.JTokenType.Null)
                return fallback;

            try
            {
                return section.ToObject<T>() ?? fallback;
            }
            catch (JsonException)
            {
                return fallback;
            }
        }

        //Generates a new settings file
        private static void createSettings()
        {
            //Generating new settings file

            //Audio must always be an explicit user choice on first run. Existing settings retain
            //their saved PlayAudio value because this method only runs when no usable settings file exists.
            RenderState.renderViewSettings.PlayAudio = false;

            string gamedir = NMSUtils.getGameInstallationDir();
            string unpackdir;

            if (gamedir == "" || gamedir is null)
            {
                Util.showInfo("NMS Installation not found. Please choose your NMS Installation folder (root).", "Info");
                unpackdir = pickFolder("Select NMS Installation Folder") ?? "";
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
                unpackdir = pickFolder("Select the unpacked GAMEDATA folder") ?? "";
            }

            //Save path settings to the environment
            RenderState.settings.GameDir = gamedir;
            RenderState.settings.UnpackDir = unpackdir;
            
            saveSettingsStatic(); //Save Settings right away

        }

        //Built-in WPF folder picker (.NET 8+). Returns null when cancelled
        private static string pickFolder(string title)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog()
            {
                Title = title,
                Multiselect = false
            };

            return dialog.ShowDialog() == true ? dialog.FolderName : null;
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

            string path = pickFolder(but.Name == "GameDirSetButton" ? "Select NMS Installation Folder" : "Select the unpacked GAMEDATA folder");

            //Cancelling keeps the current folder instead of clearing it
            if (path is null)
                return;

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
