using System.IO;
using System.Diagnostics;
using System.Xml;
using System.Collections.Generic;
using System;
using System.Reflection;
using System.Globalization;
using OpenTK.Mathematics;
using Model_Viewer;
using libMBIN;
using libMBIN.NMS.Toolkit;
using System.Linq;
using MVCore;
using MVCore.GMDL;
using Console = System.Console;
using WPFModelViewer;
using MVCore.Utils;
using libMBIN.NMS.GameComponents;
using libMBIN.NMS;
using System.CodeDom;
using MVCore.Common;
using System.Xml.Linq;


namespace MVCore
{
    public enum TYPES
    {
        MODEL=0x0,
        LOCATOR,
        GROUP,
        JOINT,
        MESH,
        LIGHT,
        EMITTER,
        COLLISION,
        REFERENCE,
        DECAL,
        GIZMO,
        GIZMOPART,
        TEXT,
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

    public class geomMeshMetaData
    {
        public string name;
        public ulong hash;
        public uint vs_size;
        public uint vs_abs_offset;
        public uint is_size;
        public uint is_abs_offset;
        public uint vp_size;
        public uint vp_abs_offset;
        public bool double_buffering;
    }

    public class geomMeshData
    {
        public ulong hash;
        public byte[] vs_buffer;
        public byte[] vp_buffer;
        public byte[] is_buffer;
    }

    public static class GEOMMBIN {



        public static GeomObject Parse_v4(ref Stream fs, ref Stream gfs)
        {
#if DEBUG
            FileStream testfs = new FileStream("test.geom", FileMode.Create);
            byte[] fs_data = new byte[fs.Length];
            fs.Read(fs_data, 0, (int) fs.Length);
            testfs.Write(fs_data, 0, (int) fs.Length);
            testfs.Close();
            //Test libmbin
#endif
            BinaryReader br = new BinaryReader(fs);
            Common.CallBacks.Log("Parsing Geometry MBIN");

            fs.Seek(0x60, SeekOrigin.Begin);

            var vert_num = br.ReadInt32();
            var indices_num = br.ReadInt32();
            var indices_flag = br.ReadInt32();
            var collision_index_count = br.ReadInt32();

            Common.CallBacks.Log("Model Vertices: {0}", vert_num);
            Common.CallBacks.Log("Model Indices: {0}", indices_num);
            Common.CallBacks.Log("Indices Flag: {0}", indices_flag);
            Common.CallBacks.Log("Collision Index Count: {0}", collision_index_count);

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
            Common.CallBacks.Log("Buffer Count: {0} VxType {1}", lod_count, vx_type);
            fs.Seek(0x8, SeekOrigin.Current);
            var mesh_descr_offset = fs.Position + br.ReadInt64();
            var buf_count = br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current); //Skipping a 1

            //Parse Small Vertex Layout Info
            var small_bufcount = br.ReadInt32();
            var small_vx_type = br.ReadInt32();
            Common.CallBacks.Log("Small Buffer Count: {0} VxType {1}", small_bufcount, small_vx_type);
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
            geom.vp_size = small_vx_type;

            //Get Bone Remapping Information
            //I'm 99% sure that boneRemap is not a case in NEXT models
            //it is still there though...
            fs.Seek(skinmatoffset, SeekOrigin.Begin);
            geom.boneRemap = new short[bc];
            for (int i = 0; i < bc; i++)
                geom.boneRemap[i] = (short) br.ReadInt32();

            //Store Joint Data
            fs.Seek(jointbindingOffset, SeekOrigin.Begin);
            geom.jointCount = jointCount;
            geom.invBMats = new float[jointCount * 16];
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
                geomMeshMetaData mmd = new geomMeshMetaData();
                mmd.name = StringUtils.read_string(br, 0x80);
                mmd.hash = br.ReadUInt64();
                mmd.vs_size = br.ReadUInt32();
                mmd.vs_abs_offset = br.ReadUInt32();
                mmd.is_size = br.ReadUInt32();
                mmd.is_abs_offset = br.ReadUInt32();
                mmd.double_buffering = br.ReadBoolean();
                br.BaseStream.Seek(7, SeekOrigin.Current);
                if (!geom.meshMetaDataDict.ContainsKey(mmd.hash))
                    geom.meshMetaDataDict[mmd.hash] = mmd;
                Common.CallBacks.Log(mmd.name);
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
                //var buf_test1 = br.ReadInt32();
                //var buf_test2 = br.ReadInt32();
                //var buf_test3 = br.ReadInt32();
                //var buf_test4 = br.ReadInt32();
                
                geom.bufInfo[buf_id]= get_bufInfo_item(buf_id, buf_localoffset, 0, buf_elem_count, buf_type);
                mesh_offsets[buf_id] = buf_localoffset;
                fs.Seek(0x10, SeekOrigin.Current);
            }

            //Get Descr
            mesh_desc = getDescr(ref mesh_offsets, buf_count);
            Common.CallBacks.Log("Mesh Description: " + mesh_desc);

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
            Common.CallBacks.Log("Small Mesh Description: " + small_mesh_desc);

            //Store description
            geom.small_mesh_descr = small_mesh_desc;
            geom.small_offsets = small_mesh_offsets;
            //Set geom interleaved
            geom.interleaved = true;


            //Load streams from the geometry stream file
            
            foreach (KeyValuePair<ulong, geomMeshMetaData> pair in geom.meshMetaDataDict)
            {
                geomMeshMetaData mmd = pair.Value;
                geomMeshData md = new geomMeshData();
                md.vs_buffer = new byte[mmd.vs_size];
                md.is_buffer = new byte[mmd.is_size];

                //Fetch Buffers
                gfs.Seek((int) mmd.vs_abs_offset, SeekOrigin.Begin);
                gfs.Read(md.vs_buffer, 0, (int) mmd.vs_size);

                gfs.Seek((int) mmd.is_abs_offset, SeekOrigin.Begin);
                gfs.Read(md.is_buffer, 0, (int) mmd.is_size);
            
                geom.meshDataDict[mmd.hash] = md;
            }

            return geom;

        }

        public static GeomObject Parse(ref Stream fs, ref Stream gfs)
        {
#if DEBUG
            FileStream testfs = new FileStream("test.geom", FileMode.Create);
            byte[] fs_data = new byte[fs.Length];
            fs.Read(fs_data, 0, (int)fs.Length);
            testfs.Write(fs_data, 0, (int)fs.Length);
            testfs.Close();
            //Test libmbin
#endif
            BinaryReader br = new BinaryReader(fs);
            CallBacks.Log("Parsing Geometry MBIN");

            //Parse main vertex layout Info
            fs.Seek(NMSUtils.GetFieldOffset("TkGeometryData", "PositionVertexLayout"), SeekOrigin.Begin);
            var mesh_pvx_descr_offset = fs.Position + br.ReadInt64();
            var pvx_buf_count = br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current); //Skipping a 1
            fs.Seek(0x8, SeekOrigin.Current); //Skip platform data
            var pvx_lod_count = br.ReadInt32(); //TODO: Not sure about that
            var pvx_type = br.ReadInt32();
            Common.CallBacks.Log(string.Format("Position Vertex Buffer Count: {0} VxType {1}", pvx_lod_count, pvx_type));

            //Parse main vertex layout Info
            fs.Seek(NMSUtils.GetFieldOffset("TkGeometryData", "VertexLayout"), SeekOrigin.Begin);
            var mesh_descr_offset = fs.Position + br.ReadInt64();
            var buf_count = br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current); //Skipping a 1
            fs.Seek(0x8, SeekOrigin.Current); //Skip platform data
            var lod_count = br.ReadInt32(); //TODO: Not sure about that
            var vx_type = br.ReadInt32();
            Common.CallBacks.Log(string.Format("Buffer Count: {0} VxType {1}", lod_count, vx_type));

            
            //Bound Hull Vert end
            var boundhull_vertend_offset = fs.Position + br.ReadInt32();
            fs.Seek(0xC, SeekOrigin.Current);

            //Bound Hull Verts
            var bhulloffset = fs.Position + br.ReadInt64();
            var bhull_count = br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);

            //Bound Hull Vert start
            var boundhull_vertstart_offset = fs.Position + br.ReadInt32();
            fs.Seek(0xC, SeekOrigin.Current);

            //Indices
            var indices_offset = fs.Position + br.ReadInt64();
            fs.Seek(0x8, SeekOrigin.Current); //Skip Section Sizes and a 1

            //Joint Bindings
            var jointbindingOffset = fs.Position + br.ReadInt64();
            var jointCount = br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);
            //Joint Extensions
            //Joint Mirror Pairs
            fs.Seek(3 * 0x10, SeekOrigin.Current);

            //BoundBoxes
            var bboxmaxoffset = fs.Position + br.ReadInt64();
            fs.Seek(0x8, SeekOrigin.Current);
            var bboxminoffset = fs.Position + br.ReadInt64();
            fs.Seek(0x8, SeekOrigin.Current);

            //Usefull Bone Remapping information
            var meshbaseskinmat_offset = fs.Position + br.ReadInt64();
            var mc = br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);

            //VertEnds
            fs.Seek(0x10, SeekOrigin.Current);

            //Vertstarts
            var vsoffset = fs.Position + br.ReadInt64();
            var partcount = br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);

            //ProcGenNodeNames
            var pg_node_name_offset = fs.Position + br.ReadInt64();
            var pg_node_name_count = br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);

            //ProcGenParentIds
            var pg_node_parentId_offset = fs.Position + br.ReadInt64();
            var pg_node_id_count = br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);


            //SkinMatrixLayout
            var skinmat_offset = fs.Position + br.ReadInt64();
            var skinmat_count = br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);

            var meshMetaData_offset = fs.Position + br.ReadInt64();
            var meshMetaData_counter = br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current); //Skip Section Sizes and a 1

            var collision_index_count = br.ReadInt32();
            var indices_num = br.ReadInt32();
            var indices_flag = br.ReadInt32();
            var vert_num = br.ReadInt32();
            
            //-------OLD------//

            Common.CallBacks.Log("Model Vertices: {0}", vert_num);
            Common.CallBacks.Log("Model Indices: {0}", indices_num);
            Common.CallBacks.Log("Indices Flag: {0}", indices_flag);
            Common.CallBacks.Log("Collision Index Count: {0}", collision_index_count);

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
            geom.vp_size = pvx_type;
            
            //Get Bone Remapping Information
            //I'm 99% sure that boneRemap is not a case in NEXT models
            //it is still there though...
            fs.Seek(skinmat_offset, SeekOrigin.Begin);
            geom.boneRemap = new short[skinmat_count];
            for (int i = 0; i < skinmat_count; i++)
                geom.boneRemap[i] = (short)br.ReadInt32();

            //Mesh Base Skin Mats
            fs.Seek(meshbaseskinmat_offset, SeekOrigin.Begin);
            geom.meshBaseSkinMat = new short[mc];
            for (int i = 0; i < mc; i++)
                geom.meshBaseSkinMat[i] = (short)br.ReadInt32();

            //Store Joint Data
            fs.Seek(jointbindingOffset, SeekOrigin.Begin);
            geom.jointCount = jointCount;
            geom.invBMats = new float[jointCount * 16];
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
            for (int i = 0; i < partcount; i++)
            {
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

            //Get ProcGen Names
            for (int i = 0; i < pg_node_name_count; i++)
            {
                fs.Seek(pg_node_name_offset + 0x10 * i, SeekOrigin.Begin);
                var name_offset = fs.Position + br.ReadInt64();
                var name_length = br.ReadInt32();
                fs.Seek(0x4, SeekOrigin.Current);

                //Fetch name
                fs.Seek(name_offset, SeekOrigin.Begin);
                string name = StringUtils.read_string(br, 0x80);
                geom.procGenNames.Add(name);
                CallBacks.Log("ProcGenName", i, name);
            }

            //Get ProcGen IDs
            fs.Seek(pg_node_parentId_offset, SeekOrigin.Begin);

            for (int i = 0; i < pg_node_id_count; i++)
            {
                int id = br.ReadInt32();
                geom.procGenIDs.Add(id);
                CallBacks.Log("ProcGenParentID", geom.procGenNames[i], id >= 0 ? geom.procGenNames[id] : -1);
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
            for (int i = 0; i < meshMetaData_counter; i++)
            {
                fs.Seek(meshMetaData_offset + 0x38 * i, SeekOrigin.Begin);

                geomMeshMetaData mmd = new geomMeshMetaData();
                var name_offset = fs.Position + br.ReadInt64();
                fs.Seek(0x8, SeekOrigin.Current);
                mmd.hash = br.ReadUInt64();
                mmd.is_abs_offset = br.ReadUInt32();
                mmd.is_size = br.ReadUInt32();
                mmd.vs_abs_offset = br.ReadUInt32();
                mmd.vs_size = br.ReadUInt32();
                mmd.vp_abs_offset = br.ReadUInt32();
                mmd.vp_size = br.ReadUInt32();
                mmd.double_buffering = br.ReadBoolean();

                //Get Name
                fs.Seek(name_offset, SeekOrigin.Begin);
                mmd.name = StringUtils.read_string(br, 0x80);

                if (!geom.meshMetaDataDict.ContainsKey(mmd.hash))
                    geom.meshMetaDataDict[mmd.hash] = mmd;
                Common.CallBacks.Log(mmd.name);
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
                //var buf_platform_data = br.ReadInt64();
                var buf_instancing_type = br.ReadInt32(); //Per Model or per vertex not sure how to use that
                var buf_type = br.ReadInt32();
                var buf_normalize = br.ReadByte();
                var buf_localoffset = br.ReadByte();
                var buf_id = br.ReadByte();
                var buf_elem_count = br.ReadByte();
                
                geom.bufInfo[buf_id] = get_bufInfo_item(buf_id, buf_localoffset, buf_normalize, buf_elem_count, buf_type);
                mesh_offsets[buf_id] = buf_localoffset;
            }

            //Get position vertex buffer information
            fs.Seek(mesh_pvx_descr_offset, SeekOrigin.Begin);
            //int[] mesh_offsets = new int[buf_count];
            for (int i = 0; i < pvx_buf_count; i++)
            {
                //var buf_platform_data = br.ReadInt64();
                var buf_instancing_type = br.ReadInt32(); //Per Model or per vertex not sure how to use that
                var buf_type = br.ReadInt32();
                var buf_normalize = br.ReadByte();
                var buf_localoffset = br.ReadByte();
                var buf_id = br.ReadByte();
                var buf_elem_count = br.ReadByte();

                geom.bufInfo[buf_id] = get_bufInfo_item(buf_id, buf_localoffset, buf_normalize, buf_elem_count, buf_type);
                mesh_offsets[buf_id] = buf_localoffset;
            }

            //Get Descr
            mesh_desc = getDescr(ref mesh_offsets, buf_count);
            Common.CallBacks.Log("Mesh Description: " + mesh_desc);

            //Store description
            geom.mesh_descr = mesh_desc;
            geom.offsets = mesh_offsets;

            /* No point reading the small vertex layout. No use for that in the viewer
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
            Common.CallBacks.Log("Small Mesh Description: " + small_mesh_desc);

            //Store description
            geom.small_mesh_descr = small_mesh_desc;
            geom.small_offsets = small_mesh_offsets;

            */
            //Set geom interleaved
            geom.interleaved = true;


            //Load streams from the geometry stream file

            foreach (KeyValuePair<ulong, geomMeshMetaData> pair in geom.meshMetaDataDict)
            {
                geomMeshMetaData mmd = pair.Value;
                geomMeshData md = new geomMeshData();
                md.vs_buffer = new byte[mmd.vs_size];
                md.vp_buffer = new byte[mmd.vp_size];
                md.is_buffer = new byte[mmd.is_size];

                //Fetch Buffers
                gfs.Seek((int)mmd.vs_abs_offset, SeekOrigin.Begin);
                gfs.Read(md.vs_buffer, 0, (int) mmd.vs_size);
 
                gfs.Seek((int)mmd.vp_abs_offset, SeekOrigin.Begin);
                gfs.Read(md.vp_buffer, 0, (int) mmd.vp_size);

                gfs.Seek((int)mmd.is_abs_offset, SeekOrigin.Begin);
                gfs.Read(md.is_buffer, 0, (int) mmd.is_size);

                geom.meshDataDict[mmd.hash] = md;
            }

            return geom;

        }


        private static bufInfo get_bufInfo_item(int buf_id, int offset, int normalize, int count, int buf_type)
        {
            int sem = buf_id;
            int off = offset;
            OpenTK.Graphics.OpenGL4.VertexAttribPointerType typ = get_type(buf_type);
            string text = get_shader_sem(buf_id);
            return new bufInfo(sem, typ, count, 0, off, text, normalize > 0);
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
                    Common.CallBacks.Log("Unknown VERTEX SECTION TYPE-----------------------------------");
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
                    Common.CallBacks.Log("Unknown VERTEX SECTION TYPE-----------------------------------");
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
        private static List<Model> localAnimScenes = new List<Model>();
        private static string activeFilePath = "";

        private static Dictionary<Type, int> SupportedComponents = new Dictionary<Type, int>
        {
            {typeof(TkAnimPoseComponentData), 0},
            {typeof(TkAnimationComponentData), 1},
            {typeof(TkLODComponentData), 2},
            {typeof(TkPhysicsComponentData), 3},
            {typeof(GcTriggerActionComponentData), 4},
            {typeof(EmptyNode), 5}
        };

        private static GeomObject ParseGeometryFile(string path)
        {
            if (path == "")
                return null;

            GeomObject gobject;

            if (Common.RenderState.activeResMgr.GLgeoms.ContainsKey(path))
            {
                //Load from dict
                gobject = Common.RenderState.activeResMgr.GLgeoms[path];
            
            }
            else
            {

#if DEBUG
                //Use libMBIN to decompile the file
                TkGeometryData geomdata = (TkGeometryData)NMSUtils.LoadNMSTemplate(path + ".PC", ref Common.RenderState.activeResMgr);
                //Save NMSTemplate to exml
                string xmlstring = MXmlFile.WriteTemplate(geomdata);
                File.WriteAllText("Temp\\temp_geom.exml", xmlstring);
#endif
                //Load Gstream and Create gobject

                Stream fs, gfs;

                fs = NMSUtils.LoadNMSFileStream(path + ".PC", ref Common.RenderState.activeResMgr);

                //Try to fetch the geometry.data.mbin file in order to fetch the geometry streams
                string gstreamfile = "";
                string[] split = path.Split('.');
                for (int i = 0; i < split.Length - 1; i++)
                    gstreamfile += split[i] + ".";
                gstreamfile += "DATA.MBIN.PC";

                gfs = NMSUtils.LoadNMSFileStream(gstreamfile, ref Common.RenderState.activeResMgr);


                gobject = Parse(ref fs, ref gfs);
                Common.RenderState.activeResMgr.GLgeoms[path] = gobject;
                Common.CallBacks.Log(string.Format("Geometry file {0} successfully parsed",
                    path + ".PC"));

                fs.Close();
                gfs.Close();
            }

            return gobject;

        }


        public static Scene LoadObjects(string path)
        {   
            TkSceneNodeData template = (TkSceneNodeData) NMSUtils.LoadNMSTemplate(path, ref Common.RenderState.activeResMgr);
            activeFilePath = path;
            Common.CallBacks.Log("Loading Objects from MBINFile");

            string sceneName = template.Name;
            Common.CallBacks.Log(string.Format("Trying to load Scene {0}", sceneName));
            string[] split = sceneName.Split('\\');
            string scnName = split[split.Length - 1];
            Common.CallBacks.updateStatus("Importing Scene: " + scnName);
            Common.CallBacks.Log(string.Format("Importing Scene: {0}", scnName));
            
            //Get Geometry File
            //Parse geometry once
            string geomfile = parseNMSTemplateAttrib<TkSceneNodeAttributeData>(template.Attributes, "GEOMETRY");
            int num_lods = 0; 
            int.TryParse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(template.Attributes, "NUMLODS"), out num_lods);
            
            GeomObject gobject = ParseGeometryFile(geomfile);
            
            //Random Generetor for colors
            Random randgen = new Random();

            //Parse root scene
            Scene root = (Scene) parseNode(template, gobject, null, null);

            //Save scene path to resourcemanager
            Common.RenderState.activeResMgr.GLScenes[path] = root; //Use input path
            
            return root;
        }


        private static string parseNMSTemplateAttrib<T>(List<T> temp, string attrib)
        {
            T elem = temp.FirstOrDefault(item => ((NMSString0x10) item.GetType().GetField("Name").GetValue(item)).Value == attrib);
            if (elem == null)
                return "";
            else
                return ((VariableSizeString) elem.GetType().GetField("Value").GetValue(elem)).Value;
        }


        private static int hasComponent(TkAttachmentData node, Type ComponentType)
        {
            for (int i = 0; i < node.Components.Count; i++)
            {
                NMSTemplate temp = node.Components[i];
                if (temp.GetType() == ComponentType)
                    return i;
            }

            return -1;
        }

        private static void ProcessAnimPoseComponent(Model node, TkAnimPoseComponentData component)
        {
            //Load PoseFile
            AnimPoseComponent apc = new AnimPoseComponent(component);
            apc.ref_object = node; //Set referenced animScene
            node.animPoseComponentID = node.Components.Count;
            node.Components.Add(apc);
        }

        private static void ProcessAnimationComponent(Model node, TkAnimationComponentData component, string default_anim_file)
        {
            AnimComponent ac = new AnimComponent(component, default_anim_file);
            node.animComponentID = node.Components.Count;
            node.Components.Add(ac); //Create Animation Component and add attach it to the component
        }

        private static void ProcessPhysicsComponent(Model node, TkPhysicsComponentData component)
        {
            PhysicsComponent pc = new PhysicsComponent(component);
            node.Components.Add(pc);
        }

        private static void ProcessTriggerActionComponent(Model node, GcTriggerActionComponentData component)
        {
            TriggerActionComponent tac = new TriggerActionComponent(component);
            node.Components.Add(tac);
        }

        private static void ProcessLODComponent(Model node, TkLODComponentData component)
        {
            //Load all LOD models as children to the node
            LODModelComponent lodmdlcomp = new LODModelComponent();
            
            for (int i = 0; i < component.LODModels.Count; i++)
            {
                string filepath = component.LODModels[i].LODModel.Filename;
                Common.CallBacks.Log("Loading LOD " + filepath);
                Scene so = LoadObjects(filepath);
                node.AddChild(so);
                //Create LOD Resource
                LODModelResource lodres = new LODModelResource(component.LODModels[i]);
                lodmdlcomp.Resources.Add(lodres);
            }
            
            node.Components.Add(lodmdlcomp);
        }

        private static void ProcessComponents(Model node, TkAttachmentData attachment, string scene_name)
        {
            if (attachment == null)
                return;

            for (int i = 0; i < attachment.Components.Count; i++)
            {
                LinkableNMSTemplate comp = attachment.Components[i];
                if (comp.Template == null)
                {
                    CallBacks.Log("Null Template detected");
                    continue;
                }
                    
                Type comp_type = comp.Template.GetType();
                
                if (!SupportedComponents.ContainsKey(comp_type))
                {
                    Common.CallBacks.Log("Unsupported Component Type " + comp_type);
                    continue;
                }
                    
                switch (SupportedComponents[comp_type])
                {
                    case 0:
                        ProcessAnimPoseComponent(node, comp.Template as TkAnimPoseComponentData);
                        break;
                    case 1:
                        ProcessAnimationComponent(node, comp.Template as TkAnimationComponentData, scene_name);
                        break;
                    case 2:
                        ProcessLODComponent(node, comp.Template as TkLODComponentData);
                        break;
                    case 3:
                        ProcessPhysicsComponent(node, comp.Template as TkPhysicsComponentData);
                        break;
                    case 4:
                        ProcessTriggerActionComponent(node, comp.Template as GcTriggerActionComponentData);
                        break;
                    case 5: //Empty Node do nothing
                        break;
                }   
            
            }


            //Setup LOD distances
            for (int i = 0; i < 5; i++)
            {
                if (i < node._LODDistances.Length)
                    node._LODDistances[i] = attachment.LodDistances[i];
                else
                    node._LODDistances[i] = 100000.0f;
            }
        }


        private static void findAnimScenes(Model node)
        {
            if (node.type == TYPES.MODEL)
            {
                Scene s = (Scene)node;

                if (s.jointDict.Values.Count > 0)
                    localAnimScenes.Add(node);
            }
                

            foreach (Model child in node.Children)
                findAnimScenes(child);
        }


        private static Material parseMaterial(string matname)
        {
            Material mat;

            Common.CallBacks.Log(string.Format("Trying to load Material {0}", matname));
            string matkey = matname; //Use the entire path


            //Material mat = MATERIALMBIN.Parse(newXml);
            mat = Material.Parse(matname, localTexMgr);
            
            //File probably not found not even in the PAKS, 
            if (mat == null)
            {
                Common.CallBacks.Log(string.Format("Warning Material Missing!!!"));
                //Generate empty material
                mat = new Material();
            }
            
            //Load default form palette on init
            //mat.palette = Model_Viewer.Palettes.paletteSel;
            mat.name_key = matkey; //Store the material key to the resource manager
                                   //Store the material to the Resources

            return mat;
        }


        private static Mesh parseMesh(TkSceneNodeData node,
            GeomObject gobject, Model parent, Scene scene)
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

            ulong nameHash = node.NameHash;
            string type = node.Type;

            //Fix double underscore names
            //TODO: CHeck if we need this again (probably affects the procedural descriptors)
            //if (name.StartsWith("_"))
            //    name = "_" + name.TrimStart('_');

            //Create model
            Mesh so = new Mesh();
            
            so.name = node.Name;
            so.nameHash = nameHash;
            so.debuggable = true;
            so.parentScene = scene;

            //Set Random Color
            so.color[0] = Common.RenderState.randgen.Next(255) / 255.0f;
            so.color[1] = Common.RenderState.randgen.Next(255) / 255.0f;
            so.color[2] = Common.RenderState.randgen.Next(255) / 255.0f;


            so.metaData = new MeshMetaData();
            so.metaData.AABBMIN = new Vector3();
            so.metaData.AABBMAX = new Vector3();

            CallBacks.Log(string.Format("Randomized Object Color {0}, {1}, {2}", so.color[0], so.color[1], so.color[2]));
            //Get Options
            so.metaData.batchstart_physics = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "BATCHSTARTPHYSI"));
            so.metaData.vertrstart_physics = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "VERTRSTARTPHYSI"));
            so.metaData.vertrend_physics = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "VERTRENDPHYSICS"));
            so.metaData.batchstart_graphics = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "BATCHSTARTGRAPH"));
            so.metaData.batchcount = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "BATCHCOUNT"));
            so.metaData.vertrstart_graphics = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "VERTRSTARTGRAPH"));
            so.metaData.vertrend_graphics = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "VERTRENDGRAPHIC"));
            so.metaData.firstskinmat = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "FIRSTSKINMAT"));
            so.metaData.lastskinmat = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "LASTSKINMAT"));
            so.metaData.LODLevel = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "LODLEVEL"));
            so.metaData.boundhullstart = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "BOUNDHULLST"));
            so.metaData.boundhullend = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "BOUNDHULLED"));
            so.metaData.AABBMIN.X = MathUtils.FloatParse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "AABBMINX"));
            so.metaData.AABBMIN.Y = MathUtils.FloatParse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "AABBMINY"));
            so.metaData.AABBMIN.Z = MathUtils.FloatParse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "AABBMINZ"));
            so.metaData.AABBMAX.X = MathUtils.FloatParse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "AABBMAXX"));
            so.metaData.AABBMAX.Y = MathUtils.FloatParse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "AABBMAXY"));
            so.metaData.AABBMAX.Z = MathUtils.FloatParse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "AABBMAXZ"));
            so.metaData.Hash = ulong.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "HASH"));

            Common.CallBacks.Log(string.Format("Batch Physics Start {0} Count {1} Vertex Physics {2} - {3} Vertex Graphics {4} - {5} SkinMats {6}-{7}",
                so.metaData.batchstart_physics, so.metaData.batchcount, so.metaData.vertrstart_physics,
                so.metaData.vertrend_physics, so.metaData.vertrstart_graphics, so.metaData.vertrend_graphics,
                so.metaData.firstskinmat, so.metaData.lastskinmat));

            //For now fetch only one attachment
            string attachment = parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "ATTACHMENT");
            TkAttachmentData attachment_data = null;
            if (attachment != "")
            {
                attachment_data = NMSUtils.LoadNMSTemplate(attachment, ref Common.RenderState.activeResMgr) as TkAttachmentData;
            }

            //so.Bbox = gobject.bboxes[iid]; //Use scene parameters
            //so.setupBSphere();
            so.parent = parent;
            so.gobject = gobject; //Store the gobject for easier access of uniforms
            so.init(transforms); //Init object transforms

            //PASS AABB info to the main object, The information in the metadata of the mesh regarding AABB SHOULD NOT BE TOUCHED
            so.AABBMIN = so.metaData.AABBMIN;
            so.AABBMAX = so.metaData.AABBMAX;

            //Check if the model should be subjected to LOD filtering
            if (so.name.Contains("LOD"))
            {
                so.hasLOD = true;
                //Override LOD level using the name
                try
                {
                    so.metaData.LODLevel = (int)Char.GetNumericValue(so.name[so.name.IndexOf("LOD") + 3]);
                }
                catch (IndexOutOfRangeException ex)
                {
                    Common.CallBacks.Log("Unable to fetch lod level from mesh name");
                    so.metaData.LODLevel = 0;
                }

            }

            //Process Attachments
            ProcessComponents(so, attachment_data, activeFilePath.Replace(".SCENE.MBIN", ".ANIM.MBIN"));
            so.animComponentID = so.hasComponent(typeof(AnimComponent));
            so.animPoseComponentID = so.hasComponent(typeof(AnimPoseComponent));

            //Search for the vao
            GLVao vao = gobject.findVao(so.metaData.Hash);

            if (vao == null)
            {
                //Generate VAO and Save vao
                vao = gobject.generateVAO(so);
                gobject.saveVAO(so.metaData.Hash, vao);
            }

            //Get Material Name
            string matname = parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "MATERIAL");


            //Search for the material
            Material mat;
            if (Common.RenderState.activeResMgr.GLmaterials.ContainsKey(matname))
                mat = Common.RenderState.activeResMgr.GLmaterials[matname];
            else
            {
                //Parse material
                mat = parseMaterial(matname);
                //Save Material to the resource manager
                Common.RenderState.activeResMgr.addMaterial(mat);
            }

            //Search for the meshVao in the gobject
            GLMeshVao meshVao = gobject.findGLMeshVao(matname, so.metaData.Hash);

            if (meshVao == null)
            {
                //Generate new meshVao
                meshVao = new GLMeshVao(so.metaData);
                meshVao.type = TYPES.MESH;
                meshVao.vao = vao;
                meshVao.material = mat; //Set meshVao Material

                //Set indicesLength
                //Calculate indiceslength per index buffer
                if (so.metaData.batchcount > 0)
                {
                    int indicesLength = (int)gobject.meshMetaDataDict[so.metaData.Hash].is_size / so.metaData.batchcount;


                    switch (indicesLength)
                    {
                        case 1:
                            meshVao.metaData.indicesLength = OpenTK.Graphics.OpenGL4.DrawElementsType.UnsignedByte;
                            break;
                        case 2:
                            meshVao.metaData.indicesLength = OpenTK.Graphics.OpenGL4.DrawElementsType.UnsignedShort;
                            break;
                        case 4:
                            meshVao.metaData.indicesLength = OpenTK.Graphics.OpenGL4.DrawElementsType.UnsignedInt;
                            break;
                    }

                }

                //Configure boneRemap properly
                meshVao.BoneRemapIndicesCount = so.metaData.lastskinmat - so.metaData.firstskinmat;
                meshVao.BoneRemapIndices = new int[meshVao.BoneRemapIndicesCount];
                for (int i = 0; i < so.metaData.lastskinmat - so.metaData.firstskinmat; i++)
                {
                    try {
                        meshVao.BoneRemapIndices[i] = gobject.boneRemap[so.metaData.firstskinmat + i];
                    } catch (IndexOutOfRangeException ex)
                    {
                        meshVao.BoneRemapIndices[i] = 0;
                    }
                    
                }
                    
                //Set skinned flag
                if (meshVao.BoneRemapIndicesCount > 0 && so.animComponentID >= 0)
                    meshVao.skinned = true;

                //Set skinned flag if its set as a metarial flag
                if (mat.has_flag(TkMaterialFlags.MaterialFlagEnum._F02_SKINNED))
                    meshVao.skinned = true;
                 
                //Generate collision mesh vao
                try
                {
                    meshVao.bHullVao = gobject.getCollisionMeshVao(so.metaData); //Missing data
                }
                catch (Exception ex)
                {
                    Common.CallBacks.Log("Error while fetching bHull Collision Mesh");
                    meshVao.bHullVao = null;
                }

                //so.setupBSphere(); //Setup Bounding Sphere Mesh

                //Save meshvao to the gobject
                gobject.saveGLMeshVAO(so.metaData.Hash, matname, meshVao);
            }

            so.meshVao = meshVao;
            so.instanceId = GLMeshBufferManager.addInstance(ref meshVao, so); //Add instance

            Common.CallBacks.Log("Object {0}, Number of skinmatrices required: {1}", so.name, so.metaData.lastskinmat - so.metaData.firstskinmat);

            //Common.CallBacks.Log("Children Count {0}", childs.ChildNodes.Count);
            foreach (TkSceneNodeData child in children)
            {
                Model part = parseNode(child, gobject, so, scene);
                so.AddChild(part);
            }

            //Finally Order children by name
            so.Children.OrderBy(i => i.Name);
            scene.nodeDict[so.Name] = so;
            return so;
        }

        private static Scene parseScene(TkSceneNodeData node,
            GeomObject gobject, Model parent, Scene scene)
        {
            Scene so = new Scene();
            so.name = node.Name;
            so.nameHash = node.NameHash;

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

            //Get Transformation
            so.parent = parent;
            so.init(transforms);
            so.gobject = gobject;

            //Fetch attributes
            so._LODDistances = new float[5];
            so._LODNum = 0;
            int.TryParse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "NUMLODS"), out so._LODNum);
            
            //Fetch extra LOD attributes
            for (int i = 1; i < so._LODNum; i++)
            {
                float attr_val = MathUtils.FloatParse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "LODDIST" + i));
                so._LODDistances[i - 1] = attr_val;
            }

            //Setup model texture manager
            so.texMgr = new textureManager();
            so.texMgr.setMasterTexManager(Common.RenderState.activeResMgr.texMgr);
            localTexMgr = so.texMgr; //setup local texMgr

            //Handle Children
            foreach (TkSceneNodeData child in children)
            {
                Model part = parseNode(child, gobject, so, so);
                so.AddChild(part);
            }

            return so;
        }


        private static Locator parseLocator(TkSceneNodeData node,
            GeomObject gobject, Model parent, Scene scene)
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

            Locator so = new Locator();
            //Fetch attributes

            //For now fetch only one attachment
            string attachment = parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "ATTACHMENT");
            TkAttachmentData attachment_data = null;
            if (attachment != "")
            {
                try
                {
                    attachment_data = NMSUtils.LoadNMSTemplate(attachment, ref Common.RenderState.activeResMgr) as TkAttachmentData;
                }
                catch (Exception e)
                {
                    attachment_data = null;
                }
            }

            if (node.Attributes.Count > 1)
                Util.showError("DM THE IDIOT TO ADD SUPPORT FOR FUCKING MULTIPLE ATTACHMENTS...", "DM THE IDIOT");

            //Set Properties
            //Testingso.Name = name + "_LOC";
            so.name = node.Name;
            so.nameHash = node.NameHash;

            //Get Transformation
            so.parent = parent;
            so.parentScene = scene;
            so.init(transforms);

            //Process Locator Attachments
            ProcessComponents(so, attachment_data, activeFilePath.Replace(".SCENE.MBIN", ".ANIM.MBIN"));
            so.animComponentID = so.hasComponent(typeof(AnimComponent));
            so.animPoseComponentID = so.hasComponent(typeof(AnimPoseComponent));
            so.actionComponentID = so.hasComponent(typeof(TriggerActionComponent));

            //Handle Children
            foreach (TkSceneNodeData child in children)
            {
                Model part = parseNode(child, gobject, so, scene);
                so.AddChild(part);
            }

            //Finally Order children by name
            so.Children.OrderBy(i => i.Name);

            scene.nodeDict[so.Name] = so;

            //Do not restore the old AnimScene let them flow
            //localAnimScene = old_localAnimScene; //Restore old_localAnimScene
            return so;

        }


        private static Joint parseJoint(TkSceneNodeData node,
            GeomObject gobject, Model parent, Scene scene)
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

            Joint so = new Joint();

            //For now fetch only one attachment
            string attachment = parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "ATTACHMENT");
            TkAttachmentData attachment_data = null;
            if (attachment != "")
            {
                try
                {
                    attachment_data = NMSUtils.LoadNMSTemplate(attachment, ref Common.RenderState.activeResMgr) as TkAttachmentData;
                }
                catch (Exception e)
                {
                    attachment_data = null;
                }
            }

            //Process Joint Attachments
            ProcessComponents(so, attachment_data, activeFilePath.Replace(".SCENE.MBIN", ".ANIM.MBIN"));

            //Set properties
            so.name = node.Name;
            so.nameHash = node.NameHash;
            //Get Transformation
            so.parent = parent;
            so.parentScene = scene;
            so.init(transforms);

            
            //Get JointIndex
            so.jointIndex = int.Parse(node.Attributes.FirstOrDefault(item => item.Name == "JOINTINDEX").Value);
            //Get InvBMatrix from gobject
            if (so.jointIndex < gobject.jointData.Count)
            {
                so.invBMat = gobject.jointData[so.jointIndex].invBindMatrix;
                so.BindMat = gobject.jointData[so.jointIndex].BindMatrix;
            }

            //Set Random Color
            so.color[0] = Common.RenderState.randgen.Next(255) / 255.0f;
            so.color[1] = Common.RenderState.randgen.Next(255) / 255.0f;
            so.color[2] = Common.RenderState.randgen.Next(255) / 255.0f;


            so.meshVao = new GLMeshVao();
            so.instanceId = GLMeshBufferManager.addInstance(ref so.meshVao, so); //Add instance
            so.meshVao.type = TYPES.JOINT;
            so.meshVao.metaData = new MeshMetaData();
            //TODO: Find a place to keep references from the joint GLMeshVAOs

            so.meshVao.vao = new GMDL.Primitives.LineSegment(children.Count, new Vector3(1.0f, 0.0f, 0.0f)).getVAO();
            so.meshVao.material = Common.RenderState.activeResMgr.GLmaterials["jointMat"];

            //Add joint to scene
            scene.jointDict[so.Name] = so;
            scene.nodeDict[so.Name] = so;

            //Handle Children
            foreach (TkSceneNodeData child in children)
            {
                Model part = parseNode(child, gobject, so, scene);
                so.AddChild(part);
            }

            //Finally Order children by name
            so.Children.OrderBy(i => i.Name);
            return so;

        }

        private static Collision parseCollision(TkSceneNodeData node,
            GeomObject gobject, Model parent, Scene scene)
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

            //Create model
            Collision so = new Collision();

            so.debuggable = true;
            so.name = node.Name + "_COLLISION";
            so.nameHash = node.NameHash;
            so.type = TYPES.COLLISION;

            //Get Options
            //In collision objects first child is probably the type
            //string collisionType = ((XmlElement)attribs.ChildNodes[0].SelectSingleNode("Property[@name='Value']")).GetAttribute("value").ToUpper();
            string collisionType = node.Attributes.FirstOrDefault(item => item.Name == "TYPE").Value.Value.ToUpper();

            Common.CallBacks.Log(string.Format("Collision Detected {0} {1}", node.Name, collisionType));

            //Get Material for all types
            string matkey = node.Name; //I will index the collision materials by their name, it shouldn't hurt anywhere
                                  // + cleaning up will be up to the resource manager

            MeshMetaData metaData = new MeshMetaData();
            if (collisionType == "MESH")
            {
                so.collisionType = (int)COLLISIONTYPES.MESH;
                metaData.batchstart_physics = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "BATCHSTART"));
                metaData.batchcount = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "BATCHCOUNT"));
                metaData.vertrstart_physics = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "VERTRSTART"));
                metaData.vertrend_physics = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "VERTREND"));
                metaData.firstskinmat = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "FIRSTSKINMAT"));
                metaData.lastskinmat = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "LASTSKINMAT"));
                metaData.boundhullstart = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "BOUNDHULLST"));
                metaData.boundhullend = int.Parse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "BOUNDHULLED"));

                so.gobject = gobject;
                so.metaData = metaData; //Set metadata

                //Find id within the vbo
                int iid = -1;
                for (int i = 0; i < gobject.vstarts.Count; i++)
                    if (gobject.vstarts[i] == metaData.vertrstart_physics)
                    {
                        iid = i;
                        break;
                    }

                if (metaData.lastskinmat - metaData.firstskinmat > 0)
                {
                    ErrorUtils.throwException("SKINNED COLLISION. CHECK YOUR SHIT!");
                }

                //Set vao
                try
                {
                    so.meshVao = new GLMeshVao(so.metaData);
                    so.instanceId = GLMeshBufferManager.addInstance(ref so.meshVao, so); //Add instance
                    so.meshVao.vao = gobject.getCollisionMeshVao(so.metaData);
                    //Use indiceslength from the gobject
                    so.meshVao.metaData.indicesLength = so.gobject.indicesLengthType;
                }
                catch (KeyNotFoundException e)
                {
                    Common.CallBacks.Log("Missing Collision Mesh " + so.name);
                    so.meshVao = null;
                }

            }
            else if (collisionType == "CYLINDER")
            {
                //Common.CallBacks.Log("CYLINDER NODE PARSING NOT IMPLEMENTED");
                //Set cvbo

                float radius = MathUtils.FloatParse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "RADIUS"));
                float height = MathUtils.FloatParse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "HEIGHT"));
                Common.CallBacks.Log(string.Format("Cylinder Collision r:{0} h:{1}", radius, height));

                metaData.batchstart_graphics = 0;
                metaData.batchcount = 120;
                metaData.vertrstart_graphics = 0;
                metaData.vertrend_graphics = 22 - 1;
                so.metaData = metaData;
                so.meshVao = new GLMeshVao(so.metaData);
                so.meshVao.vao = (new GMDL.Primitives.Cylinder(radius, height, new Vector3(0.0f, 0.0f, 0.0f), true)).getVAO();
                so.instanceId = GLMeshBufferManager.addInstance(ref so.meshVao, so); //Add instance
                so.collisionType = COLLISIONTYPES.CYLINDER;

            }
            else if (collisionType == "BOX")
            {
                //Common.CallBacks.Log("BOX NODE PARSING NOT IMPLEMENTED");
                //Set cvbo
                float width = MathUtils.FloatParse(node.Attributes.FirstOrDefault(item => item.Name == "WIDTH").Value);
                float height = MathUtils.FloatParse(node.Attributes.FirstOrDefault(item => item.Name == "HEIGHT").Value);
                float depth = MathUtils.FloatParse(node.Attributes.FirstOrDefault(item => item.Name == "DEPTH").Value);
                Common.CallBacks.Log(string.Format("Sphere Collision w:{0} h:{0} d:{0}", width, height, depth));
                //Set general vao properties
                metaData.batchstart_graphics = 0;
                metaData.batchcount = 36;
                metaData.vertrstart_graphics = 0;
                metaData.vertrend_graphics = 8 - 1;
                so.metaData = metaData;

                so.meshVao = new GLMeshVao(so.metaData);
                so.meshVao.vao = (new GMDL.Primitives.Box(width, height, depth, new Vector3(1.0f), true)).getVAO();
                so.instanceId = GLMeshBufferManager.addInstance(ref so.meshVao, so); //Add instance
                so.collisionType = COLLISIONTYPES.BOX;


            }
            else if (collisionType == "CAPSULE")
            {
                //Set cvbo
                float radius = MathUtils.FloatParse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "RADIUS"));
                float height = MathUtils.FloatParse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "HEIGHT"));
                Common.CallBacks.Log(string.Format("Capsule Collision r:{0} h:{1}", radius, height));
                metaData.batchstart_graphics = 0;
                metaData.batchcount = 726;
                metaData.vertrstart_graphics = 0;
                metaData.vertrend_graphics = 144 - 1;
                so.metaData = metaData;
                so.meshVao = new GLMeshVao(so.metaData);
                so.meshVao.vao = (new GMDL.Primitives.Capsule(new Vector3(), height, radius)).getVAO();
                so.instanceId = GLMeshBufferManager.addInstance(ref so.meshVao, so); //Add instance
                so.collisionType = COLLISIONTYPES.CAPSULE;

            }
            else if (collisionType == "SPHERE")
            {
                //Set cvbo
                float radius = MathUtils.FloatParse(node.Attributes.FirstOrDefault(item => item.Name == "RADIUS").Value);
                Common.CallBacks.Log(string.Format("Sphere Collision r:{0}", radius));
                metaData.batchstart_graphics = 0;
                metaData.batchcount = 600;
                metaData.vertrstart_graphics = 0;
                metaData.vertrend_graphics = 121 - 1;
                so.metaData = metaData;
                so.meshVao = new GLMeshVao(so.metaData);
                so.meshVao.vao = (new GMDL.Primitives.Sphere(new Vector3(), radius)).getVAO();
                so.instanceId = GLMeshBufferManager.addInstance(ref so.meshVao, so); //Add instance
                so.collisionType = COLLISIONTYPES.SPHERE;
            }
            else
            {
                Common.CallBacks.Log("NEW COLLISION TYPE: " + collisionType);
            }

            //Set material to the collision Mesh
            so.meshVao.material = Common.RenderState.activeResMgr.GLmaterials["collisionMat"];
            so.meshVao.type = TYPES.COLLISION;
            so.meshVao.collisionType = so.collisionType;

            Common.CallBacks.Log(string.Format("Batch Start {0} Count {1} ",
                metaData.batchstart_physics, metaData.batchcount));

            so.parent = parent;
            so.init(transforms);

            //Collision probably has no children biut I'm leaving that code here
            foreach (TkSceneNodeData child in children)
                so.AddChild(parseNode(child, gobject, so, scene));
            
            return so;

        }


        private static Light parseLight(TkSceneNodeData node,
            GeomObject gobject, Model parent, Scene scene)
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


            Light so = new Light();
            //Set Properties
            so.name = node.Name;
            so.nameHash = node.NameHash;
            so.type = TYPES.LIGHT;

            so.parent = parent;
            so.init(transforms);

            //Parse Light Attributes
            so.Color.X = MathUtils.FloatParse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "COL_R"));
            so.Color.Y = MathUtils.FloatParse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "COL_G"));
            so.Color.Z = MathUtils.FloatParse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "COL_B"));
            so.fov = MathUtils.FloatParse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "FOV"));
            so.intensity = MathUtils.FloatParse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "INTENSITY"));

            string attenuation = parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "FALLOFF");
            if (!Enum.TryParse<ATTENUATION_TYPE>(attenuation.ToUpper(), out so.falloff))
                ErrorUtils.throwException("Light attenuation Type " + attenuation + " Not supported");
            
            //Add Light to the resource Manager
            so.update_struct();
            Common.RenderState.activeResMgr.GLlights.Add(so);

            scene.nodeDict[so.Name] = so;

            return so;

        }

        private static Reference parseReference(TkSceneNodeData node,
            GeomObject gobject, Model parent, Scene scene)
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

            //Another Scene file referenced
            Common.CallBacks.Log("Reference Detected");

            string scene_ref = node.Attributes.FirstOrDefault(item => item.Name == "SCENEGRAPH").Value;
            Common.CallBacks.Log(string.Format("Loading Reference {0}", scene_ref));

            //Getting Scene MBIN file
            //string exmlPath = Path.GetFullPath(Util.getFullExmlPath(path));
            //Common.CallBacks.Log("Loading Scene " + path);
            //Parse MBIN to xml

            //Generate Reference object
            Reference so = new Reference(scene_ref);
            so.name = node.Name;
            so.nameHash = node.NameHash;

            //Get Transformation
            so.parent = parent;
            so.init(transforms);

            Scene new_so;
            
            //Check if scene has been parsed
            if (!Common.RenderState.activeResMgr.GLScenes.ContainsKey(scene_ref))
            {
                //Read new Scene
                //new_so = LoadObjects(scene_ref);
            }
            else
            {
                //Make a shallow copy of the scene
                new_so = (Scene) RenderState.activeResMgr.GLScenes[scene_ref].Clone();
            }

            //so.ref_scene = new_so;
            //new_so.parent = so;
            //so.children.Add(new_so); //Keep it also as a child so the rest of pipeline is not affected

            //Handle Children
            //Common.CallBacks.Log("Children Count {0}", childs.ChildNodes.Count);
            foreach (TkSceneNodeData child in children)
            {
                Model part = parseNode(child, gobject, so, scene);
                so.AddChild(part);
            }

            scene.nodeDict[so.Name] = so;

            return so;

        }

        private static Locator parseEmitter(TkSceneNodeData node,
            GeomObject gobject, Model parent, Scene scene)
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
            Locator so = new Locator();
            so.type = TYPES.EMITTER;
            //Fetch attributes

            //For now fetch only one attachment
            string attachment = parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "ATTACHMENT");
            TkAttachmentData attachment_data = null;
            if (attachment != "")
            {
                attachment_data = NMSUtils.LoadNMSTemplate(attachment, ref Common.RenderState.activeResMgr) as TkAttachmentData;
            }

            //TODO: Parse Emitter material and Emission data. from the node attributes


            //Set Properties
            //Testingso.Name = name + "_LOC";
            so.name = node.Name;
            so.nameHash = node.NameHash;

            //Get Transformation
            so.parent = parent;
            so.init(transforms);

            //Process Locator Attachments
            ProcessComponents(so, attachment_data, activeFilePath.Replace(".SCENE.MBIN", ".ANIM.MBIN"));

            //Handle Children
            foreach (TkSceneNodeData child in children)
            {
                Model part = parseNode(child, gobject, so, scene);
                so.AddChild(part);
            }

            //Do not restore the old AnimScene let them flow
            //localAnimScene = old_localAnimScene; //Restore old_localAnimScene
            return so;
        }

        private static Model parseNode(TkSceneNodeData node, 
            GeomObject gobject, Model parent, Scene scene)
        {
            Common.CallBacks.Log(string.Format("Importing Scene {0} Node {1}", scene?.name, node.Name));
            Common.CallBacks.updateStatus("Importing Scene: " + scene?.name + " Part: " + node.Name);

            TYPES typeEnum;
            if (!Enum.TryParse<TYPES>(node.Type, out typeEnum))
                ErrorUtils.throwException("Node Type " + node.Type + " Not supported");
            
            if (typeEnum == TYPES.MESH)
            {
                Common.CallBacks.Log(string.Format("Parsing Mesh {0}", node.Name));
                return parseMesh(node, gobject, parent, scene);
            }
            else if (typeEnum == TYPES.MODEL)
            {
                return parseScene(node, gobject, parent, scene);
            }
            else if (typeEnum == TYPES.LOCATOR || typeEnum == TYPES.GROUP)
            {
                Model locator = parseLocator(node, gobject, parent, scene);
                locator.type = typeEnum; //Make sure that the correct type is cached
                return locator;
            }
            else if (typeEnum == TYPES.JOINT)
            {
                return parseJoint(node, gobject, parent, scene);
            }
            else if (typeEnum == TYPES.REFERENCE)
            {
                return parseReference(node, gobject, parent, scene);
            }
            else if (typeEnum == TYPES.COLLISION)
            {
                return parseCollision(node, gobject, parent, scene);
            }
            else if (typeEnum == TYPES.LIGHT)
            {
                Common.CallBacks.Log(string.Format("Parsing Light, {0}", node.Name));
                return parseLight(node, gobject, parent, scene);
            }

            else if (typeEnum == TYPES.EMITTER)
            {
                return parseEmitter(node, gobject, parent, scene);
            }
            else
            {
                Common.CallBacks.Log(string.Format("Unknown Type, {0}", node.Type));
                Locator so = new Locator();
                //Set Properties
                so.name = node.Name + "_UNKNOWN";
                so.nameHash = node.NameHash;
                so.type = TYPES.UNKNOWN;
                //Locator Objects don't have options

                //take care of children
                return so;
                //throw new ApplicationException("Unknown mesh type");
            }
        }
    }

}
