using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using OpenTK;
using OpenTK.Input;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using MVCore.Common;
using MVCore.GMDL;
using System.Timers;
using MVCore.Input;
using GLSLHelper;
using MVCore.Text;
using MVCore.Utils;
using Model_Viewer;
using OpenTK.Platform;
using MVCore.Engine.Systems;
using libMBIN.NMS.Toolkit;

namespace MVCore.Engine
{
    public enum EngineRenderingState
    {
        ACTIVE=0x0,
        REQUEST_HANDLING,
        PAUSED,
        EXIT
    }

    public class Engine
    {
        //Window References
        private GLControl Control;
        
        public ResourceManager resMgr;

        //Init Systems
        public ActionSystem actionSys;
        public AnimationSystem animationSys;
        private RequestHandler reqHandler;
        
        //Rendering 
        public renderManager renderMgr; //TODO: Try to make it private. Noone should have a reason to access it
        public EngineRenderingState rt_State;

        //Input
        public BaseGamepadHandler gpHandler;
        public KeyboardHandler kbHandler;
        private System.Timers.Timer inputPollTimer;

        //Camera Stuff
        public System.Timers.Timer cameraMovementTimer;
        public CameraPos targetCameraPos;
        //public int movement_speed = 1;

        //Use public variables for now because getters/setters are so not worth it for our purpose
        public float light_angle_y = 0.0f;
        public float light_angle_x = 0.0f;
        public float light_distance = 5.0f;
        public float light_intensity = 1.0f;
        public float scale = 1.0f;

        
        //Palette
        Dictionary<string, Dictionary<string, Vector4>> palette;

        public Engine()
        {
            kbHandler = new KeyboardHandler();
            //gpHandler = new PS4GamePadHandler(0); //TODO: Add support for PS4 controller
            reqHandler = new RequestHandler();

            RenderState.activeGamepad = gpHandler;

            //Assign new palette to GLControl
            palette = Palettes.createPalettefromBasePalettes();

            renderMgr = new renderManager(); //Init renderManager of the engine

            //Input Polling Timer
            inputPollTimer = new System.Timers.Timer();
            inputPollTimer.Elapsed += new ElapsedEventHandler(input_poller);
            inputPollTimer.Interval = 1;

            //Camera Movement Timer
            cameraMovementTimer = new System.Timers.Timer();
            cameraMovementTimer.Elapsed += new ElapsedEventHandler(camera_timer);
            cameraMovementTimer.Interval = 20;
            //cameraMovementTimer.Start(); Start in the main function

            //Systems Init
            actionSys = new ActionSystem();
            animationSys = new AnimationSystem();
            actionSys.SetEngine(this);
            animationSys.SetEngine(this);

        }

        public void init()
        {
            //Start Timers
            inputPollTimer.Start();
            cameraMovementTimer.Start();

            //Init Gizmos
            //gizTranslate = new TranslationGizmo();
            //activeGizmo = gizTranslate;
            resMgr = new ResourceManager();
            RenderState.activeResMgr = resMgr; //Set reference first because the object generators use the activeResMgr
            if (!resMgr.initialized)
                resMgr.Init();

            //Initialize the render manager
            renderMgr.init(resMgr);
            renderMgr.setupGBuffer(Control.ClientSize.Width, Control.ClientSize.Height);
        }

        public void handleRequests()
        {
            if (reqHandler.hasOpenRequests())
            {
                ThreadRequest req = reqHandler.Fetch();
                lock (req)
                {
                    switch (req.type)
                    {
                        case THREAD_REQUEST_TYPE.QUERY_GLCONTROL_STATUS_REQUEST:
                            //At this point the renderer is up and running
                            req.status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        case THREAD_REQUEST_TYPE.INIT_RESOURCE_MANAGER:
                            resMgr.Init();
                            req.status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        case THREAD_REQUEST_TYPE.NEW_SCENE_REQUEST:
                            inputPollTimer.Stop();
                            rt_addRootScene((string)req.arguments[0]);
                            req.status = THREAD_REQUEST_STATUS.FINISHED;
                            inputPollTimer.Start();
                            break;
#if DEBUG
                        case THREAD_REQUEST_TYPE.NEW_TEST_SCENE_REQUEST:
                            inputPollTimer.Stop();
                            rt_addTestScene((int)req.arguments[0]);
                            req.status = THREAD_REQUEST_STATUS.FINISHED;
                            inputPollTimer.Start();
                            break;
#endif
                        case THREAD_REQUEST_TYPE.CHANGE_MODEL_PARENT_REQUEST:
                            Model source = (Model) req.arguments[0];
                            Model target = (Model) req.arguments[1];

                            System.Windows.Application.Current.Dispatcher.Invoke((System.Action)(() =>
                            {
                                if (source.parent != null)
                                    source.parent.Children.Remove(source);

                                //Add to target node
                                source.parent = target;
                                target.Children.Add(source);
                            }));

                            req.status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        case THREAD_REQUEST_TYPE.UPDATE_SCENE_REQUEST:
                            Scene req_scn = (Scene)req.arguments[0];
                            req_scn.update();
                            req.status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        case THREAD_REQUEST_TYPE.GL_COMPILE_ALL_SHADERS_REQUEST:
                            resMgr.compileMainShaders();
                            req.status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        case THREAD_REQUEST_TYPE.MOUSEPOSITION_INFO_REQUEST:
                            Vector4[] t = (Vector4[])req.arguments[2];
                            renderMgr.getMousePosInfo((int)req.arguments[0], (int)req.arguments[1],
                                ref t);
                            req.status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        case THREAD_REQUEST_TYPE.GL_RESIZE_REQUEST:
                            rt_ResizeViewport((int)req.arguments[0], (int)req.arguments[1]);
                            req.status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        case THREAD_REQUEST_TYPE.GL_MODIFY_SHADER_REQUEST:
                            GLShaderHelper.modifyShader((GLSLShaderConfig)req.arguments[0],
                                         (GLSLShaderText)req.arguments[1]);
                            req.status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        case THREAD_REQUEST_TYPE.GIZMO_PICKING_REQUEST:
                            //TODO: Send the nessessary arguments to the render manager and mark the active gizmoparts
                            Gizmo g = (Gizmo)req.arguments[0];
                            renderMgr.gizmoPick(ref g, (Vector2)req.arguments[1]);
                            req.status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        case THREAD_REQUEST_TYPE.TERMINATE_REQUEST:
                            rt_State = EngineRenderingState.EXIT;
                            inputPollTimer.Stop();
                            req.status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        case THREAD_REQUEST_TYPE.GL_PAUSE_RENDER_REQUEST:
                            rt_State = EngineRenderingState.PAUSED;
                            req.status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        case THREAD_REQUEST_TYPE.GL_RESUME_RENDER_REQUEST:
                            rt_State = EngineRenderingState.ACTIVE;
                            req.status = THREAD_REQUEST_STATUS.FINISHED;
                            break;
                        case THREAD_REQUEST_TYPE.NULL:
                            break;
                    }
                }
            }
        }

        public void SetControl(CGLControl control)
        {
            Control = control;
        }

        //Main Rendering Routines

        private void rt_ResizeViewport(int w, int h)
        {
            renderMgr.resize(w, h);
        }

#if DEBUG

        private void rt_SpecularTestScene()
        {
            //Once the new scene has been loaded, 
            //Initialize Palettes
            Palettes.set_palleteColors();

            //Clear Systems
            actionSys.CleanUp();
            animationSys.CleanUp();

            //Clear Resources
            resMgr.Cleanup();
            resMgr.Init();
            RenderState.activeResMgr = resMgr;
            ModelProcGen.procDecisions.Clear();

            RenderState.rootObject = null;
            RenderState.activeModel = null;
            //Clear Gizmos
            RenderState.activeGizmo = null;

            //Clear RenderStats
            RenderStats.ClearStats();


            //Stop animation if on
            bool animToggleStatus = RenderState.renderSettings.ToggleAnimations;
            RenderState.renderSettings.ToggleAnimations = false;

            //Setup new object
            Scene scene = new Scene();
            scene.name = "DEFAULT SCENE";


            //ADd Lights
            Light l = new Light();
            l.Name = "Light 1";
            l.localPosition = new Vector3(0.2f, 0.2f, -2.0f);
            l.Color = new MVector4(1.0f, 1.0f, 1.0f, 1.0f);
            l.Intensity = 100.0f;
            l.falloff = ATTENUATION_TYPE.QUADRATIC;
            Common.RenderState.activeResMgr.GLlights.Add(l);
            scene.children.Add(l);

            Light l1 = new Light();
            l1.Name = "Light 2";
            l1.localPosition = new Vector3(0.2f, -0.2f, -2.0f);
            l1.Color = new MVector4(1.0f, 1.0f, 1.0f, 1.0f);
            l1.Intensity = 100.0f;
            l1.falloff = ATTENUATION_TYPE.QUADRATIC;
            Common.RenderState.activeResMgr.GLlights.Add(l1);
            scene.children.Add(l1);

            Light l2 = new Light();
            l2.Name = "Light 3";
            l2.localPosition = new Vector3(-0.2f, 0.2f, -2.0f);
            l2.Color = new MVector4(1.0f, 1.0f, 1.0f, 1.0f);
            Common.RenderState.activeResMgr.GLlights.Add(l2);
            l2.Intensity = 100.0f;
            l2.falloff = ATTENUATION_TYPE.QUADRATIC;
            scene.children.Add(l2);

            Light l3 = new Light();
            l3.Name = "Light 4";
            l3.localPosition = new Vector3(-0.2f, -0.2f, -2.0f);
            l3.Color = new MVector4(1.0f, 1.0f, 1.0f, 1.0f);
            Common.RenderState.activeResMgr.GLlights.Add(l3);
            l3.Intensity = 100.0f;
            l3.falloff = ATTENUATION_TYPE.QUADRATIC;
            scene.children.Add(l3);

            //Generate a Sphere and center it in the scene
            Model sphere = new Mesh();
            sphere.Name = "Test Sphere";
            sphere.parent = scene;
            sphere.setParentScene(scene);
            MeshMetaData sphere_metadata = new MeshMetaData();


            int bands = 80;

            sphere_metadata.batchcount = bands * bands * 6;
            sphere_metadata.batchstart_graphics = 0;
            sphere_metadata.vertrstart_graphics = 0;
            sphere_metadata.vertrend_graphics = (bands + 1) * (bands + 1) - 1;
            sphere_metadata.indicesLength = DrawElementsType.UnsignedInt;

            sphere.meshVao = new GLMeshVao(sphere_metadata);
            sphere.meshVao.type = TYPES.MESH;
            sphere.meshVao.vao = (new GMDL.Primitives.Sphere(new Vector3(), 2.0f, 40)).getVAO();


            //Sphere Material
            Material mat = new Material();
            mat.Name = "default_scn";
            
            Uniform uf = new Uniform();
            uf.Name = "gMaterialColourVec4";
            uf.Values = new libMBIN.NMS.Vector4f();
            uf.Values.x = 1.0f;
            uf.Values.y = 0.0f;
            uf.Values.z = 0.0f;
            uf.Values.t = 1.0f;
            mat.Uniforms.Add(uf);

            uf = new Uniform();
            uf.Name = "gMaterialParamsVec4";
            uf.Values = new libMBIN.NMS.Vector4f();
            uf.Values.x = 0.15f; //Roughness
            uf.Values.y = 0.0f;
            uf.Values.z = 0.2f; //Metallic
            uf.Values.t = 0.0f;
            mat.Uniforms.Add(uf);
                
            mat.init();
            resMgr.GLmaterials["test_mat1"] = mat;
            sphere.meshVao.material = mat;
            sphere.instanceId = GLMeshBufferManager.addInstance(ref sphere.meshVao, sphere); //Add instance
            
            scene.children.Add(sphere);

            //Explicitly add default light to the rootObject
            scene.children.Add(resMgr.GLlights[0]);

            scene.updateLODDistances();
            scene.update(); //Refresh all transforms
            scene.setupSkinMatrixArrays();

            //Save scene path to resourcemanager
            RenderState.activeResMgr.GLScenes["TEST_SCENE_1"] = scene; //Use input path

            //Populate RenderManager
            renderMgr.populate(scene);

            //Clear Instances
            renderMgr.clearInstances();
            scene.updateMeshInfo(); //Update all mesh info

            scene.selected = 1;
            RenderState.rootObject = scene;
            //RenderState.activeModel = root; //Set the new scene as the new activeModel


            //Reinitialize gizmos
            RenderState.activeGizmo = new TranslationGizmo();

            //Restart anim worker if it was active
            RenderState.renderSettings.ToggleAnimations = animToggleStatus;

        }

        private void rt_addTestScene(int sceneID)
        {
            
            switch (sceneID)
            {
                case 0:
                    rt_SpecularTestScene();
                    break;
                default:
                    Console.WriteLine("Non Implemented Test Scene");
                    break;
            }

        }

#endif






        private void rt_addRootScene(string filename)
        {
            //Once the new scene has been loaded, 
            //Initialize Palettes
            Palettes.set_palleteColors();

            //Clear Systems
            actionSys.CleanUp();
            animationSys.CleanUp();

            //Clear Resources
            resMgr.Cleanup();
            resMgr.Init();
            RenderState.activeResMgr = resMgr;
            ModelProcGen.procDecisions.Clear();
            
            RenderState.rootObject = null;
            RenderState.activeModel = null;
            //Clear Gizmos
            RenderState.activeGizmo = null;

            //Clear RenderStats
            RenderStats.ClearStats();

            //Stop animation if on
            bool animToggleStatus = RenderState.renderSettings.ToggleAnimations;
            RenderState.renderSettings.ToggleAnimations = false;
            
            //Setup new object
            Model root = GEOMMBIN.LoadObjects(filename);

            //Explicitly add default light to the rootObject
            root.children.Add(resMgr.GLlights[0]);

            root.updateLODDistances();
            root.update(); //Refresh all transforms
            root.setupSkinMatrixArrays();

            //Populate RenderManager
            renderMgr.populate(root);

            //Clear Instances
            renderMgr.clearInstances();
            root.updateMeshInfo(); //Update all mesh info

            root.selected = 1;
            RenderState.rootObject = root;
            //RenderState.activeModel = root; //Set the new scene as the new activeModel
            

            //Reinitialize gizmos
            RenderState.activeGizmo = new TranslationGizmo();

            //Restart anim worker if it was active
            RenderState.renderSettings.ToggleAnimations = animToggleStatus;
        
        }

        
        private void input_poller(object sender, System.Timers.ElapsedEventArgs e)
        {
            //Console.WriteLine(gpHandler.getAxsState(0, 0).ToString() + " " +  gpHandler.getAxsState(0, 1).ToString());
            //gpHandler.reportButtons();
            //gamepadController(); //Move camera according to input

            //Move Camera
            keyboardController();
            //gamepadController();

            bool focused = false;


            //TODO: Toggle the focus in the GLControl side
            /*
            Invoke((MethodInvoker)delegate
            {
                focused = Focused;
            });
            */

            kbHandler?.updateState();
            if (focused)
            {
                
                //gpHandler?.updateState();
            }


        }

        public void issueRenderingRequest(ref ThreadRequest r)
        {
            reqHandler.issueRequest(ref r);
        }

#region Camera Update Functions

        private void camera_timer(object sender, ElapsedEventArgs e)
        {
            //Update Target for camera
            RenderState.activeCam?.updateTarget(targetCameraPos,
                (float)cameraMovementTimer.Interval);
            targetCameraPos.Reset();
        }


        

        public void updateActiveCam(Vector3 pos, Vector3 rot)
        {
            RenderState.activeCam.Position = pos;




            //RenderState.activeCam.pitch = rot.X; //Radians rotation on X axis
            //RenderState.activeCam.yaw = rot.Y; //Radians rotation on Y axis
            //RenderState.activeCam.roll = rot.Z; //Radians rotation on Z axis
        }

#endregion

        

        public void updateLightPosition(int light_id)
        {
            Light light = resMgr.GLlights[light_id];
            light.updatePosition(new Vector3((float)(light_distance * Math.Cos(MathUtils.radians(light_angle_x)) *
                                                            Math.Sin(MathUtils.radians(light_angle_y))),
                                                (float)(light_distance * Math.Sin(MathUtils.radians(light_angle_x))),
                                                (float)(light_distance * Math.Cos(MathUtils.radians(light_angle_x)) *
                                                            Math.Cos(MathUtils.radians(light_angle_y)))));
        }

       

#region INPUT_HANDLERS

        //Gamepad handler
        private void gamepadController()
        {
            if (gpHandler == null) return;
            if (!gpHandler.isConnected()) return;

            //Camera Movement
            float cameraSensitivity = 2.0f;
            float x, y, z, rotx, roty;

            x = gpHandler.getAction(ControllerActions.MOVE_X);
            y = gpHandler.getAction(ControllerActions.ACCELERATE) - gpHandler.getAction(ControllerActions.DECELERATE);
            z = gpHandler.getAction(ControllerActions.MOVE_Y_NEG) - gpHandler.getAction(ControllerActions.MOVE_Y_POS);
            rotx = -cameraSensitivity * gpHandler.getAction(ControllerActions.CAMERA_MOVE_H);
            roty = cameraSensitivity * gpHandler.getAction(ControllerActions.CAMERA_MOVE_V);

            targetCameraPos.PosImpulse.X = x;
            targetCameraPos.PosImpulse.Y = y;
            targetCameraPos.PosImpulse.Z = z;
            targetCameraPos.Rotation.X = rotx;
            targetCameraPos.Rotation.Y = roty;
        }

        //Keyboard handler
        private void keyboardController()
        {
            if (kbHandler == null) return;

            //Camera Movement
            float step = 0.002f;
            float x, y, z;

            x = kbHandler.getKeyStatus(Key.D) - kbHandler.getKeyStatus(Key.A);
            y = kbHandler.getKeyStatus(Key.W) - kbHandler.getKeyStatus(Key.S);
            z = kbHandler.getKeyStatus(Key.R) - kbHandler.getKeyStatus(Key.F);

            //Camera rotation is done exclusively using the mouse

            //rotx = 50 * step * (kbHandler.getKeyStatus(OpenTK.Input.Key.E) - kbHandler.getKeyStatus(OpenTK.Input.Key.Q));
            //float roty = (kbHandler.getKeyStatus(Key.C) - kbHandler.getKeyStatus(Key.Z));

            RenderState.rotAngles.Y += 100 * step * (kbHandler.getKeyStatus(Key.E) - kbHandler.getKeyStatus(Key.Q));
            RenderState.rotAngles.Y %= 360;

            //Move Camera
            targetCameraPos.PosImpulse.X = x;
            targetCameraPos.PosImpulse.Y = y;
            targetCameraPos.PosImpulse.Z = z;
        }


        public void CaptureInput(bool status)
        {
            if (status && !inputPollTimer.Enabled)
                inputPollTimer.Start();
            else if (!status)
                inputPollTimer.Stop();
        }

#endregion




    }
}
