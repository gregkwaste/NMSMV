﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Model_Viewer;
using MVCore.Common;
using MVCore.GMDL;
using MVCore;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Data;
using System.Threading;
using System.Reflection;
using OpenTK;
using MVCore.Utils;

namespace WPFModelViewer
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    public partial class MainWindow : Window
    {
        private CGLControl glControl;
        private int itemCounter = 0;


        //Async Request Handler
        private WorkThreadDispacher workDispatcher = new WorkThreadDispacher();
        private System.Timers.Timer requestHandler = new System.Timers.Timer();
        private List<ThreadRequest> issuedRequests = new List<ThreadRequest>();
        
        private const int WmExitSizeMove = 0x232; //Custom Windows Messages
        private const int WmEnterSizeMove = 0x231;

        //Treeview Helpers
        TextBlock old_tb;
        TextBlock start_tb;
        Model init_drag;
        Model target_drag;

        

        public MainWindow()
        {
            InitializeComponent();

            //override_assemblies();

            //Override Window Title
            Title += " " + Util.getVersion();

            //Add request timer handler
            requestHandler.Interval = 10;
            requestHandler.Elapsed += queryRequests;
            requestHandler.Start();
            workDispatcher.Start();

            //Setup Logger
            Util.loggingSr = new StreamWriter("log.out");

            //SETUP THE CALLBACKS OF MVCORE
            CallBacks.updateStatus = Util.setStatus;
            CallBacks.openAnim = Util.loadAnimationFile;
            CallBacks.Log = Util.Log;
            CallBacks.issueRequestToGLControl = Util.sendRequest;

            //Toggle waiting to attach renderdoc
            //System.Threading.Thread.Sleep(10000);

            //Initialize Resource Manager
            RenderState.activeResMgr = new ResourceManager();
            

            //Generate CGLControl
            glControl = new CGLControl();
            
            Host.Child = glControl;

            //Improve performance on Treeview
            SceneTreeView.SetValue(VirtualizingStackPanel.IsVirtualizingProperty, true);
            SceneTreeView.SetValue(VirtualizingStackPanel.VirtualizationModeProperty, VirtualizationMode.Recycling);
            System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level = System.Diagnostics.SourceLevels.Error;
            System.Diagnostics.PresentationTraceSources.DataBindingSource.Listeners.Add(new BindingErrorTraceListener());
        }

        private void override_assemblies()
        {
            //BULLSHIT
            string execpath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            
            Assembly[] l = AppDomain.CurrentDomain.GetAssemblies();

            if (File.Exists(Path.Combine(execpath, "libMBIN.dll")))
            {
                //Unload existing libMBIN
                string ass_path = Path.Combine(execpath, "libMBIN.dll");
                Assembly.Load(@ass_path);
                //AppDomain.CurrentDomain.Load();
            }
        }

        //Open File
        private void OpenFile(string filename, bool testScene, int testSceneID)
        {
            CallBacks.Log("Importing " + filename);
            ThreadRequest req;
            
            //Pause renderer
            req = new ThreadRequest();
            req.type = THREAD_REQUEST_TYPE.GL_PAUSE_RENDER_REQUEST;
            req.arguments.Clear();
            
            //Send request
            glControl.issueRenderingRequest(ref req);
            glControl.waitForRenderingRequest(ref req);

            //Clear treeview
            Application.Current.Dispatcher.BeginInvoke((System.Action)(() =>
            {
                SceneTreeView.Items.Clear();
                
            }));

            
            RenderState.rootObject?.Dispose();

            if (testScene)
                glControl.addTestScene(testSceneID);
            else
                glControl.addScene(filename);
            
            //Populate 
            RenderState.rootObject.ID = itemCounter;
            Util.setStatus("Creating Treeview...");
            //Add to UI
            Application.Current.Dispatcher.BeginInvoke((System.Action)(() =>
            {
                SceneTreeView.Items.Add(RenderState.rootObject);
            }));
            Util.setStatus("Ready");

            //Generate Request for resuming rendering
            ThreadRequest req2 = new ThreadRequest();
            req2.type = THREAD_REQUEST_TYPE.GL_RESUME_RENDER_REQUEST;
            req2.arguments.Clear();

            glControl.issueRenderingRequest(ref req2);
            //glControl.waitForRenderingRequest(ref req2);

            //Bind new camera to the controls
            CameraOptionsView.Content = RenderState.activeCam.settings;
        }


        private void OpenFile(object sender, RoutedEventArgs e)
        {
            CallBacks.Log("Opening File");
            OpenFileDialog openFileDlg = new OpenFileDialog();
            openFileDlg.Filter = "SCENE Files (*.SCENE.MBIN, *.SCENE.EXML)|*.SCENE.MBIN;*.SCENE.EXML";
            var res = openFileDlg.ShowDialog();

            if (res == false)
                return;
            
            var filename = openFileDlg.FileName;
            OpenFile(filename, false, 0);
        }

        private void OpenFilePAK(object sender, RoutedEventArgs e)
        {
            //I need to make a custom window for previewing the entire list of SCENE Files from the PAK files
            List<string> paths = new List<string>();
            
            foreach(string path in RenderState.activeResMgr.NMSFileToArchiveMap.Keys)
            {
                if (path.EndsWith(".SCENE.MBIN"))
                    paths.Add(path);
            }
            paths.Sort();
                
            Window win = new Window();
            win.Title = "Select SCENE file from List";

            //Add Keyboard HAndler
            win.KeyUp += new KeyEventHandler(delegate (object s, KeyEventArgs ee)
            {
                if (ee.Key == Key.Escape)
                    win.Close();
            });

            //Add a default grid
            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition());
            grid.RowDefinitions.Add(new RowDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.RowDefinitions[1].Height = new GridLength(20);
            grid.ColumnDefinitions[0].Width = new GridLength(50);

            ListView lb = new ListView(); //Item listbox

            //Add items
            lb.ItemsSource = paths;
            lb.MouseDoubleClick += delegate
            {
                string selected = (string)lb.SelectedItem;
                win.Close();
                OpenFile(selected, false, 0);
            };

            CollectionView viewSource = (CollectionView)CollectionViewSource.GetDefaultView(lb.ItemsSource);
            

            //Search box
            TextBox searchBox = new TextBox();
            searchBox.TextChanged += delegate
            {
                viewSource.Refresh();
            };

            viewSource.Filter = (object obj) => {

                if (string.IsNullOrEmpty(searchBox.Text))
                    return true;
                else
                    return (obj as string).IndexOf(searchBox.Text, StringComparison.OrdinalIgnoreCase) >= 0;
            };

            //SearchBox Label
            TextBlock searchLabel = new TextBlock();
            searchLabel.Text = "Search:";

            //Setup Grid
            grid.Children.Add(lb);
            lb.SetValue(Grid.RowProperty, 0);
            Grid.SetColumnSpan(lb, 2);
            grid.Children.Add(searchBox);
            searchBox.SetValue(Grid.RowProperty, 1);
            searchBox.SetValue(Grid.ColumnProperty, 1);
            grid.Children.Add(searchLabel);
            searchLabel.SetValue(Grid.ColumnProperty, 0);
            searchLabel.SetValue(Grid.RowProperty, 1);

            win.Content = grid;
            win.Show();
        
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
                                case THREAD_REQUEST_TYPE.NEW_TEST_SCENE_REQUEST:
                                case THREAD_REQUEST_TYPE.NEW_SCENE_REQUEST:
                                    CallBacks.Log("Shouldn't be here");
                                    break;
                                case THREAD_REQUEST_TYPE.LOAD_NMS_ARCHIVES_REQUEST:
                                    
                                    //Enable Open File Functions
                                    Application.Current.Dispatcher.BeginInvoke((System.Action)(() =>
                                    {
                                        OpenFileHandle.IsEnabled = true;
                                        if (req.response == 0)
                                            OpenFilePAKHandle.IsEnabled = true;
                                    }));
                                    Util.setStatus("Ready");
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
            CallBacks.Log("Bye bye :'(");

            //Check if settings window is open and close it
            Close();
        }

        private void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            //Save Settings on exit
            SettingsForm.saveSettingsStatic();

            //Stop request timer
            requestHandler.Stop();
            
            //Send Terminate Rendering request to the rt_thread
            ThreadRequest req = new ThreadRequest();
            req.type = THREAD_REQUEST_TYPE.TERMINATE_REQUEST;
            glControl.engine.issueRenderingRequest(ref req);

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
            glControl.engine.resMgr.Cleanup();
            glControl.Dispose();

        }

        private void MainWindow_OnClosed(object sender, EventArgs e)
        {
            CallBacks.Log("Window Closed");
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
                        req.type = THREAD_REQUEST_TYPE.GL_RESIZE_REQUEST;
                        req.arguments.Add(glControl.Width);
                        req.arguments.Add(glControl.Height);

                        //Send request
                        glControl.engine.issueRenderingRequest(ref req);
                        issuedRequests.Add(req);

                        //Send Unpause rendering requenst
                        req = new ThreadRequest();
                        req.type = THREAD_REQUEST_TYPE.GL_RESUME_RENDER_REQUEST;

                        //Send request
                        glControl.issueRenderingRequest(ref req);
                        issuedRequests.Add(req);

                        //Mark as handled event
                        handled = true;
                        break;
                    }
                case WmEnterSizeMove:
                    {
                        //Send Unpause rendering requenst
                        ThreadRequest req = new ThreadRequest();
                        req.type = THREAD_REQUEST_TYPE.GL_PAUSE_RENDER_REQUEST;
                        req.arguments.Clear();

                        //Send request
                        glControl.engine.issueRenderingRequest(ref req);
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

            
            glControl.StartWorkThreads();
            
            //Check if the rt_thread is ready
            ThreadRequest req = new ThreadRequest();
            req.type = THREAD_REQUEST_TYPE.QUERY_GLCONTROL_STATUS_REQUEST;
            issuedRequests.Add(req);
            glControl.issueRenderingRequest(ref req);

            while(req.status != THREAD_REQUEST_STATUS.FINISHED)
                Thread.Sleep(10);

            
            //Load Settings
            SettingsForm.loadSettingsStatic();

            //Bind default camera to the controls
            CameraOptionsView.Content = RenderState.activeCam.settings;

            //Populate GLControl
            Scene scene = new Scene();
            scene.type = TYPES.MODEL;
            scene.name = "DEFAULT SCENE";

            //Add default scene to the resource manager
            RenderState.activeResMgr.GLScenes["DEFAULT_SCENE"] = scene;

            //Force rootobject
            RenderState.rootObject = scene;
            glControl.modelUpdateQueue.Enqueue(scene);
            glControl.engine.renderMgr.populate(scene);
            
            SceneTreeView.Items.Clear();
            SceneTreeView.Items.Add(scene);
            

            //Check if Temp folder exists
#if DEBUG
            if (!Directory.Exists("Temp")) Directory.CreateDirectory("Temp");
#endif
            //Set active Components
            Util.activeStatusStrip = StatusLabel;
            Util.activeControl = glControl;
            Util.activeWindow = this;

            //Bind Settings
            RenderViewOptionsControl.Content = RenderState.renderViewSettings;
            RenderOptionsControl.Content = RenderState.renderSettings;

            //Add event handlers to GUI elements
            //sliderzNear.ValueChanged += Sliders_OnValueChanged;
            //sliderzFar.ValueChanged += Sliders_OnValueChanged;
            //sliderFOV.ValueChanged += Sliders_OnValueChanged;
            //sliderMovementSpeed.ValueChanged += Sliders_OnValueChanged;
            //sliderMovementFactor.ValueChanged += Sliders_OnValueChanged;

            //Invoke the method in order to setup the control at startup
            Sliders_OnValueChanged(null, new RoutedPropertyChangedEventArgs<double>(0.0f,0.0f));
            Dispatcher.UnhandledException += OnDispatcherUnhandledException;


            //Disable Open File Functions
            OpenFileHandle.IsEnabled = false;
            OpenFilePAKHandle.IsEnabled = false;
            TestOptions.Visibility = Visibility.Hidden; //Hide the test options by default

#if (DEBUG)
            //Enable the Test options if it is a debug version
            TestOptions.Visibility = Visibility.Visible;
            setTestComponents();
#endif

            //Issue work request 
            ThreadRequest rq = new ThreadRequest();
            //rq.arguments.Add("NMSmanifest");
            rq.arguments.Add(Path.Combine(Path.Combine(RenderState.settings.GameDir,"GAMEDATA"), "PCBANKS"));
            rq.arguments.Add(RenderState.activeResMgr);
            rq.type = THREAD_REQUEST_TYPE.LOAD_NMS_ARCHIVES_REQUEST;
            workDispatcher.sendRequest(rq);

            issuedRequests.Add(rq);
        }

#if (DEBUG)

        void addToGrid(Control c, int row_id, int col_id)
        {
            if (row_id > 0)
                c.SetValue(Grid.RowProperty, row_id);
            if (col_id > 0)
                c.SetValue(Grid.ColumnProperty, col_id);
        }

        void addToGrid(TextBlock c, int row_id, int col_id)
        {
            if (row_id > 0)
                c.SetValue(Grid.RowProperty, row_id);
            if (col_id > 0)
                c.SetValue(Grid.ColumnProperty, col_id);
        }

        void setTestComponents()
        {
            //Add Components programmatically
            Grid g = new Grid();
            for (int i = 0; i < 4; i++)
            {
                RowDefinition rd = new RowDefinition();
                rd.Height = new GridLength(20.0);
                g.RowDefinitions.Add(rd);
            }

            for (int i = 0; i < 2; i++)
            {
                ColumnDefinition cd = new ColumnDefinition();
                cd.Width = new GridLength(50.0);
                g.ColumnDefinitions.Add(cd);
            }

            //Add Options
            //Test Option 1
            TextBlock tb = new TextBlock();
            addToGrid(tb, 0, 0);
            Slider sr = new Slider();
            addToGrid(sr, 0, 1);
            sr.Minimum = 0.0;
            sr.Maximum = 1.0;
            sr.ValueChanged += TestOpts_ValueChanged;
            g.Children.Add(tb);
            g.Children.Add(sr);

            //Test Option 2
            tb = new TextBlock();
            addToGrid(tb, 1, 0);
            sr = new Slider();
            addToGrid(sr, 1, 1);
            sr.Minimum = 0.0;
            sr.Maximum = 1.0;
            sr.ValueChanged += TestOpts_ValueChanged;
            g.Children.Add(tb);
            g.Children.Add(sr);

            //Test Option 3
            tb = new TextBlock();
            addToGrid(tb, 2, 0);
            sr = new Slider();
            addToGrid(sr, 2, 1);
            sr.Minimum = 0.0;
            sr.Maximum = 1000.0;
            sr.ValueChanged += TestOpts_ValueChanged;
            g.Children.Add(tb);
            g.Children.Add(sr);

            //Test Scene Button 1
            Button bt = new Button();
            bt.Content = "Test Scene 1";
            bt.SetValue(Grid.ColumnSpanProperty, 2);
            addToGrid(bt, 3, 0);
            bt.Click += new RoutedEventHandler(delegate (object s, RoutedEventArgs ee)
            {
                OpenFile("", true, 0);
            });
            g.Children.Add(bt);
            
            TestOptions.Content = g;
        }
#endif

        void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            string errorMessage = string.Format("An unhandled exception occurred: {0}", e.Exception.Message);
            Util.showError(errorMessage, "Error");
            e.Handled = true;
        }
        
        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            CallBacks.Log("Generating ProcGen Models");
            MessageBox.Show(Util.activeWindow, "HOOOOOOOOLA");
        }

        private void RegenPose_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(Util.activeWindow, "This button should set random values to the pose slider of the active locator object");
        }


        //Event Handlers

        private void Sliders_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            //glControl.engine.updateActiveCam(newSettings, RenderState.activeCam.Position, RenderState.activeCam.Direction);
            //glControl.engine.light_distance = (float) Math.Pow(1.25f, sliderlightDistance.Value) - 1.0f;
            //glControl.engine.light_intensity = (float) sliderLightIntensity.Value;
        }

        private void CameraResetPos(object sender, RoutedEventArgs e)
        {
            Camera.SetCameraPosition(ref RenderState.activeCam, new Vector3(0.0f));
            Camera.SetCameraDirection(ref RenderState.activeCam, new Quaternion(new Vector3(0.0f, (float)Math.PI / 2.0f, 0.0f)));
        }

        private void SceneResetRotation(object sender, RoutedEventArgs e)
        {
            RenderState.rotAngles = new Vector3(0.0f);
        }

        private void SceneTreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            Model node = (Model) SceneTreeView.SelectedItem;
            if (node != null)
            {
                //Swap activeModels
                Model prev_activeModel = glControl.activeModel;
                glControl.activeModel = node;

                //Set binding to objectinfo box
                ObjectInfoBox.Content = node;

                //Set Selected
                glControl.activeModel.selected = 1;

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
            SettingsForm settingsForm = new SettingsForm();
            settingsForm.KeyUp += new KeyEventHandler(delegate (object s, KeyEventArgs ee)
            {
                if (ee.Key == Key.Escape)
                {
                    settingsForm.Close();
                    settingsForm = null;
                }
            });
            settingsForm.Show();
        }

        private void SceneTreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //Todo Maybe add a timer to prevent the grabbing process from starting on single clicks


            if ((e.OriginalSource is TextBlock) && (SceneTreeView.SelectedItem != null))
            {
                Model node = (Model) SceneTreeView.SelectedItem;
                init_drag = node; //Set start model node
                //Common.CallBacks.Log("Grabbed " + node.Name);
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
                glControl.engine.issueRenderingRequest(ref req);

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
                        target_drag = (Model) s2.DataContext; //Set current target drag
                        //Common.CallBacks.Log("Cursor Over " + target_drag.Name);
                    }

                    old_tb = tb;
                }
    
            }
        
        }

        //Updates textBox values on enter
        private void TextBox_KeyUp(object sender, KeyEventArgs e)
        {
            TextBox tb = (TextBox)sender;
            DependencyProperty prop = TextBox.TextProperty;

            if (e.Key == Key.Enter)
            {
                BindingExpression bind = BindingOperations.GetBindingExpression(tb, prop);
                if (bind != null) bind.UpdateSource();
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

        private void UpdateLibMBIN_Click(object sender, RoutedEventArgs e)
        {
            if (HTMLUtils.fetchLibMBINDLL())
            {
                System.Diagnostics.Process.Start(System.Windows.Forms.Application.ExecutablePath);
                Close();
            }
        }

#if (DEBUG)
        private void TestOpts_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Slider s = (Slider) sender;
            string name = s.Name;
            
            switch (name)
            {
                case "TestOpt1":
                    RenderState.renderSettings.testOpt1 = (float) s.Value;
                    break;
                case "TestOpt2":
                    RenderState.renderSettings.testOpt2 = (float)s.Value;
                    break;
                case "TestOpt3":
                    RenderState.renderSettings.testOpt3 = (float)s.Value;
                    break;
            }
        }
#endif
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


