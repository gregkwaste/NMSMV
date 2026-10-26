using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Model_Viewer;
using MVCore;
using MVCore.Common;

namespace WPFModelViewer
{
    public static class Util
    {
        public static int VersionMajor = 0;
        public static int VersionMedium = 96;
        public static int VersionMinor = 0;
        public static bool Prerelease = true;
        
        public static string donateLink = "https://www.paypal.com/donate/?hosted_button_id=UMHWZK6BU5MWG";
        public static readonly Random randgen = new Random();
        
        //Current GLControl Handle
        public static CGLControl activeControl;
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
#endif
            return ver;
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
                    loggingSr = new StreamWriter("log.out", logOpened);
                    logOpened = true;
                }
                loggingSr.WriteLine(message);
                loggingSr.Flush();
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

        public static void sendRequest(ref ThreadRequest req)
        {
            //This function simply issues the request for handling from the active GL Control
            //It is the senders responsibility to handle and keep track of any results if necessary

            //Should be awesome for blind requests that have to 
            activeControl?.issueRenderingRequest(ref req);
        }
    }

}
