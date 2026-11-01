using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Model_Viewer;
using MVCore;
using MVCore.Common;
using GmdlModel = MVCore.GMDL.Model;
using ParticleEmitter = MVCore.GMDL.ParticleEmitter;

namespace WPFModelViewer
{
    public static class Util
    {
        public static int VersionMajor = 0;
        public static int VersionMedium = 96;
        public static int VersionMinor = 0;
        public static bool Prerelease = true;
        
        public static readonly Random randgen = new Random();
        
        public static Window activeWindow;
        public static TextBlock activeStatusStrip;
        
        //Log file (log.out next to the exe). Opened on first use so startup crashes are logged too
        private static StreamWriter loggingSr;
        private static bool logOpened = false; //Reopen in append mode after closeLog, so late messages keep the earlier ones
        private static readonly object logLock = new object(); //Log is called from the GL driver thread and loaders too
        

        public static string getVersion()
        {
            string ver = string.Join(".", new string[] { VersionMajor.ToString(),
                                           VersionMedium.ToString(),
                                           VersionMinor.ToString()}) + (Prerelease ? "-pre" : "");
#if DEBUG
            return ver + " [DEBUG]";
#else
            return ver;
#endif
        }

        //Update Status strip
        public static void setStatus(string status)
        {
            Application.Current.Dispatcher.BeginInvoke((Action)(() =>
            {
                activeStatusStrip.Text = status;
            }));
        }

        public static void showError(string message, string caption)
        {
            CallBacks.Log(message);
            Application.Current.Dispatcher.BeginInvoke((Action)(() =>
            {
                if (activeWindow is null)
                    MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Error);
                else
                    MessageBox.Show(activeWindow, message, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }));
            
        }

        public static void showInfo(string message, string caption)
        {
            CallBacks.Log(message);
            Application.Current.Dispatcher.BeginInvoke((Action)(() =>
            {
                if (activeWindow is null)
                    MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Information);
                else
                    MessageBox.Show(activeWindow, message, caption, MessageBoxButton.OK, MessageBoxImage.Information);
            }));

        }

        public static void showInfo(Window parent, string message, string caption)
        {
            Application.Current.Dispatcher.BeginInvoke((Action)(() =>
            {
                if (parent is null)
                    MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Information);
                else
                    MessageBox.Show(parent, message, caption, MessageBoxButton.OK, MessageBoxImage.Information);
            }));

        }

        //Generic Procedures - File Loading
        public static void loadAnimationFile(string path, MVCore.GMDL.Model scn)
        {
            libMBIN.MBINFile mbinf = new libMBIN.MBINFile(path);
            mbinf.Load();
            //scn.animMeta = (libMBIN.NMS.Toolkit.TkAnimMetadata) mbinf.GetData();
        }

        public static void Log(params object[] msg)
        {
            if (msg.Length == 0)
                return;

            //Only treat the first string as a format when arguments follow it, so a plain message
            //with braces (exception text, json, shader source) is written as-is
            string message;
            if (msg[0] is string fmt && msg.Length > 1)
            {
                try
                {
                    message = string.Format(fmt, msg.Skip(1).ToArray());
                }
                catch (FormatException)
                {
                    message = string.Join(" ", msg);
                }
            }
            else
                message = string.Join(" ", msg);
#if DEBUG
            Console.WriteLine(message); //Write to console if we are in debug mode
#endif
            lock (logLock)
            {
                if (loggingSr == null)
                {
                    FileMode mode = logOpened ? FileMode.Append : FileMode.Create;
                    FileStream stream = new FileStream("log.out", mode, FileAccess.Write, FileShare.Read,
                        64 * 1024, FileOptions.SequentialScan);
                    loggingSr = new StreamWriter(stream, new UTF8Encoding(false), 64 * 1024);
                    logOpened = true;
                }
                loggingSr.WriteLine(message);
            }
        }

        public static void closeLog()
        {
            lock (logLock)
            {
                loggingSr?.Close();
                loggingSr = null;
            }
        }

        //Asks where to save a template and writes it as MBIN or MXML, by the extension picked.
        //defaultName is the file name of the source file (X.SCENE.MBIN), format the filter selected first
        public static void saveTemplate(libMBIN.NMSTemplate template, string defaultName, bool mbinFirst = true)
        {
            if (template == null)
            {
                showError("There is no file data to export", "Export");
                return;
            }

            string baseName = defaultName ?? "";
            if (baseName.EndsWith(".MBIN", StringComparison.OrdinalIgnoreCase) ||
                baseName.EndsWith(".MXML", StringComparison.OrdinalIgnoreCase))
                baseName = baseName.Substring(0, baseName.Length - 5);

            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.Title = "Export " + template.GetType().Name;
            dlg.Filter = mbinFirst ? "MBIN Files (*.MBIN)|*.MBIN|MXML Files (*.MXML)|*.MXML"
                                   : "MXML Files (*.MXML)|*.MXML|MBIN Files (*.MBIN)|*.MBIN";
            dlg.FileName = baseName + (mbinFirst ? ".MBIN" : ".MXML");
            dlg.OverwritePrompt = true;
            if (dlg.ShowDialog(activeWindow) != true)
                return;

            try
            {
                writeTemplate(template, dlg.FileName);
                MVCore.Common.CallBacks.Log("Exported " + template.GetType().Name + " to " + dlg.FileName);
            }
            catch (Exception ex)
            {
                MVCore.Common.CallBacks.Log("Export failed: " + ex.Message);
                showError("Export failed: " + ex.Message, "Export");
            }
        }

        //Exports a self-contained scene bundle rebuilt from the live model graph.
        public static void saveNode(GmdlModel node, string defaultName, bool mbinFirst = true)
        {
            if (node?.CanExportScene != true)
            {
                showError("There is no file data to export", "Export");
                return;
            }

            Microsoft.Win32.OpenFolderDialog dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select export folder for " + node.EditableName,
                InitialDirectory = @"D:\SteamLibrary\steamapps\common\No Man's Sky\GAMEDATA\MODS\NMSMV_TEST\",
                Multiselect = false
            };
            if (dialog.ShowDialog(activeWindow) != true)
                return;

            string relativeScenePath = sceneExportPath(node, defaultName, mbinFirst);
            string destination = Path.GetFullPath(Path.Combine(dialog.FolderName,
                relativeScenePath.Replace('\\', Path.DirectorySeparatorChar)));
            string exportRoot = Path.GetFullPath(dialog.FolderName)
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!destination.StartsWith(exportRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The scene name produces an unsafe export path.");
            if (File.Exists(destination) && MessageBox.Show(activeWindow,
                    "Replace the existing scene bundle?\n" + destination, "Export",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                NmsSceneBundleExporter.Export(node, destination);
            }
            catch (Exception ex)
            {
                CallBacks.Log("Export failed: " + ex);
                showError("Export failed: " + ex.Message, "Export");
            }
        }

        private static string sceneExportPath(GmdlModel node, string fallback, bool mbin)
        {
            string source = node?.SourceFile;
            if (String.IsNullOrWhiteSpace(source))
                source = fallback;

            string normalized = (source ?? "").Trim().Replace('/', '\\');
            int modelsIndex = normalized.IndexOf("MODELS\\", StringComparison.OrdinalIgnoreCase);
            normalized = modelsIndex >= 0 ? normalized.Substring(modelsIndex) : "";

            string editable = (node?.EditableName ?? "").Trim().Replace('/', '\\');
            int editableModelsIndex = editable.IndexOf("MODELS\\", StringComparison.OrdinalIgnoreCase);
            if (editableModelsIndex >= 0)
                normalized = editable.Substring(editableModelsIndex);

            string directory = "MODELS";
            int separator = normalized.LastIndexOf('\\');
            if (separator >= 0)
                directory = normalized.Substring(0, separator);

            string sceneName = safeSceneFileName(editable, fallback);
            string extension = mbin ? ".SCENE.MBIN" : ".SCENE.MXML";
            return Path.Combine(directory, sceneName + extension);
        }

        private static string safeSceneFileName(string sceneName, string fallback)
        {
            string value = String.IsNullOrWhiteSpace(sceneName) ? fallback ?? "SCENE" : sceneName;
            value = Path.GetFileName(value.Trim());
            string[] suffixes = { ".SCENE.MBIN", ".SCENE.MXML", ".MBIN", ".MXML", ".SCENE" };
            foreach (string suffix in suffixes)
                if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    value = value.Substring(0, value.Length - suffix.Length);
                    break;
                }
            char[] invalid = Path.GetInvalidFileNameChars();
            value = new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim(' ', '.');
            return value.Length == 0 ? "SCENE" : value;
        }

        private static void writeTemplate(libMBIN.NMSTemplate template, string path)
        {
            if (path.EndsWith(".MBIN", StringComparison.OrdinalIgnoreCase))
                template.WriteToMbin(path);
            else
                template.WriteToMxml(path, false, true);
        }
    }

}
