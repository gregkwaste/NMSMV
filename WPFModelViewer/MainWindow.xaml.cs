using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using gImage;
using ProjProperties = WPFModelViewer.Properties;
using GLSLHelper;
using Microsoft.Win32;
using Model_Viewer;
using MVCore.Text;
using MVCore.Common;
using MVCore.GMDL;
using MVCore;
using OpenTK.Graphics.OpenGL4;
using QuickFont;
using QuickFont.Configuration;
using System.Runtime.InteropServices;

namespace WPFModelViewer
{
    
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private CGLControl glControl;
        private Settings settings;
        private model activeModel;
        private model prev_activeModel;

        private int itemCounter = 0;

        //Handle Async Requests
        private List<ThreadRequest> issuedRequests= new List<ThreadRequest>();
        private System.Timers.Timer requestHandler = new System.Timers.Timer();

        public MainWindow()
        {
            InitializeComponent();

            //Generate CGLControl
            glControl = new CGLControl();
            glControl.Update();
            glControl.MakeCurrent();


            //Add request timer handler
            requestHandler.Interval = 10;
            requestHandler.Elapsed += queryRequests;
            requestHandler.Start();

            //Compile Shaders before starting the rendering thread
            compileShaders();
            //Load font should be done before being used by the rendering thread and after the shaders are live
            glControl.setupTextRenderer();
            glControl.setupControlParameters();

            //Load Keyboard Handler
            glControl.kbHandler = new KeyboardHandler();
            //glControl.gpHandler = new GamepadHandler(0);

            Host.Child = glControl;
        }

        //Open File
        private void OpenFile(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Opening File");
            OpenFileDialog openFileDlg = new OpenFileDialog();
            openFileDlg.Filter = "SCENE Files (*.SCENE.MBIN, *.SCENE.EXML)|*.SCENE.MBIN;*.SCENE.EXML";
            var res = openFileDlg.ShowDialog();

            if (res == false)
                return;
            
            var filename = openFileDlg.FileName;
            Console.WriteLine("Importing " + filename);

            //Generate Request for rendering thread
            ThreadRequest req = new ThreadRequest();
            req.type = THREAD_REQUEST_TYPE.NEW_SCENE_REQUEST;
            req.arguments.Clear();
            req.arguments.Add(filename);

            glControl.issueRequest(req);
            issuedRequests.Add(req);

            //Cleanup resource manager
            Util.setStatus("Ready");
        
        }

        //Request Handler
        private void queryRequests(object sender, System.Timers.ElapsedEventArgs e)
        {
            int i = 0;
            while ( i < issuedRequests.Count)
            {
                ThreadRequest req = issuedRequests[i];
                if (req.status == THREAD_REQUEST_STATUS.FINISHED)
                {
                    switch (req.type)
                    {
                        case THREAD_REQUEST_TYPE.NEW_SCENE_REQUEST:
                            glControl.rootObject.ID = itemCounter;
                            Util.setStatus("Creating Nodes...");
                            ModelNode scn_node = new ModelNode(glControl.rootObject);
                            traverse_oblist(glControl.rootObject, scn_node);
                            //Add to UI
                            Application.Current.Dispatcher.Invoke((Action)(() =>
                            {
                                SceneTreeView.Items.Clear();
                                SceneTreeView.Items.Add(scn_node);
                                
                            }));
                            Util.setStatus("Ready");
                            GC.Collect();
                            break;
                        case THREAD_REQUEST_TYPE.COMPILE_SHADER_REQUEST:
                            //Add Shader to resource manager
                            GLSLHelper.GLSLShaderConfig shader_conf = (GLSLShaderConfig) req.arguments[0];
                            RenderState.activeResMgr.GLShaderConfigs[shader_conf.name] = shader_conf;
                            RenderState.activeResMgr.GLShaders[shader_conf.name] = shader_conf.program_id;
                            File.WriteAllText("shader_compilation_" + shader_conf.name + ".log", shader_conf.log);
                            Util.setStatus("Shader Compiled Successfully!");
                            break;
                        default:
                            break;
                    }
                    issuedRequests.RemoveAt(i); //Remove request
                }
                else
                    i++;
            }
        }


        //Close Form

        private void FormClose(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Bye bye :'(");
            this.Close();
        }

        private void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            //Stop request timer
            requestHandler.Stop();

            //Send Terminate Rendering request to the rt_thread
            ThreadRequest req = new ThreadRequest();
            req.type = THREAD_REQUEST_TYPE.TERMINATE_REQUEST;
            req.arguments.Clear();
            glControl.issueRequest(req);

            //Wait for the request to finish
            while (true)
            {
                lock (req)
                {
                    if (req.status == THREAD_REQUEST_STATUS.FINISHED)
                        break;
                }
            }

            //Cleanup GL Context
            glControl.rootObject?.Dispose();
            glControl.resMgr.Cleanup();
            glControl.Dispose();
        }

        private void MainWindow_OnClosed(object sender, EventArgs e)
        {
            Console.WriteLine("Window Closed");
            
            //CLose Logger
            Util.loggingSr.Close();
        }

        //Do stuff once the GUI is ready
        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            //OVERRIDE SETTINGS
            //FileUtils.dirpath = "I:\\SteamLibrary1\\steamapps\\common\\No Man's Sky\\GAMEDATA\\PCBANKS";
            
            //Load Settings
            settings = SettingsForm.loadSettingsStatic();
            SettingsForm.saveSettingsToEnv(settings);

            //Setup Logger
            Util.loggingSr = new StreamWriter("log.out");


            //Populate GLControl
            scene scene = new scene();
            scene.type = TYPES.SCENE;
            scene.name = "DEFAULT SCENE";

            //Add cube to scene
            {
                //Create model
                Collision so = new Collision();

                //Remove that after implemented all the different collision types
                so.shader_programs = new int[] { MVCore.Common.RenderState.activeResMgr.GLShaders["MESH_SHADER"],
                                             MVCore.Common.RenderState.activeResMgr.GLShaders["DEBUG_SHADER"],
                                             MVCore.Common.RenderState.activeResMgr.GLShaders["PICKING_SHADER"]}; //Use Mesh program for collisions
                so.debuggable = true;
                so.name = "TEST_BOX";
                so.type = TYPES.MESH;

                so.Bbox[0][0] = 1.0f;
                so.Bbox[0][1] = 1.0f;
                so.Bbox[0][2] = 1.0f;
                so.Bbox[1][0] = -1.0f;
                so.Bbox[1][1] = -1.0f;
                so.Bbox[1][2] = -1.0f;

                so.main_Vao = MVCore.Common.RenderState.activeResMgr.GLPrimitiveVaos["default_box"];
                so.collisionType = COLLISIONTYPES.BOX;
                //Set general vbo properties
                so.batchstart_graphics = 0;
                so.batchcount = 36;
                so.vertrstart_graphics = 0;
                so.vertrend_graphics = 8 - 1;

                so.scene = scene;
                so.parent = scene;
                scene.children.Add(so);
            }

            

            //Force rootobject
            glControl.rootObject = scene;

            //Improve performance on Treeview
            SceneTreeView.SetValue(VirtualizingStackPanel.IsVirtualizingProperty, true);
            SceneTreeView.SetValue(VirtualizingStackPanel.VirtualizationModeProperty, VirtualizationMode.Recycling);

            SceneTreeView.Items.Clear();
            ModelNode scn_node = new ModelNode(glControl.rootObject);
            traverse_oblist(glControl.rootObject, scn_node);
            SceneTreeView.Items.Add(scn_node);
            GC.Collect();


            //Check if Temp folder exists
#if DEBUG
            if (!Directory.Exists("Temp")) Directory.CreateDirectory("Temp");
#endif
            //Set active Components
            Util.activeStatusStrip = StatusLabel;
            Util.activeControl = glControl;

            //SETUP THE CALLBACKS OF MVCORE
            MVCore.Common.CallBacks.updateStatus = Util.setStatus;
            MVCore.Common.CallBacks.openAnim = Util.loadAnimationFile;
            MVCore.Common.CallBacks.Log = Util.Log;
            MVCore.Common.CallBacks.issueRequestToGLControl = Util.sendRequest;

            //Add event handlers to GUI elements
            sliderzNear.ValueChanged += Sliders_OnValueChanged;
            sliderzFar.ValueChanged += Sliders_OnValueChanged;
            sliderFOV.ValueChanged += Sliders_OnValueChanged;
            sliderLightIntensity.ValueChanged += Sliders_OnValueChanged;
            sliderlightDistance.ValueChanged += Sliders_OnValueChanged;
            sliderMovementSpeed.ValueChanged += Sliders_OnValueChanged;


            //Invoke the method in order to setup the control at startup
            Sliders_OnValueChanged(null, new RoutedPropertyChangedEventArgs<double>(0.0f,0.0f));

            this.Dispatcher.UnhandledException += OnDispatcherUnhandledException;
        
        }

        void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            string errorMessage = string.Format("An unhandled exception occurred: {0}", e.Exception.Message);
            MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
        
        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Generating ProcGen Models");
            MessageBox.Show("HOOOOOOOOLA");
        }


        //Helpers
        private void traverse_oblist(model ob, ModelNode parent)
        {
            ob.ID = this.itemCounter;
            this.itemCounter++;
            
            if (ob.children.Count > 0)
            {
                foreach (model child in ob.children)
                {
                    //Set object index
                    //Check if child is a scene
                    ModelNode node = new ModelNode(child);
                    parent.Children.Add(node);
                    traverse_oblist(child, node);
                }
            }
        }


        public void issuemodifyShaderRequest(GLSLShaderConfig config, string shaderText, OpenTK.Graphics.OpenGL4.ShaderType shadertype)
        {
            Console.WriteLine("Sending Shader Modification Request");
            ThreadRequest req = new ThreadRequest();
            req.type = THREAD_REQUEST_TYPE.MODIFY_SHADER_REQUEST;
            req.arguments.Add(config);
            req.arguments.Add(shaderText);
            req.arguments.Add(shadertype);
            
            //Send request
            glControl.issueRequest(req);
            issuedRequests.Add(req);
        }

        //GLPreparation
        private void compileShader(string vs, string fs, string gs, string tes, string tcs, string name, ref string log)
        {
            GLSLShaderConfig shader_conf = new GLSLShaderConfig(vs,fs,gs,tcs,tes, name);
            //Set modify Shader delegate
            shader_conf.modifyShader = issuemodifyShaderRequest;

            glControl.compileShader(shader_conf);
            RenderState.activeResMgr.GLShaderConfigs[shader_conf.name] = shader_conf;
            RenderState.activeResMgr.GLShaders[shader_conf.name] = shader_conf.program_id;
            log += shader_conf.log; //Append log
        }

        private void compileShaders()
        {

#if(DEBUG)
            //Query GL Extensions
            Console.WriteLine("OPENGL AVAILABLE EXTENSIONS:");
            string[] ext = GL.GetString(StringName.Extensions).Split(' ');
            foreach (string s in ext)
            {
                if (s.Contains("explicit"))
                    Console.WriteLine(s);
                if (s.Contains("16"))
                    Console.WriteLine(s);
            }

            //Query maximum buffer sizes
            Console.WriteLine("MaxUniformBlock Size {0}", GL.GetInteger(GetPName.MaxUniformBlockSize));
#endif

            //Populate shader list
            RenderState.activeResMgr = glControl.resMgr;
            string log = "";
            
            //Geometry Shader
            //Compile Object Shaders
            //Create Shader Config
            compileShader("Shaders/Simple_VSEmpty.glsl",
                            "Shaders/Simple_FSEmpty.glsl",
                            "Shaders/Simple_GS.glsl",
                            "", "", "DEBUG_SHADER", ref log);

            //Picking Shaders
            compileShader(ProjProperties.Resources.pick_vert,
                            ProjProperties.Resources.pick_frag,
                            "","", "", "PICKING_SHADER", ref log);


            //Main Object Shader
            compileShader("Shaders/Simple_VS.glsl",
                            "Shaders/Simple_FS.glsl",
                            "", "", "", "MESH_SHADER", ref log);


            //BoundBox Shader
            compileShader("Shaders/Bound_VS.glsl",
                            "Shaders/Bound_FS.glsl",
                            "", "", "", "BBOX_SHADER", ref log);

            //Texture Mixing Shader
            compileShader("Shaders/pass_VS.glsl",
                            "Shaders/pass_FS.glsl",
                            "", "", "", "TEXTURE_MIXING_SHADER", ref log);

            //GBuffer Shaders
            compileShader("Shaders/Gbuffer_VS.glsl",
                            "Shaders/Gbuffer_FS.glsl",
                            "", "", "", "GBUFFER_SHADER", ref log);

            //Decal Shaders
            compileShader("Shaders/decal_VS.glsl",
                            "Shaders/Decal_FS.glsl",
                            "", "", "", "DECAL_SHADER", ref log);

            //Locator Shaders
            compileShader(ProjProperties.Resources.locator_vert,
                            ProjProperties.Resources.locator_frag,
                            "", "", "", "LOCATOR_SHADER", ref log);

            //Joint Shaders
            compileShader(ProjProperties.Resources.joint_vert,
                            ProjProperties.Resources.joint_frag,
                            "", "", "", "JOINT_SHADER", ref log);

            //Text Shaders
            compileShader(ProjProperties.Resources.text_vert,
                            ProjProperties.Resources.text_frag,
                            "", "", "", "TEXT_SHADER", ref log);

            //Light Shaders
            compileShader(ProjProperties.Resources.light_vert,
                            ProjProperties.Resources.light_frag,
                            "", "", "", "LIGHT_SHADER", ref log);

            //Camera Shaders
            compileShader(ProjProperties.Resources.camera_vert,
                            ProjProperties.Resources.camera_frag,
                            "", "", "", "CAMERA_SHADER", ref log);

        }


        private void Sliders_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            //Update slider values
            //Set Camera FOV
            float zNear = 0.0f;
            float zFar = 0.0f;
            int FOV = 10;
            
            zNear = (float) sliderzNear.Value;
            zFar = (float) sliderzFar.Value;
            FOV = (int) sliderFOV.Value;

            glControl.updateActiveCam(FOV, zNear, zFar);
            glControl.movement_speed = (int) Math.Floor(Math.Pow(sliderMovementFactor.Value, sliderMovementSpeed.Value));
            glControl.light_distance = (float) Math.Pow(1.25f, sliderlightDistance.Value) - 1.0f;
            glControl.light_intensity = (float) sliderLightIntensity.Value;
        }

        private void CameraResetPos(object sender, RoutedEventArgs e)
        {
            glControl.updateActiveCamPos(0.0f, 0.0f, 0.0f);
            glControl.updateControlRotation(0.0f, 0.0f);
        }

        private void updateRenderOptions(object sender, RoutedEventArgs e)
        {
            //Toggle Wireframe
            if (toggleWireframe.IsChecked.Value)
                RenderOptions.RENDERMODE = PolygonMode.Line;
            else
                RenderOptions.RENDERMODE = PolygonMode.Fill;

            //Toggle Texture Render
            if (useTextures.IsChecked.Value)
                RenderOptions.UseTextures = 1.0f;
            else
                RenderOptions.UseTextures = 0.0f;

            //Toggle Use Lighting
            if (useLighting.IsChecked.Value)
                RenderOptions.UseLighting = 1.0f;
            else
                RenderOptions.UseLighting = 0.0f;

            //Toggle Info Render
            if (toggleInfo.IsChecked.Value)
                RenderOptions.RenderInfo = true;
            else
                RenderOptions.RenderInfo = false;

            //Toggle Light Render
            if (toggleLights.IsChecked.Value)
                RenderOptions.RenderLights = true;
            else
                RenderOptions.RenderLights = false;

            //Toggle Collisions Render
            if (toggleCollisions.IsChecked.Value)
                RenderOptions.RenderCollisions = true;
            else
                RenderOptions.RenderCollisions = false;
        }

        private void SceneTreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            ModelNode node = (ModelNode) SceneTreeView.SelectedItem;
            if (node != null)
            {
                //Swap activeModels
                prev_activeModel = activeModel;
                activeModel = node.mdl;

                //Set binding to objectinfo box
                ObjectInfoBox.DataContext = node.mdl;
                
                //Set Selected
                activeModel.selected = 1;
                //Object Name
                //activeObjectName.Text = node.mdl.name;
                activeTransform.loadModel(node.mdl);
            }

            //Deselect Previews model
            if (prev_activeModel != null)
                prev_activeModel.selected = 0;
        }

        private void showAboutDialog(object sender, RoutedEventArgs e)
        {
            Window about = new AboutDialog();
            about.Show();
        }

        private void showSettingsDialog(object sender, RoutedEventArgs e)
        {
            Window setWin = new SettingsForm(settings);
            setWin.Show();
        }
    }
}

namespace WPFModelViewer
{
    internal static class NativeMethods
    {
        // http://msdn.microsoft.com/en-us/library/ms681944(VS.85).aspx
        /// <summary>
        /// Allocates a new console for the calling process.
        /// </summary>
        /// <returns>nonzero if the function succeeds; otherwise, zero.</returns>
        /// <remarks>
        /// A process can be associated with only one console,
        /// so the function fails if the calling process already has a console.
        /// </remarks>
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern int AllocConsole();

        // http://msdn.microsoft.com/en-us/library/ms683150(VS.85).aspx
        /// <summary>
        /// Detaches the calling process from its console.
        /// </summary>
        /// <returns>nonzero if the function succeeds; otherwise, zero.</returns>
        /// <remarks>
        /// If the calling process is not already attached to a console,
        /// the error code returned is ERROR_INVALID_PARAMETER (87).
        /// </remarks>
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern int FreeConsole();
    }
}


