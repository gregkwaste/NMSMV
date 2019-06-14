using System.IO;
using System.Diagnostics;
using System.Xml;
using System.Collections.Generic;
using System;
using System.Globalization;
using OpenTK;
using Model_Viewer;
using libMBIN;
using libMBIN.NMS.Toolkit;
using System.Linq;
using System.Windows.Forms;
using MVCore;
using MVCore.GMDL;
using Console = System.Console;


namespace MVCore
{
    
    public enum TYPES
    {
        MESH=0x0,
        LOCATOR,
        JOINT,
        LIGHT,
        EMITTER,
        COLLISION,
        SCENE,
        MODEL,
        REFERENCE,
        DECAL,
        UNKNOWN
    }

    public enum COLLISIONTYPES
    {
        MESH = 0x0,
        SPHERE,
        CYLINDER,
        BOX,
        CAPSULE    
    }

    public class meshMetaData
    {
        public string name;
        public ulong hash;
        public uint vs_size;
        public uint vs_abs_offset;
        public uint is_size;
        public uint is_abs_offset;
    }

    public class meshData
    {
        public ulong hash;
        public byte[] vs_buffer;
        public byte[] is_buffer;
    }

    public static class GEOMMBIN {

        public static GeomObject Parse(FileStream fs)
        {
            BinaryReader br = new BinaryReader(fs);
            Console.WriteLine("Parsing MBIN");

            fs.Seek(0x60, SeekOrigin.Begin);

            var vert_num = br.ReadInt32();
            var indices_num = br.ReadInt32();
            var indices_flag = br.ReadInt32();
            var collision_index_count = br.ReadInt32();

            Console.WriteLine("Model Vertices: {0}", vert_num);
            Console.WriteLine("Model Indices: {0}", indices_num);
            Console.WriteLine("Indices Flag: {0}", indices_flag);
            Console.WriteLine("Collision Index Count: {0}", collision_index_count);

            //Joint Bindings
            var jointbindingOffset = fs.Position + br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);
            var jointCount = br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);
            //Skip Unknown yet offset sections
            //Joint Extensions
            //Joint Mirror Pairs
            fs.Seek(3 * 0x10, SeekOrigin.Current);

            //Usefull Bone Remapping information

            var skinmatoffset = fs.Position + br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);
            var bc = br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);

            //Vertstarts
            var vsoffset = fs.Position + br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);
            var partcount = br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);
            
            //VertEnds
            fs.Seek(0x10, SeekOrigin.Current);

            //Bound Hull Vert start
            var boundhull_vertstart_offset = fs.Position + br.ReadInt32();
            fs.Seek(0xC, SeekOrigin.Current);

            //Bound Hull Vert end
            var boundhull_vertend_offset = fs.Position + br.ReadInt32();
            fs.Seek(0xC, SeekOrigin.Current);

            //MatrixLayouts
            fs.Seek(0x10, SeekOrigin.Current);

            //BoundBoxes
            var bboxminoffset = fs.Position + br.ReadInt32();
            fs.Seek(0xC, SeekOrigin.Current);
            var bboxmaxoffset = fs.Position + br.ReadInt32();
            fs.Seek(0xC, SeekOrigin.Current);

            //Bound Hull Verts
            var bhulloffset = fs.Position + br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);
            var bhull_count = br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);


            var lod_count = br.ReadInt32();
            var vx_type = br.ReadInt32();
            Console.WriteLine("Buffer Count: {0} VxType {1}", lod_count, vx_type);
            fs.Seek(0x8, SeekOrigin.Current);
            var mesh_descr_offset = fs.Position + br.ReadInt64();
            var buf_count = br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current); //Skipping a 1

            //Parse Small Vertex Layout Info
            var small_bufcount = br.ReadInt32();
            var small_vx_type = br.ReadInt32();
            Console.WriteLine("Small Buffer Count: {0} VxType {1}", small_bufcount, small_vx_type);
            fs.Seek(0x8, SeekOrigin.Current);
            var small_mesh_descr_offset = fs.Position + br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);
            br.ReadInt32(); //Skip second buf count
            fs.Seek(0x4, SeekOrigin.Current); //Skipping a 1

            //fs.Seek(0x20, SeekOrigin.Current); //Second lod offsets

            //Get primary geom offsets
            var indices_offset = fs.Position + br.ReadInt64();
            fs.Seek(0x8, SeekOrigin.Current); //Skip Section Sizes and a 1

            var meshMetaData_offset = fs.Position + br.ReadInt64();
            var meshMetaData_counter = br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current); //Skip Section Sizes and a 1

            //fs.Seek(0x10, SeekOrigin.Current);

            //Initialize geometry object
            var geom = new GeomObject();

            //Store Counts
            geom.indicesCount = indices_num;
            if (indices_flag == 0x1)
            {
                geom.indicesLength = 0x2;
                geom.indicesLengthType = OpenTK.Graphics.OpenGL4.DrawElementsType.UnsignedShort;
            }
            else
            {
                geom.indicesLength = 0x4;
                geom.indicesLengthType = OpenTK.Graphics.OpenGL4.DrawElementsType.UnsignedInt;
            }
                
            geom.vertCount = vert_num;
            geom.vx_size = vx_type;
            geom.small_vx_size = small_vx_type;

            //Get Bone Remapping Information
            //I'm 99% sure that boneRemap is not a case in NEXT models
            //it is still there though...
            fs.Seek(skinmatoffset, SeekOrigin.Begin);
            geom.boneRemap = new short[bc];
            for (int i = 0; i < bc; i++)
                geom.boneRemap[i] = (short) br.ReadInt32();

            //Store Joint Data
            fs.Seek(jointbindingOffset, SeekOrigin.Begin);
            for (int i = 0; i < jointCount; i++)
            {
                JointBindingData jdata = new JointBindingData();
                jdata.Load(fs);
                //Copy Matrix
                Array.Copy(jdata.convertMat(), 0, geom.invBMats, 16 * i, 16);
                //Store the struct
                geom.jointData.Add(jdata);
            }

            //Get Vertex Starts
            //I'm fetching that just for getting the object id within the geometry file
            fs.Seek(vsoffset, SeekOrigin.Begin);
            for (int i = 0; i < partcount; i++)
                geom.vstarts.Add(br.ReadInt32());
        
            //Get BBoxes
            //Init first
            for (int i = 0; i < partcount; i++)
            {
                Vector3[] bb = new Vector3[2];
                geom.bboxes.Add(bb);
            }

            fs.Seek(bboxminoffset, SeekOrigin.Begin);
            for (int i = 0; i < partcount; i++) {
                geom.bboxes[i][0] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                br.ReadBytes(4);
            }

            fs.Seek(bboxmaxoffset, SeekOrigin.Begin);
            for (int i = 0; i < partcount; i++)
            {
                geom.bboxes[i][1] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                br.ReadBytes(4);
            }

            //Get BoundHullStarts
            fs.Seek(boundhull_vertstart_offset, SeekOrigin.Begin);
            for (int i = 0; i < partcount; i++)
            {
                geom.bhullstarts.Add(br.ReadInt32());
            }

            //Get BoundHullEnds
            fs.Seek(boundhull_vertend_offset, SeekOrigin.Begin);
            for (int i = 0; i < partcount; i++)
            {
                geom.bhullends.Add(br.ReadInt32());
            }

            //TODO : Recheck and fix that shit
            fs.Seek(bhulloffset, SeekOrigin.Begin);
            for (int i = 0; i < bhull_count; i++)
            {
                geom.bhullverts.Add(new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                br.ReadBytes(4);
            }

            //Get indices buffer
            fs.Seek(indices_offset, SeekOrigin.Begin);
            geom.ibuffer = new byte[indices_num * geom.indicesLength];
            fs.Read(geom.ibuffer, 0, indices_num * geom.indicesLength);

            //Get MeshMetaData
            fs.Seek(meshMetaData_offset, SeekOrigin.Begin);
            for (int i = 0; i < meshMetaData_counter; i++)
            {
                meshMetaData mmd = new meshMetaData();
                mmd.name = StringUtils.read_string(br, 0x80);
                mmd.hash = br.ReadUInt64();
                mmd.vs_size = br.ReadUInt32();
                mmd.vs_abs_offset = br.ReadUInt32();
                mmd.is_size = br.ReadUInt32();
                mmd.is_abs_offset = br.ReadUInt32();
                geom.meshMetaDataDict[mmd.hash] = mmd;
                Console.WriteLine(mmd.name);
            }
        
            //Get main mesh description
            fs.Seek(mesh_descr_offset, SeekOrigin.Begin);
            var mesh_desc = "";
            //int[] mesh_offsets = new int[buf_count];
            //Set size excplicitly to 7
            int[] mesh_offsets = new int[7];
            geom.bufInfo = new List<bufInfo>();
            //Set all offsets to -1
            for (int i = 0; i < 7; i++)
            {
                mesh_offsets[i] = -1;
                geom.bufInfo.Add(null);
            }

            for (int i = 0; i < buf_count; i++)
            {
                var buf_id = br.ReadInt32();
                var buf_elem_count = br.ReadInt32();
                var buf_type = br.ReadInt32();
                var buf_localoffset = br.ReadInt32();
                geom.bufInfo[buf_id]= get_bufInfo_item(buf_id, buf_localoffset, buf_elem_count, buf_type);
                mesh_offsets[buf_id] = buf_localoffset;
                fs.Seek(0x10, SeekOrigin.Current);
            }

            //Get Descr
            mesh_desc = getDescr(ref mesh_offsets, buf_count);
            Console.WriteLine("Mesh Description: " + mesh_desc);

            //Store description
            geom.mesh_descr = mesh_desc;
            geom.offsets = mesh_offsets;
            //Get small description
            fs.Seek(small_mesh_descr_offset, SeekOrigin.Begin);
            var small_mesh_desc = "";
            //int[] mesh_offsets = new int[buf_count];
            //Set size excplicitly to 7
            int[] small_mesh_offsets = new int[7];
            //Set all offsets to -1
            for (int i = 0; i < 7; i++)
                small_mesh_offsets[i] = -1;

            for (int i = 0; i < small_bufcount; i++)
            {
                var buf_id = br.ReadInt32();
                var buf_elem_count = br.ReadInt32();
                var buf_type = br.ReadInt32();
                var buf_localoffset = br.ReadInt32();
                small_mesh_offsets[buf_id] = buf_localoffset;
                fs.Seek(0x10, SeekOrigin.Current);
            }

            //Get Small Descr
            small_mesh_desc = getDescr(ref small_mesh_offsets, small_bufcount);
            Console.WriteLine("Small Mesh Description: " + small_mesh_desc);

            //Store description
            geom.small_mesh_descr = small_mesh_desc;
            geom.small_offsets = small_mesh_offsets;
            //Set geom interleaved
            geom.interleaved = true;

            
            //Try to fetch the geometry.data.mbin file in order to fetch the streams
            string gstream_path = "";
            MVCore.Common.CallBacks.Log(string.Format("Trying to load GStream {0}", fs.Name));
            string[] split = fs.Name.Split('.');
            for (int i = 0; i < split.Length - 2; i++)
                gstream_path += split[i] + ".";
            gstream_path += "DATA.MBIN.PC";

            //Check if file exists
            if (!File.Exists(gstream_path))
                throw new IOException("Geometry Stream File Missing. Check your fucking files...");

            //Fetch streams from the gstream file based on the metadatafile
            FileStream gs_fs = new FileStream(gstream_path, FileMode.Open);
        
            foreach (KeyValuePair<ulong, meshMetaData> pair in geom.meshMetaDataDict)
            {
                meshMetaData mmd = pair.Value;
                meshData md = new meshData();
                md.vs_buffer = new byte[mmd.vs_size];
                md.is_buffer = new byte[mmd.is_size];
            
                //Fetch Buffers
                gs_fs.Seek((int) mmd.vs_abs_offset, SeekOrigin.Begin);
                gs_fs.Read(md.vs_buffer, 0, (int) mmd.vs_size);
            
                gs_fs.Seek((int) mmd.is_abs_offset, SeekOrigin.Begin);
                gs_fs.Read(md.is_buffer, 0, (int) mmd.is_size);
            
                geom.meshDataDict[mmd.hash] = md;
            }

            gs_fs.Close();

            return geom;

        }


        private static bufInfo get_bufInfo_item(int buf_id, int buf_localoffset, int count, int buf_type)
        {
            int sem = buf_id;
            int off = buf_localoffset;
            OpenTK.Graphics.OpenGL4.VertexAttribPointerType typ = get_type(buf_type);
            string text = get_shader_sem(buf_id);
            return new bufInfo(sem, typ, count, off, text);
        }


        private static string get_shader_sem(int buf_id)
        {
            switch (buf_id)
            {
                case 0:
                    return "vPosition"; //Verts
                case 1:
                    return "uvPosition0"; //Verts
                case 2:
                    return "nPosition"; //Verts
                case 3:
                    return "tPosition"; //Verts
                case 4:
                    return "bPosition"; //Verts
                case 5:
                    return "blendIndices"; //Verts
                case 6:
                    return "blendWeights"; //Verts
                default:
                    return "shit"; //Default
            }
        }

        private static OpenTK.Graphics.OpenGL4.VertexAttribPointerType get_type(int val){

            switch (val)
            {
                case (0x140B):
                    return OpenTK.Graphics.OpenGL4.VertexAttribPointerType.HalfFloat;
                case (0x1401):
                    return OpenTK.Graphics.OpenGL4.VertexAttribPointerType.UnsignedByte;
                case (0x8D9F):
                    return OpenTK.Graphics.OpenGL4.VertexAttribPointerType.Int2101010Rev;
                default:
                    Console.WriteLine("Unknown VERTEX SECTION TYPE-----------------------------------");
                    throw new ApplicationException("NEW VERTEX SECTION TYPE. FIX IT ASSHOLE...");
                    //return OpenTK.Graphics.OpenGL4.VertexAttribPointerType.UnsignedByte;
            }
        }

        private static int get_type_count(int val)
        {

            switch (val)
            {
                case (0x140B):
                    return 4;
                case (0x1401):
                    return 1;
                default:
                    Console.WriteLine("Unknown VERTEX SECTION TYPE-----------------------------------");
                    return 1;
            }
        }

        private static string getDescr(ref int[] offsets, int count)
        {
            string mesh_desc = "";


            for (int i = 0; i < count; i++)
            {
                if (offsets[i] != -1)
                {
                    switch (i)
                    {
                        case 0:
                            mesh_desc += "v"; //Verts
                            break;
                        case 1:
                            mesh_desc += "u"; //UVs
                            break;
                        case 2:
                            mesh_desc += "n"; //Normals
                            break;
                        case 3:
                            mesh_desc += "t"; //Tangents
                            break;
                        case 4:
                            mesh_desc += "p"; //Vertex Color
                            break;
                        case 5:
                            mesh_desc += "b"; //BlendIndices
                            break;
                        case 6:
                            mesh_desc += "w"; //BlendWeights
                            break;
                        default:
                            mesh_desc += "x"; //Default
                            break;
                    }
                }
            }

            return mesh_desc;
        }

        private static textureManager localTexMgr;

        private static NMSTemplate LoadNMSFile(string filepath)
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

        public static scene LoadObjects(string filepath)
        {
            TkSceneNodeData scene = (TkSceneNodeData) LoadNMSFile(filepath);
            
            Console.WriteLine("Loading Objects from MBINFile");

            string sceneName = scene.Name;
            MVCore.Common.CallBacks.Log(string.Format("Trying to load Scene {0}", sceneName));
            string[] split = sceneName.Split('\\');
            string scnName = split[split.Length - 1];
            Common.CallBacks.updateStatus("Importing Scene: " + scnName);
            Common.CallBacks.Log(string.Format("Importing Scene: {0}", scnName));
            Console.WriteLine("Importing Scene: " + scnName);

#if DEBUG
            //Save NMSTemplate to exml
            var xmlstring = EXmlFile.WriteTemplate(scene);
            File.WriteAllText("Temp\\" + scnName + ".exml", xmlstring);
#endif

            //Get Geometry File
            //Parse geometry once
            string geomfile;
            geomfile = scene.Attributes[0].Value;
            FileStream fs;
            if (!File.Exists(Path.Combine(FileUtils.dirpath, geomfile) + ".PC"))
            {
                MessageBox.Show("Could not find geometry file " + Path.Combine(FileUtils.dirpath, geomfile));
                Common.CallBacks.Log(string.Format("Could not find geometry file {0} ",
                    Path.Combine(FileUtils.dirpath, geomfile)));

                //Create Dummy Scene
                scene dummy = new scene();
                dummy.name = "DUMMY_SCENE";
                dummy.nms_template = null;
                dummy.type = TYPES.SCENE;
                dummy.shader_programs = new int[] {Common.RenderState.activeResMgr.GLShaders["LOCATOR_SHADER"],
                                              Common.RenderState.activeResMgr.GLShaders["DEBUG_SHADER"],
                                              Common.RenderState.activeResMgr.GLShaders["PICKING_SHADER"]};

                return dummy;
            }

            fs = new FileStream(Path.Combine(FileUtils.dirpath, geomfile) + ".PC", FileMode.Open);
            GeomObject gobject;
        
            if (!Common.RenderState.activeResMgr.GLgeoms.ContainsKey(geomfile))
            {
                gobject = GEOMMBIN.Parse(fs);
                Common.RenderState.activeResMgr.GLgeoms[geomfile] = gobject;
                Common.CallBacks.Log(string.Format("Geometry file {0} successfully parsed",
                    Path.Combine(FileUtils.dirpath, geomfile)));
            }
            else
            {
                //Load from dict
                gobject = Common.RenderState.activeResMgr.GLgeoms[geomfile];
            }
        
            fs.Close();

            //Random Generetor for colors
            Random randgen = new Random();

            //Parse root scene
            scene root = (scene) parseNode(scene, gobject, null, null);


            /*
            //Try to load Pose information
            //At first assemble ANIM.MBIN filepath
            string animFilePath = filepath.Replace(".SCENE.MBIN", ".ANIM.MBIN");

            if (File.Exists(animFilePath))
            {
                MVCore.Common.CallBacks.Log(string.Format("Trying to load Pose Information {0}", animFilePath));

                libMBIN.MBINFile mbinf = new libMBIN.MBINFile(animFilePath);
                mbinf.Load();

                TkAnimMetadata testAnim = (TkAnimMetadata) mbinf.GetData();
                root.poseMeta = testAnim;
                root.loadPose(10);
            }

            */
            
            return root;
        }


        private static string parseNMSTemplateAttrib<T>(List<T> temp, string attrib)
        {
            T elem = temp.FirstOrDefault(item => (string) item.GetType().GetField("Name").GetValue(item) == attrib);
            return (string) elem.GetType().GetField("Value").GetValue(elem);
        }


        private static int HasComponent(locator node, Type ComponentType)
        {
            for (int i = 0; i < node.attachment.Components.Count; i++)
            {
                NMSTemplate temp = node.attachment.Components[i];
                if (temp.GetType() == ComponentType)
                    return i;
            }

            return -1;
        }


        private static void ProcessAnimPoseComponent(locator node)
        {
            //Check if node has AnimPoseComponent
            int component_index = HasComponent(node, typeof(TkAnimPoseComponentData));

            if (component_index < 0)
                return;

            //Load PoseAnims

            TkAnimPoseComponentData pcd = (TkAnimPoseComponentData) node.attachment.Components[component_index];

            for (int i = 0; i < pcd.PoseAnims.Count; i++)
            {
                AnimPoseData my_apd = new AnimPoseData(pcd.PoseAnims[i]);
                node.poseData.Add(my_apd);
            }
        }

        private static void ProcessAnimationComponent(locator node)
        {

        }

        private static void ProcessComponents(locator node)
        {
            if (node.attachment == null)
                return;

            //Call individual Component Processors
            ProcessAnimationComponent(node);
            ProcessAnimPoseComponent(node);
        }


        private static model parseNode(TkSceneNodeData node, 
            GeomObject gobject, model parent, scene scene)
        {
            TkTransformData transform = node.Transform;
            List<TkSceneNodeAttributeData> attribs = node.Attributes;
            List<TkSceneNodeData> children = node.Children;
        
            //Load Transforms
            //Get Transformation
            var transforms = new float[] { transform.TransX,
                transform.TransY,
                transform.TransZ,
                transform.RotX,
                transform.RotY,
                transform.RotZ,
                transform.ScaleX,
                transform.ScaleY,
                transform.ScaleZ};

            //XmlElement info = (XmlElement)node.ChildNodes[0];
            //XmlElement opts = (XmlElement)node.ChildNodes[1];
            //XmlElement childs = (XmlElement)node.ChildNodes[2];

            string name = node.Name;
            string type = node.Type;
            //Fix double underscore names
            if (name.StartsWith("_"))
                name = "_" + name.TrimStart('_');

            //Notify
            Common.CallBacks.Log(string.Format("Importing Scene {0} Node {1}",scene?.name, name));
            Common.CallBacks.Log(string.Format("Transform {0} {1} {2} {3} {4} {5} {6} {7} {8}",
                transform.TransX,transform.TransY,transform.TransZ,
                transform.RotX,transform.RotY,transform.RotZ,
                transform.ScaleX,transform.ScaleY,transform.ScaleZ));
            Common.CallBacks.updateStatus("Importing Scene: " + scene?.name + " Part: " + name);
            TYPES typeEnum;
            if (!Enum.TryParse<TYPES>(node.Type, out typeEnum))
                throw new Exception("Node Type " + node.Type + "Not supported");

            if (typeEnum == TYPES.MESH)
            {
                MVCore.Common.CallBacks.Log(string.Format("Parsing Mesh {0}", name));
                //Create model
                meshModel so = new meshModel();

                so.name = name;
                so.type = typeEnum;
                so.debuggable = true;
                //Set Random Color
                so.color[0] = Common.RenderState.randgen.Next(255) / 255.0f;
                so.color[1] = Common.RenderState.randgen.Next(255) / 255.0f;
                so.color[2] = Common.RenderState.randgen.Next(255) / 255.0f;

                MVCore.Common.CallBacks.Log(string.Format("Randomized Object Color {0}, {1}, {2}", so.color[0], so.color[1], so.color[2]));
                //Get Options
                so.batchstart_physics = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "BATCHSTARTPHYSI"));
                so.vertrstart_physics = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "VERTRSTARTPHYSI"));
                so.vertrend_physics = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "VERTRENDPHYSICS"));
                so.batchstart_graphics = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "BATCHSTARTGRAPH"));
                so.batchcount = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "BATCHCOUNT"));
                so.vertrstart_graphics = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "VERTRSTARTGRAPH"));
                so.vertrend_graphics = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "VERTRENDGRAPHIC"));
                so.firstskinmat = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "FIRSTSKINMAT"));
                so.lastskinmat = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "LASTSKINMAT"));
                so.lod_level = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "LODLEVEL"));
                so.boundhullstart = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "BOUNDHULLST"));
                so.boundhullend = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "BOUNDHULLED"));

                //Get Hash
                so.hash = ulong.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "HASH"));
                MVCore.Common.CallBacks.Log(string.Format("Batch Physics Start {0} Count {1} Vertex Physics {2} - {3} Vertex Graphics {4} - {5} SkinMats {6}-{7}",
                    so.batchstart_physics, so.batchcount, so.vertrstart_physics, so.vertrend_physics, so.vertrstart_graphics, so.vertrend_graphics,
                    so.firstskinmat, so.lastskinmat));
                
                //Find id within the vbo
                int iid = -1;
                for (int i = 0; i < gobject.vstarts.Count; i++)
                    if (gobject.vstarts[i] == so.vertrstart_physics)
                    {
                        iid = i;
                        break;
                    }

                so.shader_programs = new int[] { Common.RenderState.activeResMgr.GLShaders["MESH_SHADER"],
                                                 Common.RenderState.activeResMgr.GLShaders["DEBUG_SHADER"],
                                                 Common.RenderState.activeResMgr.GLShaders["PICKING_SHADER"]};

                so.Bbox = gobject.bboxes[iid];
                //so.setupBSphere();
                so.parent = parent;
                so.nms_template = node;
                so.gobject = gobject; //Store the gobject for easier access of uniforms
                so.init(transforms); //Init object transforms

                //Get Material
                string matname = parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "MATERIAL");
                MVCore.Common.CallBacks.Log(string.Format("Trying to load Material {0}", matname));
                string[] split = matname.Split('\\');
                //string matkey = split[split.Length - 1];
                string matkey = matname; //Use the entire path
                
                //Check if material already in Resources
                if (Common.RenderState.activeResMgr.GLmaterials.ContainsKey(matkey))
                    so.Material = Common.RenderState.activeResMgr.GLmaterials[matkey];
                else
                {
                    //Parse material file
                    string mat_path = Path.GetFullPath(Path.Combine(FileUtils.dirpath, matname));
                    string mat_exmlPath = Path.GetFullPath(FileUtils.getExmlPath(mat_path));
                    //Console.WriteLine("Loading Scene " + path);

                    MVCore.Common.CallBacks.Log(string.Format("Parsing Material File {0}", mat_path));

                    //Check if path exists
                    if (File.Exists(mat_path))
                    {
                        //Material mat = MATERIALMBIN.Parse(newXml);
                        Material mat = Material.Parse(mat_path, localTexMgr);
                        //Load default form palette on init
                        //mat.palette = Model_Viewer.Palettes.paletteSel;
                        mat.name_key = matkey; //Store the material key to the resource manager
                        so.Material = mat;
                        //Store the material to the Resources
                        Common.RenderState.activeResMgr.GLmaterials[matkey] = mat;
                    } else
                    {
                        MVCore.Common.CallBacks.Log(string.Format("Warning Material Missing!!!"));
                        //Generate empty material
                        Material mat = new Material();
                        so.Material = mat;
                    }
                }

                //Generate Vao's
                so.main_Vao = gobject.getMainVao(so);
                //so.bhull_Vao = gobject.getCollisionMeshVao(so); //Missing data

                //Configure boneRemap properly
                so.BoneRemapIndicesCount = so.lastskinmat - so.firstskinmat;
                so.BoneRemapIndices = new int[so.BoneRemapIndicesCount];
                for (int i = 0; i < so.lastskinmat - so.firstskinmat; i++)
                    so.BoneRemapIndices[i] = gobject.boneRemap[so.firstskinmat + i];
               
                Console.WriteLine("Object {0}, Number of skinmatrices required: {1}", so.name, so.lastskinmat - so.firstskinmat);

                if (children.Count > 0)
                {
                    //Console.WriteLine("Children Count {0}", childs.ChildNodes.Count);
                    foreach (TkSceneNodeData child in children)
                    {
                        model part = parseNode(child, gobject, so, scene);
                        so.children.Add(part);
                    }
                }

                //Check if it is a decal object
                if (so.Material.has_flag((TkMaterialFlags.MaterialFlagEnum) TkMaterialFlags.UberFlagEnum._F51_DECAL_DIFFUSE) ||
                    so.Material.has_flag((TkMaterialFlags.MaterialFlagEnum)TkMaterialFlags.UberFlagEnum._F52_DECAL_NORMAL))
                {
                    Decal newso = new Decal(so, scene);
                    so.Dispose(); //Through away the old object
                    //Change object type
                    newso.type = TYPES.DECAL;
                    newso.shader_programs[0] = Common.RenderState.activeResMgr.GLShaders["DECAL_SHADER"];
                
                    Common.RenderState.activeResMgr.GLDecals.Add(newso);
                    return newso;
                }

                return so;
            }
            else if (typeEnum == TYPES.MODEL)
            {
                Console.WriteLine("Model Detected");

                scene so = new scene();
                so.name = name;
                so.type = typeEnum;
                //Set Shader Program
                so.shader_programs = new int[]{Common.RenderState.activeResMgr.GLShaders["LOCATOR_SHADER"],
                                               Common.RenderState.activeResMgr.GLShaders["DEBUG_SHADER"],
                                               Common.RenderState.activeResMgr.GLShaders["PICKING_SHADER"]};
                //Get Transformation
                so.parent = parent;
                so.nms_template = node;
                so.init(transforms);
                so.gobject = gobject;

                //Setup model texture manager
                so.texMgr = new textureManager();
                localTexMgr = so.texMgr; //setup local texMgr
                
                //Handle Children
                if (children.Count > 0)
                {
                    foreach (TkSceneNodeData child in children)
                    {
                        model part = parseNode(child, gobject, so, so);
                        so.children.Add(part);
                    }
                }

                //Check if root node is in the resMgr
                if (!Common.RenderState.activeResMgr.GLScenes.ContainsKey(name))
                {
                    Common.RenderState.activeResMgr.GLScenes[name] = so;
                }

                return so;
            }
            else if (typeEnum == TYPES.LOCATOR)
            {
                Console.WriteLine("Locator Detected");
                locator so = new locator(0.1f);
                //Fetch attributes
                if (node.Attributes.Count > 0)
                {
                    string attachment = parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "ATTACHMENT");
                    if (attachment != "")
                    {
                        string attachment_path = Path.GetFullPath(Path.Combine(FileUtils.dirpath, attachment));
                        so.attachment = (TkAttachmentData)LoadNMSFile(attachment_path);
                    }
                }
                
                //Set Properties
                //Testingso.Name = name + "_LOC";
                so.name = name;
                so.type = typeEnum;
                so.nms_template = node;
                //Set Shader Program
                so.shader_programs = new int[]{Common.RenderState.activeResMgr.GLShaders["LOCATOR_SHADER"],
                                               Common.RenderState.activeResMgr.GLShaders["DEBUG_SHADER"],
                                               Common.RenderState.activeResMgr.GLShaders["PICKING_SHADER"]};
                //Get Transformation
                so.parent = parent;
                so.init(transforms);

                //Handle Children
                if (children.Count > 0)
                {
                    foreach (TkSceneNodeData child in children)
                    {
                        model part = parseNode(child, gobject, so, scene);
                        so.children.Add(part);
                    }
                }

                //Process Locator Attachments
                ProcessComponents(so);

                return so;
            }
            else if (typeEnum == TYPES.JOINT)
            {
                Console.WriteLine("Joint Detected");
                Joint joint = new Joint();
                //Set properties
                joint.name = name;
                joint.type = typeEnum;
                joint.nms_template = node;
                joint.shader_programs = new int[]{ Common.RenderState.activeResMgr.GLShaders["JOINT_SHADER"],
                                                   Common.RenderState.activeResMgr.GLShaders["DEBUG_SHADER"],
                                                   Common.RenderState.activeResMgr.GLShaders["PICKING_SHADER"]};
                //Get Transformation
                joint.parent = parent;
                joint.init(transforms);
                //FORCE pose shit to match the init transform
                joint.localPosePosition = joint.localPosition;
                joint.localPoseRotation = joint.localRotation;
                joint.localPoseScale = joint.localScale;

                //Get JointIndex
                joint.jointIndex = int.Parse(node.Attributes.FirstOrDefault(item => item.Name == "JOINTINDEX").Value);
                //Set Random Color
                joint.color[0] = Common.RenderState.randgen.Next(255) / 255.0f;
                joint.color[1] = Common.RenderState.randgen.Next(255) / 255.0f;
                joint.color[2] = Common.RenderState.randgen.Next(255) / 255.0f;

                joint.main_Vao = new MVCore.Primitives.LineSegment(children.Count, new Vector3(1.0f, 0.0f, 0.0f)).getVAO();

                //Try to insert joint to scene dict
                
                if (scene != null)
                {
                    scene.jointDict[joint.name] = joint;
                }
                
                //Handle Children
                if (children.Count > 0)
                {
                    foreach (TkSceneNodeData child in children)
                    {
                        model part = parseNode(child, gobject, joint, scene);
                        joint.children.Add(part);
                    }
                }

                return joint;
            }
            else if (typeEnum == TYPES.REFERENCE)
            {
                //Another Scene file referenced
                Console.WriteLine("Reference Detected");
                Common.CallBacks.Log(string.Format("Loading Reference {0}",
                    Path.Combine(FileUtils.dirpath, node.Attributes.FirstOrDefault(item => item.Name == "SCENEGRAPH").Value)));
                
                //Getting Scene MBIN file
                string path = Path.GetFullPath(Path.Combine(FileUtils.dirpath, node.Attributes.FirstOrDefault(item => item.Name == "SCENEGRAPH").Value));
                //string exmlPath = Path.GetFullPath(Util.getFullExmlPath(path));
                //Console.WriteLine("Loading Scene " + path);
                //Parse MBIN to xml
                
                //Check if path exists
                if (File.Exists(path)) {

                    //if (!File.Exists(exmlPath))
                    //    Util.MbinToExml(path, exmlPath);

                    //Read new Scene
                    scene so = LoadObjects(path);
                    so.parent = parent;
                    //Override Name
                    so.name = name;
                    so.nms_template = node;
                    //Override transforms
                    so.init(transforms);

                    //Handle Children
                    if (children.Count > 0)
                    {
                        foreach (TkSceneNodeData child in children)
                        {
                            model part = parseNode(child, gobject, so, so);
                            so.children.Add(part);
                        }
                    }

                    //Load Objects from new xml
                    return so;
                
                } else {
                    Console.WriteLine("Reference Missing");
                    Common.CallBacks.Log(string.Format("Reference Missing"));
                    locator so = new locator(0.1f);
                    //Set Properties
                    so.name = name + "_UNKNOWN";
                    so.type = TYPES.UNKNOWN;
                    //Set Shader Program
                    so.shader_programs = new int[] { Common.RenderState.activeResMgr.GLShaders["LOCATOR_SHADER"],
                                                     Common.RenderState.activeResMgr.GLShaders["DEBUG_SHADER"],
                                                     Common.RenderState.activeResMgr.GLShaders["PICKING_SHADER"]};

                    //Locator Objects don't have options

                    //take care of children
                    return so;
                }
            
            }
            else if (typeEnum == TYPES.COLLISION)
            {
                //Create model
                Collision so = new Collision();

                //Remove that after implemented all the different collision types
                so.shader_programs = new int[] { Common.RenderState.activeResMgr.GLShaders["MESH_SHADER"],
                                                  Common.RenderState.activeResMgr.GLShaders["DEBUG_SHADER"],
                                                  Common.RenderState.activeResMgr.GLShaders["PICKING_SHADER"]}; //Use Mesh program for collisions
                so.debuggable = true;
                so.name = name + "_COLLISION";
                so.type = typeEnum;
                so.nms_template = node;

                //Get Options
                //In collision objects first child is probably the type
                //string collisionType = ((XmlElement)attribs.ChildNodes[0].SelectSingleNode("Property[@name='Value']")).GetAttribute("value").ToUpper();
                string collisionType = node.Attributes.FirstOrDefault(item => item.Name == "TYPE").Value.ToUpper();

                Console.WriteLine("Collision Detected " + name + "TYPE: " + collisionType);
                Common.CallBacks.Log(string.Format("Collision Detected {0} {1}", name, collisionType));

                //Get Material for all types
                string matkey = name; //I will index the collision materials by their name, it shouldn't hurt anywhere
                                      // + cleaning up will be up to the resource manager

                //Check if material already in Resources
                if (Common.RenderState.activeResMgr.GLmaterials.ContainsKey(matkey))
                    so.Material = Common.RenderState.activeResMgr.GLmaterials[matkey];
                else
                {
                    Material mat = new Material();
                    mat.name_key = matkey;
                    so.Material = mat;

                    //Store the material to the Resources
                    Common.RenderState.activeResMgr.GLmaterials[matkey] = mat;
                }

                if (collisionType == "MESH")
                {
                    so.collisionType = (int) COLLISIONTYPES.MESH;
                    so.batchstart_physics = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "BATCHSTART"));
                    so.batchcount = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "BATCHCOUNT"));
                    so.vertrstart_physics = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "VERTRSTART"));
                    so.vertrend_physics = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "VERTREND"));
                    so.firstskinmat = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "FIRSTSKINMAT"));
                    so.lastskinmat = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "LASTSKINMAT"));
                    so.boundhullstart = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "BOUNDHULLST"));
                    so.boundhullend = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "BOUNDHULLED"));
                    so.gobject = gobject;

                    //Find id within the vbo
                    int iid = -1;
                    for (int i = 0; i < gobject.vstarts.Count; i++)
                        if (gobject.vstarts[i] == so.vertrstart_physics)
                        {
                            iid = i;
                            break;
                        }

                    //Configure boneRemap properly
                    so.BoneRemapIndicesCount = so.lastskinmat - so.firstskinmat;
                    so.BoneRemapIndices = new int[so.BoneRemapIndicesCount];
                    for (int i = 0; i < so.lastskinmat - so.firstskinmat; i++)
                        so.BoneRemapIndices[i] = gobject.boneRemap[so.firstskinmat + i];

                    if (so.BoneRemapIndicesCount > 0)
                    {
                        throw new Exception("SKINNED COLLISION. CHECK YOUR SHIT!");
                    }

                    //Set vao
                    try
                    {
                        so.main_Vao = gobject.getCollisionMeshVao(so);
                        //Use indiceslength from the gobject
                        so.indicesLength = so.gobject.indicesLengthType;
                    } catch (System.Collections.Generic.KeyNotFoundException e)
                    {
                        Common.CallBacks.Log("Missing Collision Mesh " + so.name);
                        so.main_Vao = null;
                    }

                }
                else if (collisionType == "CYLINDER")
                {
                    //Console.WriteLine("CYLINDER NODE PARSING NOT IMPLEMENTED");
                    //Set cvbo

                    float radius = float.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "RADIUS"),
                        CultureInfo.InvariantCulture);
                    float height = float.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "HEIGHT"),
                        CultureInfo.InvariantCulture);
                    Common.CallBacks.Log(string.Format("Cylinder Collision r:{0} h:{1}", radius, height));
                    so.main_Vao = (new MVCore.Primitives.Cylinder(radius,height)).getVAO();
                    so.collisionType = COLLISIONTYPES.CYLINDER;
                    so.batchstart_graphics = 0;
                    so.batchcount = 120;
                    so.vertrstart_graphics = 0;
                    so.vertrend_graphics = 22 - 1;
                }
                else if (collisionType == "BOX")
                {
                    //Console.WriteLine("BOX NODE PARSING NOT IMPLEMENTED");
                    //Set cvbo
                    float width = float.Parse(node.Attributes.FirstOrDefault(item => item.Name == "WIDTH").Value,
                        CultureInfo.InvariantCulture);
                    float height = float.Parse(node.Attributes.FirstOrDefault(item => item.Name == "HEIGHT").Value,
                        CultureInfo.InvariantCulture);
                    float depth = float.Parse(node.Attributes.FirstOrDefault(item => item.Name == "DEPTH").Value,
                        CultureInfo.InvariantCulture);

                    Common.CallBacks.Log(string.Format("Sphere Collision w:{0} h:{0} d:{0}", width, height, depth));
                    so.main_Vao = (new MVCore.Primitives.Box(width, height, depth)).getVAO();
                    so.collisionType = COLLISIONTYPES.BOX;
                    //Set general vao properties
                    so.batchstart_graphics = 0;
                    so.batchcount = 36;
                    so.vertrstart_graphics = 0;
                    so.vertrend_graphics = 8-1;

                }
                else if (collisionType == "CAPSULE")
                {
                    //Set cvbo
                    float radius = float.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "RADIUS"),
                        CultureInfo.InvariantCulture);
                    float height = float.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "HEIGHT"),
                        CultureInfo.InvariantCulture);

                    Common.CallBacks.Log(string.Format("Capsule Collision r:{0} h:{1}", radius, height));
                    so.main_Vao = (new MVCore.Primitives.Capsule(new Vector3(), height, radius)).getVAO();
                    so.collisionType = COLLISIONTYPES.CAPSULE;
                    so.batchstart_graphics = 0;
                    so.batchcount = 726;
                    so.vertrstart_graphics = 0;
                    so.vertrend_graphics = 144 - 1;
                }
                else if (collisionType == "SPHERE")
                {
                    //Set cvbo
                    float radius = float.Parse(node.Attributes.FirstOrDefault(item => item.Name == "RADIUS").Value,
                        CultureInfo.InvariantCulture);
                    Common.CallBacks.Log(string.Format("Sphere Collision r:{0}", radius));
                    so.main_Vao = (new MVCore.Primitives.Sphere(new Vector3(), radius)).getVAO();
                    so.collisionType = COLLISIONTYPES.SPHERE;
                    so.batchstart_graphics = 0;
                    so.batchcount = 600;
                    so.vertrstart_graphics = 0;
                    so.vertrend_graphics = 121 - 1;
                }
                else
                {
                    Console.WriteLine("NEW COLLISION TYPE: " + collisionType);
                    Common.CallBacks.Log("NEW COLLISION TYPE: " + collisionType);
                }


                Console.WriteLine("Batch Start {0} Count {1} ", 
                    so.batchstart_physics, so.batchcount);

                so.parent = parent;
                so.init(transforms);

                //Collision probably has no children biut I'm leaving that code here
                if (children.Count > 0)
                    foreach (TkSceneNodeData child in children)
                        so.children.Add(parseNode(child, gobject, so, scene));

                return so;

            }
            else
            {
                Common.CallBacks.Log(string.Format("Unknown Type, {0}", type));
                locator so = new locator(0.1f);
                //Set Properties
                so.name = name + "_UNKNOWN";
                so.type = TYPES.UNKNOWN;
                so.nms_template = node;
                //Set Shader Program
                so.shader_programs = new int[] { Common.RenderState.activeResMgr.GLShaders["LOCATOR_SHADER"],
                                                 Common.RenderState.activeResMgr.GLShaders["DEBUG_SHADER"],
                                                 Common.RenderState.activeResMgr.GLShaders["PICKING_SHADER"]};

                //Locator Objects don't have options

                //take care of children
                return so;
                //throw new ApplicationException("Unknown mesh type");
            }

        
        }
 

    }

}
