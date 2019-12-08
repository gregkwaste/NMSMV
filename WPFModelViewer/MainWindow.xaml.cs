using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
using System.Windows.Interop;
using System.Windows.Input;

namespace WPFModelViewer
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

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

        private const int WmExitSizeMove = 0x232; //Custom Windows Messages
        private const int WmEnterSizeMove = 0x231;

        //Treeview Helpers
        TextBlock old_tb;
        TextBlock start_tb;
        model init_drag;
        model target_drag;

        public MainWindow()
        {
            InitializeComponent();

            //Override Window Title
            Title += " " + Util.Version;

            //Add request timer handler
            requestHandler.Interval = 10;
            requestHandler.Elapsed += queryRequests;
            requestHandler.Start();

            //Generate CGLControl
            glControl = new CGLControl();
            RenderState.activeResMgr = glControl.resMgr;
            
            Host.Child = glControl;

            //Improve performance on Treeview
            SceneTreeView.SetValue(VirtualizingStackPanel.IsVirtualizingProperty, true);
            SceneTreeView.SetValue(VirtualizingStackPanel.VirtualizationModeProperty, VirtualizationMode.Recycling);
            System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level = System.Diagnostics.SourceLevels.Error;
            System.Diagnostics.PresentationTraceSources.DataBindingSource.Listeners.Add(new BindingErrorTraceListener());
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


            ThreadRequest req;


            //Pause renderer
            req = new ThreadRequest();
            req.type = THREAD_REQUEST_TYPE.PAUSE_RENDER_REQUEST;
            req.arguments.Clear();

            //Send request
            glControl.issueRequest(ref req);

            while (req.status != THREAD_REQUEST_STATUS.FINISHED)
                System.Threading.Thread.Sleep(10);

            //Clear treeview
            Application.Current.Dispatcher.BeginInvoke((Action)(() =>
            {
                SceneTreeView.Items.Clear();
                glControl.rootObject.Dispose();
            }));
            
            //Generate Request for rendering thread
            req = new ThreadRequest();
            req.type = THREAD_REQUEST_TYPE.NEW_SCENE_REQUEST;
            req.arguments.Clear();
            req.arguments.Add(filename);

            glControl.issueRequest(ref req);
            issuedRequests.Add(req);

            //Generate Request for resuming rendering
            req = new ThreadRequest();
            req.type = THREAD_REQUEST_TYPE.RESUME_RENDER_REQUEST;
            req.arguments.Clear();
            
            glControl.issueRequest(ref req);
            issuedRequests.Add(req);

        }

        //Request Handler
        private void queryRequests(object sender, System.Timers.ElapsedEventArgs e)
        {
            int i = 0;
            lock (issuedRequests) {
                while ( i < issuedRequests.Count)
                {
                    ThreadRequest req = issuedRequests[i];
                    lock (req)
                    {
                        if (req.status == THREAD_REQUEST_STATUS.FINISHED)
                        {
                            switch (req.type)
                            {
                                case THREAD_REQUEST_TYPE.NEW_SCENE_REQUEST:
                                    glControl.rootObject.ID = itemCounter;
                                    Util.setStatus("Creating Treeview...");
                                    //Add to UI
                                    Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                                    {
                                        SceneTreeView.Items.Add(glControl.rootObject);
                                    }));
                                    Util.setStatus("Ready");
                                    break;
                                case THREAD_REQUEST_TYPE.COMPILE_SHADER_REQUEST:
                                    //Add Shader to resource manager
                                    GLSLHelper.GLSLShaderConfig shader_conf = (GLSLShaderConfig)req.arguments[0];
                                    RenderState.activeResMgr.GLShaders[shader_conf.shader_type] = shader_conf;
                                    File.WriteAllText("shader_compilation_" + Enum.GetName(typeof(SHADER_TYPE), shader_conf.shader_type) + ".log", shader_conf.log);
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
            glControl.issueRequest(ref req);

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


        private IntPtr HwndMessageHook(IntPtr wnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WmExitSizeMove:
                    {
                        //Send resizing request
                        ThreadRequest req = new ThreadRequest();
                        req.type = THREAD_REQUEST_TYPE.RESIZE_REQUEST;
                        req.arguments.Clear();
                        req.arguments.Add(glControl.Width);
                        req.arguments.Add(glControl.Height);

                        //Send request
                        glControl.issueRequest(ref req);
                        issuedRequests.Add(req);

                        //Send Unpause rendering requenst
                        req = new ThreadRequest();
                        req.type = THREAD_REQUEST_TYPE.RESUME_RENDER_REQUEST;
                        req.arguments.Clear();

                        //Send request
                        glControl.issueRequest(ref req);
                        issuedRequests.Add(req);

                        //Mark as handled event
                        handled = true;
                        break;
                    }
                case WmEnterSizeMove:
                    {
                        //Send Unpause rendering requenst
                        ThreadRequest req = new ThreadRequest();
                        req.type = THREAD_REQUEST_TYPE.PAUSE_RENDER_REQUEST;
                        req.arguments.Clear();

                        //Send request
                        glControl.issueRequest(ref req);
                        issuedRequests.Add(req);

                        //Mark as handled event
                        handled = true;
                        break;
                    }
            }
            return IntPtr.Zero;
        }


        //Do stuff once the GUI is ready
        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            //Add Hook for catching the end of resizing event in WPF
            var helper = new WindowInteropHelper(this);
            if (helper.Handle != null)
            {
                var source = HwndSource.FromHwnd(helper.Handle);
                if (source != null)
                    source.AddHook(HwndMessageHook);
            }
            
            //OVERRIDE SETTINGS
            //FileUtils.dirpath = "I:\\SteamLibrary1\\steamapps\\common\\No Man's Sky\\GAMEDATA\\PCBANKS";

            //Load Settings
            settings = SettingsForm.loadSettingsStatic();
            SettingsForm.saveSettingsToEnv(settings);

            //Setup Logger
            Util.loggingSr = new StreamWriter("log.out");

            //Check if the rt_thread is ready
            ThreadRequest req = new ThreadRequest();
            req.type = THREAD_REQUEST_TYPE.QUERY_GLCONTROL_STATUS_REQUEST;
            issuedRequests.Add(req);
            glControl.issueRequest(ref req);

            while(req.status != THREAD_REQUEST_STATUS.FINISHED)
                System.Threading.Thread.Sleep(10);
            
            //Populate GLControl
            scene scene = new scene();
            scene.type = TYPES.MODEL;
            scene.name = "DEFAULT SCENE";

            //Add default scene to the resource manager
            RenderState.activeResMgr.GLScenes["DEFAULT_SCENE"] = scene;

            //Force rootobject
            glControl.rootObject = scene;
            glControl.renderMgr.populate(scene);

            SceneTreeView.Items.Clear();
            SceneTreeView.Items.Add(scene);
            

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

        private void PlayStop_Click(object sender, RoutedEventArgs e)
        {
            glControl.toggleAnimation();
        }

        private void loadAnim(object sender, RoutedEventArgs e)
        {
            AnimationSelectForm aform = new AnimationSelectForm(glControl.animScenes);
            aform.Show();
        }

        private void RegenPose_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("This button should set random values to the pose slider of the active locator object");
        }


        //Event Handlers



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
            glControl.updateActiveCam(new OpenTK.Vector3(0.0f, 0.0f, 0.0f));
            glControl.updateControlRotation(0.0f, 0.0f);
        }

        private void SceneTreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            model node = (model) SceneTreeView.SelectedItem;
            if (node != null)
            {
                //Swap activeModels
                prev_activeModel = activeModel;
                activeModel = node;

                //Set binding to objectinfo box
                ObjectInfoBox.Content = node;

                //Set Selected
                activeModel.selected = 1;

                //Deselect Previews model
                if (prev_activeModel != null)
                    prev_activeModel.selected = 0;

                return;
            }
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

        private void SceneTreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //Todo Maybe add a timer to prevent the grabbing process from starting on single clicks


            if ((e.OriginalSource is TextBlock) && (SceneTreeView.SelectedItem != null))
            {
                model node = (model) SceneTreeView.SelectedItem;
                init_drag = node; //Set start model node
                //Console.WriteLine("Grabbed " + node.Name);
                var tv = sender as TreeView;

                //Fetch textblock
                old_tb = (TextBlock) e.OriginalSource;
                start_tb = (TextBlock) e.OriginalSource;

                DragDrop.DoDragDrop(tv, node, DragDropEffects.Move);
            }

        }

        private void SceneTreeView_Drop(object sender, DragEventArgs e)
        {
            var tv = sender as TreeView;
            IInputElement target = SceneTreeView.InputHitTest(e.GetPosition(SceneTreeView));
            if (old_tb != start_tb)
                old_tb.Background = null;
            TextBlock tb = (TextBlock) target;

            if (tb == null || target_drag == null)
                return;

            if (init_drag != target_drag)
            {
                //Remove child from parent model node
                ThreadRequest req = new ThreadRequest();
                req.type = THREAD_REQUEST_TYPE.CHANGE_MODEL_PARENT_REQUEST;
                req.arguments.Add(init_drag);
                req.arguments.Add(target_drag);
                glControl.issueRequest(ref req);

                /*
                lock (init_drag)
                {
                    if (init_drag.parent != null)
                    {
                        lock (init_drag.parent.Children)
                        {
                            init_drag.parent.Children.Remove(init_drag);
                        }
                    }
                    
                    //Add to target node
                    init_drag.parent = target_drag;
                }

                lock (target_drag.Children)
                {
                    target_drag.Children.Add(init_drag);
                }
                */

                init_drag = null;
                target_drag = null;
                e.Handled = true;
            }

        }

        private void SceneTreeView_GiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            // update the position of the visual feedback item
            Win32Point w32Mouse = new Win32Point();
            GetCursorPos(ref w32Mouse);
            var pfs = SceneTreeView.PointFromScreen(new Point(w32Mouse.X, w32Mouse.Y));
            var tb = SceneTreeView.InputHitTest(pfs) as TextBlock;
            
            if (tb != null)
            {

                if (tb != old_tb) {

                    if (old_tb != start_tb)
                        old_tb.Background = null;

                    if (tb != start_tb)
                    {
                        tb.Background = System.Windows.Media.Brushes.DarkGray;
                        var s1 = System.Windows.Media.VisualTreeHelper.GetParent(tb);
                        StackPanel s2 = (StackPanel)System.Windows.Media.VisualTreeHelper.GetParent(s1);
                        target_drag = (model) s2.DataContext; //Set current target drag
                        //Console.WriteLine("Cursor Over " + target_drag.Name);
                    }

                    old_tb = tb;
                }
    
            }
        
        }



        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(ref Win32Point pt);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Win32Point
        {
            public Int32 X;
            public Int32 Y;
        };
    }


    public class BindingErrorTraceListener : System.Diagnostics.TraceListener
    {
        public override void Write(string message)
        {
            System.Diagnostics.Trace.WriteLine(string.Format("==[Write]{0}==", message));
        }

        public override void WriteLine(string message)
        {
            System.Diagnostics.Trace.WriteLine(string.Format("==[WriteLine]{0}==", message));
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


