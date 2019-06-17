using System;
using System.Collections.Generic;
using System.IO;
using libMBIN;
using OpenTK;
using libMBIN.NMS.Toolkit;

namespace MVCore
{
    public static class NMSUtils
    {
        public static NMSTemplate LoadNMSFile(string filepath)
        {
            int load_mode = 0;
            string load_path;
            NMSTemplate template;

            if (filepath.ToUpper().EndsWith(".EXML"))
            {
                load_mode = 0;
                load_path = filepath;
            }
            else
            {
                //Try to find if there is an exml file first
                string exmlpath = Path.ChangeExtension(filepath, "exml");
                if (File.Exists(exmlpath))
                {
                    load_mode = 0;
                    load_path = exmlpath;
                }
                else
                {
                    load_mode = 1;
                    load_path = filepath;
                }
            }


            if (load_mode == 0)
            {
                //Load Exml
                string xml = File.ReadAllText(load_path);
                template = EXmlFile.ReadTemplateFromString(xml);
            }
            else
            {
                libMBIN.MBINFile mbinf = new libMBIN.MBINFile(load_path);
                mbinf.Load();
                template = mbinf.GetData();
                mbinf.Dispose();
            }

            return template;
        }


        //Animation frame data collection methods
        public static Quaternion fetchRotQuaternion(TkAnimNodeData node, TkAnimMetadata animMeta, int frameCounter)
        {
            //Load Frames
            //Console.WriteLine("Setting Frame Index {0}", frameIndex);
            TkAnimNodeFrameData frame = animMeta.AnimFrameData[frameCounter];
            TkAnimNodeFrameData stillframe = animMeta.StillFrameData;

            OpenTK.Quaternion q;
            //Check if there is a rotation for that node
            if (node.RotIndex < frame.Rotations.Count)
            {
                int rotindex = node.RotIndex;
                q = new OpenTK.Quaternion(frame.Rotations[rotindex].x,
                                frame.Rotations[rotindex].y,
                                frame.Rotations[rotindex].z,
                                frame.Rotations[rotindex].w);
            }
            else //Load stillframedata
            {
                int rotindex = node.RotIndex - frame.Rotations.Count;
                q = new OpenTK.Quaternion(stillframe.Rotations[rotindex].x,
                                stillframe.Rotations[rotindex].y,
                                stillframe.Rotations[rotindex].z,
                                stillframe.Rotations[rotindex].w);
            }

            return q;
        }

        public static Vector3 fetchTransVector(TkAnimNodeData node, TkAnimMetadata animMeta, int frameCounter)
        {
            //Load Frames
            //Console.WriteLine("Setting Frame Index {0}", frameIndex);
            TkAnimNodeFrameData frame = animMeta.AnimFrameData[frameCounter];
            TkAnimNodeFrameData stillframe = animMeta.StillFrameData;

            Vector3 v;
            //Load Translations
            if (node.TransIndex < frame.Translations.Count)
            {
                v = new Vector3(frame.Translations[node.TransIndex].x,
                                                    frame.Translations[node.TransIndex].y,
                                                    frame.Translations[node.TransIndex].z);
            }
            else //Load stillframedata
            {
                int transindex = node.TransIndex - frame.Translations.Count;
                v = new Vector3(stillframe.Translations[transindex].x,
                                                    stillframe.Translations[transindex].y,
                                                    stillframe.Translations[transindex].z);
            }

            return v;
        }


        public static Vector3 fetchScaleVector(TkAnimNodeData node, TkAnimMetadata animMeta, int frameCounter)
        {
            //Load Frames
            //Console.WriteLine("Setting Frame Index {0}", frameIndex);
            TkAnimNodeFrameData frame = animMeta.AnimFrameData[frameCounter];
            TkAnimNodeFrameData stillframe = animMeta.StillFrameData;

            Vector3 v;

            //Load Scaling - TODO
            if (node.ScaleIndex < frame.Scales.Count)
            {
                v = new Vector3(frame.Scales[node.ScaleIndex].x,
                    frame.Scales[node.ScaleIndex].y, frame.Scales[node.ScaleIndex].z);
            }
            else //Load stillframedata
            {
                int scaleindex = node.ScaleIndex - frame.Scales.Count;
                v = new Vector3(stillframe.Scales[scaleindex].x,
                    stillframe.Scales[scaleindex].y, stillframe.Scales[scaleindex].z);
            }

            return v;
        }

    }
}
