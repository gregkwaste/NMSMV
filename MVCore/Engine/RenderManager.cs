using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using GLSLHelper;
using libMBIN.NMS.Toolkit;
using MVCore.Common;
using MVCore.GMDL;
using MVCore.Engine;
using OpenTK;
using OpenTK.Graphics.OpenGL4;


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
        [FieldOffset(12)]
        public float MSAA_SAMPLES; //MSAA Samples
        [FieldOffset(16)]
        public Vector2 frameDim; //Frame Dimensions
        [FieldOffset(24)]
        public float cameraNearPlane;
        [FieldOffset(28)]
        public float cameraFarPlane;
        [FieldOffset(32)]
        public Matrix4 rotMat;
        [FieldOffset(96)]
        public Matrix4 rotMatInv;
        [FieldOffset(160)]
        public Matrix4 mvp;
        [FieldOffset(224)]
        public Matrix4 lookMatInv;
        [FieldOffset(288)]
        public Matrix4 projMatInv;
        [FieldOffset(352)]
        public Vector4 cameraPositionExposure; //Exposure is the W component
        [FieldOffset(368)]
        public int light_number;
        [FieldOffset(384)]
        public Vector3 cameraDirection;
        [FieldOffset(400)]
        public unsafe fixed float lights[32 * 64];
        public static readonly int SizeInBytes = 8592;
    };

    public class renderManager : baseResourceManager, IDisposable
    {
        List<GLMeshVao> staticObjectsQueue = new List<GLMeshVao>();
        List<GLMeshVao> movingMeshQueue = new List<GLMeshVao>();
        
        List<GLMeshVao> globalMeshList = new List<GLMeshVao>();
        List<GLMeshVao> collisionMeshList = new List<GLMeshVao>();
        List<GLMeshVao> locatorMeshList = new List<GLMeshVao>();
        List<GLMeshVao> jointMeshList = new List<GLMeshVao>();
        List<GLMeshVao> lightMeshList = new List<GLMeshVao>();

        public ResourceManager resMgr; //REf to the active resource Manager

        public ShadowRenderer shdwRenderer; //Shadow Renderer instance
        //Control Font and Text Objects
        private Text.TextRenderer txtRenderer;
        public int last_text_height;
        
        private GBuffer gbuf;
        private PBuffer pbuf;
        private FBO gizmo_fbo;
        private FBO blur_fbo;
        private int blur_fbo_scale = 2;
        private float gfTime = 0.0f;
        private Dictionary<string, int> UBOs = new Dictionary<string, int>();
        private Dictionary<string, int> SSBOs = new Dictionary<string, int>();

        private int multiBufferActiveId;
        private List<int> multiBufferSSBOs = new List<int>(4);
        private List<IntPtr> multiBufferSyncStatuses = new List<IntPtr>(4);
        
        //Octree Structure
        private Octree octree;

        //UBO structs
        CommonPerFrameUniforms cpfu;
        private byte[] atlas_cpmu;

        private int MAX_NUMBER_OF_MESHES = 2000;
        private ulong MAX_OCTREE_WIDTH = 256;
        private int MULTI_BUFFER_COUNT = 3;
        private DebugProc GLDebug;


        public void init(ResourceManager input_resMgr)
        {
#if (DEBUG)
            GL.Enable(EnableCap.DebugOutput);
            GL.Enable(EnableCap.DebugOutputSynchronous);

            GLDebug = new DebugProc(GLDebugMessage);

            GL.DebugMessageCallback(GLDebug, IntPtr.Zero);
            GL.DebugMessageControl(DebugSourceControl.DontCare, DebugTypeControl.DontCare,
                DebugSeverityControl.DontCare, 0, new int[] { 0 }, true);

            GL.DebugMessageInsert(DebugSourceExternal.DebugSourceApplication, DebugType.DebugTypeMarker, 0, DebugSeverity.DebugSeverityNotification, -1, "Debug output enabled");
#endif
            //Setup Resource Manager
            resMgr = input_resMgr;

            //Wait for the resource Manager to be initialized
            while (!resMgr.initialized)
                continue;
            
            //Setup Shadow Renderer
            shdwRenderer = new ShadowRenderer();

            //Setup text renderer
            setupTextRenderer();

            //Setup per Frame UBOs
            setupFrameUBO();

            //Setup SSBOs
            setupSSBOs(1024 * 1024); //Init SSBOs to 1MB
            multiBufferActiveId = 0;
            SSBOs["_COMMON_PER_MESH"] = multiBufferSSBOs[0];
            
            //Initialize Octree
            octree = new Octree(MAX_OCTREE_WIDTH);

        }

        private void GLDebugMessage(DebugSource source, DebugType type, int id, DebugSeverity severity, int length, IntPtr message, IntPtr userParam)
        {
            bool report = false;
            switch (severity)
            {
                case DebugSeverity.DebugSeverityHigh:
                    report = true;
                    break;
            }

            if (report)
            {
                Console.WriteLine(source == DebugSource.DebugSourceApplication ?
                $"openGL - {Marshal.PtrToStringAnsi(message, length)}" :
                $"openGL - {Marshal.PtrToStringAnsi(message, length)}\n\tid:{id} severity:{severity} type:{type} source:{source}\n");
            }
        }

        public void setupGBuffer(int width, int height)
        {
            //Create gbuffer
            gbuf = new GBuffer(width, height);
            pbuf = new PBuffer(width, height);
            blur_fbo = new FBO(TextureTarget.Texture2D, 3, width / blur_fbo_scale, height / blur_fbo_scale, false);
            gizmo_fbo = new FBO(TextureTarget.Texture2D, 2, width, height, false);
        }

        public void getMousePosInfo(int x, int y, ref Vector4[] arr)
        {
            //Fetch Depth
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, gbuf.fbo);
            GL.ReadPixels(x, y, 1, 1, 
                PixelFormat.DepthComponent, PixelType.Float, arr);
            //Fetch color from UI Fbo
        }

        public void gizmoPick(ref Gizmo activeGizmo, Vector2 mousePos)
        {
            //Reset the active status of all the gizmo parts
            activeGizmo.reset();

            //Render the gizmos in the appropriate fbo
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, gizmo_fbo.fbo);
            GL.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GLSLShaderConfig shader = resMgr.GLShaders[SHADER_TYPE.GIZMO_SHADER];
            GL.UseProgram(shader.program_id);

            foreach (GizmoPart gzPart in activeGizmo.gizmoParts)
            {
                //Render Translation Gizmo
                GLMeshVao m = gzPart.meshVao;
                //Render Start
                Matrix4 mWMat = GLMeshBufferManager.getInstanceWorldMat(m, 0);
                GL.UniformMatrix4(shader.uniformLocations["worldMat"], false, ref mWMat);
                GL.Uniform1(shader.uniformLocations["is_active"], 0.0f); //Render with default color
                m.render(shader, RENDERPASS.FORWARD);
            }


            //Pick color from the fbo in the mouse position
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, gizmo_fbo.fbo);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            float[] pixeldata = new float[10];
            GL.ReadPixels((int) mousePos.X,  gizmo_fbo.size_y -  (int) mousePos.Y, 1, 1,
                PixelFormat.Rgba, PixelType.Float, pixeldata);

            Vector3 pixelColor = new Vector3(pixeldata[0], pixeldata[1], pixeldata[2]);
            //Console.WriteLine("Picking read color: {0}", pixelColor);

            //Identify selected part and set status
            foreach (GizmoPart gzPart in activeGizmo.gizmoParts)
            {
                if ((gzPart.pick_color - pixelColor).Length < 1e-5)
                {
                    gzPart.active = true;
                } 
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

        }


        public void progressTime(double dt)
        {
            gfTime += (float) dt / 500;
            gfTime = gfTime % 1000.0f;
        }

        public void cleanup()
        {
            //Just cleanup the queues
            //The resource manager will handle the cleanup of the buffers and shit

            globalMeshList.Clear();
            collisionMeshList.Clear();
            locatorMeshList.Clear();
            jointMeshList.Clear();
            lightMeshList.Clear();
            staticObjectsQueue.Clear();
            movingMeshQueue.Clear();
            octree.clear();
        }

        public void identifyActiveShaders()
        {
            RenderState.activeResMgr.activeGLDeferredLITShaders.Clear();
            RenderState.activeResMgr.activeGLDeferredUNLITShaders.Clear();
            RenderState.activeResMgr.activeGLForwardTransparentShaders.Clear();

            //LIT Deferred
            foreach (GLSLShaderConfig conf in RenderState.activeResMgr.GLDeferredLITShaderMap.Values)
            {
                if (RenderState.activeResMgr.opaqueMeshShaderMap[conf.shaderHash].Count > 0)
                    RenderState.activeResMgr.activeGLDeferredLITShaders.Add(conf);
            }

            //UNLIT DEFERRED
            foreach (GLSLShaderConfig conf in RenderState.activeResMgr.GLDeferredUNLITShaderMap.Values)
            {
                if (RenderState.activeResMgr.opaqueMeshShaderMap[conf.shaderHash].Count > 0)
                    RenderState.activeResMgr.activeGLDeferredUNLITShaders.Add(conf);
            }

            //TRANSPARENT FORWARD
            foreach (GLSLShaderConfig conf in RenderState.activeResMgr.GLForwardShaderMapTransparent.Values)
            {
                if (RenderState.activeResMgr.transparentMeshShaderMap[conf.shaderHash].Count > 0)
                    RenderState.activeResMgr.activeGLForwardTransparentShaders.Add(conf);
            }

            //DECALS (Use the depth buffer but rendered forward
            foreach (GLSLShaderConfig conf in RenderState.activeResMgr.GLDeferredShaderMapDecal.Values)
            {
                if (RenderState.activeResMgr.decalMeshShaderMap[conf.shaderHash].Count > 0)
                    RenderState.activeResMgr.activeGLDeferredDecalShaders.Add(conf);
            }
        }

        public void populate(GMDL.Model root)
        {
            cleanup();

            //Populate octree
            octree.insert(root);
            octree.report();

            foreach (Scene s in resMgr.GLScenes.Values)
                process_models(s);

            //Add gizmo meshes manually to the globalmeshlist\
            //Translation gizmo parts
            globalMeshList.Add(resMgr.GLPrimitiveMeshVaos["default_translation_gizmo_x_axis"]);
            globalMeshList.Add(resMgr.GLPrimitiveMeshVaos["default_translation_gizmo_y_axis"]);
            globalMeshList.Add(resMgr.GLPrimitiveMeshVaos["default_translation_gizmo_z_axis"]);


            //Add default light mesh
            process_model(resMgr.GLlights[0].meshVao);
            
            identifyActiveShaders();

        }

        private void process_model(GLMeshVao m)
        {
            if (m == null)
                return;

            Dictionary<int, List<GLMeshVao>> shaderMeshMap = RenderState.activeResMgr.opaqueMeshShaderMap;
            if (m.type == TYPES.COLLISION || m.type == TYPES.LOCATOR || m.type == TYPES.JOINT || m.type == TYPES.MODEL || m.type == TYPES.GIZMO || m.type == TYPES.LIGHT) { 
                shaderMeshMap = RenderState.activeResMgr.defaultMeshShaderMap;
            }
            //Check if the model is a decal
            else if (m.material.has_flag((TkMaterialFlags.MaterialFlagEnum)TkMaterialFlags.UberFlagEnum._F51_DECAL_DIFFUSE) ||
                     m.material.has_flag((TkMaterialFlags.MaterialFlagEnum)TkMaterialFlags.UberFlagEnum._F52_DECAL_NORMAL))
            {
                shaderMeshMap = RenderState.activeResMgr.decalMeshShaderMap;
            }
            //Check if the model has a transparent material
            else if (m.material.has_flag((TkMaterialFlags.MaterialFlagEnum)TkMaterialFlags.UberFlagEnum._F22_TRANSPARENT_SCALAR) ||
                     m.material.has_flag((TkMaterialFlags.MaterialFlagEnum)TkMaterialFlags.UberFlagEnum._F09_TRANSPARENT) ||
                     m.material.has_flag((TkMaterialFlags.MaterialFlagEnum)TkMaterialFlags.UberFlagEnum._F11_ALPHACUTOUT))
            {
                shaderMeshMap = RenderState.activeResMgr.transparentMeshShaderMap;
            }


            //Explicitly handle locator, scenes and collision meshes
            switch (m.type)
            {
                case (TYPES.MODEL):
                case (TYPES.LOCATOR):
                case (TYPES.GIZMO):
                    {
                        if (!locatorMeshList.Contains(m))
                            locatorMeshList.Add(m);
                        break;
                    }
                case (TYPES.COLLISION):
                    collisionMeshList.Add(m);
                    break;
                case (TYPES.JOINT):
                    jointMeshList.Add(m);
                    break;
                case (TYPES.LIGHT):
                    lightMeshList.Add(m);
                    break;
                default:
                    {
                        //Add mesh to the corresponding shader's meshlist
                        if (!shaderMeshMap.ContainsKey(m.material.shaderHash))
                            Console.WriteLine("WARNING MISSING SHADER");
                        else if (!shaderMeshMap[m.material.shaderHash].Contains(m))
                            shaderMeshMap[m.material.shaderHash].Add(m);
                        break;
                    }
            }

            //Add all meshes to the global meshlist
            if (!globalMeshList.Contains(m))
                globalMeshList.Add(m);

        }

        private void process_models(Model root)
        {
            process_model(root.meshVao);
            
            //Repeat process with children
            foreach (Model child in root.children)
            {
                process_models(child);
            }
        }

        public void clearInstances()
        {
            lock (globalMeshList)
            {
                foreach (GLMeshVao m in globalMeshList)
                    GLMeshBufferManager.clearInstances(m);
            }
        }

        public void setupTextRenderer()
        {
            //Use QFont
            //string font = "C:\\WINDOWS\\FONTS\\ARIAL.TTF";
            //string font = "DroidSansMono.ttf";
            //txtRenderer = new Text.TextRenderer(font, 10);

            //Use My Manager
            txtRenderer = new Text.TextRenderer(resMgr);
        }

        private void setupFrameUBO()
        {
            int ubo_id = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.UniformBuffer, ubo_id);
            GL.BufferData(BufferTarget.UniformBuffer, CommonPerFrameUniforms.SizeInBytes, IntPtr.Zero, BufferUsageHint.StreamRead);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);

            //Store buffer to UBO dictionary
            UBOs["_COMMON_PER_FRAME"] = ubo_id;

            //Attach the generated buffers to the binding points
            bindUBOs();
        
        }

        private void deleteSSBOs()
        {
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            //Generate 3 Buffers for the Triple buffering UBO
            for (int i = 0; i < MULTI_BUFFER_COUNT; i++)
                GL.DeleteBuffer(multiBufferSSBOs[i]);
        }

        private void resizeSSBOs(int size)
        {
            deleteSSBOs();
            atlas_cpmu = new byte[size];
            setupSSBOs(size);
        }

        private void setupSSBOs(int size)
        {
            //Allocate atlas
            //int atlas_ssbo_buffer_size = MAX_NUMBER_OF_MESHES * CommonPerMeshUniformsInstanced.SizeInBytes;
            //int atlas_ssbo_buffer_size = MAX_NUMBER_OF_MESHES * CommonPerMeshUniformsInstanced.SizeInBytes; //256 MB just to play safe
            //OpenGL Spec max size for the SSBO is 128 MB, lets stick to that
            atlas_cpmu = new byte[size];

            //Generate 3 Buffers for the Triple buffering UBO
            for (int i = 0; i < MULTI_BUFFER_COUNT; i++)
            {
                int ssbo_id = GL.GenBuffer();
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssbo_id);
                GL.BufferStorage(BufferTarget.ShaderStorageBuffer, size,
                    IntPtr.Zero, BufferStorageFlags.MapPersistentBit | BufferStorageFlags.MapWriteBit);
                //GL.BufferData(BufferTarget.UniformBuffer, atlas_ubo_buffer_size, IntPtr.Zero, BufferUsageHint.StreamDraw); //FOR OLD METHOD
                multiBufferSSBOs.Add(ssbo_id);
                multiBufferSyncStatuses.Add(GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, 0));
            }

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

            bindBuffersToShaders();
        }

        private void bindBuffersToShaders()
        {

            //UBO Bindings to the generic shaders
            GLShaderHelper.attachUBOToShaderBindingPoint(RenderState.activeResMgr.GLShaders[SHADER_TYPE.GBUFFER_LIT_SHADER], "_COMMON_PER_FRAME", 0);
            GLShaderHelper.attachUBOToShaderBindingPoint(RenderState.activeResMgr.GLShaders[SHADER_TYPE.GBUFFER_UNLIT_SHADER], "_COMMON_PER_FRAME", 0);
            GLShaderHelper.attachUBOToShaderBindingPoint(RenderState.activeResMgr.GLShaders[SHADER_TYPE.TONE_MAPPING], "_COMMON_PER_FRAME", 0);
            GLShaderHelper.attachUBOToShaderBindingPoint(RenderState.activeResMgr.GLShaders[SHADER_TYPE.INV_TONE_MAPPING], "_COMMON_PER_FRAME", 0);
            GLShaderHelper.attachUBOToShaderBindingPoint(RenderState.activeResMgr.GLShaders[SHADER_TYPE.GIZMO_SHADER], "_COMMON_PER_FRAME", 0); ;
            GLShaderHelper.attachUBOToShaderBindingPoint(RenderState.activeResMgr.GLShaders[SHADER_TYPE.TEXT_SHADER], "_COMMON_PER_FRAME", 0); ;


            foreach (GLSLShaderConfig shader in RenderState.activeResMgr.activeGLDeferredDecalShaders)
            {
                GLShaderHelper.attachUBOToShaderBindingPoint(shader, "_COMMON_PER_FRAME", 0);
                GLShaderHelper.attachSSBOToShaderBindingPoint(shader, "_COMMON_PER_MESH", 1);
            }

            foreach (GLSLShaderConfig shader in RenderState.activeResMgr.activeGLDeferredLITShaders)
            {
                GLShaderHelper.attachUBOToShaderBindingPoint(shader, "_COMMON_PER_FRAME", 0);
                GLShaderHelper.attachSSBOToShaderBindingPoint(shader, "_COMMON_PER_MESH", 1);
            }

            foreach (GLSLShaderConfig shader in RenderState.activeResMgr.activeGLDeferredUNLITShaders)
            {
                GLShaderHelper.attachUBOToShaderBindingPoint(shader, "_COMMON_PER_FRAME", 0);
                GLShaderHelper.attachSSBOToShaderBindingPoint(shader, "_COMMON_PER_MESH", 1);
            }

            foreach (GLSLShaderConfig shader in RenderState.activeResMgr.activeGLForwardTransparentShaders)
            {
                GLShaderHelper.attachUBOToShaderBindingPoint(shader, "_COMMON_PER_FRAME", 0);
                GLShaderHelper.attachSSBOToShaderBindingPoint(shader, "_COMMON_PER_MESH", 1);
            }



        }

        //This method updates UBO data for rendering
        private void prepareCommonPerFrameUBO()
        {
            //Prepare Struct
            cpfu.diffuseFlag = RenderState.renderSettings._useTextures;
            cpfu.use_lighting = RenderState.renderSettings._useLighting;
            cpfu.frameDim.X = gbuf.size[0];
            cpfu.frameDim.Y = gbuf.size[1];
            cpfu.mvp = RenderState.activeCam.viewMat;
            cpfu.rotMat = RenderState.rotMat;
            cpfu.rotMatInv = RenderState.rotMat.Inverted();
            cpfu.lookMatInv = RenderState.activeCam.lookMatInv;
            cpfu.projMatInv = RenderState.activeCam.projMatInv;
            cpfu.cameraPositionExposure.Xyz = RenderState.activeCam.Position;
            cpfu.cameraPositionExposure.W = RenderState.renderSettings._HDRExposure;
            cpfu.cameraDirection = RenderState.activeCam.Front;
            cpfu.cameraNearPlane = RenderState.activeCam.zNear;
            cpfu.cameraFarPlane = RenderState.activeCam.zFar;
            cpfu.light_number = Math.Min(32, resMgr.GLlights.Count);
            cpfu.gfTime = gfTime;
            cpfu.MSAA_SAMPLES = gbuf.msaa_samples;

            //Upload light information
            for (int i = 0; i < Math.Min(32, resMgr.GLlights.Count); i++)
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
            
            GL.BindBuffer(BufferTarget.UniformBuffer, UBOs["_COMMON_PER_FRAME"]);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, CommonPerFrameUniforms.SizeInBytes, ref cpfu);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
        }

        private bool prepareCommonPermeshSSBO(GLMeshVao m, ref int UBO_Offset)
        {

            //if (m.instance_count == 0 || m.visible_instances == 0) //use the visible_instance if we maintain an occluded status
            if (m.instance_count == 0)
                return true;

            m.UBO_aligned_size = 0;

            //Calculate aligned size
            int newsize = 4 * m.dataBuffer.Length;
            newsize = ((newsize >> 8) + 1) * 256;
            

            if (newsize + UBO_Offset > atlas_cpmu.Length)
            {
#if DEBUG
                Console.WriteLine("Mesh overload skipping...");
#endif
                return false;
            }

            m.UBO_aligned_size = newsize; //Save new size

            if (m.skinned)
                m.uploadSkinningData();

            unsafe
            {
                fixed(void* p = m.dataBuffer)
                {
                    byte* bptr = (byte*) p;

                    Marshal.Copy((IntPtr) p, atlas_cpmu, UBO_Offset, 
                        m.UBO_aligned_size);
                }
            }

            m.UBO_offset = UBO_Offset; //Save offset
            UBO_Offset += m.UBO_aligned_size; //Increase the offset

            return true;
        }

        //This Method binds UBos to binding points
        private void bindUBOs()
        {
            //Bind Matrices
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, UBOs["_COMMON_PER_FRAME"]);
        }


        public void resize(int w, int h)
        {
            gbuf?.resize(w, h);
            pbuf?.resize(w, h);
            blur_fbo?.resize(w / blur_fbo_scale, h / blur_fbo_scale);
            
            GL.Viewport(0, 0, w, h);
        }


#region Rendering Methods

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

        private void prepareCommonPerMeshSSBOs()
        {
            multiBufferActiveId = (multiBufferActiveId + 1) % MULTI_BUFFER_COUNT;

            SSBOs["_COMMON_PER_MESH"] = multiBufferSSBOs[multiBufferActiveId];

            WaitSyncStatus result =  GL.ClientWaitSync(multiBufferSyncStatuses[multiBufferActiveId], 0, 10);

            while (result == WaitSyncStatus.TimeoutExpired || result == WaitSyncStatus.WaitFailed)
            {
                //Console.WriteLine("Gamithike o dias");
                result = GL.ClientWaitSync(multiBufferSyncStatuses[multiBufferActiveId], 0, 10);
            }

            GL.DeleteSync(multiBufferSyncStatuses[multiBufferActiveId]);

            //Upload atlas UBO data
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, SSBOs["_COMMON_PER_MESH"]);

            //Prepare UBO data
            int ubo_offset = 0;
            int max_ubo_offset = atlas_cpmu.Length;
            //int max_ubo_offset = 1024 * 1024 * 32;

           //METHOD 2: Use MAP Buffer
           IntPtr ptr = GL.MapBufferRange(BufferTarget.ShaderStorageBuffer, IntPtr.Zero,
                max_ubo_offset, BufferAccessMask.MapUnsynchronizedBit | BufferAccessMask.MapWriteBit);

            //Upload Meshes
            bool atlas_fine = true;
            foreach (GLMeshVao m in globalMeshList)
            {
                atlas_fine &= prepareCommonPermeshSSBO(m, ref ubo_offset);
            }

            //Console.WriteLine("ATLAS SIZE ORIGINAL: " +  atlas_cpmu.Length + " vs  OFFSET " + ubo_offset);

            if (ubo_offset > 0.9 * atlas_cpmu.Length)
            {
                int new_size = atlas_cpmu.Length + (int)(0.25 * atlas_cpmu.Length);
                //Unmap and unbind buffer
                GL.UnmapBuffer(BufferTarget.ShaderStorageBuffer);
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
                
                resizeSSBOs(new_size);

                //Remap and rebind buffer at the current index
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, SSBOs["_COMMON_PER_MESH"]);
                ptr = GL.MapBufferRange(BufferTarget.ShaderStorageBuffer, IntPtr.Zero,
                new_size, BufferAccessMask.MapUnsynchronizedBit | BufferAccessMask.MapWriteBit);

            }

            if (ubo_offset != 0)
            {
#if (DEBUG)
                if (ubo_offset > max_ubo_offset)
                    Console.WriteLine("GAMITHIKE O DIAS");
#endif
                //at this point the ubo_offset is the actual size of the atlas buffer

                unsafe
                {
                    Marshal.Copy(atlas_cpmu, 0, ptr, ubo_offset);
                }
            }

            GL.UnmapBuffer(BufferTarget.ShaderStorageBuffer);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

        }



        private void renderDefaultMeshes()
        {
            GL.Disable(EnableCap.CullFace);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);

            //Collisions
            if (RenderState.renderViewSettings.RenderCollisions)
            {
                Material mat = resMgr.GLmaterials["collisionMat"];
                GLSLShaderConfig shader = RenderState.activeResMgr.GLDefaultShaderMap[mat.shaderHash];
                GL.UseProgram(shader.program_id); //Set Program

                //Render static meshes
                foreach (GLMeshVao m in collisionMeshList)
                {
                    if (m.instance_count == 0 || m.UBO_aligned_size == 0)
                        continue;

                    GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                        (IntPtr)(m.UBO_offset), m.UBO_aligned_size);
                    
                    m.render(shader, RENDERPASS.DEFERRED);
                }
            }

            //Collisions
            if (RenderState.renderViewSettings.RenderLights)
            {
                Material mat = resMgr.GLmaterials["lightMat"];
                GLSLShaderConfig shader = RenderState.activeResMgr.GLDefaultShaderMap[mat.shaderHash];
                GL.UseProgram(shader.program_id); //Set Program

                //Render static meshes
                foreach (GLMeshVao m in lightMeshList)
                {
                    if (m.instance_count == 0 || m.UBO_aligned_size == 0)
                        continue;

                    GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                        (IntPtr)(m.UBO_offset), m.UBO_aligned_size);

                    m.render(shader, RENDERPASS.DEFERRED);
                }
            }

            if (RenderState.renderViewSettings.RenderJoints)
            {
                Material mat = resMgr.GLmaterials["jointMat"];
                GLSLShaderConfig shader = Common.RenderState.activeResMgr.GLDefaultShaderMap[mat.shaderHash];

                GL.UseProgram(shader.program_id); //Set Program

                //Render static meshes
                foreach (GLMeshVao m in jointMeshList)
                {
                    if (m.instance_count == 0 || m.UBO_aligned_size == 0)
                        continue;

                    GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                        (IntPtr)(m.UBO_offset), m.UBO_aligned_size);

                    m.render(shader, RENDERPASS.DEFERRED);
                }
            }

            GL.PolygonMode(MaterialFace.FrontAndBack, RenderState.renderSettings.RENDERMODE);

            if (RenderState.renderViewSettings.RenderLocators)
            {
                Material mat = resMgr.GLmaterials["crossMat"];
                GLSLShaderConfig shader = RenderState.activeResMgr.GLDefaultShaderMap[mat.shaderHash];

                GL.UseProgram(shader.program_id); //Set Program

                //Render static meshes
                foreach (GLMeshVao m in locatorMeshList)
                {
                    if (m.instance_count == 0 || m.UBO_aligned_size == 0)
                        continue;

                    GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                        (IntPtr)(m.UBO_offset), m.UBO_aligned_size);

                    m.render(shader, RENDERPASS.DEFERRED);
                }
            }

            GL.Enable(EnableCap.CullFace);
        
        }

        private void renderStaticMeshes(List<GLSLShaderConfig> shaderList)
        {
            //Set polygon mode
            GL.PolygonMode(MaterialFace.FrontAndBack, RenderState.renderSettings.RENDERMODE);
            
            foreach (GLSLShaderConfig shader in shaderList)
            {
                GL.UseProgram(shader.program_id); //Set Program
                
                //Render static meshes
                foreach (GLMeshVao m in resMgr.opaqueMeshShaderMap[shader.shaderHash])
                {
                    if (m.instance_count == 0 || m.UBO_aligned_size == 0)
                        continue;

                    GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                        (IntPtr)(m.UBO_offset), m.UBO_aligned_size);

                    m.render(shader, RENDERPASS.DEFERRED);
                    if (RenderState.renderViewSettings.RenderBoundHulls)
                    {
                        GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                        m.render(shader, RENDERPASS.BHULL);
                        GL.PolygonMode(MaterialFace.FrontAndBack, RenderState.renderSettings.RENDERMODE);
                    }
                        
                }

                GL.BindBuffer(BufferTarget.UniformBuffer, 0); //Unbind UBOs


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

        }

        private void renderGeometry()
        {
            //DEFERRED STAGE - STATIC MESHES

            //At first render the static meshes
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            
            //DEFERRED STAGE

            gbuf.bind();
            gbuf.clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            
            renderStaticMeshes(RenderState.activeResMgr.activeGLDeferredLITShaders); //LIT MESHES

            //Copy depth buffer from gbuf to pbuf
            FBO.copyDepthChannel(gbuf.fbo, pbuf.fbo, pbuf.size[0], pbuf.size[1], gbuf.size[0], gbuf.size[1]);
            gbuf.bind();
            renderDecalMeshes(); //Render Decals
            
            renderDeferredLightPass(); //Deferred Lighting Pass

            pass_tex(gbuf.fbo, DrawBufferMode.ColorAttachment0, pbuf.color, gbuf.size); //Copy accumulated light to albedo
            gbuf.bind();
            renderStaticMeshes(RenderState.activeResMgr.activeGLDeferredUNLITShaders); //UNLIT MESHES
            renderDefaultMeshes(); //Collisions, Locators, Joints
            
            renderDeferredPass(); //Final unlit deferred rendering pass
            
            //FORWARD STAGE - TRANSPARENT MESHES
            
            //Copy depth channel to pbuf
            FBO.copyDepthChannel(gbuf.fbo, pbuf.fbo, pbuf.size[0], pbuf.size[1], gbuf.size[0], gbuf.size[1]);
            renderTransparent();
        
        }
        
        private void renderDecalMeshes()
        {
            GL.DepthMask(false);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            
            foreach(GLSLShaderConfig shader in RenderState.activeResMgr.activeGLDeferredDecalShaders)
            {
                GL.UseProgram(shader.program_id);
                //Upload depth texture to the shader

                //Bind Depth Buffer
                GL.Uniform1(shader.uniformLocations["mpCommonPerFrameSamplers.depthMap"], 6);
                GL.ActiveTexture(TextureUnit.Texture6);
                GL.BindTexture(TextureTarget.Texture2D, pbuf.depth);

                foreach (GLMeshVao m in RenderState.activeResMgr.decalMeshShaderMap[shader.shaderHash])
                {
                    if (m.instance_count == 0 || m.UBO_aligned_size == 0)
                        continue;

                    GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"], (IntPtr)(m.UBO_offset), m.UBO_aligned_size);
                    m.render(shader, RENDERPASS.DECAL);
                }


            }
            GL.CullFace(CullFaceMode.Back);
            GL.Enable(EnableCap.CullFace);
            GL.DepthMask(true);
        }

        private void renderTransparent()
        {
            //Render the first pass in the first channel of the pbuf
            GL.ClearTexImage(pbuf.blur1, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.ClearTexImage(pbuf.blur2, 0, PixelFormat.Rgba, PixelType.Float, new float[] { 1.0f, 1.0f ,1.0f, 1.0f});

            //Enable writing to both channels after clearing
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.DrawBuffers(2, new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment1,
                                          DrawBuffersEnum.ColorAttachment2});
            
            //At first render the static meshes
            GL.Enable(EnableCap.Blend);
            GL.DepthMask(false);
            GL.Enable(EnableCap.DepthTest); //Enable depth test
            //Set BlendFuncs for the 2 drawbuffers
            GL.BlendFunc(0, BlendingFactorSrc.One, BlendingFactorDest.One);
            GL.BlendFunc(1, BlendingFactorSrc.Zero, BlendingFactorDest.OneMinusSrcAlpha);

            //Set polygon mode
            GL.PolygonMode(MaterialFace.FrontAndBack, RenderState.renderSettings.RENDERMODE);

            foreach (GLSLShaderConfig shader in resMgr.activeGLForwardTransparentShaders)
            {
                GL.UseProgram(shader.program_id); //Set Program

                //Render transparent meshes
                foreach (GLMeshVao m in resMgr.transparentMeshShaderMap[shader.shaderHash])
                {
                    if (m.instance_count == 0 || m.UBO_aligned_size == 0)
                        continue;
                    
                    GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                        (IntPtr)(m.UBO_offset), m.UBO_aligned_size);

                    m.render(shader, RENDERPASS.FORWARD);
                    //if (RenderOptions.RenderBoundHulls)
                    //    m.render(shader, RENDERPASS.BHULL);
                }
                GL.BindBuffer(BufferTarget.UniformBuffer, 0); //Unbind UBOs

            }
            
            GL.DepthMask(true); //Re-enable depth buffer
            
            //Composite Step
            GLSLShaderConfig bwoit_composite_shader = RenderState.activeResMgr.GLShaders[SHADER_TYPE.BWOIT_COMPOSITE_SHADER];

            //Draw to main color channel
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            GL.BlendFunc(BlendingFactor.OneMinusSrcAlpha, BlendingFactor.SrcAlpha); //Set compositing blend func
            //GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha); //Set compositing blend func
            render_quad(new string[] { }, new float[] { }, new string[] { "in1Tex", "in2Tex" }, new int[] { pbuf.blur1, pbuf.blur2 }, bwoit_composite_shader);
            GL.Disable(EnableCap.Blend);
            
        }

        
        
        private void renderFinalPass()
        {
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, pbuf.fbo);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
            GL.BlitFramebuffer(0, 0, gbuf.size[0], gbuf.size[1], 0, 0, gbuf.size[0], gbuf.size[1], 
                ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
        }

        private void renderUI()
        {
            
            if (RenderState.renderViewSettings.RenderGizmos)
            {
                GL.Clear(ClearBufferMask.DepthBufferBit); //Clear depth
                GL.Enable(EnableCap.DepthTest);
                GL.Disable(EnableCap.CullFace);
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                GLSLShaderConfig shader = resMgr.GLShaders[SHADER_TYPE.GIZMO_SHADER];
                GL.UseProgram(shader.program_id);


                //Render GizmoParts
                List<string> gizmoPartNames = new List<string> { "default_translation_gizmo_x_axis",
                                                "default_translation_gizmo_y_axis",
                                                "default_translation_gizmo_z_axis" }; 


                foreach (string name in gizmoPartNames)
                {
                    //Render Translation Gizmo
                    GLMeshVao m = resMgr.GLPrimitiveMeshVaos[name];
                    //Render Start
                    //TODO: Bind the Mesh UBO directly
                    Matrix4 mWMat = GLMeshBufferManager.getInstanceWorldMat(m, 0);
                    GL.UniformMatrix4(shader.uniformLocations["worldMat"], false, ref mWMat);
                    GL.Uniform1(shader.uniformLocations["is_active"], GLMeshBufferManager.getInstanceSelectedStatus(m, 0) ? 1.0f: 0.0f);
                    m.render(shader, RENDERPASS.FORWARD);
                }

                GL.Enable(EnableCap.CullFace);

            }

            if (RenderState.renderViewSettings.RenderInfo)
                txtRenderer.render();
        }

        private void renderShadows()
        {

        }

        //Rendering Mechanism
        public void render()
        {
            //Prepare UBOs
            prepareCommonPerFrameUBO();
            
            //Render Shadows
            renderShadows();

            //Sort Lights
            sortLights();
            
            //Sort Transparent Objects
            //sortTransparent(); //NOT NEEDED ANYMORE
            
            //LOD filtering
            if (RenderState.renderSettings.LODFiltering)
            {
                //LOD_filtering(staticMeshQueue); TODO: FIX
                //LOD_filtering(transparentMeshQueue); TODO: FIX
            }

            //Prepare Mesh UBO
            prepareCommonPerMeshSSBOs();
            
            //Render octree
            //octree.render(resMgr.GLShaders[GLSLHelper.SHADER_TYPE.BBOX_SHADER].program_id);

            //Render Geometry
            renderGeometry();

            //Setup FENCE AFTER ALL THE MAIN GEOMETRY DRAWCALLS ARE ISSUED
            multiBufferSyncStatuses[multiBufferActiveId] = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, 0);

            //POST-PROCESSING
            post_process();

            //Final Pass
            renderFinalPass();

            //Render UI();

            renderUI();

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

        private void render_quad(string[] uniforms, float[] uniform_values, string[] sampler_names, int[] texture_ids, GLSLHelper.GLSLShaderConfig shaderConf)
        {
            int quad_vao = resMgr.GLPrimitiveVaos["default_renderquad"].vao_id;

            GL.UseProgram(shaderConf.program_id);
            GL.BindVertexArray(quad_vao);

            //Upload samplers
            for (int i = 0; i < sampler_names.Length; i++)
            {
                GL.Uniform1(shaderConf.uniformLocations[sampler_names[i]], i);
                GL.ActiveTexture(TextureUnit.Texture0 + i);
                GL.BindTexture(TextureTarget.Texture2D, texture_ids[i]);
            }

            //Upload uniforms - Assuming single float uniforms for now
            for (int i = 0; i < uniforms.Length; i++)
                GL.Uniform1(shaderConf.uniformLocations[uniforms[i]], uniform_values[i]);

            //Render quad
            GL.Disable(EnableCap.DepthTest);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (IntPtr)0);
            GL.BindVertexArray(0);
            GL.Enable(EnableCap.DepthTest);

        }

        private void pass_tex(int to_fbo, DrawBufferMode to_channel, int InTex, int[] to_buf_size)
        {
            //passthrough a texture to the specified to_channel of the to_fbo
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, to_fbo);
            GL.DrawBuffer(to_channel);

            GL.Disable(EnableCap.DepthTest); //Disable Depth test
            GL.Clear(ClearBufferMask.ColorBufferBit);
            GLSLShaderConfig shader = RenderState.activeResMgr.GLShaders[SHADER_TYPE.PASSTHROUGH_SHADER];
            //render_quad(new string[] {"sizeX", "sizeY" }, new float[] { to_buf_size[0], to_buf_size[1]}, new string[] { "InTex" }, new int[] { InTex }, shader);
            render_quad(new string[] { }, new float[] { }, new string[] { "InTex" }, new int[] { InTex }, shader);
            GL.Enable(EnableCap.DepthTest); //Re-enable Depth test
        }

        private void bloom()
        {
            //Load Programs
            GLSLShaderConfig gs_horizontal_blur_program = resMgr.GLShaders[SHADER_TYPE.GAUSSIAN_HORIZONTAL_BLUR_SHADER];
            GLSLShaderConfig gs_vertical_blur_program = resMgr.GLShaders[SHADER_TYPE.GAUSSIAN_VERTICAL_BLUR_SHADER];
            GLSLShaderConfig br_extract_program = resMgr.GLShaders[SHADER_TYPE.BRIGHTNESS_EXTRACT_SHADER];
            GLSLShaderConfig add_program = resMgr.GLShaders[SHADER_TYPE.ADDITIVE_BLEND_SHADER];
            
            GL.Disable(EnableCap.DepthTest);

            //Copy Color to blur fbo channel 1
            FBO.copyChannel(pbuf.fbo, blur_fbo.fbo, gbuf.size[0], gbuf.size[1], blur_fbo.size_x, blur_fbo.size_y,
                ReadBufferMode.ColorAttachment0, DrawBufferMode.ColorAttachment1);
            //pass_tex(blur_fbo.fbo, DrawBufferMode.ColorAttachment1, pbuf.color, new int[] { blur_fbo.size_x, blur_fbo.size_y });

            //Extract Brightness on the blur buffer and write it to channel 0
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, blur_fbo.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0); //Write to blur1
            
            render_quad(new string[] { }, new float[] { }, new string[] { "inTex" }, new int[] { blur_fbo.channels[1] }, br_extract_program);



            //Copy Color to blur fbo channel 1
            //FBO.copyChannel(blur_fbo.fbo, pbuf.fbo, blur_fbo.size_x, blur_fbo.size_y, gbuf.size[0], gbuf.size[1],
            //    ReadBufferMode.ColorAttachment0, DrawBufferMode.ColorAttachment0);

            //return;

            //Console.WriteLine(GL.GetError()); 

            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, blur_fbo.fbo);
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, blur_fbo.fbo);
            GL.Viewport(0, 0, blur_fbo.size_x, blur_fbo.size_y); //Change the viewport
            int blur_amount = 2;
            for (int i=0; i < blur_amount; i++)
            {
                //Step 1- Apply horizontal blur
                GL.DrawBuffer(DrawBufferMode.ColorAttachment1); //blur2
                GL.Clear(ClearBufferMask.ColorBufferBit);
                
                render_quad(new string[] { }, new float[] { }, new string[] { "diffuseTex" }, new int[] { blur_fbo.channels[0]}, gs_horizontal_blur_program);

                //Step 2- Apply horizontal blur
                GL.DrawBuffer(DrawBufferMode.ColorAttachment0); //blur2
                GL.Clear(ClearBufferMask.ColorBufferBit);

                render_quad(new string[] { }, new float[] { }, new string[] { "diffuseTex" }, new int[] { blur_fbo.channels[1] }, gs_vertical_blur_program);
            }

            //Blit to screen
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, blur_fbo.fbo);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);

            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment1);
            GL.Clear(ClearBufferMask.ColorBufferBit); //Clear Screen
            
            GL.BlitFramebuffer(0, 0, blur_fbo.size_x, blur_fbo.size_y, 0, 0, pbuf.size[0], pbuf.size[1],
            ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
            
            GL.Viewport(0, 0, gbuf.size[0], gbuf.size[1]); //Restore viewport

            //Save Color to blur2 so that we can composite on the main channel
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, pbuf.fbo);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0); //color
            GL.DrawBuffer(DrawBufferMode.ColorAttachment2); //blur2
            GL.BlitFramebuffer(0, 0, gbuf.size[0], gbuf.size[1], 0, 0, gbuf.size[0], gbuf.size[1],
            ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            render_quad(new string[] { }, new float[] { }, new string[] { "in1Tex", "in2Tex" }, new int[] { pbuf.blur2, pbuf.blur1 }, add_program);
            //render_quad(new string[] { }, new float[] { }, new string[] { "blurTex" }, new int[] { pbuf.blur1 }, gs_bloom_program);

        }

        private void fxaa()
        {
            tone_mapping(); //Apply tone mapping pbuf.color shoud be ready
            
            //Load Programs
            GLSLShaderConfig fxaa_program = resMgr.GLShaders[SHADER_TYPE.FXAA_SHADER];

            //Copy Color to first channel
            FBO.copyChannel(pbuf.fbo, pbuf.fbo, pbuf.size[0], pbuf.size[1], pbuf.size[0], pbuf.size[1],
                ReadBufferMode.ColorAttachment0, DrawBufferMode.ColorAttachment1);
            //pass_tex(pbuf.fbo, DrawBufferMode.ColorAttachment1, pbuf.color, pbuf.size);

            //Apply FXAA
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);

            render_quad(new string[] { }, new float[] { }, new string[] { "diffuseTex" }, new int[] { pbuf.blur1 }, fxaa_program);

            inv_tone_mapping(); //Invert Tone Mapping

        }

        private void tone_mapping()
        {
            //Load Programs
            GLSLShaderConfig tone_mapping_program = resMgr.GLShaders[SHADER_TYPE.TONE_MAPPING];

            //Copy Color to first channel
            pass_tex(pbuf.fbo, DrawBufferMode.ColorAttachment1, pbuf.color, pbuf.size); //LOOKS OK!

            //Apply Tone Mapping
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);

            render_quad(new string[] { }, new float[] { }, new string[] { "inTex" }, new int[] { pbuf.blur1 }, tone_mapping_program);

        }

        private void inv_tone_mapping()
        {
            //Load Programs
            GLSLShaderConfig inv_tone_mapping_program = resMgr.GLShaders[SHADER_TYPE.INV_TONE_MAPPING];

            //Copy Color to first channel
            pass_tex(pbuf.fbo, DrawBufferMode.ColorAttachment1, pbuf.color, pbuf.size); //LOOKS OK!

            //Apply Tone Mapping
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);

            render_quad(new string[] { }, new float[] { }, new string[] { "inTex" }, new int[] { pbuf.blur1 }, inv_tone_mapping_program);

        }

        private void post_process()
        {
            if (RenderState.renderSettings.UseFXAA)
                fxaa(); //FXAA (INCLUDING TONE/UNTONE)

            //Actuall Post Process effects in AA space without tone mapping
            
            if (RenderState.renderSettings.UseBLOOM)
                bloom(); //BLOOM

            tone_mapping(); //FINAL TONE MAPPING, INCLUDES GAMMA CORRECTION

        }

        private void backupDepth()
        {
            //NOT WORKING
            //Backup the depth buffer to the secondary fbo
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, gbuf.fbo);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, gbuf.fbo);
            GL.BlitFramebuffer(0, 0, gbuf.size[0], gbuf.size[1], 0, 0, gbuf.size[0], gbuf.size[1],
                ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);
            
        }

        private void restoreDepth()
        {
            //NOT WORKING
            //Backup the depth buffer to the secondary fbo
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, gbuf.fbo);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, gbuf.fbo);
            GL.BlitFramebuffer(0, 0, gbuf.size[0], gbuf.size[1], 0, 0, gbuf.size[0], gbuf.size[1],
                ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);
        }

        private void renderDeferredLightPass()
        {
            GLSLHelper.GLSLShaderConfig shader_conf = resMgr.GLShaders[GLSLHelper.SHADER_TYPE.GBUFFER_LIT_SHADER];

            //Bind the color channel of the pbuf for drawing
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0); //Draw to the light color channel only

            //TEST DRAW TO SCREEN
            //GL.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);

            //GL.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            render_quad(new string[] { }, new float[] { }, new string[] { "albedoTex", "depthTex", "normalTex", "parameterTex"}, 
                                                            new int[] { gbuf.albedo, gbuf.depth, gbuf.normals, gbuf.info}, shader_conf);
        }

        private void renderDeferredPass()
        {
            GLSLShaderConfig shader_conf = resMgr.GLShaders[GLSLHelper.SHADER_TYPE.GBUFFER_UNLIT_SHADER];

            //Bind default fbo
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0); //Draw to the light color channel only

            
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.Disable(EnableCap.DepthTest); //Disable Depth test
            render_quad(new string[] { }, new float[] { }, new string[] { "albedoTex" },
                                                            new int[] { gbuf.albedo }, shader_conf);
            GL.Enable(EnableCap.DepthTest); //Re-enable Depth test

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
