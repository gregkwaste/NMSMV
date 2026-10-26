using libMBIN.NMS.Toolkit;
using Microsoft.Win32;
using Model_Viewer;
using MVCore;
using MVCore.Common;
using MVCore.GMDL;
using MVCore.Utils;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace WPFModelViewer
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    public partial class MainWindow : Window
    {
        private CGLControl glControl;
        private System.Windows.Threading.DispatcherTimer statsTimer;
        private int itemCounter = 0;

        //Async Request Handler
        private System.Timers.Timer requestHandler = new System.Timers.Timer();

        //Treeview Helpers
        TextBlock old_tb;
        TextBlock start_tb;
        Model init_drag;
        Model target_drag;
        //Drag candidate from the last left press, the drag only starts once the mouse moves far enough
        Model drag_candidate;
        TextBlock drag_candidate_tb;
        Point drag_start;



        public MainWindow()
        {
            InitializeComponent();

            //override_assemblies();

            //Override Window Title
            Title += " " + Util.getVersion();

            //SETUP THE CALLBACKS OF MVCORE
            CallBacks.updateStatus = Util.setStatus;
            CallBacks.openAnim = Util.loadAnimationFile;
            CallBacks.Log = Util.Log;
            CallBacks.issueRequestToGLControl = Util.sendRequest;
            CallBacks.showError = Util.showError;
            CallBacks.getResource = name => Properties.Resources.ResourceManager.GetObject(name, Properties.Resources.Culture);

            //Toggle waiting to attach renderdoc
            //System.Threading.Thread.Sleep(10000);

            //The resource manager is created by the engine on the first render (MainWindow_OnLoaded waits for it)

            var settings = new GLWpfControlSettings()
            {
                MajorVersion = 4,
                MinorVersion = 6,
                UseDeviceDpi = false,
#if DEBUG
                ContextFlags = ContextFlags.Debug,
#endif
                //Compatibility profile: allows wide lines for the helpers. Forward compatible only applies to core
                Profile = ContextProfile.Compatability
            };

            Host.Start(settings);

            //Generate CGLControl
            glControl = new CGLControl(Host);

            //Stats overlay refresh. A few times a second is enough to read and keeps string work off the frame loop
            statsTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            statsTimer.Tick += updateStatsOverlay;
            statsTimer.Start();


            //Load Settings
            //OVERRIDE SETTINGS
            //FileUtils.dirpath = "I:\\SteamLibrary1\\steamapps\\common\\No Man's Sky\\GAMEDATA\\PCBANKS";

            SettingsForm.loadSettingsStatic();

            //Create SceneTreeView context menu

            //Improve performance on Treeview
            SceneTreeView.SetValue(VirtualizingStackPanel.IsVirtualizingProperty, true);
            SceneTreeView.SetValue(VirtualizingStackPanel.VirtualizationModeProperty, VirtualizationMode.Recycling);
            System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level = System.Diagnostics.SourceLevels.Error;
            System.Diagnostics.PresentationTraceSources.DataBindingSource.Listeners.Add(new BindingErrorTraceListener());

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
            glControl.engine.handleRequests();

            try
            {
                //Clear treeview
                SceneTreeView.Items.Clear();
                RenderState.rootObject?.Dispose();

                if (testScene)
                    glControl.addTestScene(testSceneID);
                else
                    glControl.addScene(filename);

                glControl.engine.handleRequests();

                if (RenderState.rootObject != null)
                {
                    //Populate 
                    RenderState.rootObject.ID = 0;
                    Util.setStatus("Creating Treeview...");
                    //Add to UI
                    SceneTreeView.Items.Add(RenderState.rootObject);
                }
                Util.setStatus("Ready");
            }
            finally
            {
                //Always resume rendering, even if the import failed
                ThreadRequest req2 = new ThreadRequest();
                req2.type = THREAD_REQUEST_TYPE.GL_RESUME_RENDER_REQUEST;
                req2.arguments.Clear();

                glControl.issueRenderingRequest(ref req2);
                glControl.engine.handleRequests();
            }

            //Bind new camera to the controls
            CameraOptionsView.Content = RenderState.activeCam.settings;
        }


        private void OpenFile(object sender, RoutedEventArgs e)
        {
            CallBacks.Log("Opening File");
            OpenFileDialog openFileDlg = new OpenFileDialog();
            openFileDlg.Filter = "SCENE Files (*.SCENE.MBIN, *.SCENE.MXML, *.SCENE.EXML)|*.SCENE.MBIN;*.SCENE.MXML;*.SCENE.EXML";
            var res = openFileDlg.ShowDialog();

            if (res == false)
                return;

            var filename = openFileDlg.FileName;
            OpenFile(filename, false, 0);
        }

        //Tool windows open once: asking again brings the open one to the front
        private Window sceneListWindow;
        private Window aboutWindow;
        private Window settingsWindow;

        private bool activateIfOpen(Window w)
        {
            if (w == null)
                return false;
            if (w.WindowState == System.Windows.WindowState.Minimized)
                w.WindowState = System.Windows.WindowState.Normal;
            w.Activate();
            return true;
        }

        private void OpenFilePAK(object sender, RoutedEventArgs e)
        {
            if (activateIfOpen(sceneListWindow))
                return;

            //I need to make a custom window for previewing the entire list of SCENE Files from the PAK files
            List<string> paths = new List<string>();

            foreach (string path in RenderState.activeResMgr.NMSFileToArchiveMap.Keys)
            {
                if (path.EndsWith(".SCENE.MBIN"))
                    paths.Add(path);
            }
            paths.Sort();

            Window win = new Window();
            win.Title = "Select SCENE file from List";
            win.Owner = this;
            sceneListWindow = win;
            win.Closed += delegate { sceneListWindow = null; };

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
            statsTimer?.Stop();

            //Stop rendering first, then free the GL resources (a queued terminate request would only run on the
            //next frame, after the resources were already gone)
            glControl.shutdown();
        }

        private void updateStatsOverlay(object sender, EventArgs e)
        {
            bool show = RenderState.renderViewSettings?.RenderInfo ?? false;
            StatsOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (!show)
                return;

            float fps = RenderStats.fpsCount;
            StatsText.Text = $"FPS: {fps:0.0} ({(fps > 0.0f ? 1000.0f / fps : 0.0f):0.00} ms)\n" +
                             $"Occluded: {RenderStats.occludedNum}\n" +
                             $"Vertices: {RenderStats.vertNum:N0}\n" +
                             $"Triangles: {RenderStats.trisNum:N0}\n" +
                             $"Textures: {RenderStats.texturesNum}\n" +
                             $"Controller: {RenderState.activeGamepad?.getName() ?? "None"}";
        }

        private void MainWindow_OnClosed(object sender, EventArgs e)
        {
            statsTimer?.Stop();
            CallBacks.Log("Window Closed");
            Util.closeLog();
        }

        //Do stuff once the GUI is ready
        private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            //Keep the open functions off until the archives are loaded
            OpenFileHandle.IsEnabled = false;
            OpenFilePAKHandle.IsEnabled = false;

            //Everything below needs the engine, which is set up on the first render. Don't rely on that
            //render happening before Loaded
            await glControl.ready.Task;

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
            //sliderFOV.ValueChanged += Sliders_OnValueChanged;
            //sliderMovementSpeed.ValueChanged += Sliders_OnValueChanged;
            //sliderMovementFactor.ValueChanged += Sliders_OnValueChanged;

            //Invoke the method in order to setup the control at startup
            Sliders_OnValueChanged(null, new RoutedPropertyChangedEventArgs<double>(0.0f, 0.0f));

            TestOptions.Visibility = Visibility.Hidden; //Hide the test options by default

#if (DEBUG)
            //Enable the Test options if it is a debug version
            TestOptions.Visibility = Visibility.Visible;
            setTestComponents();
#endif

            //Load the NMS archives in the background, then enable the open file functions
            string pakDir = Path.Combine(RenderState.settings.GameDir ?? "", "GAMEDATA", "PCBANKS");
            ResourceManager resMgr = RenderState.activeResMgr;
            int archiveStatus = -1;
            try
            {
                await Task.Run(() => NMSUtils.loadNMSArchives(pakDir, ref resMgr, ref archiveStatus));
            }
            catch (Exception ex)
            {
                Util.showError("Failed to load the NMS archives: " + ex.Message, "Error");
            }

            OpenFileHandle.IsEnabled = true;
            if (archiveStatus == 0)
                OpenFilePAKHandle.IsEnabled = true;

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

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            NMSUtils.ProcGen();
        }

        private void SelectLOD_OnClick(object sender, RoutedEventArgs e)
        {
            //The first entry is Auto, which picks the level by camera distance
            RenderState.forcedLOD = Math.Max(tb_LODLevel.SelectedIndex, 0) - 1;
            RenderState.instancesDirty = true;
        }

        private void RegenTextures_Click(object sender, RoutedEventArgs e)
        {
            //Mixing needs the GL context, so it runs on the next frame
            ThreadRequest req = new ThreadRequest();
            req.type = THREAD_REQUEST_TYPE.REGEN_PROC_TEXTURES_REQUEST;
            glControl.engine.issueRenderingRequest(ref req);
        }

        private void RegenPose_Click(object sender, RoutedEventArgs e)
        {
            if (RenderState.rootObject != null)
                randomisePoses(RenderState.rootObject);
        }

        private static void randomisePoses(Model m)
        {
            if (m.animPoseComponentID >= 0 && m._components[m.animPoseComponentID] is AnimPoseComponent apc)
                apc.randomise();

            foreach (Model child in m.Children)
                randomisePoses(child);
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
            //Frame the loaded model, or go back to the origin when there is none
            if (RenderState.rootObject != null)
                RenderState.activeCam.frameScene(RenderState.rootObject, true);
            else
                Camera.SetCameraPosition(ref RenderState.activeCam, new Vector3(0.0f));
        }

        private void SceneResetRotation(object sender, RoutedEventArgs e)
        {
            RenderState.rotAngles = new Vector3(0.0f);
        }

        private void SceneTreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            Model node = (Model)SceneTreeView.SelectedItem;
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
                RenderState.instancesDirty = true;

                return;
            }
        }

        private void showAboutDialog(object sender, RoutedEventArgs e)
        {
            if (activateIfOpen(aboutWindow))
                return;

            aboutWindow = new AboutDialog();
            aboutWindow.Owner = this;
            aboutWindow.Closed += delegate { aboutWindow = null; };
            aboutWindow.Show();
        }

        private void showSettingsDialog(object sender, RoutedEventArgs e)
        {
            if (activateIfOpen(settingsWindow))
                return;

            SettingsForm settingsForm = new SettingsForm();
            settingsForm.Owner = this;
            settingsForm.KeyUp += new KeyEventHandler(delegate (object s, KeyEventArgs ee)
            {
                if (ee.Key == Key.Escape)
                    settingsForm.Close();
            });
            settingsWindow = settingsForm;
            settingsForm.Closed += delegate { settingsWindow = null; };
            settingsForm.Show();
        }

        private void SceneTreeView_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            //Only remember what was pressed. The node is the one under the cursor, not SelectedItem, which
            //still holds the previous selection at this point. Starting the drag here made every click a drag
            //of the old node, dropped on whatever node the button was released over
            drag_candidate_tb = e.OriginalSource as TextBlock;
            drag_candidate = drag_candidate_tb?.DataContext as Model;
            drag_start = e.GetPosition(SceneTreeView);
        }

        private void SceneTreeView_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            drag_candidate = null;
            drag_candidate_tb = null;
        }

        private void SceneTreeView_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (drag_candidate == null)
                return;

            if (e.LeftButton != MouseButtonState.Pressed)
            {
                drag_candidate = null;
                drag_candidate_tb = null;
                return;
            }

            Vector delta = e.GetPosition(SceneTreeView) - drag_start;
            if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            {
                Model node = drag_candidate;
                init_drag = node; //Set start model node
                var tv = sender as TreeView;

                //Fetch textblock
                old_tb = drag_candidate_tb;
                start_tb = drag_candidate_tb;
                drag_candidate = null;
                drag_candidate_tb = null;

                DragDrop.DoDragDrop(tv, node, DragDropEffects.Move);

                //The drag is over once DoDragDrop returns, dropped or cancelled
                if (old_tb != null && old_tb != start_tb)
                    old_tb.Background = null;
                init_drag = null;
                target_drag = null;
                old_tb = null;
                start_tb = null;
            }

        }

        private void SceneTreeView_PreviewMouseRightButtonDown(object sender, MouseEventArgs e)
        {
            var item = e.Source as Model;

            var source = e.OriginalSource as DependencyObject;
            // Traverse up the visual tree to find the TreeViewItem
            while (source != null && !(source is TreeViewItem))
            {
                source = VisualTreeHelper.GetParent(source);
            }

            if (source != null)
            {
                ((TreeViewItem)source).IsSelected = true;
                e.Handled = true;
            }
        }

        private void SceneTreeView_Drop(object sender, DragEventArgs e)
        {
            //Only nodes dragged from this tree can be dropped. The target is the node under the cursor now
            Model source = init_drag;
            Model target = (SceneTreeView.InputHitTest(e.GetPosition(SceneTreeView)) as FrameworkElement)?.DataContext as Model;

            if (source == null || target == null || !canReparent(source, target))
                return;

            {
                //Remove child from parent model node
                ThreadRequest req = new ThreadRequest();
                req.type = THREAD_REQUEST_TYPE.CHANGE_MODEL_PARENT_REQUEST;
                req.arguments.Add(source);
                req.arguments.Add(target);
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

        //A node can't move under itself or one of its own children, and scene roots stay where they are
        private static bool canReparent(Model source, Model target)
        {
            if (source == target || source.parent == null || source.parent == target)
                return false;

            for (Model m = target; m != null; m = m.parent)
            {
                if (m == source)
                    return false;
            }

            return true;
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

                    if (old_tb != null && old_tb != start_tb)
                        old_tb.Background = null;

                    if (tb != start_tb)
                    {
                        tb.Background = System.Windows.Media.Brushes.DarkGray;
                        target_drag = tb.DataContext as Model; //Set current target drag
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
                //Restart the app so the new libMBIN.dll gets loaded
                Process.Start(Environment.ProcessPath);
                Close();
            }
        }


        private void SceneTreeView_Ctx_ExportTemplate(object sender, RoutedEventArgs e)
        {
            Model root_node = (e.Source as Control).DataContext as Model;
            try
            {
                TkSceneNodeData exported_node = root_node.ExportTemplate();
                exported_node.WriteToMxml("export.mxml", false, true);
                CallBacks.Log("Node Exported Successfully!", root_node.Name);
            }
            catch (Exception ex)
            {
                CallBacks.Log("Error during template export");
                CallBacks.Log(ex.Message);
            }

        }

        private void SceneTreeView_Ctx_LoadReference(object sender, RoutedEventArgs e)
        {
            Reference root_node = (e.Source as Control).DataContext as Reference;
            //Try to load the reference model and add it as a child to the reference

            if (root_node.ref_scene_filepath is null)
            {
                CallBacks.Log($"Null Reference of {root_node.Name}");
                return;
            }

            CallBacks.Log("Loading Reference " + root_node.ref_scene_filepath);
            ThreadRequest req;

            //Pause renderer
            req = new ThreadRequest();
            req.type = THREAD_REQUEST_TYPE.GL_PAUSE_RENDER_REQUEST;
            req.arguments.Clear();

            //Send request
            glControl.issueRenderingRequest(ref req);
            glControl.engine.handleRequests();

            //Send request for loading reference to the scene
            req = new ThreadRequest();
            req.type = THREAD_REQUEST_TYPE.LOAD_REFERENCE_REQUEST;
            req.arguments.Add(root_node.ref_scene_filepath);

            glControl.issueRenderingRequest(ref req);
            glControl.engine.handleRequests();

            //Animated or action-driven parts of the reference join the systems
            glControl.findAnimScenes(RenderState.rootObject);
            glControl.findActionScenes(RenderState.rootObject);

            //Generate Request for resuming rendering
            ThreadRequest req2 = new ThreadRequest();
            req2.type = THREAD_REQUEST_TYPE.GL_RESUME_RENDER_REQUEST;
            req2.arguments.Clear();

            glControl.issueRenderingRequest(ref req2);
            glControl.engine.handleRequests();
            //glControl.waitForRenderingRequest(ref req2);

            //Bind new camera to the controls
            CameraOptionsView.Content = RenderState.activeCam.settings;

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
    
        private void camSpeed_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                commitCamSpeed((TextBox)sender);
                ((TextBox)sender).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
            else if (e.Key == Key.Escape)
            {
                ((TextBox)sender).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)); //Lost focus restores the value
            }
        }

        private void camSpeed_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            commitCamSpeed((TextBox)sender);
        }

        //Apply a typed speed (clamped by the setting), or put the current one back if the text isn't a number
        private void commitCamSpeed(TextBox box)
        {
            if (float.TryParse(box.Text, out float speed) && speed > 0.0f)
                RenderState.activeCam.settings.Speed = speed;
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget(); //Show the value actually used
        }

        private void camSpeedSlider_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            RenderState.activeCam.settings.Speed = 1.0f;
        }

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
    public sealed class IsInstanceOfConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType,
                              object? parameter, CultureInfo culture)
        {
            if (parameter is Type expectedType)
                return expectedType.IsInstanceOfType(value);
            return false;
        }

        public object ConvertBack(object? value, Type targetType,
                                    object? parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public sealed class IsReferenceLoadedConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType,
                              object? parameter, CultureInfo culture)
        {
            Type ref_type = typeof(Reference);
            var prop = ref_type.GetProperty("isRefLoaded");
            if (prop == null) return false;
            var propValue = prop.GetValue(value);
            if ((bool)propValue == true)
                Console.WriteLine("Something");
                
            return (bool) propValue;
        }

        public object ConvertBack(object? value, Type targetType,
                                    object? parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class SceneNodeTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? DefaultTemplate { get; set; }
        public DataTemplate? ReferenceTemplate { get; set; }
        
        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            return item switch
            {
                Reference => ReferenceTemplate,
                _ => DefaultTemplate
            };
        }
    }

}


