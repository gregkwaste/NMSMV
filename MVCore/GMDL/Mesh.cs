using System;
using System.Collections.Generic;
using System.Text;
using OpenTK;
using OpenTK.Graphics.OpenGL4;
using GLSLHelper;
using System.IO;
using MVCore.Common;
using MVCore.Utils;
using libMBIN.NMS.Toolkit;
using System.Linq;

namespace MVCore.GMDL
{

    public class GLVao : IDisposable
    {
        //VAO ID
        public int vao_id;
        //VBO IDs
        public int vertex_buffer_object;
        public int element_buffer_object;

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        public GLVao()
        {
            vao_id = -1;
            vertex_buffer_object = -1;
            element_buffer_object = -1;
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
                        GL.DeleteBuffer(element_buffer_object);
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
            AABBMIN = new Vector3(input.AABBMIN);
            AABBMAX = new Vector3(input.AABBMAX);
        }
    }



    public class GLMeshVao : IDisposable
    {
        //Class static properties
        public const int MAX_INSTANCES = 512;

        public GLVao vao;
        public GLVao bHullVao;
        public MeshMetaData metaData;
        public float[] dataBuffer = new float[256];

        //Mesh type
        public COLLISIONTYPES collisionType;
        public TYPES type;

        //Instance Data
        public int UBO_aligned_size = 0; //Actual size of the data for the UBO, multiple to 256
        public int UBO_offset = 0; //Offset 

        public int instance_count = 0;
        public int visible_instances = 0;
        public List<Model> instanceRefs = new List<Model>();
        public float[] instanceBoneMatrices;
        private int instanceBoneMatricesTex;
        private int instanceBoneMatricesTexTBO;

        //Animation Properties
        //TODO : At some point include that shit into the instance data
        public int BoneRemapIndicesCount;
        public int[] BoneRemapIndices;
        //public float[] BoneRemapMatrices = new float[16 * 128];
        public bool skinned = false;


        public DrawElementsType indicesLength = DrawElementsType.UnsignedShort;

        //Material Properties
        public Material material;
        public Vector3 color;



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


        //Geometry Setup
        //BSphere calculator
        public GLVao setupBSphere(int instance_id)
        {
            float radius = 0.5f * (metaData.AABBMIN - metaData.AABBMAX).Length;
            Vector4 bsh_center = new Vector4(metaData.AABBMIN + 0.5f * (metaData.AABBMAX - metaData.AABBMIN), 1.0f);

            Matrix4 t_mat = GLMeshBufferManager.getInstanceWorldMat(this, instance_id);
            bsh_center = bsh_center * t_mat;

            //Create Sphere vbo
            return new Primitives.Sphere(bsh_center.Xyz, radius).getVAO();
        }


        //Rendering Methods

        public void renderBBoxes(int pass)
        {
            for (int i = 0; i > instance_count; i++)
                renderBbox(pass, i);
        }


        public void renderBbox(int pass, int instance_id)
        {
            if (GLMeshBufferManager.getInstanceOccludedStatus(this, instance_id))
                return;

            Matrix4 worldMat = GLMeshBufferManager.getInstanceWorldMat(this, instance_id);
            //worldMat = worldMat.ClearRotation();

            Vector4[] tr_AABB = new Vector4[2];
            //tr_AABB[0] = new Vector4(metaData.AABBMIN, 1.0f) * worldMat;
            //tr_AABB[1] = new Vector4(metaData.AABBMAX, 1.0f) * worldMat;

            tr_AABB[0] = new Vector4(instanceRefs[instance_id].AABBMIN, 1.0f);
            tr_AABB[1] = new Vector4(instanceRefs[instance_id].AABBMAX, 1.0f);

            //tr_AABB[0] = new Vector4(metaData.AABBMIN, 0.0f);
            //tr_AABB[1] = new Vector4(metaData.AABBMAX, 0.0f);

            //Generate all 8 points from the AABB
            float[] verts1 = new float[] {  tr_AABB[0].X, tr_AABB[0].Y, tr_AABB[0].Z,
                                           tr_AABB[1].X, tr_AABB[0].Y, tr_AABB[0].Z,
                                           tr_AABB[0].X, tr_AABB[1].Y, tr_AABB[0].Z,
                                           tr_AABB[1].X, tr_AABB[1].Y, tr_AABB[0].Z,

                                           tr_AABB[0].X, tr_AABB[0].Y, tr_AABB[1].Z,
                                           tr_AABB[1].X, tr_AABB[0].Y, tr_AABB[1].Z,
                                           tr_AABB[0].X, tr_AABB[1].Y, tr_AABB[1].Z,
                                           tr_AABB[1].X, tr_AABB[1].Y, tr_AABB[1].Z };

            //Indices
            Int32[] indices = new Int32[] { 0,1,2,
                                            2,1,3,
                                            1,5,3,
                                            5,7,3,
                                            5,4,6,
                                            5,6,7,
                                            0,2,4,
                                            2,6,4,
                                            3,6,2,
                                            7,6,3,
                                            0,4,5,
                                            1,0,5};
            //Generate OpenGL buffers
            int arraysize = sizeof(float) * verts1.Length;
            int vb_bbox, eb_bbox;
            GL.GenBuffers(1, out vb_bbox);
            GL.GenBuffers(1, out eb_bbox);

            //Upload vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, vb_bbox);
            //Allocate to NULL
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(2 * arraysize), (IntPtr)null, BufferUsageHint.StaticDraw);
            //Add verts data
            GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)arraysize, verts1);

            ////Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, eb_bbox);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(sizeof(Int32) * indices.Length), indices, BufferUsageHint.StaticDraw);


            //Render

            GL.BindBuffer(BufferTarget.ArrayBuffer, vb_bbox);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(0);

            //InverseBind Matrices
            //loc = GL.GetUniformLocation(shader_program, "invBMs");
            //GL.UniformMatrix4(loc, this.vbo.jointData.Count, false, this.vbo.invBMats);

            //Render Elements
            GL.PointSize(5.0f);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, eb_bbox);

            GL.DrawRangeElements(PrimitiveType.Triangles, 0, verts1.Length,
                indices.Length, DrawElementsType.UnsignedInt, IntPtr.Zero);

            GL.DisableVertexAttribArray(0);

            GL.DeleteBuffer(vb_bbox);
            GL.DeleteBuffer(eb_bbox);

        }


        public void renderBSphere(GLSLHelper.GLSLShaderConfig shader)
        {
            for (int i = 0; i < instance_count; i++)
            {
                GLVao bsh_Vao = setupBSphere(i);

                //Rendering

                GL.UseProgram(shader.program_id);

                //Step 2 Bind & Render Vao
                //Render Bounding Sphere
                GL.BindVertexArray(bsh_Vao.vao_id);
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                GL.DrawElements(PrimitiveType.Triangles, 600, DrawElementsType.UnsignedInt, (IntPtr)0);

                GL.BindVertexArray(0);
                bsh_Vao.Dispose();
            }


        }

        private void renderMesh()
        {
            GL.BindVertexArray(vao.vao_id);
            GL.DrawElementsInstanced(PrimitiveType.Triangles, metaData.batchcount, indicesLength,
                IntPtr.Zero, instance_count);
            GL.BindVertexArray(0);
        }

        private void renderLight()
        {
            GL.BindVertexArray(vao.vao_id);
            GL.PointSize(5.0f);
            GL.DrawArraysInstanced(PrimitiveType.Lines, 0, 2, instance_count);
            GL.DrawArraysInstanced(PrimitiveType.Points, 0, 2, instance_count); //Draw both points
            GL.BindVertexArray(0);
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
                        indicesLength, IntPtr.Zero, instance_count, -metaData.vertrstart_physics);
                    GL.DrawElementsInstancedBaseVertex(PrimitiveType.Triangles, metaData.batchcount,
                        indicesLength, IntPtr.Zero, instance_count, -metaData.vertrstart_physics);
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

            GL.BindVertexArray(0);
        }

        private void renderLocator()
        {
            GL.BindVertexArray(vao.vao_id);
            GL.DrawElementsInstanced(PrimitiveType.Lines, 6, indicesLength, IntPtr.Zero, instance_count); //Use Instancing
            GL.BindVertexArray(0);
        }

        private void renderJoint()
        {
            GL.BindVertexArray(vao.vao_id);
            GL.PointSize(5.0f);
            GL.DrawArrays(PrimitiveType.Lines, 0, metaData.batchcount);
            GL.DrawArrays(PrimitiveType.Points, 0, 1); //Draw only yourself
            GL.BindVertexArray(0);
        }

        public virtual void renderMain(GLSLShaderConfig shader)
        {
            //Upload Material Information

            //Upload Custom Per Material Uniforms
            foreach (Uniform un in material.CustomPerMaterialUniforms.Values)
            {
                if (shader.uniformLocations.Keys.Contains(un.Name))
                    GL.Uniform4(shader.uniformLocations[un.Name], un.vec.vec4);
            }

            //BIND TEXTURES
            //Diffuse Texture
            foreach (Sampler s in material.PSamplers.Values)
            {
                if (shader.uniformLocations.ContainsKey(s.Name) && s.Map != "")
                {
                    GL.Uniform1(shader.uniformLocations[s.Name], MyTextureUnit.MapTexUnitToSampler[s.Name]);
                    GL.ActiveTexture(s.texUnit.texUnit);
                    GL.BindTexture(s.tex.target, s.tex.texID);
                }
            }

            //BIND TEXTURE Buffer
            if (skinned)
            {
                GL.Uniform1(shader.uniformLocations["mpCustomPerMaterial.skinMatsTex"], 6);
                GL.ActiveTexture(TextureUnit.Texture6);
                GL.BindTexture(TextureTarget.TextureBuffer, instanceBoneMatricesTex);
                GL.TexBuffer(TextureBufferTarget.TextureBuffer,
                    SizedInternalFormat.Rgba32f, instanceBoneMatricesTexTBO);
            }

            //if (instance_count > 100)
            //    Console.WriteLine("Increase the buffers");

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

        private void renderBHull(GLSLHelper.GLSLShaderConfig shader)
        {
            if (bHullVao == null) return;
            //I ASSUME THAT EVERYTHING I NEED IS ALREADY UPLODED FROM A PREVIOUS PASS
            GL.PointSize(8.0f);
            GL.BindVertexArray(bHullVao.vao_id);

            GL.DrawElementsBaseVertex(PrimitiveType.Points, metaData.batchcount,
                        indicesLength, IntPtr.Zero, -metaData.vertrstart_physics);
            GL.DrawElementsBaseVertex(PrimitiveType.Triangles, metaData.batchcount,
                        indicesLength, IntPtr.Zero, -metaData.vertrstart_physics);
            GL.BindVertexArray(0);
        }

        public virtual void renderDebug(int pass)
        {
            GL.UseProgram(pass);
            //Step 1: Upload Uniforms
            int loc;
            //Upload Material Flags here
            //Reset
            loc = GL.GetUniformLocation(pass, "matflags");

            for (int i = 0; i < 64; i++)
                GL.Uniform1(loc + i, 0.0f);

            for (int i = 0; i < material.Flags.Count; i++)
                GL.Uniform1(loc + (int)material.Flags[i].MaterialFlag, 1.0f);

            //Upload joint transform data
            //Multiply matrices before sending them
            //Check if scene has the jointModel
            /*
            Util.mulMatArrays(ref skinMats, gobject.invBMats, scene.JMArray, 256);
            loc = GL.GetUniformLocation(pass, "skinMats");
            GL.UniformMatrix4(loc, 256, false, skinMats);
            */

            //Step 2: Render VAO
            GL.BindVertexArray(vao.vao_id);
            GL.DrawElements(PrimitiveType.Triangles, metaData.batchcount, DrawElementsType.UnsignedShort, (IntPtr)0);
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
                case RENDERPASS.BBOX:
                case RENDERPASS.BHULL:
                    //renderBbox(shader.program_id, 0);
                    //renderBSphere(shader);
                    renderBHull(shader);
                    break;
                //Render Debug
                case RENDERPASS.DEBUG:
                    //renderDebug(shader.program_id);
                    break;
                //Render for Picking
                case RENDERPASS.PICK:
                    //renderDebug(shader.program_id);
                    break;
                default:
                    //Do nothing in any other case
                    break;
            }

            return true;
        }



        public void setSkinMatrices(Scene animScene, int instance_id)
        {
            int instance_offset = 128 * 16 * instance_id;


            for (int i = 0; i < BoneRemapIndicesCount; i++)
            {
                Array.Copy(animScene.skinMats, BoneRemapIndices[i] * 16, instanceBoneMatrices, instance_offset + i * 16, 16);
            }
        }

        public void setDefaultSkinMatrices(int instance_id)
        {
            int instance_offset = 128 * 16 * instance_id;
            for (int i = 0; i < BoneRemapIndicesCount; i++)
            {
                MathUtils.insertMatToArray16(instanceBoneMatrices, instance_offset + i * 16, Matrix4.Identity);
            }

        }

        public void initializeSkinMatrices(Scene animScene)
        {
            if (instance_count == 0)
                return;
            int jointCount = animScene.jointDict.Values.Count;

            //TODO: Use the jointCount to adaptively setup the instanceBoneMatrices
            //Console.WriteLine("MAX : 128  vs Effective : " + jointCount.ToString());

            //Re-initialize the array based on the number of instances
            instanceBoneMatrices = new float[instance_count * 128 * 16];
            int bufferSize = instance_count * 128 * 16 * 4;

            //Setup the TBO
            instanceBoneMatricesTex = GL.GenTexture();
            instanceBoneMatricesTexTBO = GL.GenBuffer();

            GL.BindBuffer(BufferTarget.TextureBuffer, instanceBoneMatricesTexTBO);
            GL.BufferData(BufferTarget.TextureBuffer, bufferSize, instanceBoneMatrices, BufferUsageHint.StreamDraw);
            GL.TexBuffer(TextureBufferTarget.TextureBuffer, SizedInternalFormat.Rgba32f, instanceBoneMatricesTexTBO);
            GL.BindBuffer(BufferTarget.TextureBuffer, 0);

        }

        public void uploadSkinningData()
        {
            GL.BindBuffer(BufferTarget.TextureBuffer, instanceBoneMatricesTexTBO);
            int bufferSize = instance_count * 128 * 16 * 4;
            GL.BufferSubData(BufferTarget.TextureBuffer, IntPtr.Zero, bufferSize, instanceBoneMatrices);
            //Console.WriteLine(GL.GetError());
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
        public bool Skinned
        {
            get
            {
                if (meshVao.material != null)
                {
                    return meshVao.material.has_flag(TkMaterialFlags.UberFlagEnum._F02_SKINNED);
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

        public Mesh(Mesh input) : base(input)
        {
            //Copy attributes
            this.metaData = new MeshMetaData(input.metaData);

            //Copy Vao Refs
            this.meshVao = input.meshVao;

            //Material Stuff
            this.color = input.color;

            this.palette = input.palette;
            this.gobject = input.gobject; //Leave geometry file intact, no need to copy anything here
        }

        public void copyFrom(Mesh input)
        {
            //Copy attributes
            metaData = new MeshMetaData(input.metaData);
            hasLOD = input.hasLOD;

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

            new_m.meshVao = this.meshVao;
            new_m.instanceId = GLMeshBufferManager.addInstance(ref new_m.meshVao, new_m);

            //Clone children
            foreach (Model child in children)
            {
                Model new_child = child.Clone();
                new_child.parent = new_m;
                new_m.children.Add(new_child);
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

        public override void updateMeshInfo()
        {

#if(DEBUG)
            if (instanceId < 0)
                Console.WriteLine("test");
            if (meshVao.BoneRemapIndicesCount > 128)
                Console.WriteLine("test");
#endif

            if (!renderable || (parentScene.activeLOD != LodLevel) && RenderState.renderSettings.LODFiltering)
            {
                base.updateMeshInfo();
                Common.RenderStats.occludedNum += 1;
                return;
            }

            bool fr_status = Common.RenderState.activeCam.frustum_occlude(meshVao, worldMat * RenderState.rotMat);
            bool occluded_status = !fr_status && Common.RenderState.renderSettings.UseFrustumCulling;

            //Recalculations && Data uploads
            if (!occluded_status)
            {
                /*
                //Apply LOD filtering
                if (hasLOD && Common.RenderOptions.LODFiltering)
                //if (false)
                {
                    //Console.WriteLine("Active LoD {0}", parentScene.activeLOD);
                    if (parentScene.activeLOD != LodLevel)
                    {
                        meshVao.setInstanceOccludedStatus(instanceId, true);
                        base.updateMeshInfo();
                        return;
                    }
                }
                */

                instanceId = GLMeshBufferManager.addInstance(ref meshVao, this);

                //Upload commonperMeshUniforms
                GLMeshBufferManager.setInstanceUniform4(meshVao, instanceId,
                    "gUserDataVec4", CommonPerMeshUniforms["gUserDataVec4"].Vec.Vec);

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
                Common.RenderStats.occludedNum += 1;
            }

            //meshVao.setInstanceOccludedStatus(instanceId, occluded_status);
            base.updateMeshInfo();
        }

        public override Assimp.Node assimpExport(ref Assimp.Scene scn, ref Dictionary<int, int> meshImportStatus)
        {
            Assimp.Mesh amesh = new Assimp.Mesh();
            Assimp.Node node;
            amesh.Name = name;

            int meshHash = meshVao.GetHashCode();

            //TESTING
            if (scn.MeshCount > 20)
            {
                node = base.assimpExport(ref scn, ref meshImportStatus);
                return node;
            }

            if (!meshImportStatus.ContainsKey(meshHash))
            //if (false)
            {
                meshImportStatus[meshHash] = scn.MeshCount;

                int vertcount = metaData.vertrend_graphics - metaData.vertrstart_graphics + 1;
                MemoryStream vms = new MemoryStream(gobject.meshDataDict[metaData.Hash].vs_buffer);
                MemoryStream ims = new MemoryStream(gobject.meshDataDict[metaData.Hash].is_buffer);
                BinaryReader vbr = new BinaryReader(vms);
                BinaryReader ibr = new BinaryReader(ims);


                //Initialize Texture Component Channels
                if (gobject.bufInfo[1] != null)
                {
                    List<Assimp.Vector3D> textureChannel = new List<Assimp.Vector3D>();
                    amesh.TextureCoordinateChannels.Append(textureChannel);
                    amesh.UVComponentCount[0] = 2;
                }

                //Generate bones only for the joints related to the mesh
                Dictionary<int, Assimp.Bone> localJointDict = new Dictionary<int, Assimp.Bone>();

                //Export Bone Structure
                if (Skinned)
                //if (false)
                {
                    for (int i = 0; i < meshVao.BoneRemapIndicesCount; i++)
                    {
                        int joint_id = meshVao.BoneRemapIndices[i];
                        //Fetch name
                        Joint relJoint = null;

                        foreach (Joint jnt in parentScene.jointDict.Values)
                        {
                            if (jnt.jointIndex == joint_id)
                            {
                                relJoint = jnt;
                                break;
                            }

                        }

                        //Generate bone
                        Assimp.Bone b = new Assimp.Bone();
                        if (relJoint != null)
                        {
                            b.Name = relJoint.name;
                            b.OffsetMatrix = MathUtils.convertMatrix(relJoint.invBMat);
                        }


                        localJointDict[i] = b;
                        amesh.Bones.Add(b);
                    }
                }



                //Write geometry info

                vbr.BaseStream.Seek(0, SeekOrigin.Begin);
                for (int i = 0; i < vertcount; i++)
                {
                    Assimp.Vector3D v, vN;

                    for (int j = 0; j < gobject.bufInfo.Count; j++)
                    {
                        bufInfo buf = gobject.bufInfo[j];
                        if (buf is null)
                            continue;

                        switch (buf.semantic)
                        {
                            case 0: //vPosition
                                {
                                    switch (buf.type)
                                    {
                                        case VertexAttribPointerType.HalfFloat:
                                            uint v1 = vbr.ReadUInt16();
                                            uint v2 = vbr.ReadUInt16();
                                            uint v3 = vbr.ReadUInt16();
                                            uint v4 = vbr.ReadUInt16();

                                            //Transform vector with worldMatrix
                                            v = new Assimp.Vector3D(Utils.Half.decompress(v1), Utils.Half.decompress(v2), Utils.Half.decompress(v3));
                                            break;
                                        case VertexAttribPointerType.Float: //This is used in my custom vbos
                                            float f1 = vbr.ReadSingle();
                                            float f2 = vbr.ReadSingle();
                                            float f3 = vbr.ReadSingle();
                                            //Transform vector with worldMatrix
                                            v = new Assimp.Vector3D(f1, f2, f3);
                                            break;
                                        default:
                                            throw new Exception("Unimplemented Vertex Type");
                                    }
                                    amesh.Vertices.Add(v);
                                    break;
                                }

                            case 1: //uvPosition
                                {
                                    Assimp.Vector3D uv;
                                    uint v1 = vbr.ReadUInt16();
                                    uint v2 = vbr.ReadUInt16();
                                    uint v3 = vbr.ReadUInt16();
                                    uint v4 = vbr.ReadUInt16();
                                    //uint v4 = Convert.ToUInt16(vbr.ReadUInt16());
                                    uv = new Assimp.Vector3D(Utils.Half.decompress(v1), Utils.Half.decompress(v2), 0.0f);

                                    amesh.TextureCoordinateChannels[0].Add(uv); //Add directly to the first channel
                                    break;
                                }
                            case 2: //nPosition
                            case 3: //tPosition
                                {
                                    switch (buf.type)
                                    {
                                        case (VertexAttribPointerType.Float):
                                            float f1, f2, f3;
                                            f1 = vbr.ReadSingle();
                                            f2 = vbr.ReadSingle();
                                            f3 = vbr.ReadSingle();
                                            vN = new Assimp.Vector3D(f1, f2, f3);
                                            break;
                                        case (VertexAttribPointerType.HalfFloat):
                                            uint v1, v2, v3;
                                            v1 = vbr.ReadUInt16();
                                            v2 = vbr.ReadUInt16();
                                            v3 = vbr.ReadUInt16();
                                            vN = new Assimp.Vector3D(Utils.Half.decompress(v1), Utils.Half.decompress(v2), Utils.Half.decompress(v3));
                                            break;
                                        case (VertexAttribPointerType.Int2101010Rev):
                                            int i1, i2, i3;
                                            uint value;
                                            byte[] a32 = new byte[4];
                                            a32 = vbr.ReadBytes(4);

                                            value = BitConverter.ToUInt32(a32, 0);
                                            //Convert Values
                                            i1 = _2sComplement.toInt((value >> 00) & 0x3FF, 10);
                                            i2 = _2sComplement.toInt((value >> 10) & 0x3FF, 10);
                                            i3 = _2sComplement.toInt((value >> 20) & 0x3FF, 10);
                                            //int i4 = _2sComplement.toInt((value >> 30) & 0x003, 10);
                                            float norm = (float)Math.Sqrt(i1 * i1 + i2 * i2 + i3 * i3);

                                            vN = new Assimp.Vector3D(Convert.ToSingle(i1) / norm,
                                                             Convert.ToSingle(i2) / norm,
                                                             Convert.ToSingle(i3) / norm);

                                            //Debug.WriteLine(vN);
                                            break;
                                        default:
                                            throw new Exception("UNIMPLEMENTED NORMAL TYPE. PLEASE REPORT");
                                    }

                                    if (j == 2)
                                        amesh.Normals.Add(vN);
                                    else if (j == 3)
                                    {
                                        amesh.Tangents.Add(vN);
                                        amesh.BiTangents.Add(new Assimp.Vector3D(0.0f, 0.0f, 1.0f));
                                    }
                                    break;
                                }
                            case 4: //bPosition
                                vbr.ReadBytes(4); // skip
                                break;
                            case 5: //BlendIndices + BlendWeights
                                {
                                    int[] joint_ids = new int[4];
                                    float[] weights = new float[4];

                                    for (int k = 0; k < 4; k++)
                                    {
                                        joint_ids[k] = vbr.ReadByte();
                                    }


                                    for (int k = 0; k < 4; k++)
                                        weights[k] = Utils.Half.decompress(vbr.ReadUInt16());

                                    if (Skinned)
                                    //if (false)
                                    {
                                        for (int k = 0; k < 4; k++)
                                        {
                                            int joint_id = joint_ids[k];

                                            Assimp.VertexWeight vw = new Assimp.VertexWeight();
                                            vw.VertexID = i;
                                            vw.Weight = weights[k];
                                            localJointDict[joint_id].VertexWeights.Add(vw);

                                        }


                                    }


                                    break;
                                }
                            case 6:
                                break; //Handled by 5
                            default:
                                {
                                    throw new Exception("UNIMPLEMENTED BUF Info. PLEASE REPORT");
                                    break;
                                }

                        }
                    }

                }

                //Export Faces
                //Get indices
                ibr.BaseStream.Seek(0, SeekOrigin.Begin);
                bool start = false;
                int fstart = 0;
                for (int i = 0; i < metaData.batchcount / 3; i++)
                {
                    int f1, f2, f3;
                    //NEXT models assume that all gstream meshes have uint16 indices
                    f1 = ibr.ReadUInt16();
                    f2 = ibr.ReadUInt16();
                    f3 = ibr.ReadUInt16();

                    if (!start && this.type != TYPES.COLLISION)
                    { fstart = f1; start = true; }
                    else if (!start && this.type == TYPES.COLLISION)
                    {
                        fstart = 0; start = true;
                    }

                    int f11, f22, f33;
                    f11 = f1 - fstart;
                    f22 = f2 - fstart;
                    f33 = f3 - fstart;


                    Assimp.Face face = new Assimp.Face();
                    face.Indices.Add(f11);
                    face.Indices.Add(f22);
                    face.Indices.Add(f33);


                    amesh.Faces.Add(face);
                }

                scn.Meshes.Add(amesh);

            }

            node = base.assimpExport(ref scn, ref meshImportStatus);
            node.MeshIndices.Add(meshImportStatus[meshHash]);

            return node;
        }

        public void writeGeomToStream(StreamWriter s, ref uint index)
        {
            int vertcount = metaData.vertrend_graphics - metaData.vertrstart_graphics + 1;
            MemoryStream vms = new MemoryStream(gobject.meshDataDict[metaData.Hash].vs_buffer);
            MemoryStream ims = new MemoryStream(gobject.meshDataDict[metaData.Hash].is_buffer);
            BinaryReader vbr = new BinaryReader(vms);
            BinaryReader ibr = new BinaryReader(ims);
            //Start Writing
            //Object name
            s.WriteLine("o " + name);
            //Get Verts

            //Preset Matrices for faster export
            Matrix4 wMat = this.worldMat;
            Matrix4 nMat = Matrix4.Invert(Matrix4.Transpose(wMat));

            vbr.BaseStream.Seek(0, SeekOrigin.Begin);
            for (int i = 0; i < vertcount; i++)
            {
                Vector4 v;
                VertexAttribPointerType ntype = gobject.bufInfo[0].type;
                int v_section_bytes = 0;

                switch (ntype)
                {
                    case VertexAttribPointerType.HalfFloat:
                        uint v1 = vbr.ReadUInt16();
                        uint v2 = vbr.ReadUInt16();
                        uint v3 = vbr.ReadUInt16();
                        //uint v4 = Convert.ToUInt16(vbr.ReadUInt16());

                        //Transform vector with worldMatrix
                        v = new Vector4(Utils.Half.decompress(v1), Utils.Half.decompress(v2), Utils.Half.decompress(v3), 1.0f);
                        v_section_bytes = 6;
                        break;
                    case VertexAttribPointerType.Float: //This is used in my custom vbos
                        float f1 = vbr.ReadSingle();
                        float f2 = vbr.ReadSingle();
                        float f3 = vbr.ReadSingle();
                        //Transform vector with worldMatrix
                        v = new Vector4(f1, f2, f3, 1.0f);
                        v_section_bytes = 12;
                        break;
                    default:
                        throw new Exception("Unimplemented Vertex Type");
                }


                v = Vector4.Transform(v, this.worldMat);

                //s.WriteLine("v " + Half.decompress(v1).ToString() + " "+ Half.decompress(v2).ToString() + " " + Half.decompress(v3).ToString());
                s.WriteLine("v " + v.X.ToString() + " " + v.Y.ToString() + " " + v.Z.ToString());
                vbr.BaseStream.Seek(gobject.vx_size - v_section_bytes, SeekOrigin.Current);
            }
            //Get Normals

            vbr.BaseStream.Seek(gobject.offsets[2] + 0, SeekOrigin.Begin);
            for (int i = 0; i < vertcount; i++)
            {
                Vector4 vN;
                VertexAttribPointerType ntype = gobject.bufInfo[2].type;
                int n_section_bytes = 0;

                switch (ntype)
                {
                    case (VertexAttribPointerType.Float):
                        float f1, f2, f3;
                        f1 = vbr.ReadSingle();
                        f2 = vbr.ReadSingle();
                        f3 = vbr.ReadSingle();
                        vN = new Vector4(f1, f2, f3, 1.0f);
                        n_section_bytes = 12;
                        break;
                    case (VertexAttribPointerType.HalfFloat):
                        uint v1, v2, v3;
                        v1 = vbr.ReadUInt16();
                        v2 = vbr.ReadUInt16();
                        v3 = vbr.ReadUInt16();
                        vN = new Vector4(Utils.Half.decompress(v1), Utils.Half.decompress(v2), Utils.Half.decompress(v3), 1.0f);
                        n_section_bytes = 6;
                        break;
                    case (VertexAttribPointerType.Int2101010Rev):
                        int i1, i2, i3;
                        uint value;
                        byte[] a32 = new byte[4];
                        a32 = vbr.ReadBytes(4);

                        value = BitConverter.ToUInt32(a32, 0);
                        //Convert Values
                        i1 = _2sComplement.toInt((value >> 00) & 0x3FF, 10);
                        i2 = _2sComplement.toInt((value >> 10) & 0x3FF, 10);
                        i3 = _2sComplement.toInt((value >> 20) & 0x3FF, 10);
                        //int i4 = _2sComplement.toInt((value >> 30) & 0x003, 10);
                        float norm = (float)Math.Sqrt(i1 * i1 + i2 * i2 + i3 * i3);

                        vN = new Vector4(Convert.ToSingle(i1) / norm,
                                         Convert.ToSingle(i2) / norm,
                                         Convert.ToSingle(i3) / norm,
                                         1.0f);

                        n_section_bytes = 4;
                        //Debug.WriteLine(vN);
                        break;
                    default:
                        throw new Exception("UNIMPLEMENTED NORMAL TYPE. PLEASE REPORT");
                }

                //uint v4 = Convert.ToUInt16(vbr.ReadUInt16());
                //Transform normal with normalMatrix


                vN = Vector4.Transform(vN, nMat);

                s.WriteLine("vn " + vN.X.ToString() + " " + vN.Y.ToString() + " " + vN.Z.ToString());
                vbr.BaseStream.Seek(gobject.vx_size - n_section_bytes, SeekOrigin.Current);
            }
            //Get UVs, only for mesh objects

            vbr.BaseStream.Seek(Math.Max(gobject.offsets[1], 0) + gobject.vx_size * metaData.vertrstart_graphics, SeekOrigin.Begin);
            for (int i = 0; i < vertcount; i++)
            {
                Vector2 uv;
                int uv_section_bytes = 0;
                if (gobject.offsets[1] != -1) //Check if uvs exist
                {
                    uint v1 = vbr.ReadUInt16();
                    uint v2 = vbr.ReadUInt16();
                    uint v3 = vbr.ReadUInt16();
                    //uint v4 = Convert.ToUInt16(vbr.ReadUInt16());
                    uv = new Vector2(Utils.Half.decompress(v1), Utils.Half.decompress(v2));
                    uv_section_bytes = 0x6;
                }
                else
                {
                    uv = new Vector2(0.0f, 0.0f);
                    uv_section_bytes = gobject.vx_size;
                }

                s.WriteLine("vt " + uv.X.ToString() + " " + (1.0 - uv.Y).ToString());
                vbr.BaseStream.Seek(gobject.vx_size - uv_section_bytes, SeekOrigin.Current);
            }


            //Some Options
            s.WriteLine("usemtl(null)");
            s.WriteLine("s off");

            //Get indices
            ibr.BaseStream.Seek(0, SeekOrigin.Begin);
            bool start = false;
            uint fstart = 0;
            for (int i = 0; i < metaData.batchcount / 3; i++)
            {
                uint f1, f2, f3;
                //NEXT models assume that all gstream meshes have uint16 indices
                f1 = ibr.ReadUInt16();
                f2 = ibr.ReadUInt16();
                f3 = ibr.ReadUInt16();

                if (!start && this.type != TYPES.COLLISION)
                { fstart = f1; start = true; }
                else if (!start && this.type == TYPES.COLLISION)
                {
                    fstart = 0; start = true;
                }

                uint f11, f22, f33;
                f11 = f1 - fstart + index;
                f22 = f2 - fstart + index;
                f33 = f3 - fstart + index;


                s.WriteLine("f " + f11.ToString() + "/" + f11.ToString() + "/" + f11.ToString() + " "
                                + f22.ToString() + "/" + f22.ToString() + "/" + f22.ToString() + " "
                                + f33.ToString() + "/" + f33.ToString() + "/" + f33.ToString() + " ");


            }
            index += (uint)vertcount;
        }



        #region IDisposable Support

        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {

                    // TODO: dispose managed state (managed objects).
                    //if (material != null) material.Dispose();
                    //NOTE: No need to dispose material, because the materials reside in the resource manager
                    base.Dispose(disposing);
                }
            }
        }

        #endregion

    }

}
