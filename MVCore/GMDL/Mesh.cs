using System;
using System.Collections.Generic;
using System.Text;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;
using GLSLHelper;
using System.IO;
using MVCore.Common;
using MVCore.Utils;
using libMBIN.NMS.Toolkit;
using System.Linq;
using System.Globalization;

namespace MVCore.GMDL
{
    //Remembers the texture bound on each unit so draws that share textures skip the GL calls.
    //Only valid while nothing else binds textures, so every mesh pass starts with reset()
    public static class TextureBindCache
    {
        private static readonly int[] bound = new int[32];

        public static void bind(int unit, TextureTarget target, int id)
        {
            if (bound[unit] == id)
                return;
            bound[unit] = id;
            GL.ActiveTexture(TextureUnit.Texture0 + unit);
            GL.BindTexture(target, id);
        }

        public static void reset()
        {
            Array.Clear(bound);
        }
    }


    public class GLVao : IDisposable
    {
        //VAO ID
        public int vao_id;
        //VBO IDs
        public int vertex_pos_object;
        public int vertex_buffer_object;
        public int element_buffer_object;
        public int vertex_normal_object; //Optional, normals generated on load

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        public GLVao()
        {
            vao_id = -1;
            vertex_buffer_object = -1;
            vertex_pos_object = -1;
            element_buffer_object = -1;
            vertex_normal_object = -1;
        }

        public void Dispose()
        {
            Dispose(true);
#if DEBUG
            GC.SuppressFinalize(this);
#endif
        }

        protected void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    if (vao_id > 0)
                    {
                        GL.DeleteVertexArray(vao_id);
                        GL.DeleteBuffer(vertex_buffer_object);
                        GL.DeleteBuffer(vertex_pos_object);
                        GL.DeleteBuffer(element_buffer_object);
                        if (vertex_normal_object > 0)
                            GL.DeleteBuffer(vertex_normal_object);
                    }

                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~GLVao()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        void IDisposable.Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }


    public class MeshMetaData
    {
        //Mesh Properties
        public int vertrstart_physics;
        public int vertrend_physics;
        public int vertrstart_graphics;
        public int vertrend_graphics;
        public int batchstart_physics;
        public int batchstart_graphics;
        public int batchcount;
        public int firstskinmat;
        public int lastskinmat;
        //LOD Properties
        public int LODLevel;
        public int LODDistance1;
        public int LODDistance2;
        //New stuff Properties
        public DrawElementsType indicesLength = DrawElementsType.UnsignedShort;
        public int boundhullstart;
        public int boundhullend;
        public Vector3 AABBMIN;
        public Vector3 AABBMAX;
        public ulong Hash;

        public MeshMetaData()
        {
            //Init values to null
            vertrend_graphics = 0;
            vertrstart_graphics = 0;
            vertrend_physics = 0;
            vertrstart_physics = 0;
            batchstart_graphics = 0;
            batchstart_physics = 0;
            batchcount = 0;
            firstskinmat = 0;
            lastskinmat = 0;
            boundhullstart = 0;
            boundhullend = 0;
            Hash = 0xFFFFFFFF;
            AABBMIN = new Vector3();
            AABBMAX = new Vector3();
        }

        public MeshMetaData(MeshMetaData input)
        {
            //Init values to null
            vertrend_graphics = input.vertrend_graphics;
            vertrstart_graphics = input.vertrstart_graphics;
            vertrend_physics = input.vertrend_physics;
            vertrstart_physics = input.vertrstart_physics;
            batchstart_graphics = input.batchstart_graphics;
            batchstart_physics = input.batchstart_physics;
            batchcount = input.batchcount;
            firstskinmat = input.firstskinmat;
            lastskinmat = input.lastskinmat;
            boundhullstart = input.boundhullstart;
            boundhullend = input.boundhullend;
            Hash = input.Hash;
            LODLevel = input.LODLevel;
            indicesLength = input.indicesLength;
            AABBMIN = new Vector3(input.AABBMIN);
            AABBMAX = new Vector3(input.AABBMAX);
        }
    }



    public class GLMeshVao : IDisposable
    {
        public GLVao vao;
        public GLVao bHullVao;
        public MeshMetaData metaData;
        public float[] dataBuffer = new float[GLMeshBufferManager.instance_struct_size_floats];

        //Mesh type
        public COLLISIONTYPES collisionType;
        public TYPES type;

        //Instance Data
        public int UBO_aligned_size = 0; //Actual size of the data for the UBO, multiple to 256
        public int UBO_offset = 0; //Offset 
        public int visibleIds_aligned_size = 0;
        public int visibleIds_offset = 0;

        //Persistent instance records have stable indices. The draw count and the compact list below
        //change with visibility/LOD, but transforms, selection and skinning data do not move.
        public int stable_instance_count = 0;
        public int instance_count = 0; //Visible instances drawn this frame
        public List<Model> instanceRefs = new List<Model>();
        public Dictionary<Model, int> stableInstanceIds = new Dictionary<Model, int>();
        public int[] visibleInstanceIds = new int[4];
        //Each persistent ring slot remembers the last version of every stable instance it contains.
        //A moving instance therefore updates only its own record, never the whole mesh batch.
        public long instanceVersionCounter = 1;
        public long[] instanceVersions = new long[1];
        public long[][] uploadedInstanceVersions =
        {
            new long[1], new long[1], new long[1], new long[1]
        };
        public int[] stableUploadOffsets = { -1, -1, -1, -1 };
        public int[] stableUploadGenerations = new int[4];
        public int[] stableUploadCounts = new int[4];

        //The compact visible list is versioned independently. A stationary camera reuses the copy already
        //resident in each ring slot instead of uploading the same integer IDs every frame.
        public int visibleListVersion = 1;
        public int[] visibleListSnapshot = new int[4];
        public int visibleListSnapshotCount = -1;
        public int[] uploadedVisibleListVersions = new int[4];
        public int[] uploadedVisibleOffsets = { -1, -1, -1, -1 };
        public int[] uploadedVisibleGenerations = new int[4];
        public float[] instanceBoneMatrices;
        private int instanceBoneMatricesTex;
        private int instanceBoneMatricesTexTBO;

        //Animation Properties
        //TODO : At some point include that shit into the instance data
        public int BoneRemapIndicesCount;
        public int[] BoneRemapIndices;
        //public float[] BoneRemapMatrices = new float[16 * 128];
        public bool skinned = false;


        //Material Properties
        public Material material;
        public Vector3 color;

        //Uniform and sampler locations of the material for one shader program, resolved once
        //instead of looking every name up on every draw
        private class MaterialBinding
        {
            public Material material;
            public Uniform[] uniforms;
            public int[] uniformLocations;
            public Sampler[] samplers;
            public int[] samplerLocations;
            public int[] samplerUnits;
            public int linkVersion;
        }
        private Dictionary<int, MaterialBinding> materialBindings = new Dictionary<int, MaterialBinding>();

        private MaterialBinding getMaterialBinding(GLSLShaderConfig shader)
        {
            if (materialBindings.TryGetValue(shader.program_id, out MaterialBinding b) && b.material == material &&
                b.linkVersion == shader.linkVersion)
                return b;

            b = new MaterialBinding() { material = material, linkVersion = shader.linkVersion };

            List<Uniform> uniforms = new List<Uniform>();
            List<int> uniformLocations = new List<int>();
            foreach (Uniform un in material.CustomPerMaterialUniforms.Values)
            {
                if (shader.uniformLocations.TryGetValue(un.Name, out int loc))
                {
                    uniforms.Add(un);
                    uniformLocations.Add(loc);
                }
            }

            List<Sampler> samplers = new List<Sampler>();
            List<int> samplerLocations = new List<int>();
            List<int> samplerUnits = new List<int>();
            foreach (Sampler s in material.PSamplers.Values)
            {
                if (shader.uniformLocations.TryGetValue(s.Name, out int loc) &&
                    MyTextureUnit.MapTexUnitToSampler.TryGetValue(s.Name, out int unit))
                {
                    samplers.Add(s);
                    samplerLocations.Add(loc);
                    samplerUnits.Add(unit);
                }
            }

            b.uniforms = uniforms.ToArray();
            b.uniformLocations = uniformLocations.ToArray();
            b.samplers = samplers.ToArray();
            b.samplerLocations = samplerLocations.ToArray();
            b.samplerUnits = samplerUnits.ToArray();
            materialBindings[shader.program_id] = b;

            //A sampler keeps its unit inside the program and the unit only depends on the sampler name,
            //so set it here once instead of on every draw
            for (int i = 0; i < b.samplerLocations.Length; i++)
                GL.ProgramUniform1(shader.program_id, b.samplerLocations[i], b.samplerUnits[i]);
            if (shader.uniformLocations.TryGetValue("mpCustomPerMaterial.skinMatsTex", out int skinLoc))
                GL.ProgramUniform1(shader.program_id, skinLoc, 6);

            return b;
        }



        //Constructor
        public GLMeshVao()
        {
            vao = new GLVao();
        }

        public GLMeshVao(MeshMetaData data)
        {
            vao = new GLVao();
            metaData = new MeshMetaData(data);
        }


        private void renderMesh()
        {
            GL.BindVertexArray(vao.vao_id);
            GL.DrawElementsInstanced(PrimitiveType.Triangles, 
                metaData.batchcount, metaData.indicesLength, IntPtr.Zero, instance_count);
            //GL.BindVertexArray(0);
        }

        private void renderLight()
        {
            GL.BindVertexArray(vao.vao_id);
            GL.PointSize(5.0f);
            GL.DrawArraysInstanced(PrimitiveType.Lines, 0, 2, instance_count);
            GL.DrawArraysInstanced(PrimitiveType.Points, 0, 2, instance_count); //Draw both points
            //GL.BindVertexArray(0);
        }

        private void renderCollision()
        {
            //Step 2: Render Elements
            GL.PointSize(8.0f);
            GL.BindVertexArray(vao.vao_id);

            switch (collisionType)
            {
                //Rendering based on the original mesh buffers
                case COLLISIONTYPES.MESH:
                    GL.DrawElementsInstancedBaseVertex(PrimitiveType.Points, metaData.batchcount,
                        metaData.indicesLength, IntPtr.Zero, instance_count, -metaData.vertrstart_physics);
                    GL.DrawElementsInstancedBaseVertex(PrimitiveType.Triangles, metaData.batchcount,
                        metaData.indicesLength, IntPtr.Zero, instance_count, -metaData.vertrstart_physics);
                    break;

                //Rendering custom geometry
                case COLLISIONTYPES.BOX:
                case COLLISIONTYPES.CYLINDER:
                case COLLISIONTYPES.CAPSULE:
                case COLLISIONTYPES.SPHERE:
                    GL.DrawElementsInstanced(PrimitiveType.Points, metaData.batchcount,
                        DrawElementsType.UnsignedInt, IntPtr.Zero, instance_count);
                    GL.DrawElementsInstanced(PrimitiveType.Triangles, metaData.batchcount,
                        DrawElementsType.UnsignedInt, IntPtr.Zero, instance_count);
                    break;
            }

            //GL.BindVertexArray(0);
        }

        //Wide lines work here because the context uses the compatibility profile. Clamped to what the driver supports
        private const float LOCATOR_LINE_WIDTH = 2.0f;
        private static float locatorLineWidth = -1.0f;

        private void renderLocator()
        {
            if (locatorLineWidth < 0.0f)
            {
                float[] range = new float[2];
                GL.GetFloat(GetPName.AliasedLineWidthRange, range);
                locatorLineWidth = Math.Clamp(LOCATOR_LINE_WIDTH, range[0], Math.Max(range[1], 1.0f));
            }

            GL.LineWidth(locatorLineWidth);
            GL.BindVertexArray(vao.vao_id);
            GL.DrawElementsInstanced(PrimitiveType.Lines, metaData.batchcount > 0 ? metaData.batchcount : 6,
                metaData.indicesLength, IntPtr.Zero, instance_count); //Use Instancing
            GL.LineWidth(1.0f);
            //GL.BindVertexArray(0);
        }

        private void renderJoint()
        {
            GL.BindVertexArray(vao.vao_id);
            GL.PointSize(5.0f);
            GL.DrawArrays(PrimitiveType.Lines, 0, metaData.batchcount);
            GL.DrawArrays(PrimitiveType.Points, 0, 1); //Draw only yourself
            //GL.BindVertexArray(0);
        }

        public void renderMain(GLSLShaderConfig shader)
        {
            //Upload Material Information

            MaterialBinding binding = getMaterialBinding(shader);

            //Upload Custom Per Material Uniforms
            for (int i = 0; i < binding.uniforms.Length; i++)
                GL.Uniform4(binding.uniformLocations[i], binding.uniforms[i].vec.vec4);

            //BIND TEXTURES
            for (int i = 0; i < binding.samplers.Length; i++)
            {
                Sampler s = binding.samplers[i];
                if (s.Map != "")
                    TextureBindCache.bind(binding.samplerUnits[i], s.tex.target, s.tex.texID);
            }

            //BIND TEXTURE Buffer
            if (skinned)
            {
                TextureBindCache.bind(6, TextureTarget.TextureBuffer, instanceBoneMatricesTex); //Buffer attached once at setup
                if (shader.uniformLocations.TryGetValue("mpCustomPerMaterial.skinMatsStride", out int strideLoc))
                    GL.Uniform1(strideLoc, skinMatsStride / 16);
            }

            //if (instance_count > 100)
            //    Common.CallBacks.Log("Increase the buffers");

            switch (type)
            {
                case TYPES.GIZMO:
                case TYPES.GIZMOPART:
                case TYPES.MESH:
                case TYPES.TEXT:
                    renderMesh();
                    break; 
                case TYPES.LOCATOR:
                case TYPES.MODEL:
                    renderLocator();
                    break;
                case TYPES.JOINT:
                    renderJoint();
                    break;
                case TYPES.COLLISION:
                    renderCollision();
                    break;
                case TYPES.LIGHT:
                    renderLight();
                    break;
            }
        }

        private void renderBHull(GLSLShaderConfig shader)
        {
            if (bHullVao == null) return;
            //I ASSUME THAT EVERYTHING I NEED IS ALREADY UPLODED FROM A PREVIOUS PASS
            GL.PointSize(8.0f);
            GL.BindVertexArray(bHullVao.vao_id);

            GL.DrawElementsBaseVertex(PrimitiveType.Points, metaData.batchcount,
                        metaData.indicesLength, IntPtr.Zero, -metaData.vertrstart_physics);
            GL.DrawElementsBaseVertex(PrimitiveType.Triangles, metaData.batchcount,
                        metaData.indicesLength, IntPtr.Zero, -metaData.vertrstart_physics);
            GL.BindVertexArray(0);
        }

        //Default render method
        public bool render(GLSLShaderConfig shader, RENDERPASS pass)
        {
            //Render Object
            switch (pass)
            {
                //Render Main
                case RENDERPASS.DEFERRED:
                case RENDERPASS.FORWARD:
                case RENDERPASS.DECAL:
                    renderMain(shader);
                    break;
                case RENDERPASS.BHULL:
                    renderBHull(shader);
                    break;
                default:
                    //Do nothing in any other case
                    break;
            }

            return true;
        }



        //Bounds of the skinned mesh in scene space. Each vertex is a weighted blend of its bind position moved
        //by the bones it uses, so the union of the AABB moved by every bone the mesh uses contains it.
        public bool getSkinnedAABB(float[] skinMats, out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue);
            max = new Vector3(float.MinValue);
            if (skinMats == null || BoneRemapIndices == null)
                return false;

            Vector3 center = 0.5f * (metaData.AABBMIN + metaData.AABBMAX);
            Vector3 extent = 0.5f * (metaData.AABBMAX - metaData.AABBMIN);
            bool found = false;

            for (int i = 0; i < BoneRemapIndicesCount; i++)
            {
                int o = BoneRemapIndices[i] * 16;
                if (o < 0 || o + 16 > skinMats.Length)
                    continue;

                //Matrices are stored row major and used as row vectors (v * M)
                Vector3 c = new Vector3(
                    center.X * skinMats[o + 0] + center.Y * skinMats[o + 4] + center.Z * skinMats[o + 8] + skinMats[o + 12],
                    center.X * skinMats[o + 1] + center.Y * skinMats[o + 5] + center.Z * skinMats[o + 9] + skinMats[o + 13],
                    center.X * skinMats[o + 2] + center.Y * skinMats[o + 6] + center.Z * skinMats[o + 10] + skinMats[o + 14]);
                Vector3 e = new Vector3(
                    extent.X * Math.Abs(skinMats[o + 0]) + extent.Y * Math.Abs(skinMats[o + 4]) + extent.Z * Math.Abs(skinMats[o + 8]),
                    extent.X * Math.Abs(skinMats[o + 1]) + extent.Y * Math.Abs(skinMats[o + 5]) + extent.Z * Math.Abs(skinMats[o + 9]),
                    extent.X * Math.Abs(skinMats[o + 2]) + extent.Y * Math.Abs(skinMats[o + 6]) + extent.Z * Math.Abs(skinMats[o + 10]));

                min = Vector3.ComponentMin(min, c - e);
                max = Vector3.ComponentMax(max, c + e);
                found = true;
            }

            return found;
        }

        //Floats per instance in the bone matrix buffer. The shader gets the same stride in bones through skinMatsStride
        public int skinMatsStride
        {
            get { return Math.Max(BoneRemapIndicesCount, 1) * 16; }
        }

        //Grows the bone matrix array when more instances need it. The texture buffer follows on the next
        //upload, which reallocates it at the array's size
        private void ensureSkinCapacity(int instances)
        {
            int needed = instances * skinMatsStride;
            int oldLength = instanceBoneMatrices?.Length ?? 0;
            if (oldLength >= needed)
                return;

            Array.Resize(ref instanceBoneMatrices, Math.Max(needed, oldLength * 2));
            for (int o = oldLength; o + 16 <= instanceBoneMatrices.Length; o += 16)
                MathUtils.insertMatToArray16(instanceBoneMatrices, o, Matrix4.Identity);
        }

        public void setSkinMatrices(Scene animScene, int instance_id)
        {
            ensureSkinCapacity(instance_id + 1);
            int instance_offset = skinMatsStride * instance_id;

            for (int i = 0; i < BoneRemapIndicesCount; i++)
            {
                Array.Copy(animScene.skinMats, BoneRemapIndices[i] * 16, instanceBoneMatrices, instance_offset + i * 16, 16);
            }
        }

        public void setDefaultSkinMatrices(int instance_id)
        {
            ensureSkinCapacity(instance_id + 1);
            int instance_offset = skinMatsStride * instance_id;
            for (int i = 0; i < BoneRemapIndicesCount; i++)
            {
                MathUtils.insertMatToArray16(instanceBoneMatrices, instance_offset + i * 16, Matrix4.Identity);
            }

        }

        public void initializeSkinMatrices(Scene animScene)
        {
            if (animScene == null)
                return;

            //Identity matrices for the instances known so far, more are added on demand
            ensureSkinCapacity(Math.Max(stable_instance_count, 1));

            //Setup the TBO once, later calls reuse it
            if (instanceBoneMatricesTex == 0)
            {
                instanceBoneMatricesTex = GL.GenTexture();
                instanceBoneMatricesTexTBO = GL.GenBuffer();

                GL.BindBuffer(BufferTarget.TextureBuffer, instanceBoneMatricesTexTBO);
                GL.BufferData(BufferTarget.TextureBuffer, instanceBoneMatrices.Length * 4, instanceBoneMatrices, BufferUsageHint.StreamDraw);
                GL.BindBuffer(BufferTarget.TextureBuffer, 0);

                //Attach the buffer to the texture once. TexBuffer acts on the bound texture, so bind it first
                GL.BindTexture(TextureTarget.TextureBuffer, instanceBoneMatricesTex);
                GL.TexBuffer(TextureBufferTarget.TextureBuffer, SizedInternalFormat.Rgba32f, instanceBoneMatricesTexTBO);
                GL.BindTexture(TextureTarget.TextureBuffer, 0);
            }

        }

        public void uploadSkinningData()
        {
            if (instanceBoneMatrices == null || BoneRemapIndicesCount == 0 || instanceBoneMatricesTexTBO == 0)
                return;

            int instances = Math.Min(stable_instance_count, instanceBoneMatrices.Length / skinMatsStride);

            GL.BindBuffer(BufferTarget.TextureBuffer, instanceBoneMatricesTexTBO);

            //Orphan the buffer so the driver hands out fresh storage instead of waiting for the GPU
            //to finish drawing with last frame's matrices. This also grows it with the array
            GL.BufferData(BufferTarget.TextureBuffer, instanceBoneMatrices.Length * 4, IntPtr.Zero, BufferUsageHint.StreamDraw);
            GL.BufferSubData(BufferTarget.TextureBuffer, IntPtr.Zero, instances * skinMatsStride * 4, instanceBoneMatrices);

            GL.BindBuffer(BufferTarget.TextureBuffer, 0);
        }


        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls







        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    BoneRemapIndices = null;
                    instanceBoneMatrices = null;

                    vao?.Dispose();

                    if (instanceBoneMatricesTex > 0)
                    {
                        GL.DeleteTexture(instanceBoneMatricesTex);
                        GL.DeleteBuffer(instanceBoneMatricesTexTBO);
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~mainGLVao()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

    }




    public class Mesh : Model
    {
        //The mesh material was read from a material file, so it can be exported
        public bool HasMaterialFile => meshVao?.material?.fromFile == true;

        public int LodLevel
        {
            get
            {
                return metaData.LODLevel;
            }

        }

        public ulong Hash
        {
            get
            {
                return metaData.Hash;
            }
        }

        public MeshMetaData metaData = new MeshMetaData();
        public Vector3 color = new Vector3(); //Per instance
        public bool hasLOD = false;
        //Meshes named as members of the same FooLOD0/FooLOD1/... object share this key and mask.
        //A scene may contain many independent objects with different available LOD ranges.
        public string lodGroupKey = null;
        public uint availableLODMask = 0;
        public bool Skinned
        {
            get
            {
                if (meshVao.material != null)
                {
                    return meshVao.material.has_flag(TkMaterialFlags.MaterialFlagEnum._F02_SKINNED);
                }
                return false;
            }
        }

        public GLVao bHull_Vao;
        public GeomObject gobject; //Ref to the geometry shit

        public Material material
        {
            get
            {
                return meshVao.material;
            }
        }

        private static List<string> supportedCommonPerMeshUniforms = new List<string>() { "gUserDataVec4" };

        private Dictionary<string, Uniform> _CommonPerMeshUniforms = new Dictionary<string, Uniform>();

        public Dictionary<string, Uniform> CommonPerMeshUniforms
        {
            get
            {
                return _CommonPerMeshUniforms;
            }
        }

        //Constructor
        public Mesh() : base()
        {
            type = TYPES.MESH;
            metaData = new MeshMetaData();

            //Init MeshModel Uniforms
            foreach (string un in supportedCommonPerMeshUniforms)
            {
                Uniform my_un = new Uniform(un);
                _CommonPerMeshUniforms[my_un.Name] = my_un;
            }
        }

        private void copyFrom(Mesh input)
        {
            //Copy attributes
            metaData = new MeshMetaData(input.metaData);
            hasLOD = input.hasLOD;
            lodGroupKey = input.lodGroupKey;
            availableLODMask = input.availableLODMask;

            //Copy Vao Refs
            meshVao = input.meshVao;

            //Material Stuff
            color = input.color;

            palette = input.palette;
            gobject = input.gobject;

            base.copyFrom(input);
        }

        public override Model Clone()
        {
            Mesh new_m = new Mesh();
            new_m.copyFrom(this);
            new_m.instanceId = GLMeshBufferManager.addInstance(ref new_m.meshVao, new_m);

            //Clone children
            foreach (Model child in Children)
            {
                Model new_child = child.Clone();
                new_m.AddChild(new_child);
            }

            return new_m;
        }

        public override void update()
        {
            base.update();
            recalculateAABB(); //Update AABB
        }

        public override void setupSkinMatrixArrays()
        {
            meshVao?.initializeSkinMatrices(parentScene);

            base.setupSkinMatrixArrays();

        }

        protected override bool hideDescendants(bool lod_filter)
        {
            return lod_filter || !active || !renderable;
        }

        public override void updateOwnMeshInfo(bool lod_filter = false)
        {

#if(DEBUG)
            if (instanceId < 0)
                Common.CallBacks.Log("test");
#endif

            if (lod_filter || !active || !renderable)
                return;

            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            RenderStats.meshCount++;

            //Each logical object selects the closest level it actually contains. Non-LOD meshes remain
            //visible at every scene LOD; a FooLOD0-only object therefore does not disappear at LOD1/2.
            int effectiveLOD = closestAvailableLOD(parentScene?.activeLOD ?? 0);
            if (hasLOD && LodLevel != effectiveLOD)
            {
                RenderStats.lodFilteredNum += 1;
                RenderStats.meshTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
                return;
            }

            bool occluded_status = false;

            if (RenderState.renderSettings.UseFrustumCulling)
            {
                if (Skinned)
                {
                    //Skinned vertices are placed by the bone matrices, not by worldMat, so cull the animated bounds
                    if (meshVao.getSkinnedAABB(parentScene?.skinMats, out Vector3 skinMin, out Vector3 skinMax))
                        occluded_status = !RenderState.activeCam.frustum_occlude(skinMin, skinMax, RenderState.rotMat);
                }
                else
                    occluded_status = !RenderState.activeCam.frustum_occlude(meshVao, worldMat * RenderState.rotMat);
            }

        
            //Recalculations && Data uploads
            if (!occluded_status)
            {
                instanceId = GLMeshBufferManager.addInstance(ref meshVao, this);

                //Upload commonperMeshUniforms
                GLMeshBufferManager.setInstanceUniform4(meshVao, instanceId,
                    "gUserDataVec4", CommonPerMeshUniforms["gUserDataVec4"].Vec.Vec);

                if (colouriseColours != null && colouriseColours.Length >= 4)
                {
                    GLMeshBufferManager.setInstanceUniform4(meshVao, instanceId, "gColourisePrimary", colouriseColours[0]);
                    GLMeshBufferManager.setInstanceUniform4(meshVao, instanceId, "gColouriseSecondary", colouriseColours[1]);
                    GLMeshBufferManager.setInstanceUniform4(meshVao, instanceId, "gColouriseTernary", colouriseColours[2]);
                    GLMeshBufferManager.setInstanceUniform4(meshVao, instanceId, "gColouriseQuaternary", colouriseColours[3]);
                }

                //Selected in the scene tree: the shaders tint it green
                GLMeshBufferManager.setInstanceSelectedStatus(meshVao, instanceId, selected > 0);

                if (Skinned)
                {
                    //Update the mesh remap matrices and continue with the transform updates
                    meshVao.setSkinMatrices(parentScene, instanceId);
                    //Fallback
                    //main_Vao.setDefaultSkinMatrices();
                }
            }
            else
            {
                RenderStats.occludedNum += 1;
                //TODO: Check if we need to return at this point, because the children meshes might be occluded as well
            }

            RenderStats.meshTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
        }

        private int closestAvailableLOD(int requested)
        {
            uint available = availableLODMask;
            if (available == 0)
                return LodLevel;
            requested = Math.Clamp(requested, 0, 31);
            if ((available & (1u << requested)) != 0)
                return requested;

            //On an equal-distance tie, retain the more detailed (lower-numbered) level.
            for (int delta = 1; delta < 32; delta++)
            {
                int detailed = requested - delta;
                if (detailed >= 0 && (available & (1u << detailed)) != 0)
                    return detailed;
                int coarser = requested + delta;
                if (coarser < 32 && (available & (1u << coarser)) != 0)
                    return coarser;
            }
            return LodLevel;
        }

    }

}
