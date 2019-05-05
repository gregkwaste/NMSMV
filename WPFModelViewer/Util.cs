using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Model_Viewer;
using MVCore;

namespace WPFModelViewer
{
    public static class Util
    {
        public static string Version = "v0.80.6-Test-Version";
        public static readonly Random randgen = new Random();

        //Current GLControl Handle
        public static CGLControl activeControl;
        public static TextBlock activeStatusStrip;
        
        //Active Gamepad ID
        public static int gamepadID;
        public static int procGenNum;

        //Public LogFile
        public static StreamWriter loggingSr;
        
        //Update Status strip
        public static void setStatus(string status)
        {
            Application.Current.Dispatcher.Invoke((Action)(() =>
            {
                activeStatusStrip.Text = status;
            }));
            
        }

        //Generic Procedures - File Loading
        public static void loadAnimationFile(string path, MVCore.GMDL.scene scn)
        {
            libMBIN.MBINFile mbinf = new libMBIN.MBINFile(path);
            mbinf.Load();
            scn.animMeta = (libMBIN.Models.Structs.TkAnimMetadata)mbinf.GetData();
        }

        public static void Log(string msg)
        {
#if DEBUG
            Console.WriteLine(msg); //Write to console if we are in debug mode
#endif
            loggingSr.WriteLine(msg);
            loggingSr.Flush();
        }

        public static void sendRequest(ThreadRequest req)
        {
            //This function simply issues the request for handling from the active GL Control
            //It is the senders responsibility to handle and keep track of any results if necessary

            //Should be awesome for blind requests that have to 
            activeControl?.issueRequest(req);
        }
    }

}
