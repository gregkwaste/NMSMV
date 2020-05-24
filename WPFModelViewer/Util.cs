using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MathNet.Numerics.Statistics;
using Model_Viewer;
using MVCore;

namespace WPFModelViewer
{
    public static class Util
    {
        public static int VersionMajor = 0;
        public static int VersionMedium = 88;
        public static int VersionMinor = 8;
        public static string VersionName = "-Test-Version";
        //public static string Version = "v0.88.6-Test-Version";
        public static string donateLink = "https://www.paypal.com/cgi-bin/webscr?cmd=_donations&business=4365XYBWGTBSU&currency_code=USD&source=url";
        public static readonly Random randgen = new Random();
        
        //Current GLControl Handle
        public static CGLControl activeControl;
        public static TextBlock activeStatusStrip;
        
        //Active Gamepad ID
        public static int gamepadID;
        
        //Public LogFile
        public static StreamWriter loggingSr;
        

        public static string getVersion()
        {
            return string.Join(".", new string[] { VersionMajor.ToString(),
                                           VersionMedium.ToString(),
                                           VersionMinor.ToString()}) + VersionName;
        }

        //Update Status strip
        public static void setStatus(string status)
        {
            Application.Current.Dispatcher.BeginInvoke((Action)(() =>
            {
                activeStatusStrip.Text = status;
            }));
            
        }

        //Generic Procedures - File Loading
        public static void loadAnimationFile(string path, MVCore.GMDL.model scn)
        {
            libMBIN.MBINFile mbinf = new libMBIN.MBINFile(path);
            mbinf.Load();
            //scn.animMeta = (libMBIN.NMS.Toolkit.TkAnimMetadata) mbinf.GetData();
        }

        public static void Log(string msg)
        {
#if DEBUG
            Console.WriteLine(msg); //Write to console if we are in debug mode
#endif
            loggingSr.WriteLine(msg);
            loggingSr.Flush();
        }

        public static void sendRequest(ref ThreadRequest req)
        {
            //This function simply issues the request for handling from the active GL Control
            //It is the senders responsibility to handle and keep track of any results if necessary

            //Should be awesome for blind requests that have to 
            activeControl?.issueRequest(ref req);
        }
    }

}
