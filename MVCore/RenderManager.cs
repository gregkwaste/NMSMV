﻿using System;
using System.Collections.Generic;
using System.Text;
using OpenTK;
using OpenTK.Graphics.OpenGL4;
using libMBIN.NMS.Toolkit;
using MVCore.GMDL;
using MVCore.Common;
using System.Runtime.InteropServices;

namespace MVCore
{
    //Render Structs
    [StructLayout(LayoutKind.Explicit)]
    struct CommonPerFrameSamplers
    {
        [FieldOffset(0)]
        public int depthMap; //Depth Map Sampler ID
        
        public static readonly int SizeInBytes = 12;
    };

    [StructLayout(LayoutKind.Explicit)]
    struct CommonPerFrameUniforms
    {
        [FieldOffset(0)]
        public float diffuseFlag; //Enable Textures
        [FieldOffset(4)]
        public float use_lighting; //Enable lighting
        [FieldOffset(8)]
        public float gfTime; //Fractional Time
        [FieldOffset(16)]
        public Matrix4 rotMat;
        [FieldOffset(80)]
        public Matrix4 mvp;
        [FieldOffset(144)]
        public Vector3 cameraPosition;
        [FieldOffset(156)]
        public float cameraFarPlane;
        [FieldOffset(160)]
        public Vector3 cameraDirection;
        [FieldOffset(172)]
        public int light_number;
        [FieldOffset(176)]
        public unsafe fixed float lights[128 * 64];


        public static readonly int SizeInBytes = 32944;
    };
    
    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct CommonPerMeshUniforms
    {
        [FieldOffset(0)]
        public fixed float skinMats[80*16]; //This is mapped to mat4 //5120 bytes
        [FieldOffset(5120)]
        public Vector4 gUserDataVec4;
        [FieldOffset(5136)]
        public Vector3 color; //12 bytes
        [FieldOffset(5148)]
        public float skinned; //4 bytes (aligns to 4 bytes)
        [FieldOffset(5152)]
        public fixed float instanceData[36];
        [FieldOffset(5296)]
        public fixed byte padding[80]; //Extra padding to match the UBO aligment

        public static readonly int SizeInBytes = 5376;
    };

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct CommonPerMeshUniformsInstanced
    {
        [FieldOffset(0)]
        public fixed float skinMats[80 * 16]; //This is mapped to mat4 //5120 bytes
        [FieldOffset(5120)]
        public Vector4 gUserDataVec4;
        [FieldOffset(5136)]
        public Vector3 color; //12 bytes
        [FieldOffset(5148)]
        public float skinned; //4 bytes (aligns to 4 bytes)
        [FieldOffset(5152)]
        public fixed float instanceData[300 * 36];
        [FieldOffset(48352)]
        public fixed byte padding[32]; //Extra padding to match the UBO aligment

        public static readonly int SizeInBytes = 48384;
    };

    [StructLayout(LayoutKind.Explicit)]
    struct InstanceData
    {
        [FieldOffset(0)] //64 Bytes
        public Matrix4 worldMat;
        [FieldOffset(64)] //64 Bytes
        public Matrix4 normalMat;
        [FieldOffset(128)] //4 bytes
        public float isOccluded;
        [FieldOffset(132)]
        public float isSelected;
        
        public static readonly int SizeInBytes = 72;
    };

    public class renderManager : baseResourceManager, IDisposable
    {
        List<GLMeshVao> staticMeshQueue = new List<GLMeshVao>();
        List<GLMeshVao> staticObjectsQueue = new List<GLMeshVao>();
        List<GLMeshVao> movingMeshQueue = new List<GLMeshVao>();
        List<GLMeshVao> transparentMeshQueue = new List<GLMeshVao>();

        public ResourceManager resMgr; //REf to the active resource Manager

        public ShadowRenderer shdwRenderer; //Shadow Renderer instance
        //Control Font and Text Objects
        private Text.TextRenderer txtRenderer;
        public int last_text_height;
        

        private GBuffer gbuf;
        private float gfTime = 0.0f;
        private Dictionary<string, int> UBOs = new Dictionary<string, int>();

        private int trippleBufferActiveId;
        private List<int> trippleBufferUBOs = new List<int>(3);
        private List<IntPtr> trippleBufferSyncStatuses = new List<IntPtr>(3);
        
        //Octree Structure
        private Octree octree;
        
        //Local Counters
        private int occludedNum;

        //UBO structs
        CommonPerFrameUniforms cpfu;
        private byte[] atlas_cpmu;

        private int MAX_NUMBER_OF_MESHES = 2000;
        private ulong MAX_OCTREE_WIDTH = 256;


        public void init(ResourceManager input_resMgr)
        {
            //Setup Resource Manager
            resMgr = input_resMgr;

            //Setup Shadow Renderer
            shdwRenderer = new ShadowRenderer();

            //Setup text renderer
            setupTextRenderer();

            //Setup UBOs
            setupUBOs();

            //Initialize Octree
            octree = new Octree(MAX_OCTREE_WIDTH);
        }

        public void setupGBuffer(int width, int height)
        {
            //Create gbuffer
            gbuf = new GBuffer(resMgr, width, height);
        }


        public void progressTime(double dt)
        {
            gfTime += (float) dt / 500;
            gfTime = gfTime % 1000.0f;
        }

        public void clearInstances()
        {
            foreach (GLMeshVao m in staticObjectsQueue)
            {
                if (m is GLMeshVao mm)
                    mm.instance_count = 0;
            }

            foreach (GLMeshVao m in staticMeshQueue)
            {
                if (m is GLMeshVao mm)
                    mm.instance_count = 0;
            }

            foreach (GLMeshVao m in transparentMeshQueue)
            {
                if (m is GLMeshVao mm)
                    mm.instance_count = 0;
            }
        }

        public void cleanup()
        {
            //Just cleanup the queues
            //The resource manager will handle the cleanup of the buffers and shit

            staticMeshQueue.Clear();
            staticObjectsQueue.Clear();
            movingMeshQueue.Clear();
            transparentMeshQueue.Clear();
            octree.clear();
        }

        public void populate(GMDL.model root)
        {
            cleanup();
            
            //Populate octree
            octree.insert(root);
            octree.report();
            
            foreach (scene s in resMgr.GLScenes.Values)
                process_models(s);
        }

        private void process_model(GLMeshVao m)
        {
            if (m == null)
                return;

            //Check if the model has a transparent material
            if (m.material.has_flag((TkMaterialFlags.MaterialFlagEnum)TkMaterialFlags.UberFlagEnum._F22_TRANSPARENT_SCALAR) ||
                m.material.has_flag((TkMaterialFlags.MaterialFlagEnum)TkMaterialFlags.UberFlagEnum._F09_TRANSPARENT) ||
                m.material.has_flag((TkMaterialFlags.MaterialFlagEnum)TkMaterialFlags.UberFlagEnum._F11_ALPHACUTOUT))
            {
                if (!transparentMeshQueue.Contains(m))
                    transparentMeshQueue.Add(m);
            }
            else
            {
                if (!staticMeshQueue.Contains(m))
                    staticMeshQueue.Add(m);
            }
        }

        private void process_models(model root)
        {
            process_model(root.meshVao);
            
            //Repeat process with children
            foreach (model child in root.children)
            {
                process_models(child);
            }
        }

        public void setupTextRenderer()
        {
            //Use QFont
            //string font = "C:\\WINDOWS\\FONTS\\LUCON.TTF";
            string font = "DroidSansMono.ttf";
            txtRenderer = new MVCore.Text.TextRenderer(font, 10);
        }

        private void setupUBOs()
        {
            //Generate a UBO for CommonPerFrameUniforms & CommonPerMeshUniform
            //CommonPerFrame Uniforms Size: 4 + 4 + 16 * 4 + 16 * 4 = 136
            //CommonPerMesh Uniforms Size: 16 * 4 + 16 * 4 + 80 * 16 * 4 + 4 + 4 + 3 * 4 = 5268
            //Total Size: 136 + 5268 = 5404 Bytes


            int max_ubo_size = GL.GetInteger(GetPName.MaxUniformBlockSize);

            //Allocate atlas
            //int atlas_ubo_buffer_size = MAX_NUMBER_OF_MESHES * CommonPerMeshUniforms.SizeInBytes;
            int atlas_ubo_buffer_size = 1024 * 1024 * 16; //16MB
            atlas_cpmu = new byte[atlas_ubo_buffer_size];

            int ubo_id = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.UniformBuffer, ubo_id);
            GL.BufferData(BufferTarget.UniformBuffer, CommonPerFrameUniforms.SizeInBytes, IntPtr.Zero, BufferUsageHint.StaticRead);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);

            //Store buffer to dictionary
            UBOs["_COMMON_PER_FRAME"] = ubo_id;
            
            //Generate 3 Buffers for the Triple buffering UBO
            for (int i = 0; i < 3; i++)
            {
                ubo_id = GL.GenBuffer();
                GL.BindBuffer(BufferTarget.UniformBuffer, ubo_id);
                GL.BufferStorage(BufferTarget.UniformBuffer, atlas_ubo_buffer_size, 
                    IntPtr.Zero, BufferStorageFlags.MapPersistentBit | BufferStorageFlags.MapWriteBit);
                //GL.BufferData(BufferTarget.UniformBuffer, atlas_ubo_buffer_size, IntPtr.Zero, BufferUsageHint.StreamDraw);
                trippleBufferUBOs.Add(ubo_id);
                trippleBufferSyncStatuses.Add(GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, 0));
            }

            
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);

            //Store buffer to dictionary
            //Keep first buffer id as the active one
            UBOs["_COMMON_PER_MESH"] = trippleBufferUBOs[0];
            trippleBufferActiveId = 0;

            //Attach programs to UBO and binding point
            attachUBOToShaderBindingPoint(GLSLHelper.SHADER_TYPE.MESH_DEFERRED_SHADER, "_COMMON_PER_FRAME", 0);
            attachUBOToShaderBindingPoint(GLSLHelper.SHADER_TYPE.MESH_DEFERRED_SHADER, "_COMMON_PER_MESH", 1);
            attachUBOToShaderBindingPoint(GLSLHelper.SHADER_TYPE.MESH_FORWARD_SHADER, "_COMMON_PER_FRAME", 0);
            attachUBOToShaderBindingPoint(GLSLHelper.SHADER_TYPE.MESH_FORWARD_SHADER, "_COMMON_PER_MESH", 1);
            attachUBOToShaderBindingPoint(GLSLHelper.SHADER_TYPE.LOCATOR_SHADER, "_COMMON_PER_FRAME", 0);
            attachUBOToShaderBindingPoint(GLSLHelper.SHADER_TYPE.LOCATOR_SHADER, "_COMMON_PER_MESH", 1);
            attachUBOToShaderBindingPoint(GLSLHelper.SHADER_TYPE.JOINT_SHADER, "_COMMON_PER_FRAME", 0);
            attachUBOToShaderBindingPoint(GLSLHelper.SHADER_TYPE.JOINT_SHADER, "_COMMON_PER_MESH", 1);
            attachUBOToShaderBindingPoint(GLSLHelper.SHADER_TYPE.GBUFFER_SHADER, "_COMMON_PER_FRAME", 0);
            attachUBOToShaderBindingPoint(GLSLHelper.SHADER_TYPE.GBUFFER_SHADER, "_COMMON_PER_MESH", 1);

            //Attach the generated buffers to the binding points
            bindUBOs();

            reportUBOs();
        }
        
        //This method attaches UBOs to shader binding points
        private void attachUBOToShaderBindingPoint(GLSLHelper.SHADER_TYPE shader_type, string var_name, int binding_point)
        {
            //Binding Position 0 - Matrices UBO
            int shdr_program_id = resMgr.GLShaders[shader_type].program_id;
            int ubo_index = GL.GetUniformBlockIndex(shdr_program_id, var_name);
            GL.UniformBlockBinding(shdr_program_id, ubo_index, binding_point);
        }
        
        //This method updates UBO data for rendering
        private void prepareCommonPerFrameUBO()
        {
            //Prepare Struct
            cpfu.diffuseFlag = RenderOptions._useTextures;
            cpfu.use_lighting = RenderOptions._useLighting;
            cpfu.mvp = RenderState.mvp;
            cpfu.rotMat = RenderState.rotMat;
            cpfu.cameraPosition = RenderState.activeCam.Position;
            cpfu.cameraDirection = RenderState.activeCam.Orientation;
            cpfu.cameraFarPlane = RenderState.activeCam.zFar;
            cpfu.light_number = RenderState.activeResMgr.GLlights.Count;
            cpfu.gfTime = gfTime; 

            //Upload light information
            for (int i = 0; i < Math.Min(128, resMgr.GLlights.Count); i++)
            {
                Light l = resMgr.GLlights[i];
                //if (!l.update_changes)
                //    continue;
                l.update_changes = false; //Reset the flag

                int offset = (GLLight.SizeInBytes / 4) * i;
                GLLight strct = resMgr.GLlights[i].strct;
                unsafe
                {
                    //Position : Offset 0
                    cpfu.lights[offset + 0] = strct.position.X;
                    cpfu.lights[offset + 1] = strct.position.Y;
                    cpfu.lights[offset + 2] = strct.position.Z;
                    cpfu.lights[offset + 3] = strct.position.W;
                    //Color : Offset 16(4)
                    cpfu.lights[offset + 4] = strct.color.X;
                    cpfu.lights[offset + 5] = strct.color.Y;
                    cpfu.lights[offset + 6] = strct.color.Z;
                    cpfu.lights[offset + 7] = strct.color.W;
                    //Direction: Offset 32(8)
                    cpfu.lights[offset + 8] = strct.direction.X;
                    cpfu.lights[offset + 9] = strct.direction.Y;
                    cpfu.lights[offset + 10] = strct.direction.Z;
                    cpfu.lights[offset + 11] = strct.direction.W;
                    //Falloff: Offset 48(12)
                    cpfu.lights[offset + 12] = strct.falloff;
                    //Type: Offset 52(13)
                    cpfu.lights[offset + 13] = strct.type;
                }
            
            }
            
            //TODO: Move depthMap to the new commonperSamplers struct
            //cpfu.depthMap = shdwRenderer.depth_tex_id; //Assign Depth Map

            
            GL.BindBuffer(BufferTarget.UniformBuffer, UBOs["_COMMON_PER_FRAME"]);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, CommonPerFrameUniforms.SizeInBytes, ref cpfu);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
        }

        private void prepareCommonPermeshUBO(GLMeshVao m, ref int UBO_Offset)
        {
            if (m.instance_count == 0)
                return;

            
            /*
            if (m.instanceRefs[0].name == "Buttons1")
            {
                for (int i = 0; i < m.instance_count; i++)
                {
                    Console.WriteLine("Instance Id {0} Occluded Status: {1} transform matrix: {2}",
                    i, m.getInstanceOccludedStatus(i), m.getInstanceWorldMat(i));
                }
            }*/
            

            //TODO move that shit in the MeshVao Class
            m.UBO.color = m.color;
            m.UBO.skinned = m.skinned ? 1.0f : 0.0f;
            
            if (m.skinned)
            {
                unsafe
                {
                    //Again try to think of a better way to copy this shit
                    for (int i = 0; i < m.BoneRemapIndicesCount * 16; i++)
                    { 
                        m.UBO.skinMats[i] = m.BoneRemapMatrices[i];
                    }
                }
            }

            //Copy custom mesh parameters
            //TODO: Move that to instance data as well
            //cpmu.gUserDataVec4 = m.PgUserDataVec4.Vec.Vec;
            m.UBO.gUserDataVec4 = new Vector4(0.0f);

            //Calculate aligned size
            m.UBO_aligned_size = 5152 + m.instance_count * GLMeshVao.instance_struct_size_floats * 4;
            m.UBO_aligned_size = ((m.UBO_aligned_size >> 8) + 1) * 256;

            if (m.UBO_aligned_size + UBO_Offset > atlas_cpmu.Length)
            {
                Console.WriteLine("Mesh overload skipping...");
                return;
            }
                
            unsafe
            {
                fixed(void* p = &m.UBO)
                {
                    byte* bptr = (byte*) p;

                    Marshal.Copy((IntPtr) p, atlas_cpmu, UBO_Offset, 
                        m.UBO_aligned_size);
                }
            }

            m.UBO_offset = UBO_Offset; //Save offset
            UBO_Offset += m.UBO_aligned_size; //Increase the offset
        }

        //This Method binds UBos to binding points
        private void bindUBOs()
        {
            //Bind Matrices
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, UBOs["_COMMON_PER_FRAME"]);
            //GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 1, UBOs["mpCommonPerMesh"]);
        }

        private void reportUBOs()
        {
            //Print Debug Information for the UBO
            // Get named blocks info
            int count, info, length;
            int test_program = resMgr.GLShaders[GLSLHelper.SHADER_TYPE.MESH_DEFERRED_SHADER].program_id;
            GL.GetProgram(test_program, GetProgramParameterName.ActiveUniformBlocks, out count);
            
            for (int i = 0; i < count; ++i)
            {
                // Get blocks name
                string block_name;
                int block_size, block_bind_index;
                GL.GetActiveUniformBlockName(test_program, i, 256, out length, out block_name);
                GL.GetActiveUniformBlock(test_program, i, ActiveUniformBlockParameter.UniformBlockDataSize, out block_size);
                Console.WriteLine("Block {0} Data Size {1}", block_name, block_size);

                GL.GetActiveUniformBlock(test_program, i, ActiveUniformBlockParameter.UniformBlockBinding, out block_bind_index);
                Console.WriteLine("    Block Binding Point {0}", block_bind_index);
                
                GL.GetInteger(GetIndexedPName.UniformBufferBinding, block_bind_index, out info);
                Console.WriteLine("    Block Bound to Binding Point: {0} {{", info);

                int block_active_uniforms;
                GL.GetActiveUniformBlock(test_program, i, ActiveUniformBlockParameter.UniformBlockActiveUniforms, out block_active_uniforms);
                int[] uniform_indices = new int[block_active_uniforms];
                GL.GetActiveUniformBlock(test_program, i, ActiveUniformBlockParameter.UniformBlockActiveUniformIndices, uniform_indices);


                int[] uniform_types = new int[block_active_uniforms];
                int[] uniform_offsets = new int[block_active_uniforms];
                int[] uniform_sizes = new int[block_active_uniforms];

                //Fetch Parameters for all active Uniforms
                GL.GetActiveUniforms(test_program, block_active_uniforms, uniform_indices, ActiveUniformParameter.UniformType, uniform_types);
                GL.GetActiveUniforms(test_program, block_active_uniforms, uniform_indices, ActiveUniformParameter.UniformOffset, uniform_offsets);
                GL.GetActiveUniforms(test_program, block_active_uniforms, uniform_indices, ActiveUniformParameter.UniformSize, uniform_sizes);
                
                for (int k = 0; k < block_active_uniforms; ++k)
                {
                    int actual_name_length;
                    string name;
                    
                    GL.GetActiveUniformName(test_program, uniform_indices[k], 256, out actual_name_length, out name);
                    Console.WriteLine("\t{0}", name);

                    Console.WriteLine("\t\t    type: {0}", uniform_types[k]);
                    Console.WriteLine("\t\t    offset: {0}", uniform_offsets[k]);
                    Console.WriteLine("\t\t    size: {0}", uniform_sizes[k]);
                    
                    /*
                    GL.GetActiveUniforms(test_program, i, ref uniform_indices[k], ActiveUniformParameter.UniformArrayStride, out uniArrayStride);
                    Console.WriteLine("\t\t    array stride: {0}", uniArrayStride);

                    GL.GetActiveUniforms(test_program, i, ref uniform_indices[k], ActiveUniformParameter.UniformMatrixStride, out uniMatStride);
                    Console.WriteLine("\t\t    matrix stride: {0}", uniMatStride);
                    */
                }
                Console.WriteLine("}}");
            }

        }

        public void resize(int w, int h)
        {
            gbuf?.resize(w, h);
            GL.Viewport(0, 0, w, h);
        }


        #region Rendering Methods

        private void resetOcclusionStatus()
        {
            foreach (GLMeshVao m in staticMeshQueue)
            {
                for (int i = 0; i < m.instance_count; i++)
                    m.setInstanceOccludedStatus(i, false);
            }

            foreach (GLMeshVao m in transparentMeshQueue)
            {
                for (int i = 0; i < m.instance_count; i++)
                    m.setInstanceOccludedStatus(i, false);
            }
        }

        private void sortLights()
        {
            Light mainLight = resMgr.GLlights[0];

            resMgr.GLlights.RemoveAt(0);
            
            resMgr.GLlights.Sort(
                delegate (Light l1, Light l2)
                {
                    float d1 = (l1.worldPosition - RenderState.activeCam.Position).Length;
                    float d2 = (l2.worldPosition - RenderState.activeCam.Position).Length;

                    return d1.CompareTo(d2);
                }
            );

            resMgr.GLlights.Insert(0, mainLight);
        }


        private void sortTransparent()
        {
            //I need to check if transparent meshes have more than one instance, because this looks like it is not the case

            transparentMeshQueue.Sort(
                delegate (GLMeshVao l1, GLMeshVao l2)
                {
                    //Calculating distance assuming that the meshes do not move.

                    //TODO: Fix that shit on instanced transparent meshes

                    Vector3 l1_AABBMIN = l1.instanceRefs[0].AABBMIN;
                    Vector3 l1_AABBMAX = l1.instanceRefs[0].AABBMAX;

                    Vector3 l2_AABBMIN = l2.instanceRefs[0].AABBMIN;
                    Vector3 l2_AABBMAX = l2.instanceRefs[0].AABBMAX;

                    //Calculate distance from the point to the AABB
                    Vector3 p = RenderState.activeCam.Position;
                    float d1 = MathUtils.distance_Point_to_AABB_alt(l1_AABBMIN, l1_AABBMAX, p);
                    float d2 = MathUtils.distance_Point_to_AABB_alt(l2_AABBMIN, l2_AABBMAX, p);

                    //float d1 = MathUtils.distance_Point_to_AABB(l1.Bbox[0], l1.Bbox[1], p);
                    //float d2 = MathUtils.distance_Point_to_AABB(l2.Bbox[0], l2.Bbox[1], p);

                    //Console.WriteLine(l1.name + " Distance from camera: {0}", d1);
                    //Console.WriteLine(l2.name + " Distance from camera: {0}", d2);

                    //return d1.CompareTo(d2); //Front to back
                    return d2.CompareTo(d1); //Back To front
                }
            );
        }

        private void LOD_filtering(List<GLMeshVao> model_list)
        {
            /* TODO : REplace this shit with occlusion based on the instance_ids
            foreach (GLMeshVao m in model_list)
            {
                int i = 0;
                int occluded_instances = 0;
                while (i < m.instance_count)
                {
                    //Skip non LODed meshes
                    if (!m.name.Contains("LOD"))
                    {
                        i++;
                        continue;
                    }

                    //Calculate distance from camera
                    Vector3 bsh_center = m.Bbox[0] + 0.5f * (m.Bbox[1] - m.Bbox[0]);

                    //Move sphere to object's root position
                    Matrix4 mat = m.getInstanceWorldMat(i);
                    bsh_center = (new Vector4(bsh_center, 1.0f) * mat).Xyz;

                    double distance = (bsh_center - Common.RenderState.activeCam.Position).Length;

                    //Find active LOD
                    int active_lod = m.parent.LODNum - 1;
                    for (int j = 0; j < m.parentScene.LODNum - 1; j++)
                    {
                        if (distance < m.parentScene.LODDistances[j])
                        {
                            active_lod = j;
                            break;
                        }
                    }

                    //occlude the other LOD levels
                    for (int j = 0; j < m.parentScene.LODNum; j++)
                    {
                        if (j == active_lod)
                            continue;
                        
                        string lod_text = "LOD" + j;
                        if (m.name.Contains(lod_text))
                        {
                            m.setInstanceOccludedStatus(i, true);
                            occluded_instances++;
                        }
                    }
                    
                    i++;
                }

                if (m.instance_count == occluded_instances)
                    m.occluded = true;
            }
            */
        }

        /* NOT USED
        private void frustum_occlusion(List<GLMeshVao> model_list)
        {
            foreach (GLMeshVao m in model_list)
            {
                int occluded_instances = 0;
                for (int i = 0; i < m.instance_count; i++)
                {
                    if (m.getInstanceOccludedStatus(i))
                        continue;
                    
                    if (!RenderState.activeCam.frustum_occlude(m, i))
                    {
                        occludedNum++;
                        occluded_instances++;
                        m.setInstanceOccludedStatus(i, false);
                    }
                }
            }
        }
        */

        private void prepareCommonPerMeshUBOs()
        {
            trippleBufferActiveId = (trippleBufferActiveId + 1) % 3;

            UBOs["_COMMON_PER_MESH"] = trippleBufferUBOs[trippleBufferActiveId];

            WaitSyncStatus result =  GL.ClientWaitSync(trippleBufferSyncStatuses[trippleBufferActiveId], 0, 100);

            while (result == WaitSyncStatus.TimeoutExpired || result == WaitSyncStatus.WaitFailed)
            {
                //Console.WriteLine("Gamithike o dias");
                result = GL.ClientWaitSync(trippleBufferSyncStatuses[trippleBufferActiveId], 0, 100);
            }

            GL.DeleteSync(trippleBufferSyncStatuses[trippleBufferActiveId]);

            //Prepare UBO data
            int ubo_offset = 0;
            int max_ubo_offset = MAX_NUMBER_OF_MESHES * CommonPerMeshUniforms.SizeInBytes;
            
            //Upload StaticMeshes
            foreach (GLMeshVao m in staticMeshQueue)
            {
                prepareCommonPermeshUBO(m, ref ubo_offset);
            }

            //Upload TransparentMeshes
            foreach (GLMeshVao m in transparentMeshQueue)
            {
                prepareCommonPermeshUBO(m, ref ubo_offset);
            }

            if (ubo_offset == 0)
                return;
            
            if (ubo_offset > max_ubo_offset)
                Console.WriteLine("GAMITHIKE O DIAS");

            //at this point the ubo_offset is the actual size of the atlas buffer
            
            //Upload atlas UBO data
            GL.BindBuffer(BufferTarget.UniformBuffer, UBOs["_COMMON_PER_MESH"]);

            //METHOD 2: Use MAP Buffer
            IntPtr ptr = GL.MapBufferRange(BufferTarget.UniformBuffer, IntPtr.Zero,
                ubo_offset, BufferAccessMask.MapUnsynchronizedBit | BufferAccessMask.MapPersistentBit | BufferAccessMask.MapWriteBit);

            //Console.WriteLine(GL.GetError());

            unsafe
            {
                Marshal.Copy(atlas_cpmu, 0, ptr, ubo_offset);
            }

            GL.UnmapBuffer(BufferTarget.UniformBuffer);

        }


        private void renderStaticMeshes()
        {
            //At first render the static meshes
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);

            GLSLHelper.GLSLShaderConfig shader = resMgr.GLShaders[GLSLHelper.SHADER_TYPE.MESH_DEFERRED_SHADER];

            //Enable the Deferred Mesh Shader
            GL.UseProgram(shader.program_id);

            //Render Static Objects
            foreach (GLMeshVao m in staticObjectsQueue)
            {
                GL.BindBufferRange(BufferRangeTarget.UniformBuffer, 1, UBOs["_COMMON_PER_MESH"], (IntPtr)(m.UBO_offset), m.UBO_aligned_size);
                m.render(shader, RENDERPASS.DEFERRED);
            }
                
            //Render static meshes
            foreach (GLMeshVao m in staticMeshQueue)
            {
                if (m.instance_count == 0)
                    continue;
                
                GL.BindBufferRange(BufferRangeTarget.UniformBuffer, 1, UBOs["_COMMON_PER_MESH"],
                    (IntPtr)(m.UBO_offset), m.UBO_aligned_size);

                m.render(shader, RENDERPASS.DEFERRED);
                if (RenderOptions.RenderBoundHulls)
                    m.render(shader, RENDERPASS.BHULL);
            }


            foreach (GLMeshVao m in transparentMeshQueue)
            {
                if (m.instance_count == 0)
                    continue;

                GL.BindBufferRange(BufferRangeTarget.UniformBuffer, 1, UBOs["_COMMON_PER_MESH"],
                    (IntPtr)(m.UBO_offset), m.UBO_aligned_size);

                m.render(shader, RENDERPASS.DEFERRED);
                //if (RenderOptions.RenderBoundHulls)
                //    m.render(RENDERPASS.BHULL);
            }



            /*
            //TESTING - Render Bound Boxes for the transparent meshes
            shader = resMgr.GLShaders[GLSLHelper.SHADER_TYPE.BBOX_SHADER];
            GL.UseProgram(shader.program_id);

            //I don't expect any other object type here
            foreach (GLMeshVao m in transparentMeshQueue)
            {
                if (m.instance_count == 0)
                    continue;

                //GL.BindBufferRange(BufferRangeTarget.UniformBuffer, 1, UBOs["_COMMON_PER_MESH"],
                //    (IntPtr)(m.UBO_offset), m.UBO_aligned_size);

                m.render(shader, RENDERPASS.BHULL);
                //if (RenderOptions.RenderBoundHulls)
                //    m.render(RENDERPASS.BHULL);
            }
            */


        }

        private void renderTransparent(int pass)
        {
            //Restore depth buffer
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, gbuf.dump_fbo);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, gbuf.fbo);
            GL.BlitFramebuffer(0, 0, gbuf.size[0], gbuf.size[1], 0, 0, gbuf.size[0], gbuf.size[1], ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);
            
            GL.BindFramebuffer(FramebufferTarget.FramebufferExt, gbuf.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment3);

            //At first render the static meshes
            GL.Enable(EnableCap.Blend);
            GL.Enable(EnableCap.DepthTest); //Enable depth test
            GL.DepthFunc(DepthFunction.Lequal);
            GL.Disable(EnableCap.CullFace);
            //GL.CullFace(CullFaceMode.Front);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            //GL.BlendFunc(BlendingFactor.Src1Alpha, BlendingFactor.ONem);


            GLSLHelper.GLSLShaderConfig shader = resMgr.GLShaders[GLSLHelper.SHADER_TYPE.MESH_FORWARD_SHADER];

            GL.UseProgram(resMgr.GLShaders[GLSLHelper.SHADER_TYPE.MESH_FORWARD_SHADER].program_id);
            
            //I don't expect any other object type here
            foreach (GLMeshVao m in transparentMeshQueue)
            {
                if (m.instance_count == 0)
                    continue;
                
                GL.BindBufferRange(BufferRangeTarget.UniformBuffer, 1, UBOs["_COMMON_PER_MESH"],
                    (IntPtr)(m.UBO_offset), m.UBO_aligned_size);

                m.render(shader, RENDERPASS.FORWARD);
                //if (RenderOptions.RenderBoundHulls)
                //    m.render(RENDERPASS.BHULL);
            }

            GL.Enable(EnableCap.CullFace);
            GL.Disable(EnableCap.Blend);
        }

        private void renderShadows()
        {
            
        }

        private void renderFinalPass()
        {
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, gbuf.fbo);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment3);
            
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
            GL.BlitFramebuffer(0, 0, gbuf.size[0], gbuf.size[1], 0, 0, gbuf.size[0], gbuf.size[1], 
                ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
        }

        //Rendering Mechanism
        public void render()
        {
            DateTime oldtime = DateTime.UtcNow;
            DateTime temptime;

            gbuf.bind(); //Bing GBuffer

            //Prepare UBOs
            prepareCommonPerFrameUBO();
            
            //Render Shadows
            renderShadows();

            //Sort Lights
            sortLights();
            
            //Sort Transparent Objects
            sortTransparent();
            
            //LOD filtering
            if (RenderOptions.LODFiltering)
            {
                LOD_filtering(staticMeshQueue);
                LOD_filtering(transparentMeshQueue);
            }


            //Prepare Mesh UBO
            prepareCommonPerMeshUBOs();

            //Render octree
            //octree.render(resMgr.GLShaders[GLSLHelper.SHADER_TYPE.BBOX_SHADER].program_id);

            //Render Geometry (Deferred)
            renderStaticMeshes();

            //Render Static Objects  (Forward)
            //renderStaticObjects();

            //Light Pass Deferred
            renderLightPass();
            
            //Light Pass Forward (Transparent/Glowing objects for now
            //renderTransparent(0);

            //Setup FENCE AFTER ALL THE DRAWCALLS ARE DONE
            trippleBufferSyncStatuses[trippleBufferActiveId] = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, 0);
            
            //POST-PROCESSING
            //post_process();

            //Final Pass
            renderFinalPass();
            //TODO
            
            //No need to blit without a renderbuffer
            //gbuf?.stop();

            //Render info right on the 0 buffer
            if (RenderOptions.RenderInfo)
                render_info();
        }

        private void render_lights()
        {

            for (int i = 0; i < resMgr.GLlights.Count; i++)
                resMgr.GLlights[i].meshVao.render(resMgr.GLShaders[GLSLHelper.SHADER_TYPE.MESH_DEFERRED_SHADER], RENDERPASS.DEFERRED);
        }

        private void render_cameras()
        {
            int active_program = resMgr.GLShaders[GLSLHelper.SHADER_TYPE.BBOX_SHADER].program_id;

            GL.UseProgram(active_program);
            int loc;
            //Send object world Matrix to all shaders

            foreach (Camera cam in resMgr.GLCameras)
            {
                //Old rendering the inverse clip space
                //Upload uniforms
                //loc = GL.GetUniformLocation(active_program, "self_mvp");
                //Matrix4 self_mvp = cam.viewMat;
                //GL.UniformMatrix4(loc, false, ref self_mvp);

                //New rendering the exact frustum plane
                loc = GL.GetUniformLocation(active_program, "worldMat");
                Matrix4 test = Matrix4.Identity;
                test[0, 0] = -1.0f;
                test[1, 1] = -1.0f;
                test[2, 2] = -1.0f;
                GL.UniformMatrix4(loc, false, ref test);

                //Render all inactive cameras
                if (!cam.isActive) cam.render();

            }

        }

        private void render_info()
        {
            //GL.Clear(ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.Blend);
            GL.Disable(EnableCap.DepthTest);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

            float text_pos_x = gbuf.size[0] - 20.0f;
            float text_pos_y = 80.0f;


            txtRenderer.clearPrimitives();
            //txtRenderer.clearNonStaticPrimitives();


            //Update only dynamic stuff
            
            System.Drawing.SizeF size = new System.Drawing.SizeF();
            Vector3 pos;
            for (int i = 0; i < 5; i++)
            {
                //Set Vector Pos
                pos.X = text_pos_x;
                pos.Y = text_pos_y;
                pos.Z = 0;
                switch (i)
                {
                    case 0:
                        size = txtRenderer.addDrawing(string.Format("FPS: {0:F1}", RenderStats.fpsCount),
                            pos, System.Drawing.Color.Yellow, MVCore.Text.GLTEXT_INDEX.FPS, false);
                        break;
                    case 1:
                        size = txtRenderer.addDrawing(string.Format("OccludedNum: {0:D1}", RenderStats.occludedNum),
                            pos, System.Drawing.Color.Yellow, MVCore.Text.GLTEXT_INDEX.FPS, false);
                        break;
                    case 2:
                        size = txtRenderer.addDrawing(string.Format("Total Vertices: {0:D1}", RenderStats.vertNum),
                            pos, System.Drawing.Color.Yellow, MVCore.Text.GLTEXT_INDEX.FPS, false);
                        break;
                    case 3:
                        size = txtRenderer.addDrawing(string.Format("Total Triangles: {0:D1}", RenderStats.trisNum),
                            pos, System.Drawing.Color.Yellow, MVCore.Text.GLTEXT_INDEX.FPS, false);
                        break;
                    case 4:
                        size = txtRenderer.addDrawing(string.Format("Textures: {0:D1}", RenderStats.texturesNum),
                            pos, System.Drawing.Color.Yellow, MVCore.Text.GLTEXT_INDEX.FPS, false);
                        break;
                }
                text_pos_y -= (size.Height + 2.0f);
            }
            
            //Render drawings
            txtRenderer.update();
            txtRenderer.render(gbuf.size[0], gbuf.size[1]);

            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.DepthTest);

        }


        private void render_quad(string[] uniforms, float[] uniform_values, string[] sampler_names, int[] texture_ids, GLSLHelper.GLSLShaderConfig shaderConf)
        {
            int quad_vao = resMgr.GLPrimitiveVaos["default_renderquad"].vao_id;


            GL.UseProgram(shaderConf.program_id);
            GL.BindVertexArray(quad_vao);

            //Upload samplers
            for (int i = 0; i < sampler_names.Length; i++)
            {
                if (shaderConf.uniformLocations.ContainsKey(sampler_names[i]))
                {
                    GL.Uniform1(shaderConf.uniformLocations[sampler_names[i]], i);
                    GL.ActiveTexture(TextureUnit.Texture0 + i);
                    GL.BindTexture(TextureTarget.Texture2DMultisample, texture_ids[i]);
                }
            }

            //Upload uniforms - Assuming single float uniforms for now
            for (int i = 0; i < uniforms.Length; i++)
            {
                if (shaderConf.uniformLocations.ContainsKey(uniforms[i]))
                {
                    GL.Uniform1(shaderConf.uniformLocations[uniforms[i]], i);
                }
            }

            //Render quad
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (IntPtr)0);
            GL.BindVertexArray(0);
        
        }

        private void bloom()
        {

            //TODO: I THINK THIS IS BROKEN - NEEDS RE-FIX IF ENABLED AT ALL
            GL.Disable(EnableCap.DepthTest);
            
            //Apply Gaussian Blur Passes
            GLSLHelper.GLSLShaderConfig pass_through_program = resMgr.GLShaders[GLSLHelper.SHADER_TYPE.PASSTHROUGH_SHADER];
            GLSLHelper.GLSLShaderConfig gs_program = resMgr.GLShaders[GLSLHelper.SHADER_TYPE.GAUSSIAN_BLUR_SHADER];
            int quad_vao = resMgr.GLPrimitiveVaos["default_renderquad"].vao_id;

            for (int i=0; i < 10; i++)
            {
                //Apply Gaussian Blur Shader
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, gbuf.dump_fbo);
                GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
                GL.Clear(ClearBufferMask.ColorBufferBit);
                
                GL.UseProgram(gs_program.program_id);


                render_quad(new string[] { }, new float[] { }, new string[] { "diffuseTex" }, new int[] { gbuf.dump_rgba8_1 }, gs_program);
                
                //Use passthrough shader to pass the dump texture back to the bloom
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, gbuf.fbo);
                GL.DrawBuffer(DrawBufferMode.ColorAttachment3);
                GL.Clear(ClearBufferMask.ColorBufferBit);
                
                render_quad(new string[] { }, new float[] { }, new string[] { "InTex" }, new int[] { gbuf.dump_rgba8_1 }, pass_through_program);

            }
        }

        private void post_process()
        {
            bloom();
        }

        private void renderLightPass()
        {

            GLSLHelper.GLSLShaderConfig shader_conf = resMgr.GLShaders[GLSLHelper.SHADER_TYPE.GBUFFER_SHADER];

            //Backup the depth buffer to the secondary fbo
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, gbuf.fbo);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, gbuf.dump_fbo);
            GL.BlitFramebuffer(0, 0, gbuf.size[0], gbuf.size[1], 0, 0, gbuf.size[0], gbuf.size[1], 
                ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);

            //Bind default fbo
            GL.BindFramebuffer(FramebufferTarget.FramebufferExt, gbuf.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment3); //Draw to the light color channel only
            
            GL.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            render_quad(new string[] { }, new float[] { }, new string[] { "albedoTex", "positionTex", "normalTex", "depthTex", "parameterTex", "parameter2Tex" }, 
                                                            new int[] { gbuf.albedo,  gbuf.positions, gbuf.normals, gbuf.dump_depth, gbuf.info, gbuf.info2}, shader_conf);

        }

        #endregion Rendering Methods

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cleanup(); //Clean local resources
                    gbuf.Dispose(); //Dispose gbuffer
                    txtRenderer.Dispose(); //Dispose textRenderer
                    shdwRenderer.Dispose(); //Dispose shadowRenderer
                }

                disposedValue = true;
            }
        }

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

}
