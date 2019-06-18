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
    public class renderManager : baseResourceManager, IDisposable
    {
        List<model> staticMeshQeueue = new List<model>();
        List<model> movingMeshQeueue = new List<model>();
        List<model> transparentMeshQeueue = new List<model>();
        public ResourceManager resMgr; //REf to the active resource Manager
        //Control Font and Text Objects
        private Text.TextRenderer txtRenderer;

        private GBuffer gbuf;
        private Dictionary<string, int> UBOs = new Dictionary<string, int>();

        //Local Counters
        private int occludedNum;

        private static Dictionary<string, int> UBOVarOffsets = new Dictionary<string, int>
            {   {"diffuseFlag" , 0 },
                {"use_lighting" , 4 },
                {"mvp" , 8 },
                {"rotMat" , 72 },
                {"worldMat",  136},
                {"nMat",  200},
                {"selected",  264},
                {"skinned",  268},
                {"color",  272},
                {"skinMats",  284}
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

            public static readonly int SizeInBytes = 144;
        };

        [StructLayout(LayoutKind.Explicit)]
        struct CommonPerMeshUniforms
        {
            [FieldOffset(0)] //64 Bytes
            public Matrix4 worldMat;
            [FieldOffset(64)] //64 Bytes
            public Matrix4 nMat;
            [FieldOffset(128)] //4 bytes
            public unsafe fixed float skinMats[80 * 16]; //This is mapped to mat4 //5120 bytes
            [FieldOffset(5248)]
            public Vector3 color; //12 bytes
            [FieldOffset(5260)] 
            public float skinned; //4 bytes (aligns to 4 bytes)
            [FieldOffset(5264)] 
            public float selected; //4 Bytes
            public static readonly int SizeInBytes = 5268;
        };


        public void init(ResourceManager input_resMgr)
        {
            //Setup Resource Manager
            resMgr = input_resMgr;

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
            process_models(root);
        }

        private void process_models(GMDL.model root)
        {
            //Preprocess models
            if (root.type == TYPES.MESH)
            {
                meshModel m = (meshModel)root;

                //Check if the model has a transparent material
                if (m.Material.has_flag((TkMaterialFlags.MaterialFlagEnum) TkMaterialFlags.UberFlagEnum._F22_TRANSPARENT_SCALAR))
                {
                    transparentMeshQeueue.Add(m);
                }
                else
                {
                    staticMeshQeueue.Add(m);
                }
            }
            else
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
            GL.BufferData(BufferTarget.UniformBuffer, CommonPerFrameUniforms.SizeInBytes + CommonPerMeshUniforms.SizeInBytes, IntPtr.Zero, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
            //Store buffer to dictionary
            UBOs["Uniforms"] = ubo_id;
            
            //Attach programs to UBO and binding point
            attachUBOToShaderBindingPoint("MESH_SHADER", "Uniforms", 0);
            attachUBOToShaderBindingPoint("LOCATOR_SHADER", "Uniforms", 0);

            //Attach the generated buffers to the binding points
            bindUBOs();

            reportUBOs();
        }
        
        //This method attaches UBOs to shader binding points
        private void attachUBOToShaderBindingPoint(string shader, string var_name, int binding_point)
        {
            //Binding Position 0 - Matrices UBO
            int ubo_index = GL.GetUniformBlockIndex(resMgr.GLShaders[shader].program_id, var_name);
            GL.UniformBlockBinding(resMgr.GLShaders[shader].program_id, ubo_index, binding_point);
        }
        
        //This method updates UBO data for rendering
        private void prepareCommonPerFrameUBO()
        {
            //Prepare Struct
            CommonPerFrameUniforms cpfu;
            cpfu.diffuseFlag = RenderOptions._useTextures;
            cpfu.use_lighting = RenderOptions._useLighting;
            cpfu.mvp = RenderState.mvp;
            cpfu.rotMat = RenderState.rotMat;


            GL.BindBuffer(BufferTarget.UniformBuffer, UBOs["Uniforms"]);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, CommonPerFrameUniforms.SizeInBytes, ref cpfu);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
        }

        private void prepareCommonPermeshUBO(model m)
        {
            //Prepare Struct
            CommonPerMeshUniforms cpmu;
            cpmu.worldMat = m.worldMat;
            Matrix4 nMat = (m.worldMat * RenderState.rotMat).Inverted();
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
                }

                cpmu.skinned = (float) mm.skinned;
                cpmu.color = mm.color;
            }
            else
            {
                cpmu.skinned = 0.0f;
                cpmu.color = new Vector3(1.0f, 0.0f, 0.0f);
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
            int test_program = resMgr.GLShaders["MESH_SHADER"].program_id;
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
                    int actual_name_length, uniType, uniOffset, uniSize, uniArrayStride, uniMatStride;
                    string name;
                    int uni_params;

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

        private void renderStatic(int pass)
        {
            //At first render the static meshes
            GL.Enable(EnableCap.Texture2D);
            GL.Enable(EnableCap.DepthTest);

            foreach (model m in staticMeshQeueue)
            {
                if (m.renderable)
                {
                    if (m.type == TYPES.MESH)
                    {
                        //Object program
                        //Local Transformation is the same for all objects 
                        //Pending - Personalize local matrix on each object
                        //loc = GL.GetUniformLocation(active_program, "light");
                        //GL.Uniform3(loc, resMgr.GLlights[0].localPosition);

                        //Upload Light Intensity
                        //loc = GL.GetUniformLocation(active_program, "intensity");
                        //GL.Uniform1(210, resMgr.GLlights[0].intensity);

                        //Upload camera position as the light
                        //GL.Uniform3(loc, cam.Position);

                        //Apply frustum culling only for mesh objects
                        if (RenderState.activeCam.frustum_occlude((meshModel)m, RenderState.rotMat))
                        {
                            prepareCommonPermeshUBO(m); //Update UBO based on current model
                            m.render(pass);
                        }   
                        else
                            occludedNum++;

                    }
                    else if (m.type == TYPES.JOINT && RenderOptions.RenderJoints)
                    {
                        prepareCommonPermeshUBO(m); //Update UBO based on current model
                        m.render(pass);
                    }
                    else if (m.type == TYPES.COLLISION && RenderOptions.RenderCollisions)
                    {
                        prepareCommonPermeshUBO(m); //Update UBO based on current model
                        m.render(pass);
                    }
                    else if (m.type == TYPES.LOCATOR || m.type == TYPES.MODEL || m.type == TYPES.LIGHT)
                    {
                        prepareCommonPermeshUBO(m); //Update UBO based on current model
                        m.render(pass);
                    }
                }
            }
        }

        private void renderTransparent(int pass)
        {
            int loc; //Used for fetching uniform locations
            //At first render the static meshes
            GL.Enable(EnableCap.Texture2D);
            GL.Enable(EnableCap.Blend);            GL.DepthMask(false); //Disable writing to the depth mask


            //Since transparentMeshQueue has been populated from meshModels
            //I don't expect any other object type here
            foreach (model m in transparentMeshQeueue)
            {
                if (m.renderable)
                {
                    if (m.type == TYPES.MESH)
                    {
                        //Object program
                        //Local Transformation is the same for all objects 
                        //Pending - Personalize local matrix on each object
                        //loc = GL.GetUniformLocation(active_program, "light");
                        //GL.Uniform3(loc, resMgr.GLlights[0].localPosition);

                        //Upload Light Intensity
                        //loc = GL.GetUniformLocation(active_program, "intensity");
                        //GL.Uniform1(210, resMgr.GLlights[0].intensity);

                        //Upload camera position as the light
                        //GL.Uniform3(loc, cam.Position);

                        //Apply frustum culling only for mesh objects
                        if (RenderState.activeCam.frustum_occlude((meshModel)m, RenderState.rotMat))
                        {
                            prepareCommonPermeshUBO(m); //Update UBO based on current model
                            m.render(pass);
                        }
                        else occludedNum++;
                    }
                }
            }

            GL.DepthMask(true); //Re-enable writing to the depth buffer
            GL.Disable(EnableCap.Blend);
        }

        //Rendering Mechanism
        public void render(int pass)
        {
            occludedNum = 0; //Reset  Counter

            gbuf.start(); //Start Gbuffer

            //Prepare UBOs
            prepareCommonPerFrameUBO();

            renderStatic(pass);
            renderTransparent(pass);

            //Store the dumps

            //gbuf.dump();
            //render_decals();

            //render_cameras();

            if (RenderOptions._renderLights)
                render_lights();

            //Dump Gbuffer
            //gbuf.dump();
            //System.Threading.Thread.Sleep(1000);

            //Render Deferred
            gbuf.render();

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
            int active_program = resMgr.GLShaders["BBOX_SHADER"].program_id;

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


            txtRenderer.clearPrimities();

            System.Drawing.SizeF size = new System.Drawing.SizeF();
            for (int i = 0; i < 5; i++)
            {
                switch (i)
                {
                    case 0:
                        size = txtRenderer.addDrawing(string.Format("FPS: {0:F1}", RenderStats.fpsCount),
                            new Vector3(text_pos_x, text_pos_y, 0.0f), System.Drawing.Color.Yellow, MVCore.Text.GLTEXT_INDEX.FPS);
                        break;
                    case 1:
                        size = txtRenderer.addDrawing(string.Format("OccludedNum: {0:D1}", occludedNum),
                            new Vector3(text_pos_x, text_pos_y, 0.0f), System.Drawing.Color.Yellow, MVCore.Text.GLTEXT_INDEX.FPS);
                        break;
                    case 2:
                        size = txtRenderer.addDrawing(string.Format("Total Vertices: {0:D1}", RenderStats.vertNum),
                            new Vector3(text_pos_x, text_pos_y, 0.0f), System.Drawing.Color.Yellow, MVCore.Text.GLTEXT_INDEX.FPS);
                        break;
                    case 3:
                        size = txtRenderer.addDrawing(string.Format("Total Triangles: {0:D1}", RenderStats.trisNum),
                            new Vector3(text_pos_x, text_pos_y, 0.0f), System.Drawing.Color.Yellow, MVCore.Text.GLTEXT_INDEX.FPS);
                        break;
                    case 4:
                        size = txtRenderer.addDrawing(string.Format("Textures: {0:D1}", RenderStats.texturesNum),
                            new Vector3(text_pos_x, text_pos_y, 0.0f), System.Drawing.Color.Yellow, MVCore.Text.GLTEXT_INDEX.FPS);
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
