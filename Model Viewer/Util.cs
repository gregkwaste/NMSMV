using System;
using System.Collections.Generic;
using System.IO;
using OpenTK;
using System.Xml;
using System.Diagnostics;
using MVCore.Common;

namespace Model_Viewer
{
    public static class Util
    {
        public static string Version = "v0.80.1";
        public static readonly Random randgen = new Random();
        
        //Current GLControl Handle
        public static CGLControl activeControl;
        public static System.Windows.Forms.ToolStripStatusLabel activeStatusStrip;

        //Active Gamepad ID
        public static int gamepadID;

        public static int procGenNum;
        
        //Update Status strip
        public static void setStatus(string status)
        {
            activeStatusStrip.Text = status;
            activeStatusStrip.Invalidate();
            activeStatusStrip.GetCurrentParent().Refresh();
        }

        //Generic Procedures - File Loading
        public static void loadAnimationFile(string path, MVCore.GMDL.scene scn)
        {
            libMBIN.MBINFile mbinf = new libMBIN.MBINFile(path);
            mbinf.Load();
            scn.animMeta = (libMBIN.Models.Structs.TkAnimMetadata) mbinf.GetData();
        }

    
    }



}
