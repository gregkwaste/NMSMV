using System;
using System.Collections.Generic;
using System.Text;
using OpenTK;
using OpenTK.Graphics.OpenGL4;
using libMBIN.NMS.Toolkit;
using MVCore.GMDL;
using MVCore.Common;

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

        //Local Counters
        private int occludedNum;
        
        
        public void init(ResourceManager input_resMgr)
        {
            //Setup text renderer
            setupTextRenderer();
            //Setup Resource Manager
            resMgr = input_resMgr;
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

        public void resize(int w, int h)
        {
            gbuf?.resize(w, h);
            GL.Viewport(0, 0, w, h);
        }


        #region Rendering Methods

        private void renderStatic(int pass)
        {
            int loc; //Used for fetching uniform locations
            //At first render the static meshes
            GL.Enable(EnableCap.Texture2D);
            GL.Enable(EnableCap.DepthTest);
            
            foreach (model m in staticMeshQeueue)
            {
                int active_program = m.shader_programs[pass];
                if (active_program == -1)
                    throw new ApplicationException("Shit program");

                GL.UseProgram(active_program);
                
                if (m.renderable)
                {
                    Matrix4 wMat = m.worldMat;
                    GL.UniformMatrix4(10, false, ref wMat);

                    //Send mvp to all shaders
                    GL.UniformMatrix4(7, false, ref RenderState.mvp);

                    //Upload Selected Flag
                    GL.Uniform1(208, m.selected);

                    if (m.type == TYPES.MESH)
                    {
                        //Sent rotation matrix individually for light calculations
                        GL.UniformMatrix4(9, false, ref RenderState.rotMat);

                        //Send DiffuseFlag
                        GL.Uniform1(206, RenderOptions._useTextures);

                        //Upload Selected Flag
                        GL.Uniform1(207, RenderOptions._useLighting);

                        //Object program
                        //Local Transformation is the same for all objects 
                        //Pending - Personalize local matrix on each object
                        loc = GL.GetUniformLocation(active_program, "light");
                        GL.Uniform3(loc, resMgr.GLlights[0].localPosition);

                        //Upload Light Intensity
                        loc = GL.GetUniformLocation(active_program, "intensity");
                        GL.Uniform1(210, resMgr.GLlights[0].intensity);


                        //Upload camera position as the light
                        //GL.Uniform3(loc, cam.Position);

                        //Apply frustum culling only for mesh objects
                        if (RenderState.activeCam.frustum_occlude((meshModel)m, RenderState.rotMat))
                            m.render(pass);
                        else
                            occludedNum++;

                    }
                    else if (m.type == TYPES.JOINT)
                    {
                        if (RenderOptions.RenderJoints)
                            m.render(pass);
                    }
                    else if (m.type == TYPES.COLLISION)
                    {
                        if (RenderOptions.RenderCollisions)
                        {
                            //Send DiffuseFlag
                            GL.Uniform1(206, 0.0f);

                            //Upload Selected Flag
                            GL.Uniform1(207, 0.0f);
                            m.render(pass);
                        }

                    }
                    else if (m.type == TYPES.LOCATOR || m.type == TYPES.MODEL || m.type == TYPES.LIGHT)
                    {
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
                int active_program = m.shader_programs[pass];
                if (active_program == -1)
                    throw new ApplicationException("Shit program");

                GL.UseProgram(active_program);
                
                if (m.renderable)
                {
                    Matrix4 wMat = m.worldMat;
                    GL.UniformMatrix4(10, false, ref wMat);

                    //Send mvp to all shaders
                    GL.UniformMatrix4(7, false, ref RenderState.mvp);

                    //Upload Selected Flag
                    GL.Uniform1(208, m.selected);

                    //TODO: Remove that check, all objects should be meshes
                    if (m.type == TYPES.MESH)
                    {

                        //Sent rotation matrix individually for light calculations
                        GL.UniformMatrix4(9, false, ref RenderState.rotMat);

                        //Send DiffuseFlag
                        GL.Uniform1(206, RenderOptions._useTextures);

                        //Upload Selected Flag
                        GL.Uniform1(207, RenderOptions._useLighting);

                        //Object program
                        //Local Transformation is the same for all objects 
                        //Pending - Personalize local matrix on each object
                        loc = GL.GetUniformLocation(active_program, "light");
                        GL.Uniform3(loc, resMgr.GLlights[0].localPosition);

                        //Upload Light Intensity
                        loc = GL.GetUniformLocation(active_program, "intensity");
                        GL.Uniform1(210, resMgr.GLlights[0].intensity);

                        //Upload camera position as the light
                        //GL.Uniform3(loc, cam.Position);

                        //Apply frustum culling only for mesh objects
                        if (RenderState.activeCam.frustum_occlude((meshModel)m, RenderState.rotMat))
                            m.render(pass);
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
            int active_program = MVCore.Common.RenderState.activeResMgr.GLShaders["LIGHT_SHADER"];
            GL.UseProgram(active_program);

            //Send mvp to all shaders
            int loc = GL.GetUniformLocation(active_program, "mvp");
            GL.UniformMatrix4(loc, false, ref RenderState.mvp);
            for (int i = 0; i < resMgr.GLlights.Count; i++)
                resMgr.GLlights[i].render(0);
        }

        private void render_cameras()
        {
            int active_program = resMgr.GLShaders["BBOX_SHADER"];

            GL.UseProgram(active_program);
            int loc;
            //Send mvp matrix to all shaders
            loc = GL.GetUniformLocation(active_program, "mvp");
            GL.UniformMatrix4(loc, false, ref RenderState.activeCam.viewMat);
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
