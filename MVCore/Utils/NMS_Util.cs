using System;
using System.Collections.Generic;
using System.IO;
using libMBIN;
using OpenTK.Mathematics;
using libMBIN.NMS.Toolkit;
using System.Security.Permissions;
using WPFModelViewer;
using System.Linq;
using OpenTK.Graphics.OpenGL4;
using Microsoft.Win32;
using Newtonsoft.Json;
using Path = System.IO.Path;
using MVCore.Common;
using System.Windows;
using libPSARC.PSARC;
using MVCore.GMDL;

namespace MVCore.Utils
{
    public static class NMSUtils
    {
        private static SortedDictionary<string, Archive> NMSArchiveMap = new SortedDictionary<string, Archive>();

        public static void DisposeArchives()
        {
            foreach (Archive archive in NMSArchiveMap.Values)
            {
                archive.Dispose();
            }
            NMSArchiveMap.Clear();
        }

        public static int GetFieldOffset(string className, string fieldName)
        {
            return 0x20 + NMSTemplate.OffsetOf(className, fieldName);
        }

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
                        ErrorUtils.throwException("File not found\n " + filepath);
                    MBINFile mbinf = new libMBIN.MBINFile(filepath);
                    mbinf.Load();
                    template = mbinf.GetData();
                    mbinf.Dispose();
                }
            } catch (Exception ex)
            {
                if (ex is DirectoryNotFoundException || ex is FileNotFoundException)
                {
                    Util.showError("File " + filepath + " Not Found...", "Error");


                } else if (ex is System.Reflection.TargetInvocationException)
                {
                    Util.showError("libMBIN failed to decompile the file. Try to update the libMBIN.dll (File->updateLibMBIN). If the issue persists contact the developer", "Error");
                }
                return null;

            }

            return template;
        }


        public static Stream LoadNMSFileStream(string filepath, ref ResourceManager resMgr)
        {
            int load_mode = 0;
            
            string conv_filepath = filepath.TrimStart('/');
            filepath = filepath.Replace('\\', '/');
            string effective_filepath = filepath;

            string exmlpath = Path.ChangeExtension(filepath, "exml");
            exmlpath = exmlpath.ToUpper(); //Make upper case

            if (File.Exists(Path.Combine(RenderState.settings.UnpackDir, exmlpath)))
                load_mode = 0; //Load Exml
            else if (File.Exists(Path.Combine(RenderState.settings.UnpackDir, filepath)))
                load_mode = 1; //Load MBIN from file
            else if (resMgr.NMSFileToArchiveMap.ContainsKey(filepath))
                load_mode = 2; //Extract file from archive
            else if (resMgr.NMSFileToArchiveMap.ContainsKey("/" + filepath))
            {
                effective_filepath = "/" + filepath;
                load_mode = 2; //Extract file from archive
            } else
            {
                CallBacks.Log("File: " + filepath + " Not found in PAKs or local folders. ");
                Util.showError("File: " + filepath + " Not found in PAKs or local folders. ", "Error");
                ErrorUtils.throwFileNotFoundException("File not found\n " + filepath);
            }
            Stream result = null;
            switch (load_mode)
            {
                case 0: //Load EXML
                    result = new FileStream(Path.Combine(RenderState.settings.UnpackDir, exmlpath), FileMode.Open);
                    break;
                case 1: //Load MBIN
                    result = new FileStream(Path.Combine(RenderState.settings.UnpackDir, filepath), FileMode.Open);
                    break;
                case 2: //Load File from Archive
                    {
                        CallBacks.Log("Trying to export File from PAK" + effective_filepath);
                        if (resMgr.NMSFileToArchiveMap.ContainsKey(effective_filepath))
                        {
                            CallBacks.Log("File was found in archives. File Index: " + resMgr.NMSFileToArchiveMap[effective_filepath]);
                        }

                        Archive arc = loadNMSArchive(resMgr.NMSFileToArchiveMap[effective_filepath]);
                        result = arc.ExtractFile(effective_filepath);
                        break;
                    }
            }

#if DEBUG
            if (result != null)
            {
                byte[] stream_data = new byte[result.Length];
                result.Seek(0, SeekOrigin.Begin);
                result.Read(stream_data, 0, stream_data.Length);
                result.Seek(0, SeekOrigin.Begin);
                string path = Path.Combine("Temp", filepath);
                if (!Directory.Exists(Path.GetDirectoryName(Path.GetFullPath(path))))
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                File.WriteAllBytes(Path.GetFullPath(path), stream_data);
            }
#endif
            return result;
        }

        public static NMSTemplate LoadNMSTemplate(string filepath, ref ResourceManager resMgr)
        {
            int load_mode = 0;
            NMSTemplate template = null;
            //filepath = Path.GetFullPath(filepath);
            filepath = filepath.Replace('\\', '/');
            string effective_filepath = filepath;

            //Checks to prevent malformed paths from further processing
            //if (filepath.Contains(' '))
            //    return null;

            string exmlpath = Path.ChangeExtension(filepath, "exml");
            exmlpath = exmlpath.ToUpper(); //Make upper case
            

            if (File.Exists(Path.Combine(RenderState.settings.UnpackDir, exmlpath)))
                load_mode = 0; //Load Exml
            else if (File.Exists(Path.Combine(RenderState.settings.UnpackDir, filepath)))
                load_mode = 1; //Load MBIN from file
            else if (resMgr.NMSFileToArchiveMap.ContainsKey(filepath))
                load_mode = 2; //Extract file from archive
            else if (resMgr.NMSFileToArchiveMap.ContainsKey("/" + filepath)) //AMUMSS BULLSHIT
            {
                effective_filepath = "/" + filepath;
                load_mode = 2; //Extract file from archive
            }
            else
            {
                CallBacks.Log("File: " + filepath + " Not found in PAKs or local folders. ");
                Util.showError("File: " + filepath + " Not found in PAKs or local folders. ", "Error");
                return null;
            }

            try
            {
                switch (load_mode)
                {
                    case 0: //Load EXML
                        {
                            string xml = File.ReadAllText(Path.Combine(RenderState.settings.UnpackDir, exmlpath));
                            template = EXmlFile.ReadTemplateFromString(xml);
                            break;
                        }
                    case 1: //Load MBIN
                        {
                            string eff_path = Path.Combine(RenderState.settings.UnpackDir, filepath);
                            MBINFile mbinf = new MBINFile(eff_path);
                            mbinf.Load();
                            template = mbinf.GetData();
                            mbinf.Dispose();
                            break;
                        }
                    case 2: //Load File from Archive
                        {
                            Archive arc = loadNMSArchive(resMgr.NMSFileToArchiveMap[effective_filepath]);
                            Stream file = arc.ExtractFile(effective_filepath);
                            MBINFile mbinf = new MBINFile(file);
                            mbinf.Load();
                            template = mbinf.GetData();
                            mbinf.Dispose();
                            break;
                        }
                }
            } catch (Exception ex)
            {
                if (ex is DirectoryNotFoundException || ex is System.IO.FileNotFoundException)
                    Util.showError("File " + effective_filepath + " Not Found...", "Error");
                else if (ex is IOException)
                    Util.showError("File " + effective_filepath + " problem...", "Error");
                else if (ex is System.Reflection.TargetInvocationException)
                {
                    Util.showError($"libMBIN failed to decompile file {effective_filepath}. If this is a vanilla file, contact the MbinCompiler developer",
                    "Error");
                    Util.Log(ex.StackTrace);
                }
                else
                {
                    Util.showError("Unhandled Exception " + ex.Message, "Error");
                }
                return null;

            }

#if DEBUG
            //Save NMSTemplate to exml
            string data = EXmlFile.WriteTemplate(template);
            string path =  Path.Combine("Temp", filepath + ".exml");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, data);
#endif
            return template;
        }

        //Animation frame data collection methods
        public static Quaternion fetchRotQuaternion(TkAnimNodeData node, TkAnimMetadata animMeta, int frameCounter)
        {
            //Load Frames
            //Common.CallBacks.Log("Setting Frame Index {0}", frameIndex);
            TkAnimNodeFrameData frame = animMeta.AnimFrameData[frameCounter];
            TkAnimNodeFrameData stillframe = animMeta.StillFrameData;

            Quaternion q;
            //Check if there is a rotation for that node
            if (node.RotIndex < frame.Rotations.Count)
            {
                int rotindex = node.RotIndex;
                q = new Quaternion((float) frame.Rotations[rotindex].x,
                                (float) frame.Rotations[rotindex].y,
                                (float) frame.Rotations[rotindex].z,
                                (float) frame.Rotations[rotindex].w);
            }
            else //Load stillframedata
            {
                int rotindex = node.RotIndex - frame.Rotations.Count;
                q = new Quaternion((float)stillframe.Rotations[rotindex].x,
                                (float)stillframe.Rotations[rotindex].y,
                                (float)stillframe.Rotations[rotindex].z,
                                (float)stillframe.Rotations[rotindex].w);
            }

            return q;
        }

        public static void fetchRotQuaternion(TkAnimNodeData node, TkAnimMetadata animMeta, int frameCounter, ref Quaternion q)
        {
            //Load Frames
            //Common.CallBacks.Log("Setting Frame Index {0}", frameIndex);
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

            q.X = (float) activeFrame.Rotations[rotIndex].x;
            q.Y = (float) activeFrame.Rotations[rotIndex].y;
            q.Z = (float) activeFrame.Rotations[rotIndex].z;
            q.W = (float) activeFrame.Rotations[rotIndex].w;

        }


        public static void fetchTransVector(TkAnimNodeData node, TkAnimMetadata animMeta, int frameCounter, ref Vector3 v)
        {
            //Load Frames
            //Common.CallBacks.Log("Setting Frame Index {0}", frameIndex);
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
            //Common.CallBacks.Log("Setting Frame Index {0}", frameIndex);
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
            //Common.CallBacks.Log("Setting Frame Index {0}", frameIndex);
            TkAnimNodeFrameData frame = animMeta.AnimFrameData[frameCounter];
            TkAnimNodeFrameData stillframe = animMeta.StillFrameData;

            Vector3 v;

            if (node.ScaleIndex < frame.Scales.Count)
            {
                v = new Vector3(
                    frame.Scales[node.ScaleIndex].x,
                    frame.Scales[node.ScaleIndex].y, 
                    frame.Scales[node.ScaleIndex].z);
            }
            else //Load stillframedata
            {
                int scaleindex = node.ScaleIndex - frame.Scales.Count;
                v = new Vector3(
                    stillframe.Scales[scaleindex].x,
                    stillframe.Scales[scaleindex].y,
                    stillframe.Scales[scaleindex].z);
            }

            return v;
        }

        public static void fetchScaleVector(TkAnimNodeData node, TkAnimMetadata animMeta, int frameCounter, ref Vector3 s)
        {
            //Load Frames
            //Common.CallBacks.Log("Setting Frame Index {0}", frameIndex);
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

            s.X = activeFrame.Scales[scaleIndex].x;
            s.Y = activeFrame.Scales[scaleIndex].y;
            s.Z = activeFrame.Scales[scaleIndex].z;
            
        }


        public static void SelectProcGenParts(Model node, ref List<string> partList)
        {
            //Identify procgen children
            HashSet<string> avail_selections = new();

            foreach (Model c in node.Children)
            {
                if (c.Name.StartsWith('_'))
                    avail_selections.Add(c.Name.Split('_')[1]);
            }

            //Process Parts
            foreach (string sel in avail_selections)
            {
                List<Model> avail_parts = new();
                foreach (Model c in node.Children)
                {
                    if (c.Name.StartsWith('_' + sel + '_'))
                        avail_parts.Add(c);
                }

                if (avail_parts.Count == 0)
                    continue;

                //Shuffle list of parts
                avail_parts = avail_parts.OrderBy(x => RenderState.randgen.Next()).ToList();

                //Select the first one
                avail_parts[0].IsRenderable = true;
                partList.Add(avail_parts[0].Name);

                for (int i = 1; i < avail_parts.Count; i++)
                    avail_parts[i].IsRenderable = false;

                SelectProcGenParts(avail_parts[0], ref partList);
            }

            //Iterate in non procgen children
            foreach (Model child in node.Children)
                if (!child.Name.StartsWith('_'))
                    SelectProcGenParts(child, ref partList);
        }

        public static void ProcGen()
        {
            CallBacks.Log("ProcGen Func");
            
            List<string> selected_procParts = new();

            //Make Selection
            SelectProcGenParts(RenderState.rootObject, ref selected_procParts);

            string partIds = "";
            for (int i = 0; i < selected_procParts.Count; i++)
                partIds += selected_procParts[i] + ' ';
            selected_procParts.Clear();

            CallBacks.Log("Proc Parts: {partIds}");
        }



        //Load Game Archive Handles
        public static Archive loadNMSArchive(string pakPath)
        {
            if (!NMSArchiveMap.ContainsKey(pakPath))
            {
                FileStream arc_stream = new FileStream(pakPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                Archive psarc = new Archive(arc_stream, true);
                NMSArchiveMap[pakPath] = psarc;
                CallBacks.Log("Loaded :" + pakPath);
            }

            return NMSArchiveMap[pakPath];
        }

        
        public static void loadNMSArchives(string gameDir, ref ResourceManager resMgr, ref int status)
        {
            CallBacks.Log("Trying to load PAK files from " + gameDir);
            if (!Directory.Exists(gameDir))
            {
                Util.showError("Unable to locate game Directory. PAK files (Vanilla + Mods) not loaded. You can still work using unpacked files", "Info");
                status = - 1;
                return;
            }
            
            //Load the handles to the resource manager
            
            //Fetch .pak files
            string[] pak_files = Directory.GetFiles(gameDir);
            DisposeArchives();

            CallBacks.updateStatus("Loading Vanilla NMS Archives...");

            foreach (string pak_path in pak_files)
            {
                if (!pak_path.EndsWith(".pak"))
                    continue;

                try
                {
                    loadNMSArchive(pak_path);
                }
                catch (Exception ex)
                {
                    Util.showError("An Error Occured : " + ex.Message, "Error");
                    CallBacks.Log("Pak file " + pak_path + " failed to load");
                    CallBacks.Log("Error : " + ex.GetType().Name + " " + ex.Message);
                }
            }
            
            if (Directory.Exists(Path.Combine(gameDir, "MODS")))
            {
                pak_files = Directory.GetFiles(Path.Combine(gameDir, "MODS"));
                Common.CallBacks.updateStatus("Loading Modded NMS Archives...");
                foreach (string pak_path in pak_files)
                {
                    if (pak_path.Contains("CUSTOMMODELS"))
                        CallBacks.Log(pak_path);

                    if (!pak_path.EndsWith(".pak"))
                        continue;
                    
                    try
                    {
                        loadNMSArchive(pak_path);
                    }
                    catch (Exception ex)
                    {
                        Util.showError("An Error Occured : " + ex.Message, "Error");
                        CallBacks.Log("Pak file " + pak_path + " failed to load");
                        CallBacks.Log("Error : " + ex.GetType().Name + " " + ex.Message);
                    }
                }
            }

            //Populate resource manager with the files
            CallBacks.updateStatus("Populating Resource Manager...");
            foreach (string arc_path in NMSArchiveMap.Keys.Reverse())
            {
                Archive arc = NMSArchiveMap[arc_path];

                foreach (string f in arc.filePaths)
                {
                    resMgr.NMSFileToArchiveMap[f] = arc_path;
                }
            }

            //NOT WORTH TO USE MANIFEST FILES


            /*
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
                        Common.CallBacks.Log(sp[0] + "   " + sp[1]);
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

                foreach (string arc_path in resMgr.NMSArchiveMap.Keys.Reverse())
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
            */

            status = 0; // All good
            DisposeArchives();
            CallBacks.updateStatus("Ready");
        }

        public static string getGameInstallationDir()
        {
            //Registry keys
            string gog32_keyname = @"HKEY_LOCAL_MACHINE\SOFTWARE\GOG.com\Games\1446213994";
            string gog32_keyval = "PATH";

            string gog64_keyname = @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\GOG.com\Games\1446213994";
            string gog64_keyval = "PATH";

            //Check Steam
            string val;

            try
            {
                val = fetchSteamGameInstallationDir();
            } catch (Exception e) {
                val = null;
            }

            if (val != null || val == "")  
                return val;
            else
                CallBacks.Log("Unable to find Steam Version");

            //Check GOG32
            val = Registry.GetValue(gog32_keyname, gog32_keyval, "") as string;
            if (val != null)
            {
                CallBacks.Log("Found GOG32 Version: " + val);
                return val;
            }
            else
                CallBacks.Log("Unable to find GOG32 Version: " + val);


            //Check GOG64
            val = Registry.GetValue(gog64_keyname, gog64_keyval, "") as string;
            if (val != null)
            {
                CallBacks.Log("Found GOG64 Version: " + val);
                return val;
            }
            else
                CallBacks.Log("Unable to find GOG64 Version: " + val);

            return null;
        }

        private static string fetchSteamGameInstallationDir()
        {
            //At first try to find the steam installation folder

            //Try to fetch the installation dir
            string steam_keyname = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam";
            string steam_keyval = "InstallPath";
            string nms_id = "275850";

            //Fetch Steam Installation Folder
            
            string steam_path = Registry.GetValue(steam_keyname, steam_keyval, null) as string;

            if (steam_path is null)
            {
                CallBacks.Log("Failed to find Steam Installation: ");
                return null;
            }
                
            CallBacks.Log("Found Steam Installation: " + steam_path);
            CallBacks.Log("Searching for NMS in the default steam directory...");

            //At first try to find acf entries in steam installation dir
            foreach (string path in Directory.GetFiles(Path.Combine(steam_path, "steamapps")))
            {
                if (!path.EndsWith(".acf"))
                    continue;

                if (path.Contains(nms_id))
                    return Path.Combine(steam_path, @"steamapps\common\No Man's Sky");
            }

            CallBacks.Log("NMS not found in default folders. Searching Steam Libraries...");

            //If that did't work try to load the libraryfolders.vdf
            StreamReader sr = new StreamReader(Path.Combine(steam_path, @"steamapps\libraryfolders.vdf"));
            List<string> libraryPaths = new List<string>();

            while (!sr.EndOfStream)
            {
                string line = sr.ReadLine();

                if (!line.Contains("\"path\""))
                    continue;

                line = line.Replace("\t", " ");
                line = line.Trim().Trim('\"');
                string path = line.Split('\"')[2];
                libraryPaths.Add(Path.Combine(path, "steamapps\\"));
            }
            
            //Check all library paths for the acf file

            foreach (string path in libraryPaths)
            {
                foreach (string filepath in Directory.GetFiles(path))
                {
                    if (!filepath.EndsWith(".acf"))
                        continue;

                    if (filepath.Contains(nms_id))
                        return Path.Combine(path, @"common\No Man's Sky");
                }
            }

            CallBacks.Log("Unable to locate Steam Installation...");
            return null;
        }

    }
}
