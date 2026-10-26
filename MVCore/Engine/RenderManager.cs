using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using GLSLHelper;
using libMBIN.NMS.Toolkit;
using MVCore.Common;
using MVCore.GMDL;
using MVCore.Engine;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;


namespace MVCore
{
    //Render Structs
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
        
        private GBuffer gbuf;
        private PBuffer pbuf;
        public int screen_fbo;
        private FBO gizmo_fbo;
        //Bloom mip chain: each level is half the size of the previous one, starting at half resolution
        private int bloomFbo = -1;
        private int[] bloomMips = new int[0];
        private int[] bloomMipWidths = new int[0];
        private int[] bloomMipHeights = new int[0];
        private const int BLOOM_MAX_MIPS = 6;
        private const int BLOOM_MIN_MIP_SIZE = 8;
        //Bloom parameters live in RenderState.renderSettings (Render Settings tab)
        private float gfTime = 0.0f;
        private Dictionary<string, int> UBOs = new Dictionary<string, int>();
        private Dictionary<string, int> SSBOs = new Dictionary<string, int>();

        private int multiBufferActiveId = 0;
        private List<int> multiBufferSSBOs = new List<int>(4);
        private List<IntPtr> multiBufferSyncStatuses = new List<IntPtr>(4); //Fence of the last frame that used each buffer
        private List<IntPtr> multiBufferPtrs = new List<IntPtr>(4); //Persistent mappings of the buffers
        
        //Octree Structure
        private Octree octree;

        //UBO structs
        CommonPerFrameUniforms cpfu;
        private int ssboSize; //Size of each per-mesh SSBO in bytes
        private ReadBufferMode finalColorAttachment = ReadBufferMode.ColorAttachment0; //pbuf channel holding the finished frame

        private int MAX_NUMBER_OF_MESHES = 2000;
        private ulong MAX_OCTREE_WIDTH = 256;
        private int MULTI_BUFFER_COUNT = 3;
        private DebugProc GLDebug;


        public void init(ResourceManager input_resMgr)
        {
#if (DEBUG)
            //Asynchronous output: synchronous mode serializes the driver. The callback may run on a driver thread
            GL.Enable(EnableCap.DebugOutput);
            GL.Disable(EnableCap.DebugOutputSynchronous);

            GLDebug = new DebugProc(GLDebugMessage);

            GL.DebugMessageCallback(GLDebug, IntPtr.Zero);
            //Only high and medium severity messages are reported, so the driver does not generate the rest
            GL.DebugMessageControl(DebugSourceControl.DontCare, DebugTypeControl.DontCare,
                DebugSeverityControl.DontCare, 0, new int[] { 0 }, false);
            GL.DebugMessageControl(DebugSourceControl.DontCare, DebugTypeControl.DontCare,
                DebugSeverityControl.DebugSeverityHigh, 0, new int[] { 0 }, true);
            GL.DebugMessageControl(DebugSourceControl.DontCare, DebugTypeControl.DontCare,
                DebugSeverityControl.DebugSeverityMedium, 0, new int[] { 0 }, true);

            GL.DebugMessageInsert(DebugSourceExternal.DebugSourceApplication, DebugType.DebugTypeMarker, 0, DebugSeverity.DebugSeverityNotification, -1, "Debug output enabled");
#endif
            //Setup Resource Manager
            resMgr = input_resMgr;

            //Wait for the resource Manager to be initialized
            while (!resMgr.initialized)
                continue;
            
            //Setup Shadow Renderer
            shdwRenderer = new ShadowRenderer();

            //Setup per Frame UBOs
            setupFrameUBO();

            //Setup SSBOs
            setupSSBOs(4 * 1024 * 1024); //Init SSBOs to 4MB
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
                case DebugSeverity.DebugSeverityMedium:
                    report = true;
                    break;
                //case DebugSeverity.DebugSeverityNotification:
                //    report = true;
                //    break;
            }

            if (report)
            {
                //Pass the text as an argument, GL messages may contain format braces
                CallBacks.Log("{0}", source == DebugSource.DebugSourceApplication ?
                $"openGL - {Marshal.PtrToStringAnsi(message, length)}" :
                $"openGL - {Marshal.PtrToStringAnsi(message, length)}\n\tid:{id} severity:{severity} type:{type} source:{source}\n");
            }
        }

        public void setupGBuffer(int width, int height)
        {
            //Create gbuffer
            gbuf = new GBuffer(width, height);
            pbuf = new PBuffer(width, height);
            setupBloom(width, height);
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
            //Common.CallBacks.Log("Picking read color: {0}", pixelColor);

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
            RenderState.activeResMgr.activeGLDeferredDecalShaders.Clear();

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
            if (m.type == TYPES.COLLISION || m.type == TYPES.GROUP || m.type == TYPES.LOCATOR || m.type == TYPES.JOINT || m.type == TYPES.MODEL || m.type == TYPES.GIZMO || m.type == TYPES.LIGHT) { 
                shaderMeshMap = RenderState.activeResMgr.defaultMeshShaderMap;
            }
            //Check if the model is a decal
            else if (m.material.ShaderFlags.Contains("_F51_DECAL_DIFFUSE") ||
                     m.material.ShaderFlags.Contains("_F52_DECAL_NORMAL"))
            {
                shaderMeshMap = RenderState.activeResMgr.decalMeshShaderMap;
            }
            //Check if the model has a transparent material
            else if (m.material.has_flag(TkMaterialFlags.MaterialFlagEnum._F30_REFRACTION) ||
                    m.material.has_flag(TkMaterialFlags.MaterialFlagEnum._F32_REFRACTION_MASK) ||
                    m.material.ShaderFlags.Contains("_F09_TRANSPARENT"))
            {
                shaderMeshMap = RenderState.activeResMgr.transparentMeshShaderMap;
            }


            //Explicitly handle locator, scenes and collision meshes
            switch (m.type)
            {
                case (TYPES.MODEL):
                case (TYPES.LOCATOR):
                case (TYPES.GROUP):
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
                            Common.CallBacks.Log("WARNING MISSING SHADER");
                        else if (!shaderMeshMap[m.material.shaderHash].Contains(m))
                            shaderMeshMap[m.material.shaderHash].Add(m);
                        break;
                    }
            }

            //Add all meshes to the global meshlist
            if (!globalMeshList.Contains(m))
                globalMeshList.Add(m);

        }

        public void process_models(Model root)
        {
            process_model(root.meshVao);
            
            //Repeat process with children
            foreach (Model child in root.Children)
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

        private void setupFrameUBO()
        {
            int ubo_id = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.UniformBuffer, ubo_id);
            GL.BufferData(BufferTarget.UniformBuffer, CommonPerFrameUniforms.SizeInBytes, IntPtr.Zero, BufferUsageHint.StreamDraw); //Written by the CPU every frame
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);

            //Store buffer to UBO dictionary
            UBOs["_COMMON_PER_FRAME"] = ubo_id;

            //Attach the generated buffers to the binding points
            bindUBOs();
        
        }

        private void deleteSSBOs()
        {
            for (int i = 0; i < multiBufferSSBOs.Count; i++)
            {
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, multiBufferSSBOs[i]);
                GL.UnmapBuffer(BufferTarget.ShaderStorageBuffer);
                GL.DeleteBuffer(multiBufferSSBOs[i]);
                if (multiBufferSyncStatuses[i] != IntPtr.Zero)
                    GL.DeleteSync(multiBufferSyncStatuses[i]);
            }
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

            multiBufferSSBOs.Clear();
            multiBufferSyncStatuses.Clear();
            multiBufferPtrs.Clear();
        }

        private void resizeSSBOs(int size)
        {
            deleteSSBOs();
            setupSSBOs(size);
            multiBufferActiveId = 0;
        }

        private void setupSSBOs(int size)
        {
            //Allocate atlas
            //int atlas_ssbo_buffer_size = MAX_NUMBER_OF_MESHES * CommonPerMeshUniformsInstanced.SizeInBytes;
            //int atlas_ssbo_buffer_size = MAX_NUMBER_OF_MESHES * CommonPerMeshUniformsInstanced.SizeInBytes; //256 MB just to play safe
            //OpenGL Spec max size for the SSBO is 128 MB, lets stick to that
            ssboSize = size;

            //Generate 3 Buffers for triple buffering. Each one is mapped once and stays mapped (persistent),
            //coherent mapping makes CPU writes visible to the GPU without explicit flushes
            for (int i = 0; i < MULTI_BUFFER_COUNT; i++)
            {
                int ssbo_id = GL.GenBuffer();
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssbo_id);
                GL.BufferStorage(BufferTarget.ShaderStorageBuffer, size, IntPtr.Zero,
                    BufferStorageFlags.MapPersistentBit | BufferStorageFlags.MapWriteBit | BufferStorageFlags.MapCoherentBit);
                IntPtr ptr = GL.MapBufferRange(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, size,
                    MapBufferAccessMask.MapWriteBit | MapBufferAccessMask.MapPersistentBit | MapBufferAccessMask.MapCoherentBit);
                multiBufferSSBOs.Add(ssbo_id);
                multiBufferPtrs.Add(ptr);
                multiBufferSyncStatuses.Add(IntPtr.Zero); //Not used by the GPU yet
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
            cpfu.cameraPositionExposure.W = MathF.Pow(2.0f, RenderState.renderSettings.HDRExposure); //Exposure in EV stops
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

        //Bytes a mesh takes in the per-mesh SSBO: the header and its instances, aligned to 256 bytes
        //so that every mesh starts at a valid SSBO binding offset
        private static int getMeshSSBOSize(GLMeshVao m)
        {
            if (m.instance_count == 0)
                return 0;
            return ((4 * getMeshSSBOFloats(m) + 255) / 256) * 256;
        }

        private static int getMeshSSBOFloats(GLMeshVao m)
        {
            int floats = GLMeshBufferManager.instanceData_Float_Offset +
                         m.instance_count * GLMeshBufferManager.instance_struct_size_floats;
            return Math.Min(floats, m.dataBuffer.Length);
        }

        private bool prepareCommonPermeshSSBO(GLMeshVao m, IntPtr ptr, ref int UBO_Offset)
        {
            m.UBO_aligned_size = 0;

            int size = getMeshSSBOSize(m);
            if (size == 0)
                return true;

            if (size + UBO_Offset > ssboSize)
            {
#if DEBUG
                CallBacks.Log("Mesh overload skipping...");
#endif
                return false;
            }

            if (m.skinned)
                m.uploadSkinningData();

            //Write straight into the mapped buffer, only the header and the used instances
            Marshal.Copy(m.dataBuffer, 0, ptr + UBO_Offset, getMeshSSBOFloats(m));

            m.UBO_aligned_size = size;
            m.UBO_offset = UBO_Offset; //Save offset
            UBO_Offset += size; //Increase the offset

            return true;
        }

        //This Method binds UBos to binding points
        private void bindUBOs()
        {
            //Bind Matrices
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, UBOs["_COMMON_PER_FRAME"]);
        }


        //Size of the window's framebuffer. The final blit scales the render targets to it, so the view
        //stays filled while the targets wait for the window size to settle
        public int screenWidth = 0;
        public int screenHeight = 0;

        public void setScreenSize(int w, int h)
        {
            screenWidth = w;
            screenHeight = h;
        }

        public bool hasRenderSize(int w, int h)
        {
            return gbuf != null && gbuf.size[0] == w && gbuf.size[1] == h;
        }

        public void resize(int w, int h)
        {
            gbuf?.resize(w, h);
            pbuf?.resize(w, h);
            setupBloom(w, h);
            
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
                    //Squared distances give the same order without a sqrt per comparison
                    float d1 = (l1.worldPosition - RenderState.activeCam.Position).LengthSquared;
                    float d2 = (l2.worldPosition - RenderState.activeCam.Position).LengthSquared;

                    return d1.CompareTo(d2);
                }
            );

            resMgr.GLlights.Insert(0, mainLight);
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
            //Grow the buffers before writing if this frame does not fit
            int required = 0;
            foreach (GLMeshVao m in globalMeshList)
                required += getMeshSSBOSize(m);

            if (required > ssboSize)
                resizeSSBOs(Math.Max(required + required / 2, ssboSize + ssboSize / 4));

            //Move to the next buffer of the ring. The GPU may still be reading it from MULTI_BUFFER_COUNT
            //frames ago, so wait for the fence placed at the end of that frame before overwriting it
            multiBufferActiveId = (multiBufferActiveId + 1) % MULTI_BUFFER_COUNT;
            waitForSSBO(multiBufferActiveId);
            SSBOs["_COMMON_PER_MESH"] = multiBufferSSBOs[multiBufferActiveId];

            IntPtr ptr = multiBufferPtrs[multiBufferActiveId];
            int ubo_offset = 0;
            foreach (GLMeshVao m in globalMeshList)
                prepareCommonPermeshSSBO(m, ptr, ref ubo_offset);
        }

        private void waitForSSBO(int id)
        {
            IntPtr sync = multiBufferSyncStatuses[id];
            if (sync == IntPtr.Zero)
                return;

            WaitSyncStatus status = GL.ClientWaitSync(sync, ClientWaitSyncFlags.SyncFlushCommandsBit, 0);
            while (status == WaitSyncStatus.TimeoutExpired)
                status = GL.ClientWaitSync(sync, ClientWaitSyncFlags.SyncFlushCommandsBit, 1000000); //1 ms

            GL.DeleteSync(sync);
            multiBufferSyncStatuses[id] = IntPtr.Zero;
        }

        //Marks the end of this frame's GPU work on the active per-mesh SSBO
        private void fenceActiveSSBO()
        {
            if (multiBufferSyncStatuses[multiBufferActiveId] != IntPtr.Zero)
                GL.DeleteSync(multiBufferSyncStatuses[multiBufferActiveId]);
            multiBufferSyncStatuses[multiBufferActiveId] = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, 0);
        }



        private void renderDefaultMeshes()
        {
            TextureBindCache.reset(); //Other passes bind textures directly
            GL.Disable(EnableCap.CullFace);
            GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Line);

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

            GL.PolygonMode(TriangleFace.FrontAndBack, RenderState.renderSettings.RENDERMODE);

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

        //Culling state while drawing a mesh list: double-sided materials need their back faces
        private bool cullFaceEnabled = true;

        private void beginMeshCulling()
        {
            GL.Enable(EnableCap.CullFace);
            cullFaceEnabled = true;
        }

        private void setMeshCulling(GLMeshVao m)
        {
            bool cull = !(m.material?.DoubleSided ?? false);
            if (cull == cullFaceEnabled)
                return;
            cullFaceEnabled = cull;
            if (cull)
                GL.Enable(EnableCap.CullFace);
            else
                GL.Disable(EnableCap.CullFace);
        }

        private void renderStaticMeshes(List<GLSLShaderConfig> shaderList)
        {
            TextureBindCache.reset(); //Other passes bind textures directly
            beginMeshCulling();
            //Set polygon mode
            GL.PolygonMode(TriangleFace.FrontAndBack, RenderState.renderSettings.RENDERMODE);
            
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

                    setMeshCulling(m);
                    m.render(shader, RENDERPASS.DEFERRED);
                    if (RenderState.renderViewSettings.RenderBoundHulls)
                    {
                        GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Line);
                        m.render(shader, RENDERPASS.BHULL);
                        GL.PolygonMode(TriangleFace.FrontAndBack, RenderState.renderSettings.RENDERMODE);
                    }
                        
                }

                GL.BindBuffer(BufferTarget.UniformBuffer, 0); //Unbind UBOs
                beginMeshCulling();


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

            //The unlit stage copies the lit image into albedo, draws on top of it and copies it back.
            //With nothing to draw both copies are wasted, and pbuf.color already holds the lit image.
            //Helpers (collisions, locators, joints, lights) are drawn later, after tone mapping
            if (needsUnlitStage())
            {
                pass_tex(gbuf.fbo, DrawBufferMode.ColorAttachment0, pbuf.color, gbuf.size); //Copy accumulated light to albedo
                gbuf.bind();
                renderStaticMeshes(RenderState.activeResMgr.activeGLDeferredUNLITShaders); //UNLIT MESHES

                renderDeferredPass(); //Final unlit deferred rendering pass
            }

            //FORWARD STAGE - TRANSPARENT MESHES (skipped when there are none: it clears two full screen
            //textures and composites)
            if (hasTransparentMeshes())
            {
                //Copy depth channel to pbuf
                FBO.copyDepthChannel(gbuf.fbo, pbuf.fbo, pbuf.size[0], pbuf.size[1], gbuf.size[0], gbuf.size[1]);
                renderTransparent();
            }
                    
        }
        
        private static bool hasDrawableMeshes(IEnumerable<GLMeshVao> meshes)
        {
            foreach (GLMeshVao m in meshes)
            {
                if (m.instance_count > 0 && m.UBO_aligned_size > 0)
                    return true;
            }
            return false;
        }

        private bool needsUnlitStage()
        {
            foreach (GLSLShaderConfig shader in RenderState.activeResMgr.activeGLDeferredUNLITShaders)
            {
                if (hasDrawableMeshes(resMgr.opaqueMeshShaderMap[shader.shaderHash]))
                    return true;
            }
            return false;
        }

        private bool hasVisibleHelpers()
        {
            var view = RenderState.renderViewSettings;
            return (view.RenderCollisions && hasDrawableMeshes(collisionMeshList)) ||
                   (view.RenderLights && hasDrawableMeshes(lightMeshList)) ||
                   (view.RenderJoints && hasDrawableMeshes(jointMeshList)) ||
                   (view.RenderLocators && hasDrawableMeshes(locatorMeshList));
        }

        //Helpers are drawn into the finished (tone mapped) frame so exposure and bloom don't change
        //their colors. They are depth tested against the scene depth from the gbuffer
        private void renderHelpers()
        {
            if (!hasVisibleHelpers())
                return;

            FBO.copyDepthChannel(gbuf.fbo, pbuf.fbo, pbuf.size[0], pbuf.size[1], gbuf.size[0], gbuf.size[1]);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.DrawBuffer(finalColorAttachment == ReadBufferMode.ColorAttachment1 ?
                DrawBufferMode.ColorAttachment1 : DrawBufferMode.ColorAttachment0);
            GL.Viewport(0, 0, gbuf.size[0], gbuf.size[1]);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthMask(true);

            renderDefaultMeshes(); //Collisions, Locators, Joints, Lights
        }

        private bool hasTransparentMeshes()
        {
            foreach (GLSLShaderConfig shader in resMgr.activeGLForwardTransparentShaders)
            {
                if (hasDrawableMeshes(resMgr.transparentMeshShaderMap[shader.shaderHash]))
                    return true;
            }
            return false;
        }

        private void renderDecalMeshes()
        {
            TextureBindCache.reset(); //Other passes bind textures directly
            GL.DepthMask(false);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(TriangleFace.Back);
            
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
            GL.CullFace(TriangleFace.Back);
            GL.Enable(EnableCap.CullFace);
            GL.DepthMask(true);
        }

        private static readonly float[] revealageClear = { 1.0f, 1.0f, 1.0f, 1.0f };
        private static readonly DrawBuffersEnum[] transparentDrawBuffers = { DrawBuffersEnum.ColorAttachment1, DrawBuffersEnum.ColorAttachment2 };
        private static readonly string[] gizmoPartNames = { "default_translation_gizmo_x_axis",
                                                            "default_translation_gizmo_y_axis",
                                                            "default_translation_gizmo_z_axis" };

        private void renderTransparent()
        {
            TextureBindCache.reset(); //Other passes bind textures directly
            //Render the first pass in the first channel of the pbuf
            GL.ClearTexImage(pbuf.blur1, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.ClearTexImage(pbuf.blur2, 0, PixelFormat.Rgba, PixelType.Float, revealageClear);

            //Enable writing to both channels after clearing
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.DrawBuffers(2, transparentDrawBuffers);
            
            //At first render the static meshes
            GL.Enable(EnableCap.Blend);
            GL.DepthMask(false);
            GL.Enable(EnableCap.DepthTest); //Enable depth test
            //Set BlendFuncs for the 2 drawbuffers
            GL.BlendFunc(0, BlendingFactorSrc.One, BlendingFactorDest.One);
            GL.BlendFunc(1, BlendingFactorSrc.Zero, BlendingFactorDest.OneMinusSrcAlpha);

            //Set polygon mode
            GL.PolygonMode(TriangleFace.FrontAndBack, RenderState.renderSettings.RENDERMODE);
            beginMeshCulling();

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

                    setMeshCulling(m);
                    m.render(shader, RENDERPASS.FORWARD);
                    //if (RenderOptions.RenderBoundHulls)
                    //    m.render(shader, RENDERPASS.BHULL);
                }
                GL.BindBuffer(BufferTarget.UniformBuffer, 0); //Unbind UBOs

            }
            
            GL.DepthMask(true); //Re-enable depth buffer
            beginMeshCulling();
            
            //Composite Step
            GLSLShaderConfig bwoit_composite_shader = RenderState.activeResMgr.GLShaders[SHADER_TYPE.BWOIT_COMPOSITE_SHADER];

            //Draw to main color channel
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            GL.BlendFunc(BlendingFactor.OneMinusSrcAlpha, BlendingFactor.SrcAlpha); //Set compositing blend func
            //GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha); //Set compositing blend func
            render_quad([], [], ["in1Tex", "in2Tex"], [TextureTarget.Texture2D, TextureTarget.Texture2D],
                [pbuf.blur1, pbuf.blur2], bwoit_composite_shader);
            GL.Disable(EnableCap.Blend);
            
        }

        
        private void renderFinalPass()
        {
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, pbuf.fbo);
            GL.ReadBuffer(finalColorAttachment);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, screen_fbo);

            int dstWidth = screenWidth > 0 ? screenWidth : gbuf.size[0];
            int dstHeight = screenHeight > 0 ? screenHeight : gbuf.size[1];
            GL.BlitFramebuffer(0, 0, gbuf.size[0], gbuf.size[1], 0, 0, dstWidth, dstHeight,
                ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);

        }

        private void renderUI()
        {
            TextureBindCache.reset(); //Other passes bind textures directly
            
            if (RenderState.renderViewSettings.RenderGizmos)
            {
                GL.Clear(ClearBufferMask.DepthBufferBit); //Clear depth
                GL.Enable(EnableCap.DepthTest);
                GL.Disable(EnableCap.CullFace);
                GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
                GLSLShaderConfig shader = resMgr.GLShaders[SHADER_TYPE.GIZMO_SHADER];
                GL.UseProgram(shader.program_id);


                //Render GizmoParts


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
            
            //Prepare Mesh UBO
            prepareCommonPerMeshSSBOs();
            
            //Render octree
            //octree.render(resMgr.GLShaders[GLSLHelper.SHADER_TYPE.BBOX_SHADER].program_id);

            //Render Geometry
            renderGeometry();

            //POST-PROCESSING
            post_process();

            //Helpers on top of the tone mapped frame
            renderHelpers();

            //Final Pass
            renderFinalPass();

            //Render UI();

            renderUI();

            //Fence after every draw that reads the per-mesh SSBO
            fenceActiveSSBO();
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

        //Spans let callers pass collection expressions ([...]), which live on the stack instead of
        //allocating five arrays per call on every frame
        private void render_quad(ReadOnlySpan<string> uniforms, ReadOnlySpan<float> uniform_values, ReadOnlySpan<string> sampler_names,
                                 ReadOnlySpan<TextureTarget> sampler_targets, ReadOnlySpan<int> texture_ids, GLSLHelper.GLSLShaderConfig shaderConf)
        {
            int quad_vao = resMgr.GLPrimitiveVaos["default_renderquad"].vao_id;

            GL.UseProgram(shaderConf.program_id);
            GL.BindVertexArray(quad_vao);

            //Upload samplers
            for (int i = 0; i < sampler_names.Length; i++)
            {
                GL.Uniform1(shaderConf.uniformLocations[sampler_names[i]], i);
                GL.ActiveTexture(TextureUnit.Texture0 + i);
                GL.BindTexture(sampler_targets[i], texture_ids[i]);
            }

            //Upload uniforms - Assuming single float uniforms for now
            for (int i = 0; i < uniforms.Length; i++)
                GL.Uniform1(shaderConf.uniformLocations[uniforms[i]], uniform_values[i]);

            //Render quad
            GL.Disable(EnableCap.DepthTest);
            GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
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
            //render_quad(["sizeX", "sizeY"], [to_buf_size[0], to_buf_size[1]], ["InTex"], [InTex], shader);
            render_quad([], [], ["InTex"], [TextureTarget.Texture2D],  [InTex], shader);
            GL.Enable(EnableCap.DepthTest); //Re-enable Depth test
        }

        private void deleteBloom()
        {
            if (bloomMips.Length > 0)
                GL.DeleteTextures(bloomMips.Length, bloomMips);
            bloomMips = new int[0];
            bloomMipWidths = new int[0];
            bloomMipHeights = new int[0];
        }

        private void setupBloom(int width, int height)
        {
            deleteBloom();

            if (bloomFbo < 0)
                bloomFbo = GL.GenFramebuffer();

            List<int> mips = new List<int>();
            List<int> widths = new List<int>();
            List<int> heights = new List<int>();
            int w = width / 2;
            int h = height / 2;
            while (mips.Count < BLOOM_MAX_MIPS && w >= BLOOM_MIN_MIP_SIZE && h >= BLOOM_MIN_MIP_SIZE)
            {
                int tex = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, tex);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R11fG11fB10f, w, h, 0,
                    PixelFormat.Rgb, PixelType.Float, IntPtr.Zero);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                mips.Add(tex);
                widths.Add(w);
                heights.Add(h);
                w /= 2;
                h /= 2;
            }
            GL.BindTexture(TextureTarget.Texture2D, 0);

            bloomMips = mips.ToArray();
            bloomMipWidths = widths.ToArray();
            bloomMipHeights = heights.ToArray();
        }

        private void bindBloomTarget(int mip)
        {
            GL.FramebufferTexture2D(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D, bloomMips[mip], 0);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            GL.Viewport(0, 0, bloomMipWidths[mip], bloomMipHeights[mip]);
        }

        //Bloom as a mip chain (downsample with a 13-tap filter, upsample with a tent filter), all in
        //linear HDR before tone mapping. The result is added straight into pbuf.color with blending
        private void bloom()
        {
            if (bloomMips.Length == 0)
                return;

            GLSLShaderConfig downsample_program = resMgr.GLShaders[SHADER_TYPE.BLOOM_DOWNSAMPLE_SHADER];
            GLSLShaderConfig upsample_program = resMgr.GLShaders[SHADER_TYPE.BLOOM_UPSAMPLE_SHADER];

            float bloomThreshold = RenderState.renderSettings.BloomThreshold;
            float bloomKnee = Math.Max(RenderState.renderSettings.BloomKnee, 1e-4f);
            float bloomIntensity = RenderState.renderSettings.BloomIntensity;
            float bloomRadius = RenderState.renderSettings.BloomRadius;

            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.Blend);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, bloomFbo);

            //Downsample: frame -> mip 0 (with the bright threshold) -> mip 1 -> ...
            int src = pbuf.color;
            for (int i = 0; i < bloomMips.Length; i++)
            {
                bindBloomTarget(i);
                render_quad(["uPrefilter", "uThreshold", "uKnee"],
                            [i == 0 ? 1.0f : 0.0f, bloomThreshold, bloomKnee],
                            ["srcTex"], [TextureTarget.Texture2D],
                            [src], downsample_program);
                src = bloomMips[i];
            }

            //Upsample: add each blurred mip onto the next larger one
            GL.Enable(EnableCap.Blend);
            GL.BlendEquation(BlendEquationMode.FuncAdd);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            for (int i = bloomMips.Length - 1; i > 0; i--)
            {
                bindBloomTarget(i - 1);
                render_quad(["uRadius", "uIntensity"], [bloomRadius, 1.0f],
                            ["srcTex"], [TextureTarget.Texture2D],
                            [bloomMips[i]], upsample_program);
            }

            //Composite mip 0 into the HDR frame
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            GL.Viewport(0, 0, gbuf.size[0], gbuf.size[1]);
            render_quad(["uRadius", "uIntensity"], [bloomRadius, bloomIntensity],
                        ["srcTex"], [TextureTarget.Texture2D],
                        [bloomMips[0]], upsample_program);

            GL.Disable(EnableCap.Blend);
        }

        private void fxaa()
        {
            //inv_tone_mapping(); //Apply tone mapping pbuf.color shoud be ready
            
            //Load Programs
            GLSLShaderConfig fxaa_program = resMgr.GLShaders[SHADER_TYPE.FXAA_SHADER];

            //Apply FXAA. Tone mapping left its result in blur1, so read it from there and write to color
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);

            render_quad([], [], ["diffuseTex"], [TextureTarget.Texture2D], [pbuf.blur1], fxaa_program);
            finalColorAttachment = ReadBufferMode.ColorAttachment0;

            //tone_mapping(); //Invert Tone Mapping

        }

        private void tone_mapping()
        {
            //Load Programs
            GLSLShaderConfig tone_mapping_program = resMgr.GLShaders[SHADER_TYPE.TONE_MAPPING];

            //Apply Tone Mapping, reading color and writing blur1 (no copy needed first)
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment1);

            render_quad([], [], ["inTex"], [TextureTarget.Texture2D], [pbuf.color], tone_mapping_program);
            finalColorAttachment = ReadBufferMode.ColorAttachment1;

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

            render_quad([], [], ["inTex"], [TextureTarget.Texture2D], [pbuf.blur1], inv_tone_mapping_program);

        }

        private void post_process()
        {
            //Actuall Post Process effects in AA space without tone mapping
            if (RenderState.renderSettings.UseBLOOM)
                bloom(); //BLOOM

            tone_mapping(); //FINAL TONE MAPPING, INCLUDES GAMMA CORRECTION

            if (RenderState.renderSettings.UseFXAA)
                fxaa(); //FXAA (INCLUDING TONE/UNTONE)
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
            GLSLShaderConfig shader_conf = resMgr.GLShaders[SHADER_TYPE.GBUFFER_LIT_SHADER];

            //Bind the color channel of the pbuf for drawing
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0); //Draw to the light color channel only

            //TEST DRAW TO SCREEN
            //GL.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);

            //GL.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            render_quad([], [], ["albedoTex", "depthTex", "normalTex", "parameterTex"],
                                                            [TextureTarget.Texture2D, TextureTarget.Texture2D,
                                                            TextureTarget.Texture2D, TextureTarget.Texture2D],
                                                            [gbuf.albedo, gbuf.depth, gbuf.normals, gbuf.info], shader_conf);
        }

        private void renderDeferredPass()
        {
            GLSLShaderConfig shader_conf = resMgr.GLShaders[SHADER_TYPE.GBUFFER_UNLIT_SHADER];

            //Bind default fbo
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0); //Draw to the light color channel only

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.Disable(EnableCap.DepthTest); //Disable Depth test
            render_quad([], [], ["albedoTex"], [TextureTarget.Texture2D],
                                                            [gbuf.albedo], shader_conf);
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
