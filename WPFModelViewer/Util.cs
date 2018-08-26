using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Model_Viewer;

namespace WPFModelViewer
{
    public static class Util
    {
        public static string Version = "v0.80.1";
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
            activeStatusStrip.Text = status;
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
            loggingSr.WriteLine(msg);
            loggingSr.Flush();
        }
    }

}
