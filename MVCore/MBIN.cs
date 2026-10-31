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
        public byte[] n_buffer; //Normals generated on load (3 floats per vertex), when the geometry has no normal data
    }

    public static class GEOMMBIN {



        //Parses a geometry file using libMBIN's TkGeometryData.
        //All counts, offsets and layouts come from the decompiled template, so changes in the
        //struct layout between game versions only need a libMBIN update.
        //Only the vertex/index streams are read as raw bytes from the .DATA file.
        public static GeomObject Parse(TkGeometryData data, Stream gfs)
        {
            Common.CallBacks.Verbose("Parsing Geometry MBIN");
            Common.CallBacks.Verbose("Model Vertices: " + data.VertexCount);
            Common.CallBacks.Verbose("Model Indices: " + data.IndexCount);
            Common.CallBacks.Verbose("Indices 16Bit: " + data.Indices16Bit);
            Common.CallBacks.Verbose("Collision Index Count: " + data.CollisionIndexCount);

            GeomObject geom = new GeomObject();

            geom.sourceGeometry = captureNativeGeometry(data);
            if (gfs.Length > Int32.MaxValue)
                throw new InvalidDataException("Geometry stream is too large to retain for export");
            long sourcePosition = gfs.Position;
            gfs.Seek(0, SeekOrigin.Begin);
            geom.sourceGeometryStream = new byte[(int)gfs.Length];
            gfs.ReadExactly(geom.sourceGeometryStream, 0, geom.sourceGeometryStream.Length);
            gfs.Seek(sourcePosition, SeekOrigin.Begin);

            //Counts
            geom.vertCount = data.VertexCount;
            geom.indicesCount = data.IndexCount;
            if (data.Indices16Bit == 1)
            {
                geom.indicesLength = 0x2;
                geom.indicesLengthType = OpenTK.Graphics.OpenGL4.DrawElementsType.UnsignedShort;
            }
            else
            {
                geom.indicesLength = 0x4;
                geom.indicesLengthType = OpenTK.Graphics.OpenGL4.DrawElementsType.UnsignedInt;
            }

            //Skinning
            geom.boneRemap = (data.SkinMatrixLayout ?? new List<int>()).Select(x => (short) x).ToArray();
            geom.meshBaseSkinMat = (data.MeshBaseSkinMat ?? new List<int>()).Select(x => (short) x).ToArray();

            List<TkJointBindingData> joints = data.JointBindings ?? new List<TkJointBindingData>();
            geom.jointCount = joints.Count;
            geom.invBMats = new float[joints.Count * 16];
            for (int i = 0; i < joints.Count; i++)
            {
                JointBindingData jdata = new JointBindingData();
                jdata.Load(joints[i]);
                Array.Copy(jdata.convertMat(), 0, geom.invBMats, 16 * i, 16);
                geom.jointData.Add(jdata);
            }

            //Per mesh ranges and bounds
            List<int> vertStarts = data.MeshVertRStart ?? new List<int>();
            geom.vstarts.AddRange(vertStarts);
            for (int i = 0; i < vertStarts.Count; i++)
            {
                Vector3[] bb = new Vector3[2];
                if (data.MeshAABBMin != null && i < data.MeshAABBMin.Count)
                    bb[0] = new Vector3(data.MeshAABBMin[i].X, data.MeshAABBMin[i].Y, data.MeshAABBMin[i].Z);
                if (data.MeshAABBMax != null && i < data.MeshAABBMax.Count)
                    bb[1] = new Vector3(data.MeshAABBMax[i].X, data.MeshAABBMax[i].Y, data.MeshAABBMax[i].Z);
                geom.bboxes.Add(bb);
            }

            //Collision hulls
            geom.bhullstarts.AddRange(data.BoundHullVertSt ?? new List<int>());
            geom.bhullends.AddRange(data.BoundHullVertEd ?? new List<int>());
            foreach (Vector4f v in data.BoundHullVerts ?? new List<Vector4f>())
                geom.bhullverts.Add(new Vector3(v.X, v.Y, v.Z));

            //Index buffer stored in the geometry file (used for collision meshes). Can be empty
            List<int> indexBuffer = data.IndexBuffer ?? new List<int>();
            geom.ibuffer = new byte[indexBuffer.Count * geom.indicesLength];
            for (int i = 0; i < indexBuffer.Count; i++)
            {
                if (geom.indicesLength == 0x2)
                    BitConverter.TryWriteBytes(new Span<byte>(geom.ibuffer, 2 * i, 2), (ushort) indexBuffer[i]);
                else
                    BitConverter.TryWriteBytes(new Span<byte>(geom.ibuffer, 4 * i, 4), indexBuffer[i]);
            }

            //ProcGen
            List<VariableSizeString> pgNames = data.ProcGenNodeNames ?? new List<VariableSizeString>();
            foreach (VariableSizeString name in pgNames)
                geom.procGenNames.Add(name.Value);
            geom.procGenIDs.AddRange(data.ProcGenParentId ?? new List<int>());

            //Vertex layouts. Attribute slots are indexed by semantic id, which is also the shader attribute location
            geom.bufInfo = new List<bufInfo>();
            geom.offsets = new int[MaxSupportedSemantic + 1];
            for (int i = 0; i <= MaxSupportedSemantic; i++)
            {
                geom.bufInfo.Add(null);
                geom.offsets[i] = -1;
            }

            geom.vp_size = data.PositionVertexLayout?.Stride ?? 0;
            geom.vx_size = data.VertexLayout?.Stride ?? 0;
            addVertexLayout(geom, data.PositionVertexLayout, 0, "Position");
            addVertexLayout(geom, data.VertexLayout, 1, "Main");

            geom.mesh_descr = getDescr(geom.offsets);
            Common.CallBacks.Verbose("Mesh Description: " + geom.mesh_descr);
            geom.interleaved = true;

            //Mesh stream metadata
            foreach (TkMeshMetaData meta in data.StreamMetaDataArray ?? new List<TkMeshMetaData>())
            {
                geomMeshMetaData mmd = new geomMeshMetaData();
                mmd.name = meta.IdString?.Value ?? "";
                mmd.hash = meta.Hash;
                mmd.vs_size = (uint) meta.VertexDataSize;
                mmd.vs_abs_offset = (uint) meta.VertexDataOffset;
                mmd.vp_size = (uint) meta.VertexPositionDataSize;
                mmd.vp_abs_offset = (uint) meta.VertexPositionDataOffset;
                mmd.is_size = (uint) meta.IndexDataSize;
                mmd.is_abs_offset = (uint) resolveIndexDataOffset(meta, gfs.Length);
                mmd.double_buffering = meta.DoubleBufferGeometry;

                if (!geom.meshMetaDataDict.ContainsKey(mmd.hash))
                    geom.meshMetaDataDict[mmd.hash] = mmd;
                Common.CallBacks.Verbose(mmd.name);
            }

            //Load the streams from the geometry stream file
            foreach (geomMeshMetaData mmd in geom.meshMetaDataDict.Values)
            {
                geomMeshData md = new geomMeshData();
                md.hash = mmd.hash;
                md.vs_buffer = readStreamSection(gfs, mmd.vs_abs_offset, mmd.vs_size, mmd.name, "vertex");
                md.vp_buffer = readStreamSection(gfs, mmd.vp_abs_offset, mmd.vp_size, mmd.name, "position");
                md.is_buffer = readStreamSection(gfs, mmd.is_abs_offset, mmd.is_size, mmd.name, "index");

                //Generate normals when the geometry has no normal data at all
                if (geom.bufInfo[2] == null && geom.packedFrameOffset < 0)
                    md.n_buffer = generateNormals(geom, md);

                geom.meshDataDict[mmd.hash] = md;
            }

            return geom;
        }

        private static NativeGeometryData captureNativeGeometry(TkGeometryData data)
        {
            NativeGeometryData result = new NativeGeometryData
            {
                PositionLayout = captureLayout(data.PositionVertexLayout),
                MainLayout = captureLayout(data.VertexLayout),
                BoundHullEnds = new List<int>(data.BoundHullVertEd ?? new List<int>()),
                BoundHullStarts = new List<int>(data.BoundHullVertSt ?? new List<int>()),
                IndexBuffer = new List<int>(data.IndexBuffer ?? new List<int>()),
                JointMirrorPairs = new List<int>(data.JointMirrorPairs ?? new List<int>()),
                MeshBaseSkinMat = new List<int>(data.MeshBaseSkinMat ?? new List<int>()),
                MeshVertEnds = new List<int>(data.MeshVertREnd ?? new List<int>()),
                MeshVertStarts = new List<int>(data.MeshVertRStart ?? new List<int>()),
                ProcGenParentIds = new List<int>(data.ProcGenParentId ?? new List<int>()),
                SkinMatrixLayout = new List<int>(data.SkinMatrixLayout ?? new List<int>()),
                CollisionIndexCount = data.CollisionIndexCount,
                IndexCount = data.IndexCount,
                Indices16Bit = data.Indices16Bit,
                VertexCount = data.VertexCount
            };

            result.BoundHullVertices.AddRange((data.BoundHullVerts ?? new List<Vector4f>())
                .Select(value => new Vector4(value.X, value.Y, value.Z, value.W)));
            result.MeshAABBMin.AddRange((data.MeshAABBMin ?? new List<Vector4f>())
                .Select(value => new Vector4(value.X, value.Y, value.Z, value.W)));
            result.MeshAABBMax.AddRange((data.MeshAABBMax ?? new List<Vector4f>())
                .Select(value => new Vector4(value.X, value.Y, value.Z, value.W)));
            result.ProcGenNodeNames.AddRange((data.ProcGenNodeNames ?? new List<VariableSizeString>())
                .Select(value => value?.Value ?? ""));

            foreach (TkJointBindingData binding in data.JointBindings ?? new List<TkJointBindingData>())
                result.JointBindings.Add(new NativeGeometryJointBinding
                {
                    InvBindMatrix = cloneArray(binding.InvBindMatrix),
                    BindRotate = cloneArray(binding.BindRotate),
                    BindScale = cloneArray(binding.BindScale),
                    BindTranslate = cloneArray(binding.BindTranslate)
                });
            foreach (TkJointExtentData extent in data.JointExtents ?? new List<TkJointExtentData>())
                result.JointExtents.Add(new NativeGeometryJointExtent
                {
                    Center = cloneArray(extent.JointExtentCenter),
                    Max = cloneArray(extent.JointExtentMax),
                    Min = cloneArray(extent.JointExtentMin),
                    StdDev = cloneArray(extent.JointExtentStdDev)
                });
            foreach (TkJointMirrorAxis axis in data.JointMirrorAxes ?? new List<TkJointMirrorAxis>())
                result.JointMirrorAxes.Add(new NativeGeometryJointMirrorAxis
                {
                    Mode = axis.MirrorAxisMode,
                    RotAdjustW = axis.RotAdjustW, RotAdjustX = axis.RotAdjustX,
                    RotAdjustY = axis.RotAdjustY, RotAdjustZ = axis.RotAdjustZ,
                    RotAxisX = axis.RotMirrorAxisX, RotAxisY = axis.RotMirrorAxisY,
                    RotAxisZ = axis.RotMirrorAxisZ, TransAxisX = axis.TransMirrorAxisX,
                    TransAxisY = axis.TransMirrorAxisY, TransAxisZ = axis.TransMirrorAxisZ
                });
            foreach (TkMeshMetaData metadata in data.StreamMetaDataArray ?? new List<TkMeshMetaData>())
                result.Streams.Add(new NativeGeometryStreamMetadata
                {
                    Id = metadata.IdString?.Value ?? "", Hash = metadata.Hash,
                    IndexOffset = metadata.IndexDataOffset, IndexSize = metadata.IndexDataSize,
                    VertexOffset = metadata.VertexDataOffset, VertexSize = metadata.VertexDataSize,
                    PositionOffset = metadata.VertexPositionDataOffset,
                    PositionSize = metadata.VertexPositionDataSize,
                    DoubleBuffered = metadata.DoubleBufferGeometry
                });
            return result;
        }

        private static NativeGeometryVertexLayout captureLayout(TkVertexLayout layout)
        {
            if (layout == null)
                return null;
            NativeGeometryVertexLayout result = new NativeGeometryVertexLayout
            {
                PlatformData = layout.PlatformData,
                ElementCount = layout.ElementCount,
                Stride = layout.Stride
            };
            foreach (TkVertexElement element in layout.VertexElements ?? new List<TkVertexElement>())
                result.Elements.Add(new NativeGeometryVertexElement
                {
                    Instancing = (int)element.Instancing, Type = element.Type,
                    Normalise = element.Normalise, Offset = element.Offset,
                    SemanticID = element.SemanticID, Size = element.Size
                });
            return result;
        }

        private static float[] cloneArray(float[] values) =>
            values == null ? Array.Empty<float>() : (float[])values.Clone();

        //Shader attribute locations 0-6: position, uv, normal, tangent, color, blend indices, blend weights
        private const int MaxSupportedSemantic = 6;
        //One 32 bit value holding the normal, tangent and bitangent sign (replaces semantics 2 and 3).
        //It is bound as an integer attribute at location 11 and decoded in the shader
        private const int PackedFrameSemantic = 11;

        private static void addVertexLayout(GeomObject geom, TkVertexLayout layout, int stream, string streamName)
        {
            if (layout?.VertexElements == null)
                return;

            foreach (TkVertexElement elem in layout.VertexElements)
            {
                int semantic = elem.SemanticID;
                string desc = $"{streamName} stream: semantic {semantic}, type 0x{elem.Type:X}, size {elem.Size}, offset {elem.Offset}";

                if (semantic == PackedFrameSemantic && elem.Type == (int) OpenTK.Graphics.OpenGL4.VertexAttribPointerType.UnsignedInt)
                {
                    geom.packedFrameOffset = elem.Offset;
                    geom.packedFrameStream = stream;
                    Common.CallBacks.Verbose("Packed normal/tangent frame, decoded in the shader. " + desc);
                    continue;
                }

                if (semantic > MaxSupportedSemantic)
                {
                    Common.CallBacks.Log("Skipping unsupported vertex attribute. " + desc);
                    continue;
                }

                //GL type constants map directly to VertexAttribPointerType values
                if (!Enum.IsDefined(typeof(OpenTK.Graphics.OpenGL4.VertexAttribPointerType), elem.Type))
                {
                    Common.CallBacks.Log("Skipping vertex attribute with unknown type. " + desc);
                    continue;
                }

                if (geom.bufInfo[semantic] != null)
                    Common.CallBacks.Log("Vertex attribute defined twice, using the last one. " + desc);

                var type = (OpenTK.Graphics.OpenGL4.VertexAttribPointerType) elem.Type;
                bufInfo buf = new bufInfo(semantic, type, elem.Size, layout.Stride, elem.Offset,
                    get_shader_sem(semantic), elem.Normalise > 0);
                buf.stream = stream;
                geom.bufInfo[semantic] = buf;
                geom.offsets[semantic] = elem.Offset;
                Common.CallBacks.Verbose("Vertex attribute. " + desc);
            }
        }

        //IndexDataOffset is stored relative to VertexDataOffset in current geometry files.
        //Fall back to treating it as absolute if the relative position doesn't fit in the stream file
        private static long resolveIndexDataOffset(TkMeshMetaData meta, long streamLength)
        {
            long relative = (long) meta.VertexDataOffset + meta.IndexDataOffset;
            if (relative + meta.IndexDataSize <= streamLength)
                return relative;

            Common.CallBacks.Log($"Mesh {meta.IdString?.Value}: index data does not fit relative to the vertex data, using it as an absolute offset");
            return meta.IndexDataOffset;
        }

        private static byte[] readStreamSection(Stream s, long offset, long size, string meshName, string sectionName)
        {
            if (offset < 0 || size < 0 || offset + size > s.Length)
                throw new InvalidDataException($"Mesh {meshName}: {sectionName} data (offset {offset}, size {size}) is outside the geometry stream file ({s.Length} bytes)");

            byte[] buffer = new byte[size];
            s.Seek(offset, SeekOrigin.Begin);
            s.ReadExactly(buffer, 0, (int) size);
            return buffer;
        }

        //Builds smooth per vertex normals (3 floats per vertex) from the positions and indices
        private static byte[] generateNormals(GeomObject geom, geomMeshData md)
        {
            bufInfo pos = geom.bufInfo[0];
            if (pos == null || geom.vp_size == 0)
                return null;
            if (pos.type != OpenTK.Graphics.OpenGL4.VertexAttribPointerType.HalfFloat &&
                pos.type != OpenTK.Graphics.OpenGL4.VertexAttribPointerType.Float)
                return null;

            int vertexCount = md.vp_buffer.Length / geom.vp_size;
            Vector3[] positions = new Vector3[vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                int o = i * geom.vp_size + pos.offset;
                if (pos.type == OpenTK.Graphics.OpenGL4.VertexAttribPointerType.HalfFloat)
                    positions[i] = new Vector3((float) BitConverter.ToHalf(md.vp_buffer, o),
                                               (float) BitConverter.ToHalf(md.vp_buffer, o + 2),
                                               (float) BitConverter.ToHalf(md.vp_buffer, o + 4));
                else
                    positions[i] = new Vector3(BitConverter.ToSingle(md.vp_buffer, o),
                                               BitConverter.ToSingle(md.vp_buffer, o + 4),
                                               BitConverter.ToSingle(md.vp_buffer, o + 8));
            }

            Vector3[] normals = new Vector3[vertexCount];
            int indexCount = md.is_buffer.Length / geom.indicesLength;
            for (int i = 0; i + 2 < indexCount; i += 3)
            {
                int a = readIndex(md.is_buffer, i, geom.indicesLength);
                int b = readIndex(md.is_buffer, i + 1, geom.indicesLength);
                int c = readIndex(md.is_buffer, i + 2, geom.indicesLength);
                if (a >= vertexCount || b >= vertexCount || c >= vertexCount)
                    continue;
                //Area weighted face normal
                Vector3 n = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
                normals[a] += n;
                normals[b] += n;
                normals[c] += n;
            }

            float[] result = new float[vertexCount * 3];
            for (int i = 0; i < vertexCount; i++)
            {
                Vector3 n = normals[i].LengthSquared > 1e-20f ? normals[i].Normalized() : Vector3.UnitY;
                result[3 * i + 0] = n.X;
                result[3 * i + 1] = n.Y;
                result[3 * i + 2] = n.Z;
            }

            byte[] bytes = new byte[result.Length * sizeof(float)];
            Buffer.BlockCopy(result, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        private static int readIndex(byte[] buffer, int i, int indexLength)
        {
            return indexLength == 2 ? BitConverter.ToUInt16(buffer, 2 * i) : (int) BitConverter.ToUInt32(buffer, 4 * i);
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
                    return "unknown"; //Default
            }
        }

        private static string getDescr(int[] offsets)
        {
            string mesh_desc = "";
            string codes = "vuntpbw"; //Verts, UVs, Normals, Tangents, Vertex Color, BlendIndices, BlendWeights

            for (int i = 0; i < offsets.Length; i++)
            {
                if (offsets[i] != -1)
                    mesh_desc += i < codes.Length ? codes[i] : 'x';
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
            {typeof(EmptyNode), 5},
            {typeof(GcInteractionComponentData), 6},
            {typeof(GcSimpleInteractionComponentData), 7},
            {typeof(TkRotationComponentData), 8},
            {typeof(GcColouriseComponentData), 9}
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
                //Use libMBIN to decompile the geometry file
                TkGeometryData geomdata = NMSUtils.LoadNMSTemplate(path + ".PC", ref Common.RenderState.activeResMgr) as TkGeometryData;
                if (geomdata is null)
                    throw new InvalidDataException("Geometry file could not be loaded: " + path + ".PC");

#if DEBUG
                //Save NMSTemplate to mxml
                string xmlstring = MXmlFile.WriteTemplate(geomdata);
                File.WriteAllText("Temp\\temp_geom.mxml", xmlstring);
#endif
                //The geometry stream file holds the raw vertex and index data
                string gstreamfile = "";
                string[] split = path.Split('.');
                for (int i = 0; i < split.Length - 1; i++)
                    gstreamfile += split[i] + ".";
                gstreamfile += "DATA.MBIN.PC";

                using (Stream gfs = NMSUtils.LoadNMSFileStream(gstreamfile, ref Common.RenderState.activeResMgr))
                {
                    gobject = Parse(geomdata, gfs);
                }

                Common.RenderState.activeResMgr.GLgeoms[path] = gobject;
                Common.CallBacks.Verbose(string.Format("Geometry file {0} successfully parsed",
                    path + ".PC"));
            }

            return gobject;

        }


        private static readonly HashSet<string> loadingScenes = new HashSet<string>();

        public static Scene LoadObjects(string path)
        {
            string key = path.Replace('\\', '/').ToUpperInvariant();
            if (!loadingScenes.Add(key))
                throw new InvalidDataException("Scene " + path + " references itself");

            //References and LOD models load in the middle of their parent scene, so the parent's file path
            //(default animation file) and texture manager come back once they are done
            string prevFilePath = activeFilePath;
            textureManager prevTexMgr = localTexMgr;
            try
            {
                return loadScene(path);
            }
            finally
            {
                activeFilePath = prevFilePath;
                localTexMgr = prevTexMgr;
                loadingScenes.Remove(key);
            }
        }

        private static Scene loadScene(string path)
        {   
            TkSceneNodeData template = NMSUtils.LoadNMSTemplate(path, ref Common.RenderState.activeResMgr) as TkSceneNodeData;
            if (template is null)
                throw new FileNotFoundException("Scene file could not be loaded: " + path);
            activeFilePath = path;
            Common.CallBacks.Verbose("Loading Objects from MBINFile");

            string sceneName = template.Name;
            Common.CallBacks.Verbose(string.Format("Trying to load Scene {0}", sceneName));
            string[] split = sceneName.Split('\\');
            string scnName = split[split.Length - 1];
            Common.CallBacks.Log("[Import] Loading scene {0}", scnName);
            
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
            root.SourceFile = path;
            Common.CallBacks.Log("[Import] Loaded scene {0}", scnName);

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


        //Many nodes name an entity file that the game does not ship. The node loads without components
        //and the import goes on, so this is a log line, not an error dialog
        //The entity is kept on the node for the export
        private static TkAttachmentData loadAttachment(Model node, string attachment)
        {
            node.AttachmentPath = attachment;
            if (!NMSUtils.NMSFileExists(attachment, Common.RenderState.activeResMgr))
            {
                Common.CallBacks.Log("Entity file " + attachment + " not found, the node is loaded without components");
                return null;
            }
            node.AttachmentTemplate = NMSUtils.LoadNMSTemplate(attachment, ref Common.RenderState.activeResMgr) as TkAttachmentData;
            return node.AttachmentTemplate;
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

        private static Vector4 colourToVector(Colour colour)
        {
            return new Vector4(colour.R, colour.G, colour.B, colour.A);
        }

        private static void ProcessLODComponent(Model node, TkLODComponentData component)
        {
            //Load all LOD models as children to the node
            LODModelComponent lodmdlcomp = new LODModelComponent(component);
            int count = 0;
            
            for (int i = 0; i < component.LODModels.Count; i++)
            {
                string filepath = component.LODModels[i].LODModel.Filename;
                Common.CallBacks.Verbose("Loading LOD " + filepath);
                Scene so;
                try
                {
                    so = LoadObjects(filepath);
                }
                catch (Exception ex)
                {
                    Common.CallBacks.Log("Failed to load LOD model " + filepath + ": " + ex.Message);
                    continue;
                }
                so.lodModelIndex = count++;
                node.AddChild(so);
                //Create LOD Resource
                LODModelResource lodres = new LODModelResource(component.LODModels[i]);
                lodmdlcomp.Resources.Add(lodres);
            }
            
            node.lodModelCount = count;
            node._LODNum = Math.Max(count, 1);
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
                
                //TkAudioComponentData is matched by name and its fields are read by name, so any libMBIN version builds
                if (comp_type.Name == "TkAudioComponentData")
                {
                    node.Components.Add(new AudioComponent(comp.Template));
                    continue;
                }

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
                    case 6:
                        node.Components.Add(new InteractionComponent(comp.Template as GcInteractionComponentData));
                        break;
                    case 7:
                        node.Components.Add(new InteractionComponent(comp.Template as GcSimpleInteractionComponentData));
                        break;
                    case 8:
                        node.Components.Add(new RotationComponent(comp.Template as TkRotationComponentData));
                        break;
                    case 9:
                    {
                        GcColouriseComponentData colourise = comp.Template as GcColouriseComponentData;
                        node.colouriseColours = new Vector4[]
                        {
                            colourToVector(colourise.PrimaryColour),
                            colourToVector(colourise.SecondaryColour),
                            colourToVector(colourise.TernaryColour),
                            colourToVector(colourise.QuaternaryColour)
                        };
                        break;
                    }
                }   
            
            }

            node.actionComponentID = node.hasComponent(typeof(TriggerActionComponent));


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

            Common.CallBacks.Verbose(string.Format("Trying to load Material {0}", matname));
            string matkey = matname; //Use the entire path


            try
            {
                mat = Material.Parse(matname, localTexMgr);
            } catch (Exception ex)
            {
                Common.CallBacks.Log(string.Format("Material {0} could not be loaded: {1}", matname, ex.Message));
                mat = null;
            }

            //File probably not found not even in the PAKS
            if (mat == null)
            {
                Common.CallBacks.Log(string.Format("Warning Material {0} Missing. Using an empty material", matname));
                //Generate an empty material. init() compiles its shader so the mesh can still be rendered
                mat = new Material();
                mat.texMgr = localTexMgr;
                mat.init();
            }
            
            //Load default form palette on init
            //mat.palette = Model_Viewer.Palettes.paletteSel;
            mat.name_key = matkey; //Store the material key to the resource manager
                                   //Store the material to the Resources

            return mat;
        }

        private static string decalNodePath(Model node)
        {
            Stack<string> parts = new Stack<string>();
            for (Model current = node; current != null; current = current.parent)
                parts.Push(current.name ?? "<unnamed>");
            return string.Join("/", parts);
        }

        private static string formatMatrix(Matrix4 m)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "[{0:0.######},{1:0.######},{2:0.######},{3:0.######};" +
                "{4:0.######},{5:0.######},{6:0.######},{7:0.######};" +
                "{8:0.######},{9:0.######},{10:0.######},{11:0.######};" +
                "{12:0.######},{13:0.######},{14:0.######},{15:0.######}]",
                m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24,
                m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44);
        }

        private static void logDecalImport(Mesh mesh, TkSceneNodeData node, string materialPath, Material material)
        {
            if (material == null || (!material.ShaderFlags.Contains("_F51_DECAL_DIFFUSE") &&
                                     !material.ShaderFlags.Contains("_F52_DECAL_NORMAL")))
                return;

            string attributes = string.Join(", ", (node.Attributes ?? new List<TkSceneNodeAttributeData>())
                .Select(a => a.Name + "=" + a.Value));
            string samplers = string.Join(", ", material.PSamplers.Values
                .OrderBy(s => s.PName).Select(s => s.PName + "=" + s.PMap));
            string uniforms = string.Join(", ", material.CustomPerMaterialUniforms.Values
                .OrderBy(u => u.PName).Select(u => string.Format(CultureInfo.InvariantCulture,
                    "{0}=({1:0.######},{2:0.######},{3:0.######},{4:0.######})",
                    u.PName, u.vec.X, u.vec.Y, u.vec.Z, u.vec.W)));

            CallBacks.Log("[DecalImport] scene={0}; node={1}; path={2}; material={3}; class={4}; flags={5}",
                mesh.parentScene?.name ?? "<none>", mesh.name, decalNodePath(mesh), materialPath,
                material.PClass, string.Join(",", material.ShaderFlags));
            CallBacks.Log("[DecalImport] transform local={0}; world={1}; lod={2}; hash={3}; batch={4}+{5}; vertices={6}..{7}; aabb=({8})..({9})",
                formatMatrix(mesh.localMat), formatMatrix(mesh.worldMat), mesh.metaData.LODLevel,
                mesh.metaData.Hash, mesh.metaData.batchstart_graphics, mesh.metaData.batchcount,
                mesh.metaData.vertrstart_graphics, mesh.metaData.vertrend_graphics,
                mesh.metaData.AABBMIN, mesh.metaData.AABBMAX);
            CallBacks.Log("[DecalImport] samplers: {0}", samplers);
            CallBacks.Log("[DecalImport] uniforms: {0}", uniforms);
            CallBacks.Log("[DecalImport] node attributes: {0}", attributes);
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

            CallBacks.Verbose(string.Format("Randomized Object Color {0}, {1}, {2}", so.color[0], so.color[1], so.color[2]));
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

            Common.CallBacks.Verbose(string.Format("Batch Physics Start {0} Count {1} Vertex Physics {2} - {3} Vertex Graphics {4} - {5} SkinMats {6}-{7}",
                so.metaData.batchstart_physics, so.metaData.batchcount, so.metaData.vertrstart_physics,
                so.metaData.vertrend_physics, so.metaData.vertrstart_graphics, so.metaData.vertrend_graphics,
                so.metaData.firstskinmat, so.metaData.lastskinmat));

            //For now fetch only one attachment
            string attachment = parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "ATTACHMENT");
            TkAttachmentData attachment_data = null;
            if (attachment != "")
            {
                attachment_data = loadAttachment(so, attachment);
            }

            //so.Bbox = gobject.bboxes[iid]; //Use scene parameters
            //so.setupBSphere();
            so.parent = parent;
            so.gobject = gobject; //Store the gobject for easier access of uniforms
            so.init(transforms); //Init object transforms

            //PASS AABB info to the main object, The information in the metadata of the mesh regarding AABB SHOULD NOT BE TOUCHED
            so.AABBMIN = so.metaData.AABBMIN;
            so.AABBMAX = so.metaData.AABBMAX;

            //Only names with an actual LOD number participate in a replacement group. Ordinary LOD0
            //metadata is common on meshes that must remain visible at every scene LOD.
            int lodMarker = so.name.IndexOf("LOD", StringComparison.OrdinalIgnoreCase);
            if (lodMarker >= 0 && lodMarker + 3 < so.name.Length && char.IsDigit(so.name[lodMarker + 3]))
            {
                so.hasLOD = true;
                so.metaData.LODLevel = so.name[lodMarker + 3] - '0';
                so.lodGroupKey = so.name.Substring(0, lodMarker + 3) + "#" +
                                 so.name.Substring(lodMarker + 4);
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

            logDecalImport(so, node, matname, mat);

            Common.CallBacks.Verbose("Object {0}, Number of skinmatrices required: {1}", so.name, so.metaData.lastskinmat - so.metaData.firstskinmat);

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
                    attachment_data = loadAttachment(so, attachment);
                }
                catch (Exception e)
                {
                    attachment_data = null;
                }
            }

            if (node.Attributes.Count > 1)
                CallBacks.showError("DM THE IDIOT TO ADD SUPPORT FOR FUCKING MULTIPLE ATTACHMENTS...", "DM THE IDIOT");

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
                    attachment_data = loadAttachment(so, attachment);
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

            Common.CallBacks.Verbose(string.Format("Collision Detected {0} {1}", node.Name, collisionType));

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
                Common.CallBacks.Verbose(string.Format("Cylinder Collision r:{0} h:{1}", radius, height));

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
                Common.CallBacks.Verbose(string.Format("Sphere Collision w:{0} h:{0} d:{0}", width, height, depth));
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
                Common.CallBacks.Verbose(string.Format("Capsule Collision r:{0} h:{1}", radius, height));
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
                Common.CallBacks.Verbose(string.Format("Sphere Collision r:{0}", radius));
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

            Common.CallBacks.Verbose(string.Format("Batch Start {0} Count {1} ",
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
            so.falloff = parseLightFalloff(attenuation, so.Name);
            if (float.TryParse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "RADIUS"),
                System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float radius))
                so.radius = radius;
            if (int.TryParse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "COOKIE_IDX"), out int cookie))
                so.cookieIndex = cookie;
            if (float.TryParse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "VOLUMETRIC"),
                System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float volumetric))
                so.volumetric = volumetric;
            if (int.TryParse(parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "LIGHTLAYERS"), out int layers))
                so.lightLayers = layers;
            
            //Add Light to the resource Manager
            so.update_struct();
            Common.RenderState.activeResMgr.GLlights.Add(so);

            scene.nodeDict[so.Name] = so;

            return so;

        }

        //FALLOFF is either a name ("quadratic", "linear", ...) or, in newer files, a number that is the
        //distance exponent (2 = quadratic, 1 = linear, 0.5 = square root, 0 = constant).
        //An unknown value falls back to quadratic instead of failing the whole scene
        private static ATTENUATION_TYPE parseLightFalloff(string value, string lightName)
        {
            string v = value.Trim();

            if (float.TryParse(v, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float exponent))
            {
                if (exponent < 0.25f)
                    return ATTENUATION_TYPE.CONSTANT;
                if (exponent < 0.75f)
                    return ATTENUATION_TYPE.LINEAR_SQRT;
                if (exponent < 1.5f)
                    return ATTENUATION_TYPE.LINEAR;
                return ATTENUATION_TYPE.QUADRATIC;
            }

            if (Enum.TryParse(v, true, out ATTENUATION_TYPE type) && type != ATTENUATION_TYPE.COUNT)
                return type;

            Common.CallBacks.Log("Light " + lightName + ": falloff \"" + value + "\" not recognized, using quadratic");
            return ATTENUATION_TYPE.QUADRATIC;
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
            Common.CallBacks.Verbose("Reference Detected");

            string scene_ref = node.Attributes.FirstOrDefault(item => item.Name == "SCENEGRAPH").Value;
            Common.CallBacks.Verbose(string.Format("Loading Reference {0}", scene_ref));

            //Getting Scene MBIN file
            //string mxmlPath = Path.GetFullPath(Util.getFullMxmlPath(path));
            //Common.CallBacks.Log("Loading Scene " + path);
            //Parse MBIN to xml

            //Generate Reference object
            Reference so = new Reference(scene_ref);
            so.name = node.Name;
            so.nameHash = node.NameHash;

            //Get Transformation
            so.parent = parent;
            so.init(transforms);

            Scene new_so = null;
            
            //Check if scene has been parsed
            if (Common.RenderState.settings.LoadReferences)
            {
                if (Common.RenderState.activeResMgr.GLScenes.ContainsKey(scene_ref))
                {
                    //Make a shallow copy of the scene
                    new_so = (Scene)RenderState.activeResMgr.GLScenes[scene_ref].Clone();
                }
                else if (!NMSUtils.NMSFileExists(scene_ref, Common.RenderState.activeResMgr))
                {
                    Common.CallBacks.Log("Referenced scene " + scene_ref + " not found");
                }
                else {
                    try
                    {
                        new_so = LoadObjects(scene_ref);
                    }
                    catch (Exception ex)
                    {
                        //A broken reference should not abort the whole import
                        Common.CallBacks.Log("Failed to load reference " + scene_ref + ": " + ex.Message);
                        new_so = null;
                    }
                }

                if (new_so != null)
                {
                    so.AddChild(new_so);
                    so.isRefLoaded = true;
                }
            }

            //Handle Children
            //Common.CallBacks.Log("Children Count {0}", childs.ChildNodes.Count);
            foreach (TkSceneNodeData child in children)
            {
                Model part = parseNode(child, gobject, so, scene);
                so.AddChild(part);
            }

            scene.nodeDict[so.name] = so; //Reference.Name carries the (Loaded) tag

            return so;

        }

        private static ParticleEmitter parseEmitter(TkSceneNodeData node,
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
            ParticleEmitter so = new ParticleEmitter();
            so.parentScene = scene;
            //Fetch attributes

            //For now fetch only one attachment
            string attachment = parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "ATTACHMENT");
            TkAttachmentData attachment_data = null;
            if (attachment != "")
            {
                attachment_data = loadAttachment(so, attachment);
            }

            //The runtime scene keeps the material and an authoring-time particle-data reference. The
            //.particle.mXml itself is not present in the shipped PAKs, so the particle system infers a
            //bounded profile while retaining these paths for future exact overrides.
            so.MaterialPath = parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "MATERIAL");
            so.DataPath = parseNMSTemplateAttrib<TkSceneNodeAttributeData>(node.Attributes, "DATA");
            if (!string.IsNullOrEmpty(so.MaterialPath))
            {
                if (!Common.RenderState.activeResMgr.GLmaterials.TryGetValue(so.MaterialPath, out Material material))
                {
                    material = parseMaterial(so.MaterialPath);
                    Common.RenderState.activeResMgr.addMaterial(material);
                }
                so.ParticleMaterial = material;
            }


            //Set Properties
            //Testingso.Name = name + "_LOC";
            so.name = node.Name;
            so.nameHash = node.NameHash;

            //Get Transformation
            so.parent = parent;
            so.init(transforms);

            string samplerSummary = so.ParticleMaterial == null ? "<none>" : string.Join(", ",
                so.ParticleMaterial.PSamplers.Values.OrderBy(s => s.PName).Select(s => s.PName + "=" + s.PMap));
            CallBacks.Log("[EmitterImport] scene={0}; node={1}; path={2}; material={3}; data={4}; class={5}; samplers={6}",
                scene?.name ?? "<none>", so.name, decalNodePath(so), so.MaterialPath, so.DataPath,
                so.ParticleMaterial?.PClass ?? "<none>", samplerSummary);

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
            Model m = parseNodeType(node, gobject, parent, scene);
            m.IsSceneNode = true;
            foreach (TkSceneNodeAttributeData attribute in node.Attributes ?? new List<TkSceneNodeAttributeData>())
            {
                string name = attribute.Name?.Value ?? "";
                if (name != "")
                    m.SourceAttributes[name] = attribute.Value?.Value ?? "";
            }
            removeDerivedSourceAttributes(m);
            return m;
        }

        private static void removeDerivedSourceAttributes(Model model)
        {
            void remove(params string[] names)
            {
                foreach (string name in names)
                    model.SourceAttributes.Remove(name);
            }

            remove("ATTACHMENT");
            if (model is Scene)
            {
                remove("GEOMETRY", "NUMLODS", "LODDIST1", "LODDIST2", "LODDIST3", "LODDIST4", "LODDIST5");
            }
            if (model is Mesh)
            {
                remove("MATERIAL", "BATCHSTARTPHYSI", "VERTRSTARTPHYSI", "VERTRENDPHYSICS",
                    "BATCHSTARTGRAPH", "BATCHCOUNT", "VERTRSTARTGRAPH", "VERTRENDGRAPHIC",
                    "FIRSTSKINMAT", "LASTSKINMAT", "LODLEVEL", "BOUNDHULLST", "BOUNDHULLED",
                    "AABBMINX", "AABBMINY", "AABBMINZ", "AABBMAXX", "AABBMAXY", "AABBMAXZ", "HASH");
            }
            if (model is Joint)
                remove("JOINTINDEX");
            if (model is Light)
                remove("COL_R", "COL_G", "COL_B", "FOV", "FALLOFF", "INTENSITY", "RADIUS",
                    "COOKIE_IDX", "VOLUMETRIC", "LIGHTLAYERS");
            if (model is Reference)
                remove("SCENEGRAPH");
            if (model is ParticleEmitter)
                remove("DATA", "MATERIAL");
        }

        private static Model parseNodeType(TkSceneNodeData node, 
            GeomObject gobject, Model parent, Scene scene)
        {
            Common.CallBacks.Verbose(string.Format("Importing Scene {0} Node {1}", scene?.name, node.Name));

            TYPES typeEnum;
            if (!Enum.TryParse<TYPES>(node.Type, out typeEnum))
                ErrorUtils.throwException("Node Type " + node.Type + " Not supported");
            
            if (typeEnum == TYPES.MESH)
            {
                Common.CallBacks.Verbose(string.Format("Parsing Mesh {0}", node.Name));
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
                Common.CallBacks.Verbose(string.Format("Parsing Light, {0}", node.Name));
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
