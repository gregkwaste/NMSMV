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
using MVCore.Text;
using MVCore.Engine.Systems;


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

    public class renderManager : IDisposable
    {
        List<GLMeshVao> globalMeshList = new List<GLMeshVao>();
        private readonly HashSet<GLMeshVao> loggedDecalRoutes = new HashSet<GLMeshVao>();
        List<GLMeshVao> collisionMeshList = new List<GLMeshVao>();
        List<GLMeshVao> locatorMeshList = new List<GLMeshVao>();
        List<GLMeshVao> jointMeshList = new List<GLMeshVao>();
        List<GLMeshVao> lightMeshList = new List<GLMeshVao>();

        private ResourceManager resMgr;

        public ShadowRenderer shdwRenderer; //Shadow Renderer instance
        public LightClusters lightClusters; //Scene lights sorted into view clusters every frame
        private ParticleSystem particleSystem;
        private EngineTextRenderer textRenderer;
        private readonly System.Diagnostics.Stopwatch textStatsClock = System.Diagnostics.Stopwatch.StartNew();
        private string viewportStatsText = "";

        //GPU times of the whole frame and of the deferred light pass, logged every few seconds
        private GpuTimer frameTimer, lightPassTimer;
        private readonly System.Diagnostics.Stopwatch frameStatsClock = System.Diagnostics.Stopwatch.StartNew();
        private readonly System.Diagnostics.Stopwatch frameCpuClock = new();
        public double lastRenderCpuMs { get; private set; } //CPU time of the last render(), for the frame loop stats
        public double lastFrameGpuMs => frameTimer?.Ms ?? 0.0;
        public double lastMeshUploadCpuMs { get; private set; }
        public double lastFinalBlitCpuMs { get; private set; }
        public int lastStableRecordsUploaded { get; private set; }
        public int lastVisibleIdsUploaded { get; private set; }

        //GPU time of a span of draw calls, from a pair of timestamps (unlike elapsed time queries, these can
        //nest). The queries are read a few frames late, so the CPU never waits for them
        private class GpuTimer : IDisposable
        {
            private const int QUERIES = 4;
            private readonly int[] starts = new int[QUERIES];
            private readonly int[] ends = new int[QUERIES];
            private readonly bool[] used = new bool[QUERIES];
            private int frame = 0;
            public double Ms { get; private set; }

            public GpuTimer()
            {
                GL.GenQueries(QUERIES, starts);
                GL.GenQueries(QUERIES, ends);
            }

            public void Begin()
            {
                //Read the result of this slot's last use before reusing it
                int q = frame % QUERIES;
                if (used[q])
                {
                    GL.GetQueryObject(ends[q], GetQueryObjectParam.QueryResultAvailable, out int available);
                    if (available != 0)
                    {
                        GL.GetQueryObject(starts[q], GetQueryObjectParam.QueryResult, out long start);
                        GL.GetQueryObject(ends[q], GetQueryObjectParam.QueryResult, out long end);
                        Ms = (end - start) / 1.0e6;
                    }
                }
                GL.QueryCounter(starts[q], QueryCounterTarget.Timestamp);
            }

            public void End()
            {
                int q = frame % QUERIES;
                GL.QueryCounter(ends[q], QueryCounterTarget.Timestamp);
                used[q] = true;
                frame++;
            }

            public void Dispose()
            {
                GL.DeleteQueries(QUERIES, starts);
                GL.DeleteQueries(QUERIES, ends);
            }
        }
        
        private GBuffer gbuf;
        private PBuffer pbuf;
        public int screen_fbo;
        //Bloom mip chain: each level is half the size of the previous one, starting at half resolution
        private int bloomFbo = -1;
        private int[] bloomMips = new int[0];
        private int[] bloomMipWidths = new int[0];
        private int[] bloomMipHeights = new int[0];
        private const int BLOOM_MAX_MIPS = 6;
        private const int BLOOM_MIN_MIP_SIZE = 8;
        //Bloom parameters live in RenderState.renderSettings (Render Settings tab)
        private double gfTime = 0.0; //Seconds. Kept in double so the wrap below stays exact
        private Dictionary<string, int> UBOs = new Dictionary<string, int>();
        private Dictionary<string, int> SSBOs = new Dictionary<string, int>();

        private int multiBufferActiveId = 0;
        private List<int> multiBufferSSBOs = new List<int>(4);
        private List<IntPtr> multiBufferSyncStatuses = new List<IntPtr>(4); //Fence of the last frame that used each buffer
        private List<IntPtr> multiBufferPtrs = new List<IntPtr>(4); //Persistent mappings of the buffers
        
        //UBO structs
        CommonPerFrameUniforms cpfu;
        private int ssboSize; //Size of each per-mesh SSBO in bytes
        private int ssboGeneration;
        private ReadBufferMode finalColorAttachment = ReadBufferMode.ColorAttachment0; //pbuf channel holding the finished frame

        private int MULTI_BUFFER_COUNT = 3;
#if DEBUG
        private DebugProc GLDebug;
#endif


        public void init(ResourceManager input_resMgr, ParticleSystem inputParticleSystem)
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
            particleSystem = inputParticleSystem;
            if (!resMgr.initialized)
                throw new InvalidOperationException("The resource manager must be initialized before the renderer");
            
            //Setup Shadow Renderer
            shdwRenderer = new ShadowRenderer();

            //Setup the scene light clusters
            lightClusters = new LightClusters();
            frameTimer = new GpuTimer();
            lightPassTimer = new GpuTimer();
            textRenderer = new EngineTextRenderer(resMgr, "Resources/droid.fnt", "Resources/droid.png");

            //Setup per Frame UBOs
            setupFrameUBO();

            //Setup SSBOs
            setupSSBOs(4 * 1024 * 1024); //Init SSBOs to 4MB
            multiBufferActiveId = 0;
            SSBOs["_COMMON_PER_MESH"] = multiBufferSSBOs[0];
            
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
        }

        public void progressTime(double dt)
        {
            //dt is in milliseconds, the shaders expect seconds like the game's gfTime.
            //Wrap after an hour so the float sent to the GPU keeps its precision. Common scroll steps
            //(multiples of 1/3600) land on whole uv repeats there, so the wrap is seamless for them
            gfTime += dt / 1000.0;
            gfTime %= 3600.0;
        }

        public void cleanup()
        {
            //Just cleanup the queues
            //The resource manager will handle the cleanup of the buffers and shit

            resetInstanceRegistries();
            loggedDecalRoutes.Clear();
            globalMeshList.Clear();
            collisionMeshList.Clear();
            locatorMeshList.Clear();
            jointMeshList.Clear();
            lightMeshList.Clear();
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

            foreach (GLSLShaderConfig shader in RenderState.activeResMgr.activeGLDeferredDecalShaders)
            {
                foreach (GLMeshVao mesh in RenderState.activeResMgr.decalMeshShaderMap[shader.shaderHash])
                {
                    if (!loggedDecalRoutes.Add(mesh))
                        continue;

                    CallBacks.Log("[DecalRoute] shader={0}; material={1}; class={2}; stableInstances={3}; visibleInstances={4}; indices={5}",
                        shader.shaderHash, mesh.material?.Name ?? "<none>", mesh.material?.PClass ?? "<none>",
                        mesh.stable_instance_count, mesh.instance_count, mesh.metaData?.batchcount ?? 0);
                }
            }
        }

        public void populate()
        {
            cleanup();

            foreach (Scene s in resMgr.GLScenes.Values)
                process_models(s);

            //Add default light mesh
            process_model(resMgr.GLlights[0].meshVao);

            logOccluderInventory();
            
            identifyActiveShaders();

        }

        //One-time scene inventory used to decide whether the game's simplified shadow geometry
        //is substantial enough to justify building a separate local-light visibility structure. Triangle
        //counts include stable instances and are approximate because batch counts are index counts.
        private void logOccluderInventory()
        {
            int shadowProxyMeshes = 0, shadowProxyInstances = 0;
            int regularCasterMeshes = 0, regularCasterInstances = 0;
            long shadowProxyTriangles = 0, regularCasterTriangles = 0;

            foreach (GLMeshVao mesh in globalMeshList)
            {
                if (mesh == null || mesh.metaData == null)
                    continue;

                int instances = Math.Max(mesh.stable_instance_count, 0);
                long triangles = Math.Max(mesh.metaData.batchcount, 0) / 3L * instances;

                if (mesh.type == TYPES.COLLISION)
                    continue;

                if (mesh.material?.ShadowOnly ?? false)
                {
                    shadowProxyMeshes++;
                    shadowProxyInstances += instances;
                    shadowProxyTriangles += triangles;
                }
                else if (mesh.material?.CastShadow ?? false)
                {
                    regularCasterMeshes++;
                    regularCasterInstances += instances;
                    regularCasterTriangles += triangles;
                }
            }

            CallBacks.Log("[Occluders] shadow-only proxies: {0} mesh batches, {1} instances, ~{2} triangles; ordinary shadow casters: {3} mesh batches, {4} instances, ~{5} triangles",
                shadowProxyMeshes, shadowProxyInstances, shadowProxyTriangles,
                regularCasterMeshes, regularCasterInstances, regularCasterTriangles);
            CallBacks.Log("[Occluders] the current shadow map uses these meshes for the key light only; collision geometry is excluded from local-light occlusion analysis");
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

        public void resetInstanceRegistries()
        {
            lock (globalMeshList)
            {
                foreach (GLMeshVao m in globalMeshList)
                    GLMeshBufferManager.resetInstances(m);
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

            //Copy of the per frame data with the key light as the camera, bound while drawing the shadow map
            ubo_id = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.UniformBuffer, ubo_id);
            GL.BufferData(BufferTarget.UniformBuffer, CommonPerFrameUniforms.SizeInBytes, IntPtr.Zero, BufferUsageHint.StreamDraw);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
            UBOs["_COMMON_PER_FRAME_SHADOW"] = ubo_id;

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
            //OpenGL Spec max size for the SSBO is 128 MB, lets stick to that
            ssboSize = size;
            ssboGeneration++;

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
            //Only the key light goes in the UBO, the scene lights are in the light cluster buffers
            cpfu.light_number = Math.Min(1, resMgr.GLlights.Count);
            cpfu.gfTime = (float) gfTime;

            //Upload light information
            for (int i = 0; i < cpfu.light_number; i++)
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

        private static int alignSSBORange(int bytes)
        {
            return ((bytes + 255) / 256) * 256;
        }

        //The instance record range contains every stable record for the batch. The visible-ID range
        //contains only this frame's compact draw list. Both ranges share the mapped frame ring.
        private static int getMeshSSBOSize(GLMeshVao m)
        {
            if (m.stable_instance_count == 0)
                return 0;
            return alignSSBORange(4 * getMeshSSBOFloats(m));
        }

        private static int getMeshSSBOFloats(GLMeshVao m)
        {
            int floats = GLMeshBufferManager.instanceData_Float_Offset +
                         m.stable_instance_count * GLMeshBufferManager.instance_struct_size_floats;
            return Math.Min(floats, m.dataBuffer.Length);
        }

        private static int getVisibleIdsSSBOSize(GLMeshVao m)
        {
            return m.instance_count == 0 ? 0 : alignSSBORange(m.instance_count * sizeof(int));
        }

        private static void updateVisibleListVersion(GLMeshVao m)
        {
            bool changed = m.visibleListSnapshotCount != m.instance_count;
            if (!changed)
            {
                for (int i = 0; i < m.instance_count; i++)
                {
                    if (m.visibleListSnapshot[i] != m.visibleInstanceIds[i])
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (!changed)
                return;

            if (m.visibleListSnapshot.Length < m.instance_count)
                Array.Resize(ref m.visibleListSnapshot, Math.Max(m.instance_count, 2 * m.visibleListSnapshot.Length));
            Array.Copy(m.visibleInstanceIds, m.visibleListSnapshot, m.instance_count);
            m.visibleListSnapshotCount = m.instance_count;
            unchecked { m.visibleListVersion++; }
            if (m.visibleListVersion == 0)
                m.visibleListVersion = 1;
        }

        private bool prepareCommonPermeshSSBO(GLMeshVao m, IntPtr ptr, int ringId,
                                               ref int stableOffset, ref int visibleOffset,
                                               out int stableRecordsCopied,
                                               out int visibleIdsCopied)
        {
            stableRecordsCopied = 0;
            visibleIdsCopied = 0;
            m.UBO_aligned_size = 0;
            m.visibleIds_aligned_size = 0;

            int instanceSize = getMeshSSBOSize(m);
            int visibleIdsSize = getVisibleIdsSSBOSize(m);
            if (instanceSize == 0)
                return true;

            if (stableOffset + instanceSize > ssboSize || visibleOffset + visibleIdsSize > ssboSize)
            {
#if DEBUG
                CallBacks.Log("Mesh overload skipping...");
#endif
                return false;
            }

            if (m.skinned)
                m.uploadSkinningData();

            m.UBO_aligned_size = instanceSize;
            m.UBO_offset = stableOffset;

            bool stableLayoutChanged = m.stableUploadOffsets[ringId] != stableOffset ||
                                       m.stableUploadGenerations[ringId] != ssboGeneration ||
                                       m.stableUploadCounts[ringId] != m.stable_instance_count;
            if (stableLayoutChanged)
            {
                //Only the four-float batch header is unconditional. Instance records are populated lazily
                //below when they are visible in this ring slot.
                Marshal.Copy(m.dataBuffer, 0, ptr + stableOffset,
                             GLMeshBufferManager.instanceData_Float_Offset);
                Array.Clear(m.uploadedInstanceVersions[ringId], 0, m.stable_instance_count);
                m.stableUploadOffsets[ringId] = stableOffset;
                m.stableUploadGenerations[ringId] = ssboGeneration;
                m.stableUploadCounts[ringId] = m.stable_instance_count;
            }

            //Stable IDs are direct record slots. Only visible records whose version is stale in this
            //particular ring buffer are copied; invisible dirty records wait until they are drawn.
            for (int i = 0; i < m.instance_count; i++)
            {
                int instanceId = m.visibleInstanceIds[i];
                long version = m.instanceVersions[instanceId];
                if (m.uploadedInstanceVersions[ringId][instanceId] == version)
                    continue;

                int sourceFloat = GLMeshBufferManager.instanceBase(instanceId);
                IntPtr destination = ptr + stableOffset + sourceFloat * sizeof(float);
                Marshal.Copy(m.dataBuffer, sourceFloat, destination,
                             GLMeshBufferManager.instance_struct_size_floats);
                m.uploadedInstanceVersions[ringId][instanceId] = version;
                stableRecordsCopied++;
            }
            stableOffset += instanceSize;

            m.visibleIds_aligned_size = visibleIdsSize;
            m.visibleIds_offset = visibleOffset;
            if (visibleIdsSize > 0)
            {
                if (m.uploadedVisibleListVersions[ringId] != m.visibleListVersion ||
                    m.uploadedVisibleOffsets[ringId] != visibleOffset ||
                    m.uploadedVisibleGenerations[ringId] != ssboGeneration)
                {
                    Marshal.Copy(m.visibleInstanceIds, 0, ptr + visibleOffset, m.instance_count);
                    m.uploadedVisibleListVersions[ringId] = m.visibleListVersion;
                    m.uploadedVisibleOffsets[ringId] = visibleOffset;
                    m.uploadedVisibleGenerations[ringId] = ssboGeneration;
                    visibleIdsCopied = m.instance_count;
                }
                visibleOffset += visibleIdsSize;
            }

            return true;
        }

        private void bindPerMeshSSBOs(GLMeshVao m)
        {
            GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                (IntPtr)m.UBO_offset, m.UBO_aligned_size);
            GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 5, SSBOs["_COMMON_PER_MESH"],
                (IntPtr)m.visibleIds_offset, m.visibleIds_aligned_size);
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

        private void prepareCommonPerMeshSSBOs()
        {
            //Grow the buffers before writing if this frame does not fit
            int stableRequired = 0;
            int visibleRequired = 0;
            foreach (GLMeshVao m in globalMeshList)
            {
                updateVisibleListVersion(m);
                stableRequired += getMeshSSBOSize(m);
                visibleRequired += getVisibleIdsSSBOSize(m);
            }
            int required = stableRequired + visibleRequired;

            if (required > ssboSize)
                resizeSSBOs(Math.Max(required + required / 2, ssboSize + ssboSize / 4));

            //Move to the next buffer of the ring. The GPU may still be reading it from MULTI_BUFFER_COUNT
            //frames ago, so wait for the fence placed at the end of that frame before overwriting it
            multiBufferActiveId = (multiBufferActiveId + 1) % MULTI_BUFFER_COUNT;
            waitForSSBO(multiBufferActiveId);
            SSBOs["_COMMON_PER_MESH"] = multiBufferSSBOs[multiBufferActiveId];

            IntPtr ptr = multiBufferPtrs[multiBufferActiveId];
            int stableOffset = 0;
            int visibleOffset = stableRequired;
            lastStableRecordsUploaded = 0;
            lastVisibleIdsUploaded = 0;
            foreach (GLMeshVao m in globalMeshList)
            {
                if (prepareCommonPermeshSSBO(m, ptr, multiBufferActiveId,
                                             ref stableOffset, ref visibleOffset,
                                             out int stableRecordsCopied,
                                             out int visibleIdsCopied))
                {
                    lastStableRecordsUploaded += stableRecordsCopied;
                    lastVisibleIdsUploaded += visibleIdsCopied;
                }
            }
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

                    bindPerMeshSSBOs(m);
                    
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

                    bindPerMeshSSBOs(m);

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

                    bindPerMeshSSBOs(m);

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

                    bindPerMeshSSBOs(m);

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
            bool showShadowMeshes = RenderState.renderViewSettings.RenderShadowMeshes;
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

                    //Shadow proxies are hidden unless the user asks to see them
                    if (!showShadowMeshes && (m.material?.ShadowOnly ?? false))
                        continue;

                    bindPerMeshSSBOs(m);

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
            FramebufferBlitter.CopyDepth(gbuf.fbo, pbuf.fbo, pbuf.size[0], pbuf.size[1], gbuf.size[0], gbuf.size[1]);
            gbuf.bind();
            renderDecalMeshes(); //Render Decals
            
            lightPassTimer.Begin();
            //Remove authored spotlights whose cones cannot intersect each cluster. The shader keeps
            //the exact per-pixel cone test as a correctness fallback.
            lightClusters.filterSpotCones(RenderState.activeCam);
            renderDeferredLightPass(); //Deferred Lighting Pass
            lightPassTimer.End();

            //The unlit stage copies the lit image into albedo, draws on top of it and copies it back.
            //With nothing to draw both copies are wasted, and pbuf.color already holds the lit image.
            //Helpers (collisions, locators, joints, lights) are drawn later, after tone mapping
            if (needsUnlitStage())
            {
                pass_tex(gbuf.fbo, DrawBufferMode.ColorAttachment0, pbuf.color); //Copy accumulated light to albedo
                gbuf.bind();
                renderStaticMeshes(RenderState.activeResMgr.activeGLDeferredUNLITShaders); //UNLIT MESHES

                renderDeferredPass(); //Final unlit deferred rendering pass
            }

            //FORWARD STAGE - TRANSPARENT MESHES (skipped when there are none: it clears two full screen
            //textures and composites)
            if (hasTransparentMeshes())
            {
                //Cone-vs-cluster filtering is geometry independent and is valid for transparent pixels.
                lightClusters.bindFilteredBuffers();
                //Copy depth channel to pbuf
                FramebufferBlitter.CopyDepth(gbuf.fbo, pbuf.fbo, pbuf.size[0], pbuf.size[1], gbuf.size[0], gbuf.size[1]);
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

            FramebufferBlitter.CopyDepth(gbuf.fbo, pbuf.fbo, pbuf.size[0], pbuf.size[1], gbuf.size[0], gbuf.size[1]);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.DrawBuffer(finalColorAttachment == ReadBufferMode.ColorAttachment1 ?
                DrawBufferMode.ColorAttachment1 : DrawBufferMode.ColorAttachment0);
            GL.Viewport(0, 0, gbuf.size[0], gbuf.size[1]);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthMask(true);

            renderDefaultMeshes(); //Collisions, Locators, Joints, Lights
        }

        //Particles are intentionally isolated from the weighted transparent-mesh compositor. They are
        //drawn over the tone-mapped result, but retain scene depth testing. Additive profiles provide a
        //usable glow approximation until particle authoring data and an HDR/bloom route are available.
        private void renderParticleEffects()
        {
            if (particleSystem == null || particleSystem.ParticleCount == 0)
                return;

            FramebufferBlitter.CopyDepth(gbuf.fbo, pbuf.fbo, pbuf.size[0], pbuf.size[1], gbuf.size[0], gbuf.size[1]);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.DrawBuffer(finalColorAttachment == ReadBufferMode.ColorAttachment1 ?
                DrawBufferMode.ColorAttachment1 : DrawBufferMode.ColorAttachment0);
            GL.Viewport(0, 0, gbuf.size[0], gbuf.size[1]);
            particleSystem.render();
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

            //Do not inherit blending or a depth function from a preceding helper/fullscreen pass.
            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
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

                    if (shader.uniformLocations.TryGetValue("decalAabbMin", out int decalMinLocation))
                    {
                        Vector3 decalMin = m.metaData.AABBMIN;
                        GL.Uniform3(decalMinLocation, ref decalMin);
                    }
                    if (shader.uniformLocations.TryGetValue("decalAabbMax", out int decalMaxLocation))
                    {
                        Vector3 decalMax = m.metaData.AABBMAX;
                        GL.Uniform3(decalMaxLocation, ref decalMax);
                    }

                    bindPerMeshSSBOs(m);
                    m.render(shader, RENDERPASS.DECAL);
                }


            }
            GL.CullFace(TriangleFace.Back);
            GL.Enable(EnableCap.CullFace);
            GL.DepthMask(true);
            GL.DepthFunc(DepthFunction.Less);
            GL.Disable(EnableCap.Blend);
            GL.ActiveTexture(TextureUnit.Texture0);
        }

        private static readonly float[] revealageClear = { 1.0f, 1.0f, 1.0f, 1.0f };
        private static readonly DrawBuffersEnum[] transparentDrawBuffers = { DrawBuffersEnum.ColorAttachment1, DrawBuffersEnum.ColorAttachment2 };
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
                    
                    bindPerMeshSSBOs(m);

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
            
            TransformGizmo gizmo = RenderState.activeGizmo;
            if (RenderState.renderViewSettings.RenderGizmos && gizmo?.reference != null)
            {
                //Draw into the tone-mapped intermediate image so the existing FXAA pass also
                //smooths the procedurally generated gizmo edges.
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
                GL.DrawBuffer(DrawBufferMode.ColorAttachment1);
                GL.Viewport(0, 0, gbuf.size[0], gbuf.size[1]);
                GL.Clear(ClearBufferMask.DepthBufferBit); //Clear depth
                GL.Enable(EnableCap.DepthTest);
                GL.Disable(EnableCap.CullFace);
                GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
                GLSLShaderConfig shader = resMgr.GLShaders[SHADER_TYPE.GIZMO_SHADER];
                GL.UseProgram(shader.program_id);


                Matrix4 gizmoWorld = gizmo.GetWorldMatrix(RenderState.activeCam, gbuf.size[1]);
                GL.UniformMatrix4(shader.uniformLocations["worldMat"], false, ref gizmoWorld);
                foreach (GizmoPart part in gizmo.Parts)
                {
                    if (!resMgr.GLPrimitiveMeshVaos.TryGetValue(part.meshName, out GLMeshVao mesh))
                        continue;
                    bool highlighted = part.type == gizmo.HoveredPart || part.type == gizmo.ActivePart;
                    GL.Uniform1(shader.uniformLocations["is_active"], highlighted ? 1.0f : 0.0f);
                    GL.BindVertexArray(mesh.vao.vao_id);
                    GL.DrawElements(PrimitiveType.Triangles, mesh.metaData.batchcount,
                                    mesh.metaData.indicesLength, IntPtr.Zero);
                }
                GL.BindVertexArray(0);

                GL.Enable(EnableCap.CullFace);

            }
        }

        //True when this frame's shadow map holds casters and the light pass should sample it
        private bool shadowsActive = false;

        //Draws the meshes that cast shadows (materials with CastShadow, and the ShadowOnly proxies) into the
        //key light's shadow map. The material shaders are reused: only the per frame mvp is swapped for the
        //light's, so skinning, instancing and alpha cutouts behave as in the main pass
        private void renderShadows()
        {
            shadowsActive = false;
            if (!RenderState.renderSettings.UseShadows || RenderState.renderSettings._useLighting <= 0.0f ||
                resMgr.GLlights.Count == 0 || RenderState.activeCam == null)
                return;

            GLLight key = resMgr.GLlights[0].strct;
            if (key.position.W < 1.0f) //Key light switched off
                return;

            //Light positions and the gbuffer live in the rotated scene space
            Camera cam = RenderState.activeCam;
            Vector3 center = (new Vector4(cam.sceneCenter, 1.0f) * RenderState.rotMat).Xyz;
            if (!shdwRenderer.update(key.position.Xyz, center, cam.sceneRadius))
                return;

            CommonPerFrameUniforms shadowFrame = cpfu;
            shadowFrame.mvp = shdwRenderer.lightViewProj;
            GL.BindBuffer(BufferTarget.UniformBuffer, UBOs["_COMMON_PER_FRAME_SHADOW"]);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, CommonPerFrameUniforms.SizeInBytes, ref shadowFrame);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, UBOs["_COMMON_PER_FRAME_SHADOW"]);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, shdwRenderer.depth_fbo);
            GL.Viewport(0, 0, ShadowRenderer.SHADOW_MAP_SIZE, ShadowRenderer.SHADOW_MAP_SIZE);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthMask(true);
            GL.Clear(ClearBufferMask.DepthBufferBit);
            GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
            //Shadow proxies are often open or single sided, so both faces cast
            GL.Disable(EnableCap.CullFace);
            //Slope scaled depth bias against shadow acne
            GL.Enable(EnableCap.PolygonOffsetFill);
            GL.PolygonOffset(2.0f, 4.0f);
            TextureBindCache.reset(); //Other passes bind textures directly

            int casters = 0;
            renderShadowCasters(RenderState.activeResMgr.activeGLDeferredLITShaders, ref casters);
            renderShadowCasters(RenderState.activeResMgr.activeGLDeferredUNLITShaders, ref casters);

            GL.Disable(EnableCap.PolygonOffsetFill);
            GL.Enable(EnableCap.CullFace);
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, UBOs["_COMMON_PER_FRAME"]);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.Viewport(0, 0, gbuf.size[0], gbuf.size[1]);

            shadowsActive = casters > 0;
        }

        private void renderShadowCasters(List<GLSLShaderConfig> shaderList, ref int casters)
        {
            foreach (GLSLShaderConfig shader in shaderList)
            {
                bool programBound = false;
                foreach (GLMeshVao m in resMgr.opaqueMeshShaderMap[shader.shaderHash])
                {
                    if (m.instance_count == 0 || m.UBO_aligned_size == 0 || m.material == null)
                        continue;
                    if (!m.material.CastShadow && !m.material.ShadowOnly)
                        continue;

                    if (!programBound)
                    {
                        GL.UseProgram(shader.program_id);
                        programBound = true;
                    }

                    bindPerMeshSSBOs(m);
                    m.render(shader, RENDERPASS.DEFERRED);
                    casters++;
                }
            }
        }

        //Rendering Mechanism
        public void render()
        {
            frameCpuClock.Restart();
            frameTimer.Begin();

            //Prepare UBOs
            prepareCommonPerFrameUBO();

            //Sort the scene lights into the view clusters, for the lighting passes. With lighting off the
            //shaders do not read them
            if (RenderState.renderSettings._useLighting > 0.0f)
                lightClusters.update(resMgr.GLlights, RenderState.activeCam, gbuf.size[0], gbuf.size[1]);
            
            //Sort Transparent Objects
            //sortTransparent(); //NOT NEEDED ANYMORE
            
            //Prepare Mesh UBO
            long phaseStart = System.Diagnostics.Stopwatch.GetTimestamp();
            prepareCommonPerMeshSSBOs();
            lastMeshUploadCpuMs = System.Diagnostics.Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds;

            //Render Shadows (needs this frame's per mesh data and bone matrices)
            renderShadows();

            //Render Geometry
            renderGeometry();

            //POST-PROCESSING
            post_process();

            renderParticleEffects();

            //Helpers on top of the tone mapped frame
            renderHelpers();

            //Final Pass
            phaseStart = System.Diagnostics.Stopwatch.GetTimestamp();
            renderFinalPass();
            lastFinalBlitCpuMs = System.Diagnostics.Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds;

            renderEngineText();

            //Fence after every draw that reads the per-mesh SSBO
            fenceActiveSSBO();

            frameTimer.End();
            frameCpuClock.Stop();
            lastRenderCpuMs = frameCpuClock.Elapsed.TotalMilliseconds;
            logFrameStats();
        }

        private void renderEngineText()
        {
            if (textRenderer == null || !(RenderState.renderViewSettings?.RenderInfo ?? false))
                return;

            if (viewportStatsText.Length == 0 || textStatsClock.ElapsedMilliseconds >= 250)
            {
                textStatsClock.Restart();
                float fps = RenderStats.fpsCount;
                viewportStatsText = $"FPS: {fps:0.0} ({(fps > 0.0f ? 1000.0f / fps : 0.0f):0.00} ms)\n" +
                    $"Occluded: {RenderStats.occludedNum}\n" +
                    $"Vertices: {RenderStats.vertNum:N0}\n" +
                    $"Triangles: {RenderStats.trisNum:N0}\n" +
                    $"Textures: {RenderStats.texturesNum}\n" +
                    $"Controller: {RenderState.activeGamepad?.getName() ?? "None"}";
            }

            textRenderer.Render(viewportStatsText, 8.0f, 8.0f, 14.0f,
                new Vector3(1.0f, 1.0f, 0.5f), screenWidth, screenHeight);
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

        private void pass_tex(int to_fbo, DrawBufferMode to_channel, int InTex)
        {
            //passthrough a texture to the specified to_channel of the to_fbo
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, to_fbo);
            GL.DrawBuffer(to_channel);

            GL.Disable(EnableCap.DepthTest); //Disable Depth test
            GL.Clear(ClearBufferMask.ColorBufferBit);
            GLSLShaderConfig shader = RenderState.activeResMgr.GLShaders[SHADER_TYPE.PASSTHROUGH_SHADER];
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
            pass_tex(pbuf.fbo, DrawBufferMode.ColorAttachment1, pbuf.color);

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

            //The gizmo is an editor overlay, but including it here lets the existing FXAA pass
            //anti-alias its silhouette without adding another full-screen render pass.
            renderUI();

            if (RenderState.renderSettings.UseFXAA)
                fxaa(); //FXAA (INCLUDING TONE/UNTONE)
        }

        //Frame and lighting stats in the log every few seconds
        private void logFrameStats()
        {
            if (frameStatsClock.ElapsedMilliseconds < 3000)
                return;
            frameStatsClock.Restart();
            lightClusters.refreshPostFilterStats();
            CallBacks.Log("[Lights] {0} in view ({1} global, {2} constant); spatial lists before spot cones: " +
                "{3:0.0} avg / {4} max retained, {5:0.0} avg / {6} max candidates, " +
                "after spot cones: {7:0.0} avg / {8} max; " +
                "{9}/{10} clusters overflowed on {11}x{12}x{13} grid, {14} assignments dropped; " +
                "cluster build {15:0.00} ms, light pass GPU {16:0.00} ms",
                lightClusters.activeLightCount, lightClusters.globalLightCount, lightClusters.constantLightCount,
                lightClusters.avgLightsPerCluster, lightClusters.maxLightsPerCluster,
                lightClusters.avgCandidateLightsPerCluster, lightClusters.maxCandidateLightsPerCluster,
                lightClusters.avgPostFilterLightsPerCluster, lightClusters.maxPostFilterLightsPerCluster,
                lightClusters.overflowClusterCount, lightClusters.clusterCount,
                lightClusters.tilesX, lightClusters.tilesY, LightClusters.DEPTH_SLICES,
                lightClusters.droppedLightAssignments, lightClusters.updateMs, lightPassTimer.Ms);
            CallBacks.Log("[Frame] render() CPU {0:0.00} ms, GPU {1:0.00} ms at {2}x{3}",
                frameCpuClock.Elapsed.TotalMilliseconds, frameTimer.Ms, gbuf.size[0], gbuf.size[1]);
            if (particleSystem != null && particleSystem.EmitterCount > 0)
                CallBacks.Log("[Particles] {0} live particles across {1} emitters",
                    particleSystem.ParticleCount, particleSystem.EmitterCount);
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

            Matrix4 shadowMat = shdwRenderer.lightViewProj;
            int shadowMatLoc = shader_conf.getLocation("shadowMat");
            if (shadowMatLoc >= 0)
                GL.ProgramUniformMatrix4(shader_conf.program_id, shadowMatLoc, false, ref shadowMat);

            render_quad(["shadowsEnabled", "shadowTexelSize"], [shadowsActive ? 1.0f : 0.0f, shdwRenderer.texelWorldSize],
                                                            ["albedoTex", "depthTex", "normalTex", "parameterTex", "shadowMap"],
                                                            [TextureTarget.Texture2D, TextureTarget.Texture2D,
                                                            TextureTarget.Texture2D, TextureTarget.Texture2D, TextureTarget.Texture2D],
                                                            [gbuf.albedo, gbuf.depth, gbuf.normals, gbuf.info, shdwRenderer.depth_tex_id], shader_conf);
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
                    pbuf.Dispose(); //Dispose post-process buffers
                    shdwRenderer.Dispose(); //Dispose shadowRenderer
                    lightClusters.Dispose(); //Dispose the light cluster buffers
                    frameTimer.Dispose();
                    lightPassTimer.Dispose();
                    textRenderer?.Dispose();
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
