using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Model_Viewer;
using MVCore;
using MVCore.Common;

namespace WPFModelViewer
{
    public static class Util
    {
        public static int VersionMajor = 0;
        public static int VersionMedium = 91;
        public static int VersionMinor = 0;
        
        public static string donateLink = "https://www.paypal.com/donate/?hosted_button_id=UMHWZK6BU5MWG";
        public static readonly Random randgen = new Random();
        
        //Current GLControl Handle
        public static CGLControl activeControl;
        public static Window activeWindow;
        public static TextBlock activeStatusStrip;
        
        //Public LogFile
        public static StreamWriter loggingSr;
        

        public static string getVersion()
        {
            string ver = string.Join(".", new string[] { VersionMajor.ToString(),
                                           VersionMedium.ToString(),
                                           VersionMinor.ToString()});
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
            string message = string.Join(" ", msg);
#if DEBUG
            Console.WriteLine(message); //Write to console if we are in debug mode
#endif
            loggingSr.WriteLine(message);
            loggingSr.Flush();
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
