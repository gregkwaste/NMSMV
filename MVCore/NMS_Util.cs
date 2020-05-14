using System;
using System.Collections.Generic;
using System.IO;
using libMBIN;
using OpenTK;
using libMBIN.NMS.Toolkit;
using System.Security.Permissions;
using WPFModelViewer;
using SharpFont;
using System.Linq;

namespace MVCore
{
    public static class NMSUtils
    {
        public static NMSTemplate LoadNMSFileOLD(string filepath)
        {
            int load_mode = 0;
            NMSTemplate template;

            string exmlpath = Path.ChangeExtension(filepath, "exml");
            exmlpath = exmlpath.ToUpper(); //Make upper case

            if (File.Exists(exmlpath))
                load_mode = 0;
            else
                load_mode = 1;


            //Load Exml
            try
            {
                if (load_mode == 0)
                {
                    string xml = File.ReadAllText(exmlpath);
                    template = EXmlFile.ReadTemplateFromString(xml);
                }
                else
                {
                    if (!File.Exists(filepath))
                        throw new FileNotFoundException("File not found\n " + filepath);
                    libMBIN.MBINFile mbinf = new libMBIN.MBINFile(filepath);
                    mbinf.Load();
                    template = mbinf.GetData();
                    mbinf.Dispose();
                }
            } catch (Exception ex)
            {
                if (ex is System.IO.DirectoryNotFoundException || ex is System.IO.FileNotFoundException)
                {
                    System.Windows.Forms.MessageBox.Show("File " + filepath + " Not Found...", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);

                } else if (ex is System.Reflection.TargetInvocationException)
                {
                    System.Windows.Forms.MessageBox.Show("libMBIN failed to decompile file. If this is a vanilla file, contact the MbinCompiler developer",
                    "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    
                }
                return null;

            }

            return template;
        }


        public static Stream LoadNMSFileStream(string filepath, ref ResourceManager resMgr)
        {
            int load_mode = 0;
            
            filepath = filepath.Replace('\\', '/');

            string exmlpath = Path.ChangeExtension(filepath, "exml");
            exmlpath = exmlpath.ToUpper(); //Make upper case

            if (File.Exists(Path.Combine(FileUtils.dirpath, exmlpath)))
                load_mode = 0; //Load Exml
            else if (File.Exists(Path.Combine(FileUtils.dirpath, filepath)))
                load_mode = 1; //Load MBIN from file
            else if (resMgr.NMSFileToArchiveMap.ContainsKey(filepath))
                load_mode = 2; //Extract file from archive
            else
                throw new FileNotFoundException("File not found\n " + filepath);

            switch (load_mode)
            {
                case 0: //Load EXML
                    return new FileStream(Path.Combine(FileUtils.dirpath, exmlpath), FileMode.Open);
                case 1: //Load MBIN
                    return new FileStream(Path.Combine(FileUtils.dirpath, filepath), FileMode.Open);
                case 2: //Load File from Archive
                    return resMgr.NMSFileToArchiveMap[filepath].ExtractFile(filepath);
            }

            return null;
        }

        public static NMSTemplate LoadNMSTemplate(string filepath, ref ResourceManager resMgr)
        {
            int load_mode = 0;
            NMSTemplate template = null;

            filepath = filepath.Replace('\\', '/');

            string exmlpath = Path.ChangeExtension(filepath, "exml");
            exmlpath = exmlpath.ToUpper(); //Make upper case

            if (File.Exists(Path.Combine(FileUtils.dirpath, exmlpath)))
                load_mode = 0; //Load Exml
            else if (File.Exists(Path.Combine(FileUtils.dirpath, filepath)))
                load_mode = 1; //Load MBIN from file
            else if (resMgr.NMSFileToArchiveMap.ContainsKey(filepath))
                load_mode = 2; //Extract file from archive
            else
                throw new FileNotFoundException("File not found\n " + filepath);

            try
            {
                switch (load_mode)
                {
                    case 0: //Load EXML
                        {
                            string xml = File.ReadAllText(Path.Combine(FileUtils.dirpath, exmlpath));
                            return EXmlFile.ReadTemplateFromString(xml);
                        }
                    case 1: //Load MBIN
                        {
                            string eff_path = Path.Combine(FileUtils.dirpath, filepath);
                            MBINFile mbinf = new MBINFile(eff_path);
                            mbinf.Load();
                            template = mbinf.GetData();
                            mbinf.Dispose();
                            return template;
                        }
                    case 2: //Load File from Archive
                        {
                            Stream file = resMgr.NMSFileToArchiveMap[filepath].ExtractFile(filepath);
                            MBINFile mbinf = new MBINFile(file);
                            mbinf.Load();
                            template = mbinf.GetData();
                            mbinf.Dispose();
                            return template;
                        }
                }
            } catch (Exception ex)
            {

                if (ex is System.IO.DirectoryNotFoundException || ex is System.IO.FileNotFoundException)
                    System.Windows.Forms.MessageBox.Show("File " + filepath + " Not Found...", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                else if (ex is System.IO.IOException)
                    System.Windows.Forms.MessageBox.Show("File " + filepath + " problem...", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                else if (ex is System.Reflection.TargetInvocationException)
                {
                    System.Windows.Forms.MessageBox.Show("libMBIN failed to decompile file. If this is a vanilla file, contact the MbinCompiler developer",
                    "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                }
                return null;

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

        public static void fetchRotQuaternion(TkAnimNodeData node, TkAnimMetadata animMeta, int frameCounter, ref Quaternion q)
        {
            //Load Frames
            //Console.WriteLine("Setting Frame Index {0}", frameIndex);
            TkAnimNodeFrameData frame = animMeta.AnimFrameData[frameCounter];
            TkAnimNodeFrameData stillframe = animMeta.StillFrameData;
            TkAnimNodeFrameData activeFrame = null;
            int rotIndex = -1;
            //Check if there is a rotation for that node
            
            if (node.RotIndex < frame.Rotations.Count)
            {
                activeFrame = frame;
                rotIndex = node.RotIndex;
}
            else //Load stillframedata
            {
                activeFrame = stillframe;
                rotIndex = node.RotIndex - frame.Rotations.Count;
            }

            q.X = activeFrame.Rotations[rotIndex].x;
            q.Y = activeFrame.Rotations[rotIndex].y;
            q.Z = activeFrame.Rotations[rotIndex].z;
            q.W = activeFrame.Rotations[rotIndex].w;

        }


        public static void fetchTransVector(TkAnimNodeData node, TkAnimMetadata animMeta, int frameCounter, ref Vector3 v)
        {
            //Load Frames
            //Console.WriteLine("Setting Frame Index {0}", frameIndex);
            TkAnimNodeFrameData frame = animMeta.AnimFrameData[frameCounter];
            TkAnimNodeFrameData stillframe = animMeta.StillFrameData;
            TkAnimNodeFrameData activeFrame;
            int transIndex = -1;

            //Load Translations
            if (node.TransIndex < frame.Translations.Count)
            {
                transIndex = node.TransIndex;
                activeFrame = frame;
                
            }
            else //Load stillframedata
            {
                transIndex = node.TransIndex - frame.Translations.Count;
                activeFrame = stillframe;
            }


            v.X = activeFrame.Translations[transIndex].x;
            v.Y = activeFrame.Translations[transIndex].y;
            v.Z = activeFrame.Translations[transIndex].z;
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

            if (node.ScaleIndex < frame.Scales.Count)
            {
                v = new Vector3(
                    frame.Scales[node.ScaleIndex].x / frame.Scales[node.ScaleIndex].t,
                    frame.Scales[node.ScaleIndex].y / frame.Scales[node.ScaleIndex].t, 
                    frame.Scales[node.ScaleIndex].z / frame.Scales[node.ScaleIndex].t );
            }
            else //Load stillframedata
            {
                int scaleindex = node.ScaleIndex - frame.Scales.Count;
                v = new Vector3(
                    stillframe.Scales[scaleindex].x / stillframe.Scales[scaleindex].t,
                    stillframe.Scales[scaleindex].y / stillframe.Scales[scaleindex].t,
                    stillframe.Scales[scaleindex].z / stillframe.Scales[scaleindex].t );
            }

            return v;
        }

        public static void fetchScaleVector(TkAnimNodeData node, TkAnimMetadata animMeta, int frameCounter, ref Vector3 s)
        {
            //Load Frames
            //Console.WriteLine("Setting Frame Index {0}", frameIndex);
            TkAnimNodeFrameData frame = animMeta.AnimFrameData[frameCounter];
            TkAnimNodeFrameData stillframe = animMeta.StillFrameData;
            TkAnimNodeFrameData activeFrame = null;
            int scaleIndex = -1;

            if (node.ScaleIndex < frame.Scales.Count)
            {
                scaleIndex = node.ScaleIndex;
                activeFrame = frame;
            }
            else //Load stillframedata
            {
                scaleIndex = node.ScaleIndex - frame.Scales.Count;
                activeFrame = stillframe;
            }

            s.X = activeFrame.Scales[scaleIndex].x / activeFrame.Scales[scaleIndex].t;
            s.Y = activeFrame.Scales[scaleIndex].y / activeFrame.Scales[scaleIndex].t;
            s.Z = activeFrame.Scales[scaleIndex].z / activeFrame.Scales[scaleIndex].t;
            
        }


        //Load Game Archive Handles
        
        public static void loadNMSArchives(string filepath, string gameDir, ref ResourceManager resMgr)
        {
            //Load the handles to the resource manager
            
            //Fetch .pak files
            string[] pak_files = Directory.GetFiles(gameDir);
            resMgr.NMSArchiveMap.Clear();

            Common.CallBacks.updateStatus("Loading NMS Archives...");

            foreach (string pak_path in pak_files)
            {
                if (!pak_path.EndsWith(".pak"))
                    continue;

                FileStream arc_stream = new FileStream(pak_path, FileMode.Open);
                libPSARC.PSARC.Archive psarc = new libPSARC.PSARC.Archive(arc_stream, true);
                
                resMgr.NMSArchiveMap[pak_path] = psarc;
            }
    
            
            //Check if manifest file exists
            if (File.Exists(filepath))
            {
                Common.CallBacks.updateStatus("Loading NMS File manifest...");
                
                //Read Input Data
                FileStream fs = new FileStream(filepath, FileMode.Open);
                byte[] comp_data = new byte[fs.Length];
                fs.Read(comp_data, 0, (int) fs.Length);
                fs.Close();

                MemoryStream decomp_ms = new MemoryStream();
                zlib.ZOutputStream zout = new zlib.ZOutputStream(decomp_ms);
                zout.Write(comp_data, 0, comp_data.Length);
                zout.finish();

                //Read the assignment from the files
                decomp_ms.Seek(0, SeekOrigin.Begin);
                StreamReader sr = new StreamReader(decomp_ms);

                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();
                    string[] sp = line.Split('\t');
                    if (sp.Length != 2)
                        Console.WriteLine(sp[0] + "   " + sp[1]);
                    resMgr.NMSFileToArchiveMap[sp[0]] = resMgr.NMSArchiveMap[sp[1]];
                }

                sr.Close();
                zout.Close();

            }
            else
            {
                Common.CallBacks.updateStatus("NMS File Manifest not found. Creating...");

                MemoryStream ms = new MemoryStream();
                MemoryStream comp_ms = new MemoryStream();
                StreamWriter sw = new StreamWriter(ms);

                foreach (string arc_path in resMgr.NMSArchiveMap.Keys)
                {
                    libPSARC.PSARC.Archive arc = resMgr.NMSArchiveMap[arc_path];

                    foreach (string f in arc.filePaths)
                    {
                        sw.WriteLine(f + '\t' + arc_path);
                        resMgr.NMSFileToArchiveMap[f] = resMgr.NMSArchiveMap[arc_path];
                    }
                }

                //Compress memorystream
                zlib.ZOutputStream zout = new zlib.ZOutputStream(comp_ms, zlib.zlibConst.Z_DEFAULT_COMPRESSION);

                //Copy Data to the zlib stream\
                sw.Flush();
                byte[] comp_data = ms.ToArray();
                
                zout.Write(comp_data, 0, (int) ms.Length);
                zout.finish(); //Compress

                //Write memorystream to file
                comp_ms.Seek(0, SeekOrigin.Begin);
                FileStream fs = new FileStream(filepath, FileMode.CreateNew);
                fs.Write(comp_ms.ToArray(), 0, (int) comp_ms.Length);
                fs.Close();
                comp_ms.Close();
                ms.Close();
            
            }
        }

        public static void unloadNMSArchives(ref ResourceManager resMgr)
        {
            foreach (libPSARC.PSARC.Archive arc in resMgr.NMSArchiveMap.Values)
            {
                arc.Dispose();
            }
        }

    }
}
