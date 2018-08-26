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
using MVCore;
using MVCore.Common;
using MVCore.GMDL;
using OpenTK.Graphics.OpenGL;
using OpenTK;

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
        public MainWindow()
        {
            InitializeComponent();

            //Generate CGLControl
            glControl = new CGLControl();

            glControl.Update();
            glControl.MakeCurrent();

            //Compile and allocate shaders
            compileShaders();

            //Load font
            glControl.font = setupFont();
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
            openFileDlg.Filter = "SCENE Files (*.SCENE.MBIN)|*.SCENE.MBIN;";
            var res = openFileDlg.ShowDialog();

            if (res == false)
                return;
            
            var filename = openFileDlg.FileName;
            Console.WriteLine("Importing " + filename);
            
            //Setup GLControl with new scene
            glControl.addRootScene(filename);

            if (glControl.rootObject != null)
            {
                glControl.rootObject.ID = itemCounter;

                Util.setStatus("Creating Nodes...");
                SceneTreeView.Items.Clear();
                ModelNode scn_node = new ModelNode(glControl.rootObject);
                traverse_oblist(glControl.rootObject, scn_node);
                SceneTreeView.Items.Add(scn_node);
                GC.Collect();
            }
            
            //Cleanup resource manager
            Util.setStatus("Ready");

        }

        //Close Form

        private void FormClose(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Bye bye :'(");
            this.Close();
        }

        private void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
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

            //MVCore Init
            //Load NMSTemplate Enumerators
            Palettes.loadNMSEnums();

            //Populate GLControl
            scene scene = new scene();
            scene.type = TYPES.SCENE;
            scene.name = "DEFAULT SCENE";
            scene.shader_programs = new int[] { RenderState.activeResMgr.shader_programs[1],
                RenderState.activeResMgr.shader_programs[5],
                RenderState.activeResMgr.shader_programs[6]};

            //Add Test Cube
            Collision cube = new Collision();
            cube.batchcount = 600;
            cube.main_Vao = (new MVCore.Primitives.Sphere(new Vector3(0.0f,0.0f,0.0f), 1.0f)).getVAO();
            //cube.main_Vao = (new MVCore.Primitives.Box(1.0f, 1.0f, 1.0f)).getVAO();
            //Remove that after implemented all the different collision types
            cube.shader_programs = new int[] { RenderState.activeResMgr.shader_programs[0],
                RenderState.activeResMgr.shader_programs[5],
                RenderState.activeResMgr.shader_programs[6]}; //Use Mesh program for collisions
            cube.name = "TEST_CUBE";
            cube.collisionType = COLLISIONTYPES.BOX;
            ModelNode cube_node = new ModelNode(cube);
            cube_node.IsChecked = true;
            scene.children.Add(cube);


            glControl.rootObject = scene;
            ModelNode node = new ModelNode(scene);
            node.Children.Add(cube_node);
            node.IsChecked = true;

            //Improve performance on Treeview
            SceneTreeView.SetValue(VirtualizingStackPanel.IsVirtualizingProperty, true);
            SceneTreeView.SetValue(VirtualizingStackPanel.VirtualizationModeProperty, VirtualizationMode.Recycling);
            SceneTreeView.Items.Add(node);

            //Check if Temp folder exists
#if DEBUG
            if (!Directory.Exists("Temp")) Directory.CreateDirectory("Temp");
#endif
            //Set active Components
            Util.activeStatusStrip = StatusLabel;
            Util.activeControl = glControl;

            //FILL THE CALLBACKS OF MVCORE
            MVCore.Common.CallBacks.updateStatus = Util.setStatus;
            MVCore.Common.CallBacks.openAnim = Util.loadAnimationFile;
            MVCore.Common.CallBacks.Log = Util.Log;

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


        //GLPreparation
        private void compileShaders()
        {
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

            //Populate shader list
            RenderState.activeResMgr = glControl.resMgr;
            RenderState.activeResMgr.shader_programs = new int[11];
            string vvs, ggs, ffs;

            string log = "";
            int vertex_shader_ob, fragment_shader_ob;

            //Geometry Shader
            //Compile Object Shaders
            vvs = GLSL_Preprocessor.Parser("Shaders/Simple_VSEmpty.glsl");
            ggs = GLSL_Preprocessor.Parser("Shaders/Simple_GS.glsl");
            ffs = GLSL_Preprocessor.Parser("Shaders/Simple_FSEmpty.glsl");

            GLShaderHelper.CreateShaders(vvs, ffs, ggs, "", "", out vertex_shader_ob,
                    out fragment_shader_ob, out RenderState.activeResMgr.shader_programs[5], ref log);

            //Picking Shaders
            GLShaderHelper.CreateShaders(ProjProperties.Resources.pick_vert, ProjProperties.Resources.pick_frag, "", "", "", out vertex_shader_ob,
                out fragment_shader_ob, out RenderState.activeResMgr.shader_programs[6], ref log);

            //Main Shader
            vvs = GLSL_Preprocessor.Parser("Shaders/Simple_VS.glsl");
            ffs = GLSL_Preprocessor.Parser("Shaders/Simple_FS.glsl");
            GLShaderHelper.CreateShaders(vvs, ffs, "", "", "", out vertex_shader_ob,
                    out fragment_shader_ob, out RenderState.activeResMgr.shader_programs[0], ref log);

            //Texture Mixing Shaders
            vvs = GLSL_Preprocessor.Parser("Shaders/pass_VS.glsl");
            ffs = GLSL_Preprocessor.Parser("Shaders/pass_FS.glsl");
            GLShaderHelper.CreateShaders(vvs, ffs, "", "", "", out vertex_shader_ob,
                    out fragment_shader_ob, out RenderState.activeResMgr.shader_programs[3], ref log);

            //GBuffer Shaders
            vvs = GLSL_Preprocessor.Parser("Shaders/Gbuffer_VS.glsl");
            ffs = GLSL_Preprocessor.Parser("Shaders/Gbuffer_FS.glsl");
            GLShaderHelper.CreateShaders(vvs, ffs, "", "", "", out vertex_shader_ob,
                    out fragment_shader_ob, out RenderState.activeResMgr.shader_programs[9], ref log);

            //Decal Shaders
            vvs = GLSL_Preprocessor.Parser("Shaders/decal_VS.glsl");
            ffs = GLSL_Preprocessor.Parser("Shaders/Decal_FS.glsl");
            GLShaderHelper.CreateShaders(vvs, ffs, "", "", "", out vertex_shader_ob,
                    out fragment_shader_ob, out RenderState.activeResMgr.shader_programs[10], ref log);

            //Locator Shaders
            GLShaderHelper.CreateShaders(ProjProperties.Resources.locator_vert, ProjProperties.Resources.locator_frag, "", "", "", out vertex_shader_ob,
                out fragment_shader_ob, out RenderState.activeResMgr.shader_programs[1], ref log);

            //Joint Shaders
            GLShaderHelper.CreateShaders(ProjProperties.Resources.joint_vert, ProjProperties.Resources.joint_frag, "", "", "", out vertex_shader_ob,
                out fragment_shader_ob, out RenderState.activeResMgr.shader_programs[2], ref log);

            //Text Shaders
            GLShaderHelper.CreateShaders(ProjProperties.Resources.text_vert, ProjProperties.Resources.text_frag, "", "", "", out vertex_shader_ob,
                out fragment_shader_ob, out RenderState.activeResMgr.shader_programs[4], ref log);

            //Light Shaders
            GLShaderHelper.CreateShaders(ProjProperties.Resources.light_vert, ProjProperties.Resources.light_frag, "", "", "", out vertex_shader_ob,
                out fragment_shader_ob, out RenderState.activeResMgr.shader_programs[7], ref log);

            //Camera Shaders
            GLShaderHelper.CreateShaders(ProjProperties.Resources.camera_vert, ProjProperties.Resources.camera_frag, "", "", "", out vertex_shader_ob,
                out fragment_shader_ob, out RenderState.activeResMgr.shader_programs[8], ref log);


            //Save log
            StreamWriter sr = new StreamWriter("shader_compilation_log.out");
            sr.Write(log);
            sr.Close();

        }


        private FontGL setupFont()
        {
            FontGL font = new FontGL();

            //Test BMP Image Class
            //BMPImage bm = new BMPImage("courier.bmp");

            //Create font to memory
            MemoryStream ms = FontGL.createFont();
            BMPImage bm = new BMPImage(ms);

            //Testing some inits
            font.initFromImage(bm);
            font.tex = bm.GLid;
            font.program = RenderState.activeResMgr.shader_programs[4];

            //Set default settings
            float scale = 0.75f;
            font.space = scale * 0.20f;
            font.width = scale * 0.20f;
            font.height = scale * 0.35f;

            return font;
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
            glControl.movement_speed = (int) Math.Floor(Math.Pow(2.0f, sliderMovementSpeed.Value));
            glControl.light_distance = (float) Math.Pow(1.25f, sliderlightDistance.Value) - 1.0f;
            glControl.light_intensity = (float) sliderLightIntensity.Value;
        }

        private void CameraResetPos(object sender, RoutedEventArgs e)
        {
            glControl.updateActiveCamPos(0.0f, 0.0f, 0.0f);
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
                //Set Selected
                activeModel.selected = 1;
                //Object Name
                activeObjectName.Text = node.mdl.name;
                //Object Type
                activeObjectType.Text = ((TYPES) node.mdl.type).ToString();
                //Material Name
                if (node.mdl.material != null)
                    activeObjectMatName.Text = node.mdl.material.name;
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
