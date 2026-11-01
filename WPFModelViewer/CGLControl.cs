using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows.Input;
using GLSLHelper;
//Custom Imports
using MVCore;
using MVCore.Common;
using MVCore.GMDL;
using MVCore.Utils;
using MVCore.Input;
using MVCore.Engine.Systems;
using OpenTK.Mathematics;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Input;
using MVCore.Engine;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.Common;
using System.Linq.Expressions;
using System.Threading.Tasks;
using libMBIN.NMS.GameComponents;
using OpenTK.Windowing.GraphicsLibraryFramework;
using NativeGLControl = OpenTK.GLControl.GLControl;

namespace Model_Viewer
{
    public class CGLControl
    {
        //Mouse Pos
        private MouseMovementStatus mouseMovementStatus = MouseMovementStatus.IDLE;

        private readonly NativeGLControl _nativeControl;
        private Thread nativeRenderThread;
        private volatile bool nativeStopRequested;
        private volatile bool nativeWindowActive = true;
        //Scene Stuff
        //public Model rootObject;
        public Queue<Model> modelUpdateQueue = new Queue<Model>();
        private readonly object modelUpdateLock = new object();
        private readonly HashSet<Model> modelUpdateRoots = new HashSet<Model>();
        private readonly HashSet<Scene> animatedSceneRoots = new HashSet<Scene>();

        //Render-only camera-culling index. LOD branches remain ordinary scene nodes; each candidate keeps
        //the branch choices inherited from its ancestors without retaining non-render topology nodes.
        private sealed class FlatLodGate
        {
            public Model group;
            public int requiredIndex;
            public FlatLodGate parent;
        }

        private sealed class FlatMeshGate
        {
            public Mesh mesh;
            public FlatMeshGate parent;
        }

        private sealed class FlatNamedLodGate
        {
            public Model branch;
            public FlatNamedLodGate parent;
        }

        private sealed class FlatRenderItem
        {
            public Model node;
            public FlatLodGate lodGates;
            public FlatNamedLodGate namedLodGates;
            public FlatMeshGate meshGates;

            public bool Hidden
            {
                get
                {
                    for (FlatLodGate gate = lodGates; gate != null; gate = gate.parent)
                        if (gate.group.activeLODModel != gate.requiredIndex)
                            return true;
                    for (FlatNamedLodGate gate = namedLodGates; gate != null; gate = gate.parent)
                        if (!gate.branch.isNamedLodBranchActive())
                            return true;
                    for (FlatMeshGate gate = meshGates; gate != null; gate = gate.parent)
                        if (!gate.mesh.active || !gate.mesh.renderable)
                            return true;
                    return false;
                }
            }
        }

        private readonly List<FlatRenderItem> flatRenderItems = new List<FlatRenderItem>();
        private readonly List<Model> flatLodGroups = new List<Model>();
        private readonly List<Scene> flatScenes = new List<Scene>();
        private readonly Dictionary<Scene, uint> flatSceneLodMasks = new Dictionary<Scene, uint>();
        private readonly Dictionary<Scene, int> loggedLodFallbacks = new Dictionary<Scene, int>();
        private readonly HashSet<GLMeshVao> flatIndexedMeshes = new HashSet<GLMeshVao>();
        private readonly HashSet<(Model parent, string key, uint geometryMask, uint lightMask)>
            loggedLodLightGaps = new HashSet<(Model parent, string key, uint geometryMask, uint lightMask)>();
        private Model loggedLodLightGapRoot;
        private int namedLodGroupCount;
        private int namedLodBranchCount;
        private const bool USE_FLAT_CAMERA_CULLING = true; //Set false for the original recursive camera path
        private Matrix4 lastViewMat; //Camera state the instances were last culled with
        private Matrix4 lastRotMat;
        private Vector3 lastCameraPosition;
        //Gizmo
        public TransformGizmo activeGizmo => RenderState.activeGizmo;

        //Rendering Engine
        public Engine engine;

        //Rendering Thread
        //Frame clock. dt is the last frame's length in milliseconds (animations, actions and shader time use ms)
        private int frames = 0;
        private double dt = 0.0;
        private readonly Stopwatch frameClock = new Stopwatch();
        private readonly Stopwatch fpsClock = new Stopwatch();
        //A longer frame (a file load, a pause, a breakpoint) counts as this long, so the camera and animations don't jump
        private const double MAX_FRAME_MS = 100.0;

        //Completed once the engine (resource manager, cameras, shaders) exists, which happens on the first render
        public readonly TaskCompletionSource ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        //Input Polling Thread
        private bool _capture_input = false;
        private bool padFramePressed = false; //Frame button state on the last frame
        
        public CGLControl(NativeGLControl baseControl)
        {
            _nativeControl = baseControl;
            RenderState.rotAngles.Y = 0;
            engine = new Engine();
            registerNativeFunctions();
        }

        private int viewportWidth => _nativeControl.ClientSize.Width;
        private int viewportHeight => _nativeControl.ClientSize.Height;

        public async void SetActiveModel(Model model)
        {
            await engine.InvokeOnRenderThreadAsync(() =>
            {
                if (RenderState.activeModel != null) RenderState.activeModel.selected = 0;
                RenderState.activeModel = model;
                if (model != null) model.selected = 1;
                TransformGizmo gizmo = RenderState.activeGizmo ??= new TransformGizmo();
                gizmo.SetReference(model);
                RenderState.instancesDirty = true;
            });
        }

        public async void SetGizmoMode(GIZMO_MODE mode)
        {
            await engine.InvokeOnRenderThreadAsync(() => activeGizmo?.SetMode(mode));
        }

        private void queueModelUpdate(Model model)
        {
            if (model == null)
                return;
            lock (modelUpdateLock)
                modelUpdateQueue.Enqueue(model);
        }

        private void initializeRenderer(int screenFramebuffer)
        {
            engine.init();
            RenderState.activeGizmo ??= new TransformGizmo();
            engine.renderMgr.screen_fbo = screenFramebuffer;
            RenderState.activeCam.settings = RenderState.camSettings.settings;
            RenderState.activeCam.Position =
                new Vector3(RenderState.camSettings.PosX, RenderState.camSettings.PosY, RenderState.camSettings.PosZ);
            RenderState.activeCam.yaw = RenderState.camSettings.Yaw;
            RenderState.activeCam.pitch = RenderState.camSettings.Pitch;

            //A small default scene makes ready mean the renderer is safe for UI access as well as initialized.
            Scene scene = new Scene { type = TYPES.MODEL, name = "DEFAULT SCENE" };
            scene.IsSceneNode = true;
            scene.SourceFile = "DEFAULT.SCENE.MBIN";
            RenderState.activeResMgr.GLScenes["DEFAULT_SCENE"] = scene;
            RenderState.rootObject = scene;
            queueModelUpdate(scene);
            engine.renderMgr.populate();

            ready.TrySetResult();
        }

        private void registerNativeFunctions()
        {
            _nativeControl.Load += (sender, args) =>
            {
                if (nativeRenderThread != null)
                    return;

                //NativeWindow creation leaves the context current on the WinForms UI thread.
                //Release it here before the dedicated renderer thread claims ownership.
                _nativeControl.Context?.MakeNoneCurrent();
                nativeRenderThread = new Thread(nativeRenderLoop)
                {
                    IsBackground = true,
                    Name = "NMSMV OpenGL Renderer"
                };
                nativeRenderThread.Start();
            };
            _nativeControl.SizeChanged += (sender, args) => resizeTimer.Restart();
            _nativeControl.MouseEnter += nativeMouseEnter;
            _nativeControl.MouseLeave += nativeMouseLeave;
            _nativeControl.MouseDown += nativeMouseDown;
            _nativeControl.MouseMove += nativeMouseMove;
            _nativeControl.MouseUp += nativeMouseUp;
            _nativeControl.MouseWheel += nativeMouseWheel;
            _nativeControl.KeyDown += nativeKeyDown;
            _nativeControl.KeyUp += nativeKeyUp;
            _nativeControl.LostFocus += (sender, args) => engine.kbHandler.Clear();
            _nativeControl.MouseCaptureChanged += (sender, args) =>
            {
                if (!_nativeControl.Capture && mouseMovementStatus != MouseMovementStatus.IDLE)
                {
                    if (mouseMovementStatus == MouseMovementStatus.GIZMO_MOVEMENT)
                    {
                        _ = engine.InvokeOnRenderThreadAsync(() => activeGizmo?.EndDrag());
                        RenderState.instancesDirty = true;
                    }
                    mouseMovementStatus = MouseMovementStatus.IDLE;
                }
            };
        }

        private void nativeRenderLoop()
        {
            bool rendererInitialized = false;
            try
            {
                _nativeControl.MakeCurrent();
                if (_nativeControl.Context != null)
                    _nativeControl.Context.SwapInterval = 1;
                initializeRenderer(0);
                rendererInitialized = true;

                while (!nativeStopRequested)
                {
                    RenderLocal();
                    _nativeControl.SwapBuffers();
                }
            }
            catch (Exception ex)
            {
                CallBacks.Log("Native renderer failed: " + ex);
                ready.TrySetException(ex);
            }
            finally
            {
                if (rendererInitialized)
                {
                    try
                    {
                        engine.Dispose();
                    }
                    catch (Exception ex)
                    {
                        CallBacks.Log("GL cleanup on exit failed: " + ex);
                    }
                }
            }
        }

        private void applyPendingResize()
        {
            //The control can recreate its framebuffer when it resizes, so fetch it every frame
            int w = viewportWidth;
            int h = viewportHeight;
            engine.renderMgr.screen_fbo = 0;
            engine.renderMgr.setScreenSize(w, h);

            if (w <= 0 || h <= 0 || engine.renderMgr.hasRenderSize(w, h))
                return; //Minimized or already the right size
            if (resizeTimer.IsRunning && resizeTimer.ElapsedMilliseconds < RESIZE_SETTLE_MS)
                return; //Still resizing

            resizeTimer.Reset();
            engine.renderMgr.resize(w, h);
        }

        //Frame loop stats for the log: time between frames and time spent in frameUpdate, every few seconds
        private readonly Stopwatch loopStatsClock = Stopwatch.StartNew();
        private readonly Stopwatch updateClock = new Stopwatch();
        private readonly Stopwatch callbackWorkClock = new Stopwatch();
        private double previousCallbackWorkMs = 0.0;
        private int loopFrames = 0;
        private double loopIntervalSum = 0.0, loopIntervalMax = 0.0, loopUpdateSum = 0.0, loopUpdateMax = 0.0;
        private double loopCallbackSum = 0.0, loopCallbackMax = 0.0, loopOutsideSum = 0.0, loopOutsideMax = 0.0;

        private int lastGc0, lastGc1, lastGc2;

        //Time of the parts of frameUpdate in the last frame: actions/animations/audio, transform updates,
        //camera, and culling + instancing (only when something changed)
        private double phaseSystemsMs, phaseQueueMs, phaseCameraMs, phaseInstancesMs, phaseMeshesMs, phaseLightsMs, phaseJointsMs;
        private double loopMeshesSum = 0.0, loopLightsSum = 0.0, loopJointsSum = 0.0, loopInstancesRebuiltSum = 0.0;
        private bool lastInstancesRebuilt;
        private bool lastFlatCameraCull;
        private int loopRebuilds = 0;
        private int loopFlatCameraCulls = 0;
        private double loopSystemsSum = 0.0, loopQueueSum = 0.0, loopInstancesSum = 0.0;

        private int lastLodFallbackScenes = 0;
        private int lastLoggedForcedLod = int.MinValue;

        private void logLoopStats(double intervalMs, double updateMs, double callbackWorkMs, double outsideCallbackMs)
        {
            //A frame that took long gets its own line with what the frame did, and the garbage collections
            //since the last frame. Other costs (WPF, driver, waiting for vsync) show up as the rest
            int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
            if (intervalMs > 20.0)
            {
                CallBacks.Log("[Spike] frame interval {0:0.0} ms: outside callback {13:0.0}, callback work {14:0.0}; frameUpdate {1:0.0} (systems {9:0.0}, transforms {10:0.0}, culling+instancing {11:0.0}, instances rebuilt {12}), render CPU {2:0.0} (mesh upload {15:0.0}, final blit {16:0.0}, cluster build {3:0.0}, rebuilt {4}), render GPU {5:0.0}, GC gen0/1/2 +{6}/+{7}/+{8}",
                    intervalMs, updateMs, engine.renderMgr.lastRenderCpuMs, engine.renderMgr.lightClusters.updateMs,
                    engine.renderMgr.lightClusters.rebuilt, engine.renderMgr.lastFrameGpuMs,
                    gc0 - lastGc0, gc1 - lastGc1, gc2 - lastGc2,
                    phaseSystemsMs, phaseQueueMs, phaseInstancesMs, lastInstancesRebuilt,
                    outsideCallbackMs, callbackWorkMs, engine.renderMgr.lastMeshUploadCpuMs,
                    engine.renderMgr.lastFinalBlitCpuMs);
            }
            lastGc0 = gc0;
            lastGc1 = gc1;
            lastGc2 = gc2;

            loopFrames++;
            if (lastInstancesRebuilt)
                loopRebuilds++;
            if (lastFlatCameraCull)
                loopFlatCameraCulls++;
            loopSystemsSum += phaseSystemsMs;
            loopQueueSum += phaseQueueMs;
            loopInstancesSum += phaseInstancesMs;
            if (lastInstancesRebuilt)
            {
                loopMeshesSum += phaseMeshesMs;
                loopLightsSum += phaseLightsMs;
                loopJointsSum += phaseJointsMs;
                loopInstancesRebuiltSum += phaseInstancesMs;
            }
            loopIntervalSum += intervalMs;
            loopIntervalMax = Math.Max(loopIntervalMax, intervalMs);
            loopUpdateSum += updateMs;
            loopUpdateMax = Math.Max(loopUpdateMax, updateMs);
            loopCallbackSum += callbackWorkMs;
            loopCallbackMax = Math.Max(loopCallbackMax, callbackWorkMs);
            loopOutsideSum += outsideCallbackMs;
            loopOutsideMax = Math.Max(loopOutsideMax, outsideCallbackMs);

            if (loopStatsClock.ElapsedMilliseconds < 3000)
                return;
            CallBacks.Log("[Loop] {0} frames, interval avg {1:0.00} ms / max {2:0.00} ms, frameUpdate avg {3:0.00} ms / max {4:0.00} ms (systems {5:0.00}, transforms {6:0.00}, culling+instancing {7:0.00}; instances rebuilt in {8} frames, flat camera culls {9})",
                loopFrames, loopIntervalSum / loopFrames, loopIntervalMax, loopUpdateSum / loopFrames, loopUpdateMax,
                loopSystemsSum / loopFrames, loopQueueSum / loopFrames, loopInstancesSum / loopFrames, loopRebuilds, loopFlatCameraCulls);
            CallBacks.Log("[Callback] work avg {0:0.00} ms / max {1:0.00} ms, outside callback avg {2:0.00} ms / max {3:0.00} ms; last render mesh upload {4:0.00} ms, final blit {5:0.00} ms",
                loopCallbackSum / loopFrames, loopCallbackMax, loopOutsideSum / loopFrames, loopOutsideMax,
                engine.renderMgr.lastMeshUploadCpuMs, engine.renderMgr.lastFinalBlitCpuMs);
            double perRebuild = loopRebuilds > 0 ? 1.0 / loopRebuilds : 0.0;
            CallBacks.Log("[Instances] per rebuild: {0:0.00} ms in {1} meshes, {2:0.00} ms in {3} lights ({4} uploads), {5:0.00} ms in {6} joints ({7} uploads), total {8:0.00} ms (rest is the tree walk, LOD and locators)",
                loopMeshesSum * perRebuild, RenderStats.meshCount,
                loopLightsSum * perRebuild, RenderStats.lightCount, RenderStats.lightUploads,
                loopJointsSum * perRebuild, RenderStats.jointCount, RenderStats.jointUploads,
                loopInstancesRebuiltSum * perRebuild);
            CallBacks.Log("[Culling] frustum culling {0}, LOD filtering {1}: {2} meshes tested, {3} frustum culled, {4} LOD filtered, {5} scenes used a missing-level fallback in the last rebuild",
                RenderState.renderSettings.UseFrustumCulling ? "ON" : "OFF", RenderState.renderSettings.LODFiltering ? "ON" : "OFF",
                RenderStats.meshCount, RenderStats.occludedNum, RenderStats.lodFilteredNum, lastLodFallbackScenes);
            CallBacks.Log("[InstanceData] {0} stable records copied, {1} visible IDs uploaded in the last frame",
                engine.renderMgr.lastStableRecordsUploaded, engine.renderMgr.lastVisibleIdsUploaded);
            loopStatsClock.Restart();
            loopFrames = 0;
            loopRebuilds = 0;
            loopFlatCameraCulls = 0;
            loopMeshesSum = loopLightsSum = loopJointsSum = loopInstancesRebuiltSum = 0.0;
            loopSystemsSum = loopQueueSum = loopInstancesSum = 0.0;
            loopIntervalSum = loopIntervalMax = loopUpdateSum = loopUpdateMax = 0.0;
            loopCallbackSum = loopCallbackMax = loopOutsideSum = loopOutsideMax = 0.0;
        }

        private void RenderLocal()
        {
            //One clock for everything that moves: the camera gets seconds, the rest milliseconds
            double interval = frameClock.IsRunning ? frameClock.Elapsed.TotalMilliseconds : 0.0;
            double outsideCallback = Math.Max(0.0, interval - previousCallbackWorkMs);
            dt = Math.Min(interval, MAX_FRAME_MS);
            frameClock.Restart();
            callbackWorkClock.Restart();

            if (engine.rt_State != EngineRenderingState.EXIT)
            {
                engine.executeRenderCommands();

                if (engine.rt_State == EngineRenderingState.ACTIVE)
                {
                    applyPendingResize();
                    processInput(dt / 1000.0);
                    updateClock.Restart();
                    frameUpdate();
                    updateClock.Stop();
                    engine.renderMgr.render(); //Render Everything
                    callbackWorkClock.Stop();
                    previousCallbackWorkMs = callbackWorkClock.Elapsed.TotalMilliseconds;
                    logLoopStats(interval, updateClock.Elapsed.TotalMilliseconds,
                        previousCallbackWorkMs, outsideCallback);

                    GL.ClearColor(1.0f, 1.0f, 1.0f, 1.0f);
                }
            }

            if (callbackWorkClock.IsRunning)
            {
                callbackWorkClock.Stop();
                previousCallbackWorkMs = callbackWorkClock.Elapsed.TotalMilliseconds;
            }

            //GL.ClearColor(1.0f, 0.0f, 0.0f, 1.0f);
            //GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            

        }

        //Stop the engine, then free the GL resources with this control's context current.
        //Called from the window's Closing handler, before the control and its context go away
        public void shutdown()
        {
            nativeStopRequested = true;
            if (nativeRenderThread != null && !nativeRenderThread.Join(5000))
                CallBacks.Log("Native renderer did not stop within five seconds");
        }

        public void setWindowActive(bool active)
        {
            nativeWindowActive = active;
            if (!active)
                engine?.kbHandler?.Clear();
        }

        public void setActiveCam(int index)
        {
            if (index < 0 || index >= RenderState.activeResMgr.GLCameras.Count)
                return;
            if (RenderState.activeCam != null)
                RenderState.activeCam.isActive = false;
            RenderState.activeCam = RenderState.activeResMgr.GLCameras[index];
            RenderState.activeCam.isActive = true;
            CallBacks.Log("Switching Camera to {0}", index);
        }

#region GLControl Methods
        //dt in seconds
        private void processInput(double dt)
        {
            float x = 0.0f, y = 0.0f, z = 0.0f, rot = 0.0f;
            //The keyboard only drives the camera while the mouse is over the viewport
            if (_capture_input)
            {
                x = engine.kbHandler.getKeyStatus(RenderState.settings.KeyRight) - engine.kbHandler.getKeyStatus(RenderState.settings.KeyLeft);
                z = engine.kbHandler.getKeyStatus(RenderState.settings.KeyUp) - engine.kbHandler.getKeyStatus(RenderState.settings.KeyDown);
                y = engine.kbHandler.getKeyStatus(Key.R) - engine.kbHandler.getKeyStatus(Key.F);
                rot = engine.kbHandler.getKeyStatus(Key.E) - engine.kbHandler.getKeyStatus(Key.Q);
            }

            //The pad works whenever the window is active, wherever the mouse is
            BaseGamepadHandler pad = engine.gpHandler;
            if (pad != null && nativeWindowActive)
            {
                pad.updateState();
                if (pad.isConnected())
                    processGamepad(pad, dt, ref x, ref y, ref z, ref rot);
            }

            x = Math.Clamp(x, -1.0f, 1.0f);
            y = Math.Clamp(y, -1.0f, 1.0f);
            z = Math.Clamp(z, -1.0f, 1.0f);
            rot = Math.Clamp(rot, -1.0f, 1.0f);

            Vector3 newPos = RenderState.activeCam.Right * x + RenderState.activeCam.Front * z + Camera.BaseUp * y;
            if (newPos.LengthSquared > 1.0f)
                newPos.Normalize(); //Diagonals are not faster
            newPos *= (float) dt * RenderState.activeCam.settings.Speed * RenderState.activeCam.speedScale;
            newPos += RenderState.activeCam.Position;

            RenderState.activeCam.Position = newPos;
            //Scene rotation in degrees per second, so it doesn't depend on the frame rate
            RenderState.rotAngles.Y += 30.0f * (float) dt * rot;
            RenderState.rotAngles.Y %= 360;
        }

        //Adds the pad's movement to the keyboard's and turns the camera. Partial stick tilt moves slower
        private void processGamepad(BaseGamepadHandler pad, double dt, ref float x, ref float y, ref float z, ref float rot)
        {
            x += pad.getAction(ControllerActions.MOVE_X);
            z += pad.getAction(ControllerActions.MOVE_Z);
            y += pad.getAction(ControllerActions.MOVE_Y_POS) - pad.getAction(ControllerActions.MOVE_Y_NEG);
            rot += pad.getAction(ControllerActions.ROTATE_SCENE_POS) - pad.getAction(ControllerActions.ROTATE_SCENE_NEG);

            //Turn rate in degrees per second at full tilt (120 at the default sensitivity), same angle limits as the mouse
            Camera cam = RenderState.activeCam;
            float look = 150.0f * cam.settings.Sensitivity * (float) dt;
            cam.pitch = (cam.pitch + pad.getAction(ControllerActions.CAMERA_MOVE_H) * look) % 360.0f;
            cam.yaw = Math.Clamp(cam.yaw + pad.getAction(ControllerActions.CAMERA_MOVE_V) * look, -89.0f, 89.0f);

            //Frame once per press, not on every frame the button is held
            bool frame = pad.getAction(ControllerActions.FRAME_SCENE) > 0.5f;
            if (frame && !padFramePressed && RenderState.rootObject != null)
                cam.frameScene(RenderState.rootObject, true);
            padFramePressed = frame;
        }

        private void setKeyState(Key key, bool pressed)
        {
            if (!_capture_input)
                return;
            engine.kbHandler.SetKeyState(key, pressed);
        }

        private void handleKeyDown(Key key)
        {
            if (!_capture_input)
                return;

            engine.kbHandler.SetKeyState(key, true);

            //Debug.WriteLine("Key pressed {0}",e.Key.ToString());
            switch (key)
            {
                //Frame the model
                case Key.Home:
                    if (RenderState.rootObject != null)
                        RenderState.activeCam.frameScene(RenderState.rootObject, true);
                    break;
                /*
                //Toggle Wireframe
                case Keys.I:
                    if (RenderOptions.RENDERMODE == PolygonMode.Fill)
                        RenderOptions.RENDERMODE = PolygonMode.Line;
                    else
                        RenderOptions.RENDERMODE = PolygonMode.Fill;
                    break;
                //Toggle Texture Render
                case Keys.O:
                    RenderOptions.UseTextures = 1.0f - RenderOptions.UseTextures;
                    break;
                //Toggle Collisions Render
                case Keys.OemOpenBrackets:
                    RenderOptions.RenderCollisions = !RenderOptions.RenderCollisions;
                    break;
                //Toggle Debug Render
                case Keys.OemCloseBrackets:
                    RenderOptions.RenderDebug = !RenderOptions.RenderDebug;
                    break;
                */
                //Interact with what's near the camera, like the player's interact key
                case Key.I:
                    RenderState.renderViewSettings.EmulateActions = true; //Emulation starts on the next frame, then the interact runs
                    engine.actionSys.RequestPlayerInteract();
                    break;
                //Switch cameras
                case Key.NumPad0:
                    //Cycle through the cameras, when there is more than one
                    if (engine.resMgr.GLCameras.Count > 1)
                        setActiveCam((engine.resMgr.GLCameras.IndexOf(RenderState.activeCam) + 1) % engine.resMgr.GLCameras.Count);
                    break;
                //Animation playback (Play/Pause Mode) with Space
                //case Keys.Space:
                //    toggleAnimation();
                //    break;
                default:
                    //Common.CallBacks.Log("Not Implemented Yet");
                    break;
            }

        }

        private void nativeMouseEnter(object sender, EventArgs e)
        {
            _capture_input = true;
            engine.kbHandler.Clear();
            engine.msHandler.Clear();
            System.Drawing.Point p = _nativeControl.PointToClient(System.Windows.Forms.Cursor.Position);
            engine.msHandler.Position = engine.msHandler.PrevPosition = new Vector2(p.X, p.Y);
            engine.msHandler.Delta = Vector2.Zero;
            _nativeControl.Focus();
        }

        private void nativeMouseLeave(object sender, EventArgs e)
        {
            if (!_nativeControl.Capture)
                _capture_input = false;
        }

        private void nativeMouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (mouseMovementStatus != MouseMovementStatus.CAMERA_MOVEMENT)
                return;
            RenderState.activeCam.settings.Speed *= (float)Math.Pow(1.25, e.Delta / 120.0);
        }

        private async void nativeMouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            _nativeControl.Focus();
            engine.msHandler.Position = new Vector2(e.X, e.Y);
            if (!_capture_input)
                return;

            bool left = (System.Windows.Forms.Control.MouseButtons & System.Windows.Forms.MouseButtons.Left) != 0;
            bool right = (System.Windows.Forms.Control.MouseButtons & System.Windows.Forms.MouseButtons.Right) != 0;
            engine.msHandler.SetButtonState(System.Windows.Input.MouseButton.Left, left);
            engine.msHandler.SetButtonState(System.Windows.Input.MouseButton.Right, right);

            Vector2 mouse = new Vector2(e.X, e.Y);
            int width = viewportWidth, height = viewportHeight;
            bool dragging = false;
            await engine.InvokeOnRenderThreadAsync(() =>
            {
                activeGizmo?.UpdateHover(mouse, width, height, RenderState.activeCam);
                dragging = left && activeGizmo?.BeginDrag(mouse, width, height, RenderState.activeCam) == true;
            });
            if ((System.Windows.Forms.Control.MouseButtons & System.Windows.Forms.MouseButtons.Left) == 0)
            {
                await engine.InvokeOnRenderThreadAsync(() => activeGizmo?.EndDrag());
                return;
            }
            if (dragging)
            {
                CallBacks.Log("Engaging gizmo movement");
                mouseMovementStatus = MouseMovementStatus.GIZMO_MOVEMENT;
                _nativeControl.Capture = true;
            }
            else if (left)
            {
                mouseMovementStatus = MouseMovementStatus.CAMERA_MOVEMENT;
                _nativeControl.Capture = true;
            }
        }

        private void nativeMouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            Vector2 current = new Vector2(e.X, e.Y);
            engine.msHandler.PrevPosition = engine.msHandler.Position;
            engine.msHandler.Position = current;
            engine.msHandler.Delta = current - engine.msHandler.PrevPosition;

            bool left = (e.Button & System.Windows.Forms.MouseButtons.Left) != 0;
            int width = viewportWidth, height = viewportHeight;
            if (mouseMovementStatus == MouseMovementStatus.IDLE)
                _ = engine.InvokeOnRenderThreadAsync(() =>
                    activeGizmo?.UpdateHover(current, width, height, RenderState.activeCam));
            else if (mouseMovementStatus == MouseMovementStatus.GIZMO_MOVEMENT && left)
                _ = engine.InvokeOnRenderThreadAsync(() =>
                {
                    if (activeGizmo?.Drag(current) == true)
                        queueModelUpdate(activeGizmo.reference);
                });
            if (mouseMovementStatus == MouseMovementStatus.CAMERA_MOVEMENT && left)
            {
                Camera cam = RenderState.activeCam;
                cam.pitch = (cam.pitch + engine.msHandler.Delta.X * cam.settings.Sensitivity) % 360.0f;
                cam.yaw = Math.Clamp(cam.yaw - engine.msHandler.Delta.Y * cam.settings.Sensitivity, -89.0f, 89.0f);
            }
        }

        private void nativeMouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            bool left = (System.Windows.Forms.Control.MouseButtons & System.Windows.Forms.MouseButtons.Left) != 0;
            bool right = (System.Windows.Forms.Control.MouseButtons & System.Windows.Forms.MouseButtons.Right) != 0;
            engine.msHandler.SetButtonState(System.Windows.Input.MouseButton.Left, left);
            engine.msHandler.SetButtonState(System.Windows.Input.MouseButton.Right, right);

            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                if (mouseMovementStatus == MouseMovementStatus.GIZMO_MOVEMENT)
                {
                    _ = engine.InvokeOnRenderThreadAsync(() => activeGizmo?.EndDrag());
                    RenderState.instancesDirty = true;
                }
                mouseMovementStatus = MouseMovementStatus.IDLE;
                _nativeControl.Capture = false;
                _capture_input = _nativeControl.ClientRectangle.Contains(_nativeControl.PointToClient(System.Windows.Forms.Cursor.Position));
            }
        }

        private void nativeKeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            handleKeyDown(KeyInterop.KeyFromVirtualKey(e.KeyValue));
            e.Handled = true;
        }

        private void nativeKeyUp(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            setKeyState(KeyInterop.KeyFromVirtualKey(e.KeyValue), false);
            e.Handled = true;
        }

        //Reallocating every render target on each step of a window drag makes resizing stutter, so the
        //targets are resized once the size has stopped changing. Until then the old ones are stretched
        private readonly Stopwatch resizeTimer = new Stopwatch();
        private const int RESIZE_SETTLE_MS = 100;

#endregion GLControl Methods

        public async Task addTestSceneAsync(int sceneID)
        {
            lock (modelUpdateLock)
                modelUpdateQueue.Clear();

#if DEBUG
            await engine.LoadTestSceneAsync(sceneID);
            SetActiveModel(null);
#else
            await Task.FromException(new NotSupportedException("Test scenes are only available in Debug builds."));
#endif
        }

        public async Task addSceneAsync(string filename)
        {
            lock (modelUpdateLock)
                modelUpdateQueue.Clear();

            await engine.LoadSceneAsync(filename);
            SetActiveModel(null);
        }

        private void rebuildFlatRenderIndex()
        {
            flatRenderItems.Clear();
            flatLodGroups.Clear();
            flatScenes.Clear();
            flatSceneLodMasks.Clear();
            loggedLodFallbacks.Clear();
            flatIndexedMeshes.Clear();
            engine.renderMgr.resetInstanceRegistries();

            if (RenderState.rootObject != null)
            {
                indexNamedLodBranches(RenderState.rootObject, null);
                addFlatRenderItem(RenderState.rootObject, null, null, null);
                configureLodLightAttenuation();
            }

            Dictionary<(Scene scene, string key), uint> objectLodMasks =
                new Dictionary<(Scene scene, string key), uint>();
            int persistentMeshes = 0;
            foreach (FlatRenderItem item in flatRenderItems)
            {
                if (item.node is not Mesh mesh)
                    continue;
                if (!mesh.hasLOD || mesh.parentScene == null || string.IsNullOrEmpty(mesh.lodGroupKey) ||
                    mesh.LodLevel < 0 || mesh.LodLevel >= 32)
                {
                    mesh.availableLODMask = 0;
                    persistentMeshes++;
                    continue;
                }

                var group = (mesh.parentScene, mesh.lodGroupKey);
                objectLodMasks.TryGetValue(group, out uint mask);
                objectLodMasks[group] = mask | (1u << mesh.LodLevel);
            }

            int singleLevelGroups = 0;
            foreach (uint mask in objectLodMasks.Values)
                if (mask != 0 && (mask & (mask - 1)) == 0)
                    singleLevelGroups++;
            foreach (FlatRenderItem item in flatRenderItems)
            {
                if (item.node is Mesh mesh && mesh.hasLOD && mesh.parentScene != null &&
                    !string.IsNullOrEmpty(mesh.lodGroupKey) &&
                    objectLodMasks.TryGetValue((mesh.parentScene, mesh.lodGroupKey), out uint mask))
                    mesh.availableLODMask = mask;
            }

            int meshes = 0, collisions = 0, joints = 0, lights = 0, locators = 0, namedLodLights = 0;
            foreach (FlatRenderItem item in flatRenderItems)
            {
                if (item.node is Mesh) meshes++;
                else if (item.node is Collision) collisions++;
                else if (item.node is Joint) joints++;
                else if (item.node is Light)
                {
                    lights++;
                    if (item.namedLodGates != null)
                        namedLodLights++;
                }
                else locators++;
            }

            int missingLod0 = 0, levelsBeyondDeclaredCount = 0;
            foreach (KeyValuePair<Scene, uint> pair in flatSceneLodMasks)
            {
                if ((pair.Value & 1u) == 0)
                    missingLod0++;
                int declaredCount = Math.Max(pair.Key._LODNum, 1);
                if (declaredCount < 32 && (pair.Value >> declaredCount) != 0)
                    levelsBeyondDeclaredCount++;
            }

            CallBacks.Log("[CameraCull] indexed {0} render candidates ({1} meshes, {2} collisions, {3} joints, {4} lights, {5} locators), {6} branch LOD groups and {7} scenes",
                flatRenderItems.Count, meshes, collisions, joints, lights, locators,
                flatLodGroups.Count, flatScenes.Count);
            CallBacks.Log("[LODIndex] {0} mesh-owning scenes have no LOD0; {1} contain mesh levels beyond their declared NUMLODS",
                missingLod0, levelsBeyondDeclaredCount);
            CallBacks.Log("[LODObjects] {0} persistent meshes, {1} logical LOD groups ({2} single-level groups use closest-level fallback)",
                persistentMeshes, objectLodMasks.Count, singleLevelGroups);
            CallBacks.Log("[LODContainers] {0} named container groups, {1} branches; {2} lights inherit their visibility",
                namedLodGroupCount, namedLodBranchCount, namedLodLights);
        }

        private static bool tryParseNamedLod(string name, out string groupKey, out int level)
        {
            groupKey = null;
            level = -1;
            if (string.IsNullOrEmpty(name))
                return false;

            int marker = name.LastIndexOf("LOD", StringComparison.OrdinalIgnoreCase);
            int digitStart = marker + 3;
            if (marker < 0 || digitStart >= name.Length || !char.IsDigit(name[digitStart]))
                return false;

            int digitEnd = digitStart;
            while (digitEnd < name.Length && char.IsDigit(name[digitEnd]))
                digitEnd++;
            if (!int.TryParse(name.Substring(digitStart, digitEnd - digitStart), out level) || level >= 32)
                return false;

            groupKey = (name.Substring(0, digitStart) + "#" + name.Substring(digitEnd)).ToUpperInvariant();
            return true;
        }

        private void indexNamedLodBranches(Model node, Scene inheritedScene)
        {
            Scene scene = inheritedScene;
            if (node is Scene nodeScene && (scene == null || nodeScene._LODNum > 1))
                scene = nodeScene;

            if (node == RenderState.rootObject)
            {
                namedLodGroupCount = 0;
                namedLodBranchCount = 0;
                node.namedLodLevel = -1;
                node.namedLodAvailableMask = 0;
                node.namedLodLightAvailableMask = 0;
                node.namedLodScene = null;
                node.namedLodGroupKey = null;
            }

            Dictionary<string, List<(Model node, int level)>> groups =
                new Dictionary<string, List<(Model node, int level)>>(StringComparer.OrdinalIgnoreCase);
            foreach (Model child in node.Children)
            {
                child.namedLodLevel = -1;
                child.namedLodAvailableMask = 0;
                child.namedLodLightAvailableMask = 0;
                child.namedLodScene = null;
                child.namedLodGroupKey = null;
                if (!tryParseNamedLod(child.name, out string key, out int level))
                    continue;
                if (!groups.TryGetValue(key, out List<(Model node, int level)> branches))
                {
                    branches = new List<(Model node, int level)>();
                    groups.Add(key, branches);
                }
                branches.Add((child, level));
            }

            foreach (List<(Model node, int level)> branches in groups.Values)
            {
                uint mask = 0;
                foreach ((Model branch, int level) in branches)
                    mask |= 1u << level;
                foreach ((Model branch, int level) in branches)
                {
                    branch.namedLodLevel = level;
                    branch.namedLodAvailableMask = mask;
                    branch.namedLodScene = scene;
                    branch.namedLodGroupKey = tryParseNamedLod(branch.name, out string key, out _) ? key : null;
                }
                namedLodGroupCount++;
                namedLodBranchCount += branches.Count;
            }

            foreach (Model child in node.Children)
                indexNamedLodBranches(child, scene);
        }

        private static float lightEnergy(Light light)
        {
            float luminance = 0.2126f * Math.Max(light.Color.X, 0.0f) +
                              0.7152f * Math.Max(light.Color.Y, 0.0f) +
                              0.0722f * Math.Max(light.Color.Z, 0.0f);
            return luminance * Math.Max(light.intensity, 0.0f);
        }

        private static string formatLodMask(uint mask)
        {
            List<string> levels = new List<string>();
            for (int level = 0; level < 32; level++)
                if ((mask & (1u << level)) != 0)
                    levels.Add("LOD" + level);
            return levels.Count == 0 ? "[]" : "[" + string.Join(",", levels) + "]";
        }

        private static string namedLodGroupPath(Model parent, string key)
        {
            Stack<string> parts = new Stack<string>();
            for (Model node = parent; node != null; node = node.parent)
            {
                string part = node.name ?? "";
                if (!string.IsNullOrEmpty(part))
                    parts.Push(part);
            }
            string parentPath = string.Join("/", parts);
            return string.IsNullOrEmpty(parentPath) ? key : parentPath + "/" + key;
        }

        private void configureLodLightAttenuation()
        {
            if (!ReferenceEquals(loggedLodLightGapRoot, RenderState.rootObject))
            {
                loggedLodLightGaps.Clear();
                loggedLodLightGapRoot = RenderState.rootObject;
            }

            Dictionary<(Model parent, string key), Dictionary<int, float>> energies =
                new Dictionary<(Model parent, string key), Dictionary<int, float>>();
            Dictionary<(Model parent, string key), uint> lightMasks =
                new Dictionary<(Model parent, string key), uint>();
            HashSet<Model> namedBranches = new HashSet<Model>();
            List<(Light light, Model branch)> assignments = new List<(Light light, Model branch)>();

            //Build a separate availability mask for lights. A branch can have valid LOD3 geometry but no
            //LOD3 light; the preceding light level then reaches zero at that geometry transition.
            foreach (FlatRenderItem item in flatRenderItems)
            {
                for (FlatNamedLodGate gate = item.namedLodGates; gate != null; gate = gate.parent)
                {
                    Model branch = gate.branch;
                    namedBranches.Add(branch);
                    branch.namedLodLightAvailableMask = 0;
                    if (item.node is not Light || branch.parent == null ||
                        string.IsNullOrEmpty(branch.namedLodGroupKey))
                        continue;

                    var groupId = (branch.parent, branch.namedLodGroupKey);
                    lightMasks.TryGetValue(groupId, out uint mask);
                    lightMasks[groupId] = mask | (1u << branch.namedLodLevel);
                }
            }
            foreach (Model branch in namedBranches)
            {
                if (branch.parent != null && !string.IsNullOrEmpty(branch.namedLodGroupKey) &&
                    lightMasks.TryGetValue((branch.parent, branch.namedLodGroupKey), out uint mask))
                    branch.namedLodLightAvailableMask = mask;
            }

            foreach (FlatRenderItem item in flatRenderItems)
            {
                if (item.node is not Light light)
                    continue;

                light.lodAttenuationControlled = false;
                light.lodEntryAttenuationScale = 1.0f;
                light.lodExitAttenuationScale = 1.0f;
                light.lodAttenuationLevel = -1;
                light.lodAttenuationScene = null;

                //The head is the nearest named LOD branch. Outer gates still control visibility, but must
                //not apply the compensation a second time to nested light containers.
                Model branch = item.namedLodGates?.branch;
                if (branch == null || branch.parent == null || string.IsNullOrEmpty(branch.namedLodGroupKey))
                    continue;

                var groupId = (branch.parent, branch.namedLodGroupKey);
                if (!energies.TryGetValue(groupId, out Dictionary<int, float> levels))
                {
                    levels = new Dictionary<int, float>();
                    energies.Add(groupId, levels);
                }
                levels.TryGetValue(branch.namedLodLevel, out float energy);
                levels[branch.namedLodLevel] = energy + lightEnergy(light);
                assignments.Add((light, branch));
            }

            int compensatedLights = 0;
            int compensatedGroups = 0;
            int fadeOutLights = 0;
            int fadeOutGroups = 0;
            float minGain = float.MaxValue, maxGain = float.MinValue;
            HashSet<(Model parent, string key)> changedGroups = new HashSet<(Model parent, string key)>();
            HashSet<(Model parent, string key)> fadingGroups = new HashSet<(Model parent, string key)>();
            foreach ((Light light, Model branch) in assignments)
            {
                var groupId = (branch.parent, branch.namedLodGroupKey);
                Dictionary<int, float> levels = energies[groupId];
                int previousLevel = -1;
                foreach (int level in levels.Keys)
                    if (level < branch.namedLodLevel && level > previousLevel)
                        previousLevel = level;

                float gain = 1.0f;
                if (previousLevel >= 0 && levels.TryGetValue(previousLevel, out float previousEnergy) &&
                    levels.TryGetValue(branch.namedLodLevel, out float currentEnergy) && currentEnergy > 1e-6f)
                    gain = Math.Clamp(previousEnergy / currentEnergy, 0.125f, 8.0f);

                float exitGain = 1.0f;
                int nextGeometryLevel = -1;
                for (int level = branch.namedLodLevel + 1; level < 32; level++)
                    if ((branch.namedLodAvailableMask & (1u << level)) != 0)
                    {
                        nextGeometryLevel = level;
                        break;
                    }
                if (nextGeometryLevel >= 0 && lightMasks.TryGetValue(groupId, out uint lightMask) &&
                    (lightMask & (1u << nextGeometryLevel)) == 0)
                {
                    //The next geometry LOD deliberately has no light representation. Reach zero exactly
                    //at that boundary, so changing branches cannot produce a visible lighting snap.
                    exitGain = 0.0f;
                    fadeOutLights++;
                    fadingGroups.Add(groupId);
                }

                light.lodAttenuationControlled = true;
                light.lodEntryAttenuationScale = gain;
                light.lodExitAttenuationScale = exitGain;
                light.lodAttenuationLevel = branch.namedLodLevel;
                light.lodAttenuationScene = branch.namedLodScene;
                if (Math.Abs(gain - 1.0f) > 1e-3f)
                {
                    compensatedLights++;
                    changedGroups.Add(groupId);
                    minGain = Math.Min(minGain, gain);
                    maxGain = Math.Max(maxGain, gain);
                }
            }
            compensatedGroups = changedGroups.Count;
            fadeOutGroups = fadingGroups.Count;
            if (compensatedLights == 0)
                minGain = maxGain = 1.0f;

            foreach ((Model parent, string key) groupId in fadingGroups)
            {
                uint geometryMask = 0;
                foreach (Model branch in namedBranches)
                    if (ReferenceEquals(branch.parent, groupId.parent) && branch.namedLodGroupKey == groupId.key)
                    {
                        geometryMask = branch.namedLodAvailableMask;
                        break;
                    }
                lightMasks.TryGetValue(groupId, out uint lightMask);
                var diagnosticKey = (groupId.parent, groupId.key, geometryMask, lightMask);
                if (loggedLodLightGaps.Add(diagnosticKey))
                    CallBacks.Log("[LODLightGap] {0}: geometry {1}, lights {2}; the final light level fades to zero before the next lightless geometry LOD",
                        namedLodGroupPath(groupId.parent, groupId.key), formatLodMask(geometryMask),
                        formatLodMask(lightMask));
            }

            CallBacks.Log("[LODLightAttenuation] {0} LOD lights bound; {1} lights in {2} groups require boundary compensation, gain {3:0.###}..{4:0.###}; {5} lights in {6} groups fade to zero before a lightless LOD",
                assignments.Count, compensatedLights, compensatedGroups, minGain, maxGain,
                fadeOutLights, fadeOutGroups);
        }

        private void addFlatRenderItem(Model node, FlatLodGate inheritedLodGates,
                                       FlatNamedLodGate inheritedNamedLodGates,
                                       FlatMeshGate inheritedMeshGates)
        {
            FlatLodGate lodGates = inheritedLodGates;
            if (node.lodModelIndex >= 0 && node.parent != null)
            {
                lodGates = new FlatLodGate
                {
                    group = node.parent,
                    requiredIndex = node.lodModelIndex,
                    parent = inheritedLodGates
                };
            }

            FlatNamedLodGate namedLodGates = inheritedNamedLodGates;
            if (node.namedLodLevel >= 0)
            {
                namedLodGates = new FlatNamedLodGate
                {
                    branch = node,
                    parent = inheritedNamedLodGates
                };
            }

            bool renderCandidate = node is Mesh || node is Collision || node is Joint ||
                                   node is Light || node is Locator;
            if (renderCandidate && node.meshVao != null)
            {
                //Some helper VAOs are not part of the resource manager's global list. Reset them the first
                //time they occur as well, then assign every candidate a stable slot for this scene topology.
                if (flatIndexedMeshes.Add(node.meshVao))
                    GLMeshBufferManager.resetInstances(node.meshVao);
                GLMeshBufferManager.registerInstance(node.meshVao, node);
                flatRenderItems.Add(new FlatRenderItem
                {
                    node = node,
                    lodGates = lodGates,
                    namedLodGates = namedLodGates,
                    meshGates = inheritedMeshGates
                });

                if (node is Mesh sceneMesh && sceneMesh.hasLOD && sceneMesh.parentScene != null &&
                    sceneMesh.LodLevel >= 0 && sceneMesh.LodLevel < 32)
                {
                    flatSceneLodMasks.TryGetValue(sceneMesh.parentScene, out uint lodMask);
                    flatSceneLodMasks[sceneMesh.parentScene] = lodMask | (1u << sceneMesh.LodLevel);
                }
            }

            if (node.lodModelCount > 0)
                flatLodGroups.Add(node);
            if (node is Scene scene)
                flatScenes.Add(scene);

            FlatMeshGate childMeshGates = inheritedMeshGates;
            if (node is Mesh mesh)
                childMeshGates = new FlatMeshGate { mesh = mesh, parent = inheritedMeshGates };

            foreach (Model child in node.Children)
                addFlatRenderItem(child, lodGates, namedLodGates, childMeshGates);
        }

        private int selectAvailableSceneLOD(Scene scene)
        {
            int selected = Model.selectLOD(scene.worldPosition, scene._LODDistances, scene._LODNum, false);
            if (!flatSceneLodMasks.TryGetValue(scene, out uint available) || available == 0 ||
                selected >= 32 || (available & (1u << selected)) != 0)
                return selected;

            //Malformed or unusual assets can declare a level for which they contain no mesh at all.
            //Prefer the closest more-detailed level, then the closest less-detailed level, so the whole
            //scene does not disappear while preserving intentional omissions within a valid level.
            for (int level = selected - 1; level >= 0; level--)
                if ((available & (1u << level)) != 0)
                    return level;
            for (int level = selected + 1; level < 32; level++)
                if ((available & (1u << level)) != 0)
                    return level;
            return selected;
        }

        private void updateSceneLods()
        {
            lastLodFallbackScenes = 0;
            foreach (Scene scene in flatScenes)
            {
                int requested = Model.selectLOD(scene.worldPosition, scene._LODDistances, scene._LODNum, false);
                int available = selectAvailableSceneLOD(scene);
                scene.activeLOD = available;
                if (available != requested)
                {
                    lastLodFallbackScenes++;
                    int state = requested * 32 + available;
                    if (!loggedLodFallbacks.TryGetValue(scene, out int previous) || previous != state)
                    {
                        flatSceneLodMasks.TryGetValue(scene, out uint mask);
                        string identity = string.IsNullOrEmpty(scene.SourceFile) ? scene.Name : scene.SourceFile;
                        CallBacks.Log("[LODFallback] {0}: requested LOD{1}, available mask 0x{2:X8}, using LOD{3}",
                            identity, requested, mask, available);
                        loggedLodFallbacks[scene] = state;
                    }
                }
                else
                    loggedLodFallbacks.Remove(scene);
            }
        }

        private void logAppliedLodSelection()
        {
            int forced = RenderState.forcedLOD;
            if (forced == lastLoggedForcedLod)
                return;

            SortedDictionary<int, int> sceneLevels = new SortedDictionary<int, int>();
            foreach (Scene scene in flatScenes)
            {
                sceneLevels.TryGetValue(scene.activeLOD, out int count);
                sceneLevels[scene.activeLOD] = count + 1;
            }

            List<string> levelCounts = new List<string>();
            foreach (KeyValuePair<int, int> pair in sceneLevels)
                levelCounts.Add($"LOD{pair.Key}={pair.Value}");

            int visibleIds = 0;
            foreach (GLMeshVao mesh in flatIndexedMeshes)
                visibleIds += mesh.instance_count;

            string mode = forced < 0 ? "Auto" : $"forced LOD{forced}";
            CallBacks.Log("[LODSelection] applied {0}; scene levels: {1}; LOD-filtered meshes {2}, visible instance IDs {3}",
                mode, string.Join(", ", levelCounts), RenderStats.lodFilteredNum, visibleIds);
            lastLoggedForcedLod = forced;
        }

        private static bool overlayEnabled(Model node)
        {
            //Emitter visibility must be refreshed even when locator crosses are hidden; its override records
            //the inherited LOD state before Locator decides whether to submit the helper cross.
            if (node is ParticleEmitter)
                return true;
            if (node is Collision)
                return RenderState.renderViewSettings.RenderCollisions;
            if (node is Joint)
                return RenderState.renderViewSettings.RenderJoints;
            if (node is Light)
                return true; //Lighting LOD state must update even when the yellow helper markers are hidden
            if (node is Locator)
                return RenderState.renderViewSettings.RenderLocators;
            return true;
        }

        private void rebuildInstancesForCamera(bool updateLods)
        {
            //Only objects that actually own an LOD choice need their distance recalculated. The flat entries
            //then apply those choices as inherited gates while retaining the original depth-first order.
            if (updateLods)
            {
                foreach (Model group in flatLodGroups)
                    group.activeLODModel = Model.selectLOD(group.worldPosition, group._LODDistances,
                                                           group.lodModelCount, true);
                updateSceneLods();
            }

            engine.renderMgr.clearInstances();
            foreach (FlatRenderItem item in flatRenderItems)
            {
                if (overlayEnabled(item.node))
                    item.node.updateOwnMeshInfo(item.Hidden);
            }
        }

        private void frameUpdate()
        {
            //Capture Input
            //engine.input_poller(dt);

            //Common.CallBacks.Log(RenderState.renderSettings.RENDERMODE);

            //Set time to the renderManager
            engine.renderMgr.progressTime(dt);
            long phaseStart = Stopwatch.GetTimestamp();
            
            //Everything that moves is updated first, so culling and instancing below see this frame's state

            //Common.CallBacks.Log("Dt {0}", dt);
            engine.updateSystems((float)dt);

            //Progress animations. Only scenes whose pose changed need their transforms updated
            if (RenderState.renderSettings.ToggleAnimations)
            {
                animatedSceneRoots.Clear();
                foreach (Model s in engine.animationSys.ActiveScenes)
                    if (s.parentScene != null)
                        animatedSceneRoots.Add(s.parentScene);

                //Skeletal animation changes joints and bone matrices, not the object transforms of every
                //static mesh in the scene. Propagate the joint hierarchy and refresh only skinning data.
                foreach (Scene scene in animatedSceneRoots)
                {
                    scene.update();
                    updateAnimatedSkinData(scene);
                }

                //Spinning parts (TkRotationComponentData)
                foreach (Model s in engine.rotationSys.ActiveScenes)
                    queueModelUpdate(s);
            }

            long phaseSystems = Stopwatch.GetTimestamp();

            //Update moving roots once, even if several clips or systems touched the same scene. Animation
            //and rotating parts keep their current instance membership and update the existing slots below.
            modelUpdateRoots.Clear();
            lock (modelUpdateLock)
                while (modelUpdateQueue.Count > 0)
                    modelUpdateRoots.Add(modelUpdateQueue.Dequeue());

            foreach (Model m in modelUpdateRoots)
            {
                m.update();
                updateExistingInstanceData(m);
            }

            engine.particleSys.update((float)dt);

            long phaseQueue = Stopwatch.GetTimestamp();

            //Camera & Light Positions. A collapsed or minimized viewport has no size, then the last aspect is kept
            Camera cam = RenderState.activeCam;
            if (viewportWidth > 0 && viewportHeight > 0)
                cam.aspect = viewportWidth / (float)viewportHeight;

            //Apply extra viewport rotation
            Matrix4 Rotx = Matrix4.CreateRotationX(MathUtils.radians(RenderState.rotAngles.X));
            Matrix4 Roty = Matrix4.CreateRotationY(MathUtils.radians(RenderState.rotAngles.Y));
            Matrix4 Rotz = Matrix4.CreateRotationZ(MathUtils.radians(RenderState.rotAngles.Z));
            RenderState.rotMat = Rotz * Rotx * Roty;
            //RenderState.rotMat = Matrix4.Identity;

            cam.updateViewMatrix();

            Matrix4 viewMat = cam.viewMat;
            bool sceneRotationChanged = RenderState.rotMat != lastRotMat;
            bool cameraPositionChanged = cam.Position != lastCameraPosition;
            bool cameraChanged = viewMat != lastViewMat || sceneRotationChanged;
            if (cameraChanged)
            {
                lastViewMat = viewMat;
                lastRotMat = RenderState.rotMat;
                lastCameraPosition = cam.Position;
            }

            //Topology/visibility/settings use the recursive source-of-truth path. Camera-only changes use
            //the equivalent depth-first flat index and its inherited LOD gates.
            long phaseCamera = Stopwatch.GetTimestamp();
            bool explicitlyDirty = RenderState.consumeInstancesDirty();
            bool flatCameraCull = USE_FLAT_CAMERA_CULLING && cameraChanged && !explicitlyDirty && flatRenderItems.Count > 0;
            bool fullInstancesDirty = explicitlyDirty || (cameraChanged && !flatCameraCull);
            bool instancesRebuilt = fullInstancesDirty || flatCameraCull;
            lastFlatCameraCull = flatCameraCull;

            if (instancesRebuilt)
            {
                RenderStats.occludedNum = 0;
                RenderStats.lodFilteredNum = 0;
                RenderStats.meshTicks = RenderStats.lightTicks = 0;
                RenderStats.meshCount = RenderStats.lightCount = RenderStats.lightUploads = 0;
                RenderStats.jointTicks = 0;
                RenderStats.jointCount = RenderStats.jointUploads = 0;
            }

            if (fullInstancesDirty)
            {
                rebuildFlatRenderIndex(); //Rebuild stable records only when topology/settings explicitly changed
                RenderState.rootObject?.updateLODDistances(); //Pick the LOD levels (forced, LOD0 with filtering off, or by distance)
                updateSceneLods(); //Validate the selection against the mesh levels actually present
                engine.renderMgr.clearInstances(); //Clear All mesh instances
                RenderState.rootObject?.updateMeshInfo(); //Reapply frustum culling and re-setup visible instances
                logAppliedLodSelection();
            }
            else if (flatCameraCull)
                rebuildInstancesForCamera(cameraPositionChanged || sceneRotationChanged);

            //Where the frame update went, for the log
            long phaseEnd = Stopwatch.GetTimestamp();
            double toMs = 1000.0 / Stopwatch.Frequency;
            phaseSystemsMs = (phaseSystems - phaseStart) * toMs;
            phaseQueueMs = (phaseQueue - phaseSystems) * toMs;
            phaseCameraMs = (phaseCamera - phaseQueue) * toMs;
            phaseInstancesMs = (phaseEnd - phaseCamera) * toMs;
            lastInstancesRebuilt = instancesRebuilt;
            if (instancesRebuilt)
            {
                phaseMeshesMs = RenderStats.meshTicks * toMs;
                phaseLightsMs = RenderStats.lightTicks * toMs;
                phaseJointsMs = RenderStats.jointTicks * toMs;
            }

            //Update Frame Counter
            fps(); //The stats overlay in MainWindow reads RenderStats

        }

        //Instance IDs are stable for the current scene topology. Visibility and LOD only rebuild each VAO's
        //compact ID list, so animated objects can update their persistent record even while off screen.
        private static bool hasCurrentInstance(Model m)
        {
            return m.meshVao != null && m.instanceId >= 0 &&
                   m.instanceId < m.meshVao.stable_instance_count &&
                   m.instanceId < m.meshVao.instanceRefs.Count &&
                   ReferenceEquals(m.meshVao.instanceRefs[m.instanceId], m);
        }

        //Explicit object motion (TkRotationComponentData and viewport edits) changes the world transform
        //of the node and its descendants, so refresh those existing stable instance slots.
        private static void updateExistingInstanceData(Model node)
        {
            if (node is Joint joint)
                joint.RefreshLineGeometry();
            if (hasCurrentInstance(node) && !(node is Joint) && !(node is Light))
            {
                GLMeshBufferManager.setInstanceWorldMat(node.meshVao, node.instanceId, node.worldMat);
                Matrix4 inverse = node.getWorldMatInv();
                GLMeshBufferManager.setInstanceWorldMatInv(node.meshVao, node.instanceId, inverse);
                GLMeshBufferManager.setInstanceNormalMat(node.meshVao, node.instanceId, Matrix4.Transpose(inverse));

                if (node is Mesh mesh && mesh.Skinned && mesh.parentScene != null)
                    mesh.meshVao.setSkinMatrices(mesh.parentScene, mesh.instanceId);
            }

            foreach (Model child in node.Children)
                updateExistingInstanceData(child);
        }

        //Skinned meshes receive animation through their joint palette. Rigid meshes parented below animated
        //joints (for example the portal terminal) and locator helpers need their instance transforms refreshed.
        private static void updateAnimatedSkinData(Model node)
        {
            if (node is Joint joint)
                joint.RefreshLineGeometry();
            if (node is Scene scene)
                scene.updateSkinMatrices();

            if (hasCurrentInstance(node) &&
                (node is Locator || (node is Mesh && node.parentScene != null)))
            {
                if (node is Mesh mesh && mesh.Skinned)
                {
                    mesh.meshVao.setSkinMatrices(mesh.parentScene, mesh.instanceId);
                }
                

                {
                    GLMeshBufferManager.setInstanceWorldMat(node.meshVao, node.instanceId, node.worldMat);
                    Matrix4 inverse = node.getWorldMatInv();
                    GLMeshBufferManager.setInstanceWorldMatInv(node.meshVao, node.instanceId, inverse);
                    GLMeshBufferManager.setInstanceNormalMat(node.meshVao, node.instanceId, Matrix4.Transpose(inverse));
                }
            }

            foreach (Model child in node.Children)
                updateAnimatedSkinData(child);
        }

        //Frames per second, averaged over about a second
        private void fps()
        {
            if (!fpsClock.IsRunning)
                fpsClock.Start();

            frames++;
            double ms = fpsClock.Elapsed.TotalMilliseconds;
            if (ms >= 1000.0)
            {
                RenderStats.fpsCount = (float) (1000.0 * frames / ms);
                frames = 0;
                fpsClock.Restart();
            }
        }
    
    }

}
