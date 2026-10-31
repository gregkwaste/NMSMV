using System;
using System.Collections.Generic;
using System.IO;
using libMBIN;
using OpenTK.Mathematics;
using libMBIN.NMS.Toolkit;
using System.Linq;
using OpenTK.Graphics.OpenGL4;
using Microsoft.Win32;
using Newtonsoft.Json;
using Path = System.IO.Path;
using MVCore.Common;
using libHGPAK.HGPAK;
using MVCore.GMDL;
using Accessibility;

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

        //Returns the path of the unpacked MXML file for the given MBIN path
        private static string getUnpackedXmlPath(string filepath)
        {
            return Path.ChangeExtension(filepath, "MXML").ToUpper();
        }

        public static int GetFieldOffset(string className, string fieldName)
        {
            return 0x20 + NMSTemplate.OffsetOf(className, fieldName);
        }

        public static NMSTemplate LoadNMSFileOLD(string filepath)
        {
            int load_mode = 0;
            NMSTemplate template;

            string mxmlpath = getUnpackedXmlPath(filepath);

            if (File.Exists(mxmlpath))
                load_mode = 0;
            else
                load_mode = 1;


            //Load Mxml
            try
            {
                if (load_mode == 0)
                {
                    string xml = File.ReadAllText(mxmlpath);
                    template = MXmlFile.ReadTemplateFromString(xml);
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
                    CallBacks.showError("File " + filepath + " Not Found...", "Error");


                } else if (ex is System.Reflection.TargetInvocationException)
                {
                    CallBacks.showError("libMBIN failed to decompile the file. Try to update the libMBIN.dll (File->updateLibMBIN). If the issue persists contact the developer", "Error");
                }
                return null;

            }

            return template;
        }


        //True if the file is in the unpacked folder or in the loaded PAKs
        public static bool NMSFileExists(string filepath, ResourceManager resMgr)
        {
            filepath = filepath.Replace('\\', '/');
            return File.Exists(Path.Combine(RenderState.settings.UnpackDir, filepath)) ||
                   File.Exists(Path.Combine(RenderState.settings.UnpackDir, getUnpackedXmlPath(filepath))) ||
                   resMgr.NMSFileToArchiveMap.ContainsKey(filepath) ||
                   resMgr.NMSFileToArchiveMap.ContainsKey("/" + filepath);
        }

        public static Stream LoadNMSFileStream(string filepath, ref ResourceManager resMgr, bool showError = true)
        {
            int load_mode = 0;
            
            string conv_filepath = filepath.TrimStart('/');
            filepath = filepath.Replace('\\', '/');
            string effective_filepath = filepath;

            string mxmlpath = getUnpackedXmlPath(filepath);

            //This returns raw binary streams (e.g. geometry), so unpacked MXML files are not used here
            if (File.Exists(Path.Combine(RenderState.settings.UnpackDir, filepath)))
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
                if (showError)
                    CallBacks.showError("File: " + filepath + " Not found in PAKs or local folders. ", "Error");
                ErrorUtils.throwFileNotFoundException("File not found\n " + filepath);
            }
            Stream result = null;
            switch (load_mode)
            {
                case 0: //Load MXML
                    CallBacks.Verbose("Loading mxml file from unpacked folder" + effective_filepath);
                    result = new FileStream(Path.Combine(RenderState.settings.UnpackDir, mxmlpath), FileMode.Open);
                    break;
                case 1: //Load MBIN
                    CallBacks.Verbose("Loading MBIN file from unpacked folder" + effective_filepath);
                    result = new FileStream(Path.Combine(RenderState.settings.UnpackDir, filepath), FileMode.Open);
                    break;
                case 2: //Load File from Archive
                    {
                        CallBacks.Verbose("Trying to export File from PAK" + effective_filepath);
                        if (resMgr.NMSFileToArchiveMap.ContainsKey(effective_filepath))
                        {
                            CallBacks.Verbose("File was found in archives. File Index: " + resMgr.NMSFileToArchiveMap[effective_filepath]);
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
            
            //Load Template from stream
            
            int load_mode = 0;
            NMSTemplate template = null;
            //filepath = Path.GetFullPath(filepath);
            filepath = filepath.Replace('\\', '/');
            string effective_filepath = filepath;

            //Checks to prevent malformed paths from further processing
            //if (filepath.Contains(' '))
            //    return null;

            string mxmlpath = getUnpackedXmlPath(filepath);
            

            if (File.Exists(Path.Combine(RenderState.settings.UnpackDir, mxmlpath)))
                load_mode = 0; //Load Mxml
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
                CallBacks.showError("File: " + filepath + " Not found in PAKs or local folders. ", "Error");
                return null;
            }

            try
            {
                switch (load_mode)
                {
                    case 0: //Load MXML
                        {
                            CallBacks.Verbose("Loading mxml template from unpacked folder: " + effective_filepath);
                            string xml = File.ReadAllText(Path.Combine(RenderState.settings.UnpackDir, mxmlpath));
                            template = MXmlFile.ReadTemplateFromString(xml);
                            break;
                        }
                    case 1: //Load MBIN
                        {
                            CallBacks.Verbose("Loading mbin template from unpacked folder: " + effective_filepath);
                            string eff_path = Path.Combine(RenderState.settings.UnpackDir, effective_filepath);
                            MBINFile mbinf = new MBINFile(eff_path);
                            mbinf.Load();
                            template = mbinf.GetData();
                            mbinf.Dispose();
                            break;
                        }
                    case 2: //Load File from Archive
                        {
                            CallBacks.Verbose("Loading template from PAK: " + effective_filepath);
                            Archive arc = loadNMSArchive(resMgr.NMSFileToArchiveMap[effective_filepath]);
                            Stream file = arc.ExtractFile(effective_filepath);
                            MBINFile mbinf = new MBINFile(file);
#if DEBUG
                            //Check for decompilation issues and save locally
                            if (mbinf.Header is null)
                            {
                                //Read the whole file from the start, then restore the position for libMBIN
                                long position = file.Position;
                                byte[] file_content = new byte[file.Length];
                                file.Seek(0, SeekOrigin.Begin);
                                file.ReadExactly(file_content, 0, file_content.Length);
                                file.Seek(position, SeekOrigin.Begin);
                                string mbin_path = Path.Combine("Temp", filepath);
                                Directory.CreateDirectory(Path.GetDirectoryName(mbin_path));
                                File.WriteAllBytes(mbin_path, file_content);
                            } 
#endif

                            mbinf.Load();
                            template = mbinf.GetData();
                            mbinf.Dispose();
                            break;
                        }
                }
            } catch (Exception ex)
            {
                if (ex is DirectoryNotFoundException || ex is System.IO.FileNotFoundException)
                    CallBacks.showError("File " + effective_filepath + " Not Found...", "Error");
                else if (ex is IOException)
                    CallBacks.showError("File " + effective_filepath + " problem...", "Error");
                else if (ex is System.Reflection.TargetInvocationException)
                {
                    CallBacks.showError($"libMBIN failed to decompile file {effective_filepath}. If this is a vanilla file, contact the MbinCompiler developer",
                    "Error");
                    CallBacks.Log(ex.StackTrace);
                }
                else
                {
                    CallBacks.showError("Unhandled Exception " + ex.Message, "Error");
                }
                return null;

            }

#if DEBUG
            //Save NMSTemplate to mxml
            if (template == null)
                return null;
            string data = MXmlFile.WriteTemplate(template);
            string path =  Path.Combine("Temp", filepath + ".mxml");
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
                q = new Quaternion((float) frame.Rotations[rotindex].X,
                                (float) frame.Rotations[rotindex].Y,
                                (float) frame.Rotations[rotindex].Z,
                                (float) frame.Rotations[rotindex].W);
            }
            else //Load stillframedata
            {
                int rotindex = node.RotIndex - frame.Rotations.Count;
                q = new Quaternion((float)stillframe.Rotations[rotindex].X,
                                (float)stillframe.Rotations[rotindex].Y,
                                (float)stillframe.Rotations[rotindex].Z,
                                (float)stillframe.Rotations[rotindex].W);
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

            q.X = (float) activeFrame.Rotations[rotIndex].X;
            q.Y = (float) activeFrame.Rotations[rotIndex].Y;
            q.Z = (float) activeFrame.Rotations[rotIndex].Z;
            q.W = (float) activeFrame.Rotations[rotIndex].W;

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

            v.X = activeFrame.Translations[transIndex].X;
            v.Y = activeFrame.Translations[transIndex].Y;
            v.Z = activeFrame.Translations[transIndex].Z;
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
                v = new Vector3(frame.Translations[node.TransIndex].X,
                                                    frame.Translations[node.TransIndex].Y,
                                                    frame.Translations[node.TransIndex].Z);
            }
            else //Load stillframedata
            {
                int transindex = node.TransIndex - frame.Translations.Count;
                v = new Vector3(stillframe.Translations[transindex].X,
                                                    stillframe.Translations[transindex].Y,
                                                    stillframe.Translations[transindex].Z);
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
                    frame.Scales[node.ScaleIndex].X,
                    frame.Scales[node.ScaleIndex].Y, 
                    frame.Scales[node.ScaleIndex].Z);
            }
            else //Load stillframedata
            {
                int scaleindex = node.ScaleIndex - frame.Scales.Count;
                v = new Vector3(
                    stillframe.Scales[scaleindex].X,
                    stillframe.Scales[scaleindex].Y,
                    stillframe.Scales[scaleindex].Z);
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

            s.X = activeFrame.Scales[scaleIndex].X;
            s.Y = activeFrame.Scales[scaleIndex].Y;
            s.Z = activeFrame.Scales[scaleIndex].Z;
            
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

            CallBacks.Log($"Proc Parts: {partIds}");
        }

        //Load Game Archive Handles
        public static Archive loadNMSArchive(string pakPath)
        {
            if (!NMSArchiveMap.ContainsKey(pakPath))
            {
                FileStream arc_stream = new FileStream(pakPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                Archive psarc = new Archive(arc_stream, true);
                NMSArchiveMap[pakPath] = psarc;
                CallBacks.Verbose("Loaded :" + pakPath);
            }

            return NMSArchiveMap[pakPath];
        }

        
        public static void loadNMSArchives(string gameDir, ref ResourceManager resMgr, ref int status)
        {
            CallBacks.Log("Trying to load PAK files from " + gameDir);
            if (!Directory.Exists(gameDir))
            {
                CallBacks.showError("Unable to locate game Directory. PAK files (Vanilla + Mods) not loaded. You can still work using unpacked files", "Info");
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
                    CallBacks.showError("An Error Occured : " + ex.Message, "Error");
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
                        CallBacks.Verbose(pak_path);

                    if (!pak_path.EndsWith(".pak"))
                        continue;
                    
                    try
                    {
                        loadNMSArchive(pak_path);
                    }
                    catch (Exception ex)
                    {
                        CallBacks.showError("An Error Occured : " + ex.Message, "Error");
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

            //TEMP (dev tool): component usage report, runs once
            //scanComponents(ref resMgr);

            status = 0; // All good
            DisposeArchives();
            CallBacks.updateStatus("Ready");
        }

        //TEMP (dev tool): scans every archived entity and scene and writes ComponentScan.md and ComponentScan.csv to the
        //working folder, listing which models use the entity components the viewer supports or might support. Runs
        //once, delete ComponentScan.md to scan again
        private static readonly string[] scanSupported = { "TkAnimPoseComponentData", "TkAnimationComponentData",
            "TkLODComponentData", "TkPhysicsComponentData", "GcTriggerActionComponentData" };
        private static readonly string[] scanCandidates = { "GcDestructableComponentData", "GcShootableComponentData",
            "TkAudioComponentData", "GcCreatureFullBodyIKComponentData", "GcCombatEffectsComponentData" };
        private static readonly string[] scanEmulated = { "GcPlayerNearbyEvent", "GcStateTimeEvent", "GcAnimFrameEvent",
            "GcNodeActivationAction", "GcGoToStateAction", "GcPlayAnimAction", "EmptyNode", "None" };

        private static void scanComponents(ref ResourceManager resMgr)
        {
            if (File.Exists("ComponentScan.md"))
                return;

            var clock = System.Diagnostics.Stopwatch.StartNew();
            Dictionary<string, List<string>> entityComponents = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> componentEntities = new Dictionary<string, List<string>>();
            Dictionary<string, HashSet<string>> entityModels = new Dictionary<string, HashSet<string>>();
            Dictionary<string, int> triggerTypeEntities = new Dictionary<string, int>();
            List<string> fullyEmulated = new List<string>();
            Dictionary<string, List<string>> animFeatures = new Dictionary<string, List<string>>();
            int entities = 0, scenes = 0, failed = 0;
            List<KeyValuePair<string, string>> files = resMgr.NMSFileToArchiveMap.ToList();

            //Pass 1: entities and their components
            foreach (KeyValuePair<string, string> kv in files)
            {
                if (!kv.Key.EndsWith(".ENTITY.MBIN", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!(scanLoad(kv) is TkAttachmentData data))
                {
                    failed++;
                    continue;
                }
                if (++entities % 500 == 0)
                    CallBacks.updateStatus("Scanning entities... " + entities);

                string entity = normalizeScanPath(kv.Key);
                List<string> comps = new List<string>();
                foreach (var comp in data.Components)
                {
                    string type = comp.Template?.GetType().Name ?? "Linked " + comp.Linked?.Value;
                    comps.Add(type);
                    if (!componentEntities.TryGetValue(type, out List<string> list))
                        componentEntities[type] = list = new List<string>();
                    list.Add(entity);

                    if (comp.Template is libMBIN.NMS.GameComponents.GcTriggerActionComponentData tac)
                        scanTriggerActions(entity, tac, triggerTypeEntities, fullyEmulated);
                    else if (comp.Template is TkAnimationComponentData acd)
                        scanAnimFeatures(entity, acd, animFeatures);
                }
                entityComponents[entity] = comps;
            }

            //Pass 2: scenes, to find the models that attach each entity
            foreach (KeyValuePair<string, string> kv in files)
            {
                if (!kv.Key.EndsWith(".SCENE.MBIN", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!(scanLoad(kv) is TkSceneNodeData root))
                {
                    failed++;
                    continue;
                }
                if (++scenes % 500 == 0)
                    CallBacks.updateStatus("Scanning scenes... " + scenes);
                collectAttachments(normalizeScanPath(kv.Key), root, entityModels);
            }

            string modelsOf(string entity, int max)
            {
                if (!entityModels.TryGetValue(entity, out HashSet<string> s))
                    return "(no scene attaches it)";
                List<string> sorted = s.OrderBy(x => x).ToList();
                return string.Join(", ", sorted.Take(max)) + (sorted.Count > max ? " (+" + (sorted.Count - max) + " more)" : "");
            }

            //Report
            List<string> md = new List<string>();
            md.Add("# Component scan");
            md.Add("");
            md.Add("Scanned " + entities + " entities and " + scenes + " scenes in " + clock.Elapsed.TotalMinutes.ToString("0.0") +
                   " min. " + failed + " files failed to load.");
            md.Add("");
            md.Add("## Components by use");
            md.Add("");
            md.Add("| Component | Status | Entities | Models |");
            md.Add("|---|---|---|---|");
            foreach (KeyValuePair<string, List<string>> kv in componentEntities.OrderByDescending(x => x.Value.Count))
            {
                string status = scanSupported.Contains(kv.Key) ? "supported" : scanCandidates.Contains(kv.Key) ? "candidate" : "";
                int models = kv.Value.Where(entityModels.ContainsKey).SelectMany(e => entityModels[e]).Distinct().Count();
                md.Add("| " + kv.Key + " | " + status + " | " + kv.Value.Count + " | " + models + " |");
            }

            foreach (string comp in scanSupported.Concat(scanCandidates))
            {
                md.Add("");
                md.Add("## " + comp);
                md.Add("");
                if (!componentEntities.TryGetValue(comp, out List<string> list))
                {
                    md.Add("Not used by any entity.");
                    continue;
                }
                //Entities that sit on a model first, they are the ones that can be loaded and tested
                foreach (string e in list.OrderBy(e => entityModels.ContainsKey(e) ? 0 : 1).ThenBy(e => e).Take(15))
                    md.Add("- " + e + ": " + modelsOf(e, 2));
            }

            md.Add("");
            md.Add("## TriggerAction event and action types");
            md.Add("");
            md.Add("| Type | Emulated | Entities |");
            md.Add("|---|---|---|");
            foreach (KeyValuePair<string, int> kv in triggerTypeEntities.OrderByDescending(x => x.Value))
                md.Add("| " + kv.Key + " | " + (scanEmulated.Contains(kv.Key) ? "yes" : "no") + " | " + kv.Value + " |");
            md.Add("");
            md.Add("### Entities that only use emulated types (" + fullyEmulated.Count + ")");
            md.Add("");
            foreach (string e in fullyEmulated.OrderBy(e => entityModels.ContainsKey(e.Split(' ')[0]) ? 0 : 1).ThenBy(e => e).Take(30))
                md.Add("- " + e + ": " + modelsOf(e.Split(' ')[0], 2));

            md.Add("");
            md.Add("## Animation features");
            md.Add("");
            md.Add("| Feature | Clips | Examples |");
            md.Add("|---|---|---|");
            foreach (KeyValuePair<string, List<string>> kv in animFeatures.OrderByDescending(x => x.Value.Count))
                md.Add("| " + kv.Key + " | " + kv.Value.Count + " | " + string.Join("<br>", kv.Value.Take(5)) + " |");

            List<string> csv = new List<string> { "Model;Entity;Components" };
            foreach (KeyValuePair<string, List<string>> kv in entityComponents.OrderBy(x => x.Key))
            {
                string comps = string.Join(",", kv.Value);
                if (entityModels.TryGetValue(kv.Key, out HashSet<string> models))
                    foreach (string m in models.OrderBy(x => x))
                        csv.Add(m + ";" + kv.Key + ";" + comps);
                else
                    csv.Add(";" + kv.Key + ";" + comps);
            }

            File.WriteAllLines("ComponentScan.md", md);
            File.WriteAllLines("ComponentScan.csv", csv);
            CallBacks.Log("Component scan written to " + Path.GetFullPath("ComponentScan.md"));
        }

        private static NMSTemplate scanLoad(KeyValuePair<string, string> file)
        {
            try
            {
                Archive arc = loadNMSArchive(file.Value);
                using Stream s = arc.ExtractFile(file.Key);
                MBINFile mbinf = new MBINFile(s);
                mbinf.Load();
                NMSTemplate data = mbinf.GetData();
                mbinf.Dispose();
                return data;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string normalizeScanPath(string path)
        {
            return (path ?? "").Replace('\\', '/').TrimStart('/').ToUpperInvariant();
        }

        private static void collectAttachments(string scene, TkSceneNodeData node, Dictionary<string, HashSet<string>> entityModels)
        {
            if (node.Attributes != null)
            {
                foreach (TkSceneNodeAttributeData attr in node.Attributes)
                {
                    if (attr.Name?.Value != "ATTACHMENT" || string.IsNullOrEmpty(attr.Value?.Value))
                        continue;
                    string entity = normalizeScanPath(attr.Value.Value);
                    if (!entityModels.TryGetValue(entity, out HashSet<string> set))
                        entityModels[entity] = set = new HashSet<string>();
                    set.Add(scene);
                }
            }

            if (node.Children != null)
                foreach (TkSceneNodeData child in node.Children)
                    collectAttachments(scene, child, entityModels);
        }

        private static void scanTriggerActions(string entity, libMBIN.NMS.GameComponents.GcTriggerActionComponentData tac,
            Dictionary<string, int> typeEntities, List<string> fullyEmulated)
        {
            HashSet<string> types = new HashSet<string>();
            foreach (var state in tac.States)
                foreach (var trig in state.Triggers)
                {
                    types.Add(trig.Event?.GetType().Name ?? "None");
                    foreach (NMSTemplate a in trig.Action)
                        types.Add(a?.GetType().Name ?? "None");
                }

            foreach (string t in types)
                typeEntities[t] = typeEntities.GetValueOrDefault(t) + 1;
            if (types.All(t => scanEmulated.Contains(t)))
                fullyEmulated.Add(entity + " (" + tac.States.Count + " states)");
        }

        private static void scanAnimFeatures(string entity, TkAnimationComponentData acd, Dictionary<string, List<string>> features)
        {
            void add(string feature, TkAnimationData clip)
            {
                if (!features.TryGetValue(feature, out List<string> list))
                    features[feature] = list = new List<string>();
                list.Add(entity + ": " + clip.Anim?.Value);
            }

            List<TkAnimationData> clips = new List<TkAnimationData>();
            if (acd.Idle != null)
                clips.Add(acd.Idle);
            if (acd.Anims != null)
                clips.AddRange(acd.Anims);

            foreach (TkAnimationData clip in clips)
            {
                if (clip.AnimType == TkAnimationData.AnimTypeEnum.OneShotBlendable)
                    add("OneShotBlendable", clip);
                if (clip.AnimType == TkAnimationData.AnimTypeEnum.Control)
                    add("Control", clip);
                if (clip.Additive)
                    add("Additive", clip);
                if (!string.IsNullOrEmpty(clip.AdditiveBaseAnim?.Value))
                    add("AdditiveBaseAnim", clip);
                if (!string.IsNullOrEmpty(clip.StartNode?.Value) || clip.ExtraStartNodes?.Count > 0)
                    add("StartNode", clip);
                if (!string.IsNullOrEmpty(clip.Mask?.Value) || clip.AdditionalMasks?.Count > 0)
                    add("Mask", clip);
            }
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
