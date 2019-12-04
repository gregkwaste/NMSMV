using System;
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
        public unsafe fixed float lights[32 * 64];


        public static readonly int SizeInBytes = 2224;
    };

    [StructLayout(LayoutKind.Explicit)]
    struct CommonPerMeshUniforms
    {
        [FieldOffset(0)] //64 Bytes
        public Matrix4 nMat;
        [FieldOffset(64)] //4 bytes
        public unsafe fixed float skinMats[80 * 16]; //This is mapped to mat4 //5120 bytes
        [FieldOffset(5184)]
        public Vector4 gUserDataVec4;
        [FieldOffset(5200)]
        public Vector3 color; //12 bytes
        [FieldOffset(5212)]
        public float skinned; //4 bytes (aligns to 4 bytes)
        [FieldOffset(5216)]
        public float selected; //4 Bytes
        [FieldOffset(5232)] //64*100 Bytes
        public unsafe fixed float worldMats[300 * 16];

        public static readonly int SizeInBytes = 24432;
    };


    public class renderManager : baseResourceManager, IDisposable
    {
        List<model> staticMeshQeueue = new List<model>();
        List<model> movingMeshQeueue = new List<model>();
        List<model> transparentMeshQeueue = new List<model>();
        public ResourceManager resMgr; //REf to the active resource Manager

        public ShadowRenderer shdwRenderer; //Shadow Renderer instance
        //Control Font and Text Objects
        private Text.TextRenderer txtRenderer;
        public int last_text_height;
        
        private GBuffer gbuf;
        private Dictionary<string, int> UBOs = new Dictionary<string, int>();

        //Local Counters
        private int occludedNum;

        //UBO structs
        CommonPerFrameUniforms cpfu;
        
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
        }

        public void setupGBuffer(int width, int height)
        {
            //Create gbuffer
            gbuf = new GBuffer(resMgr, width, height);
        }


        public void clearInstances()
        {
            foreach (model m in staticMeshQeueue)
            {
                if (m is meshModel mm)
                    mm.instances.Clear();
            }

            foreach (model m in transparentMeshQeueue)
            {
                if (m is meshModel mm)
                    mm.instances.Clear();
            }
        }

        public void cleanup()
        {
            //Just cleanup the queues
            //The resource manager will handle the cleanup of the buffers and shit

            staticMeshQeueue.Clear();
            movingMeshQeueue.Clear();
            transparentMeshQeueue.Clear();
        }

        public void populate(GMDL.model root)
        {
            cleanup();
            staticMeshQeueue.Add(root); //TODO: Think of a better way
            foreach (scene s in resMgr.GLScenes.Values)
                process_models(s);
        }

        private void process_models(GMDL.model root)
        {
            if (root.type == TYPES.MESH) {
                meshModel m = (meshModel) root;

                //Check if the model has a transparent material
                if (m.Material.has_flag((TkMaterialFlags.MaterialFlagEnum) TkMaterialFlags.UberFlagEnum._F22_TRANSPARENT_SCALAR) ||
                    m.Material.has_flag((TkMaterialFlags.MaterialFlagEnum)TkMaterialFlags.UberFlagEnum._F09_TRANSPARENT))
                {
                    transparentMeshQeueue.Add(m);
                }
                else
                {
                    staticMeshQeueue.Add(m);
                }
            } else
            {
                staticMeshQeueue.Add(root); //For now add everything else to the static mesh queue
            }

            //Repeat process with children
            foreach (GMDL.model child in root.children)
            {
                process_models(child);
            }
        }

        public void setupTextRenderer()
        {
            //Use QFont
            //string font = "C:\\WINDOWS\\FONTS\\LUCON.TTF";
            string font = "C:\\WINDOWS\\FONTS\\ARIAL.TTF";
            txtRenderer = new MVCore.Text.TextRenderer(font, 10);
        }

        private void setupUBOs()
        {
            //Generate a UBO for CommonPerFrameUniforms & CommonPerMeshUniform
            //CommonPerFrame Uniforms Size: 4 + 4 + 16 * 4 + 16 * 4 = 136
            //CommonPerMesh Uniforms Size: 16 * 4 + 16 * 4 + 80 * 16 * 4 + 4 + 4 + 3 * 4 = 5268
            //Total Size: 136 + 5268 = 5404 Bytes

            int ubo_id = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.UniformBuffer, ubo_id);
            GL.BufferData(BufferTarget.UniformBuffer, CommonPerFrameUniforms.SizeInBytes + CommonPerMeshUniforms.SizeInBytes, IntPtr.Zero, BufferUsageHint.StaticRead);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
            //Store buffer to dictionary
            UBOs["Uniforms"] = ubo_id;
            
            //Attach programs to UBO and binding point
            attachUBOToShaderBindingPoint(GLSLHelper.SHADER_TYPE.MESH_SHADER, "Uniforms", 0);
            attachUBOToShaderBindingPoint(GLSLHelper.SHADER_TYPE.LOCATOR_SHADER, "Uniforms", 0);
            attachUBOToShaderBindingPoint(GLSLHelper.SHADER_TYPE.JOINT_SHADER, "Uniforms", 0);
            attachUBOToShaderBindingPoint(GLSLHelper.SHADER_TYPE.GBUFFER_SHADER, "Uniforms", 0);

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

            //Upload light information
            for (int i = 0; i < Math.Min(32, resMgr.GLlights.Count); i++)
            {
                Light l = resMgr.GLlights[i];
                if (!l.update_changes)
                    continue;
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

            
            GL.BindBuffer(BufferTarget.UniformBuffer, UBOs["Uniforms"]);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, CommonPerFrameUniforms.SizeInBytes, ref cpfu);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
        }

        private void prepareCommonPermeshUBO(model m)
        {
            //Prepare Struct
            CommonPerMeshUniforms cpmu;
            Matrix4 nMat = m.worldMat.Inverted();
            nMat.Transpose();
            cpmu.nMat = nMat;
            cpmu.selected = m.selected;
            
            if (m.type == TYPES.MESH)
            {
                //Copy SkinMatrices
                meshModel mm = m as meshModel;

                unsafe
                {
                    //This is the worst way possible....
                    for (int i = 0; i < mm.BoneRemapIndicesCount * 16; i++)
                    {
                        cpmu.skinMats[i] = mm.BoneRemapMatrices[i];
                    }

                    //Store the intance world Matrices
                    for (int i = 0; i < Math.Min(64, mm.instances.Count); i++)
                    {
                        for (int j = 0; j < 4; j++)
                        {
                            for (int k = 0; k < 4; k++)
                            {
                                cpmu.worldMats[16 * i + 4 * j + k] = mm.instances[i][j, k];
                            }
                        }
                    }
                }


                cpmu.skinned = (float) mm.skinned;
                cpmu.color = mm.color;

                //Copy custom mesh parameters
                cpmu.gUserDataVec4 = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
            }
            else
            {
                unsafe
                {
                    for (int j = 0; j < 4; j++)
                    {
                        for (int k = 0; k < 4; k++)
                        {
                            cpmu.worldMats[4 * j + k] = m.worldMat[j, k];
                        }
                    }
                }
                
                cpmu.skinned = 0.0f;
                cpmu.color = new Vector3(1.0f, 0.0f, 0.0f);
                cpmu.gUserDataVec4 = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
            }

            //Updates matrices UBO
            GL.BindBuffer(BufferTarget.UniformBuffer, UBOs["Uniforms"]);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero + CommonPerFrameUniforms.SizeInBytes, CommonPerMeshUniforms.SizeInBytes, ref cpmu);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
        }

        //This Method binds UBos to binding points
        private void bindUBOs()
        {
            //Bind Matrices
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, UBOs["Uniforms"]);
        }

        private void reportUBOs()
        {
            //Print Debug Information for the UBO
            // Get named blocks info
            int count, dataSize, info, length;
            int test_program = resMgr.GLShaders[GLSLHelper.SHADER_TYPE.MESH_SHADER].program_id;
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
            foreach (model m in staticMeshQeueue)
            {
                m.occluded = false;
            }

            foreach (model m in transparentMeshQeueue)
            {
                m.occluded = false;
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
                    float d2 = (l1.worldPosition - RenderState.activeCam.Position).Length;

                    return d1.CompareTo(d2);
                }
            );

            resMgr.GLlights.Insert(0, mainLight);
        }
        
        private void LOD_filtering(List<model> model_list)
        {
            foreach (model m in model_list)
            {
                switch (m.type)
                {
                    case TYPES.MESH:
                        {
                            meshModel mm = m as meshModel;
                            int i = 0;
                            while (i < mm.instances.Count)
                            {
                                //Skip non LODed meshes
                                if (!m.name.Contains("LOD"))
                                {
                                    i++;
                                    continue;
                                }

                                //Calculate distance from camera
                                Vector3 bsh_center = mm.Bbox[0] + 0.5f * (mm.Bbox[1] - mm.Bbox[0]);

                                //Move sphere to object's root position
                                bsh_center = (new Vector4(bsh_center, 1.0f) * mm.instances[i]).Xyz;

                                double distance = (bsh_center - Common.RenderState.activeCam.Position).Length;
                                

                                for (int j = 0; j < mm.LODDistances.Count; j++)
                                {
                                    string lod_text = "LOD" + j;
                                    if (m.name.Contains(lod_text) && distance > mm.LODDistances[j])
                                    {
                                        mm.instances.RemoveAt(i);
                                    }
                                }

                                i++;
                            }

                            if (mm.instances.Count == 0)
                                m.occluded = true;

                            break;
                        }
                    default:
                        break;
                }

                
            }
        }

        private void frustum_occlusion(List<model> model_list)
        {
            foreach (model m in model_list)
            {
                if (!m.renderable) continue;
                if (m.occluded) continue;

                switch (m.type)
                {
                    case TYPES.MESH:
                        {
                            meshModel mm = m as meshModel;
                            int i = 0;
                            while (i < mm.instances.Count)
                            {
                                if (!RenderState.activeCam.frustum_occlude(mm, mm.instances[i], Matrix4.Identity))
                                {
                                    occludedNum++;
                                    mm.instances.RemoveAt(i);
                                }
                                else
                                    i++;
                            }

                            if (mm.instances.Count == 0)
                                m.occluded = true;

                            break;
                        }
                    default:
                        break;
                }
            }
        }


        private void renderMain()
        {
            foreach (model m in staticMeshQeueue)
            {
                if (!m.renderable) continue;
                if (m.occluded) continue;

                prepareCommonPermeshUBO(m);
                m.render(RENDERPASS.MAIN);
                if (RenderOptions.RenderBoundHulls && (m.type == TYPES.MESH))
                    m.render(RENDERPASS.BHULL);

            }
        }

        private void renderStatic()
        {
            //At first render the static meshes
            GL.Enable(EnableCap.DepthTest);
            //GL.Enable(EnableCap.CullFace);

            renderMain();
        }

        private void renderTransparent(int pass)
        {
            int loc; //Used for fetching uniform locations
            //At first render the static meshes
            GL.Enable(EnableCap.Texture2D);
            GL.Enable(EnableCap.Blend);
            GL.DepthMask(false); //Disable writing to the depth mask

            //Since transparentMeshQueue has been populated from meshModels
            //I don't expect any other object type here
            foreach (model m in transparentMeshQeueue)
            {
                if (!m.renderable) continue;
                if (m.occluded) continue;

                if (m.type == TYPES.MESH)
                {
                    prepareCommonPermeshUBO(m); //Update UBO based on current model
                    m.render(RENDERPASS.MAIN);
                    if (RenderOptions.RenderBoundHulls && (m.type == TYPES.MESH))
                        m.render(RENDERPASS.BHULL);
                }
                
            }

            GL.DepthMask(true); //Re-enable writing to the depth buffer
            GL.Disable(EnableCap.Blend);
        }

        private void renderShadows()
        {
            
        }

        //Rendering Mechanism
        public void render()
        {
            occludedNum = 0; //Reset  Counter

            gbuf.bind(); //Bing GBuffer

            //Prepare UBOs
            prepareCommonPerFrameUBO();

            //Render Shadows
            renderShadows();

            //Occlusion
            resetOcclusionStatus();

            //Sort Lights
            sortLights();

            //LOD filtering
            if (RenderOptions.LODFiltering)
            {
                LOD_filtering(staticMeshQeueue);
                LOD_filtering(transparentMeshQeueue);
            }
            
            //Apply frustum culling
            if (RenderOptions.UseFrustumCulling)
            {
                frustum_occlusion(staticMeshQeueue);
                frustum_occlusion(transparentMeshQeueue);
            }
            
            //Render Geometry
            renderStatic();
            renderTransparent(0);

            //Store the dumps

            //gbuf.dump();
            //render_decals();
            //render_cameras();

            //Dump Gbuffer
            //gbuf.dump();
            //System.Threading.Thread.Sleep(1000);

            //POST-PROCESSING
            //post_process();

            //Light Pass
            renderLightPass();


            //Final Pass
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
                resMgr.GLlights[i].render(0);
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
                        size = txtRenderer.addDrawing(string.Format("OccludedNum: {0:D1}", occludedNum),
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
                
                render_quad(new string[] { }, new float[] { }, new string[] { "InTex" }, new int[] { gbuf.dump_rgba8_2 }, pass_through_program);

            }
        }

        private void post_process()
        {
            bloom();
        }

        private void renderLightPass()
        {

            GLSLHelper.GLSLShaderConfig shader_conf = resMgr.GLShaders[GLSLHelper.SHADER_TYPE.GBUFFER_SHADER];

            //Bind default fbo
            GL.BindFramebuffer(FramebufferTarget.FramebufferExt, gbuf.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment3); //Draw to the light color channel only
            
            GL.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            render_quad(new string[] { }, new float[] { }, new string[] { "albedoTex", "positionTex", "normalTex", "depthTex", "parameterTex" }, 
                                                            new int[] { gbuf.albedo,  gbuf.positions, gbuf.normals, gbuf.depth, gbuf.info}, shader_conf);

            //Blit buffer to the default framebuffer

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, gbuf.fbo);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment3);

            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);

            GL.BlitFramebuffer(0, 0, gbuf.size[0], gbuf.size[1], 0, 0, gbuf.size[0], gbuf.size[1], ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
            
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
