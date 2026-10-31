using libMBIN.NMS.Toolkit;
using Microsoft.Win32;
using Model_Viewer;
using MVCore;
using MVCore.Common;
using MVCore.GMDL;
using MVCore.Utils;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
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
        private OpenTK.GLControl.GLControl nativeViewportControl;
        private sealed class PakSceneEntry
        {
            public string Name { get; init; }
            public long SizeBytes { get; init; }
            public string Size => formatFileSize(SizeBytes);
        }

        private static string formatFileSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            double value = Math.Max(bytes, 0);
            int unit = 0;
            while (value >= 1024.0 && unit < units.Length - 1)
            {
                value /= 1024.0;
                unit++;
            }
            return unit == 0 ? $"{value:0} {units[unit]}" : $"{value:0.##} {units[unit]}";
        }

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
#if DEBUG
            CallBacks.VerboseLog = Util.Log;
#endif
            CallBacks.showError = Util.showError;
            CallBacks.getResource = name => Properties.Resources.ResourceManager.GetObject(name, Properties.Resources.Culture);

            //Toggle waiting to attach renderdoc
            //System.Threading.Thread.Sleep(10000);

            //The resource manager is created by the engine on the first render (MainWindow_OnLoaded waits for it)

            var settings = new OpenTK.GLControl.GLControlSettings
            {
                APIVersion = new Version(4, 6),
                IsEventDriven = false,
                Profile = ContextProfile.Compatability,
#if DEBUG
                Flags = ContextFlags.Debug,
#endif
            };
            nativeViewportControl = new OpenTK.GLControl.GLControl(settings)
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                TabStop = true
            };
            glControl = new CGLControl(nativeViewportControl);
            NativeHost.Child = nativeViewportControl;
            CallBacks.enqueueRenderCommand = command => glControl.engine.InvokeOnRenderThreadAsync(command);

            Activated += (sender, args) => glControl.setWindowActive(true);
            Deactivated += (sender, args) => glControl.setWindowActive(false);


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
        private async Task OpenFileAsync(string filename, bool testScene, int testSceneID)
        {
            CallBacks.Log("Importing " + filename);

            SceneTreeView.Items.Clear();
            if (testScene)
                await glControl.addTestSceneAsync(testSceneID);
            else
                await glControl.addSceneAsync(filename);

            if (RenderState.rootObject != null)
            {
                RenderState.rootObject.ID = 0;
                Util.setStatus("Creating Treeview...");
                SceneTreeView.Items.Add(RenderState.rootObject);
            }
            Util.setStatus("Ready");

            //Bind new camera to the controls
            CameraOptionsView.Content = RenderState.activeCam.settings;
        }


        private async void OpenFile(object sender, RoutedEventArgs e)
        {
            CallBacks.Log("Opening File");
            OpenFileDialog openFileDlg = new OpenFileDialog();
            openFileDlg.Filter = "SCENE Files (*.SCENE.MBIN, *.SCENE.MXML)|*.SCENE.MBIN;*.SCENE.MXML";
            var res = openFileDlg.ShowDialog();

            if (res == false)
                return;

            var filename = openFileDlg.FileName;
            await OpenFileAsync(filename, false, 0);
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

            showSceneList("Select SCENE file from List", selected => OpenFileAsync(selected, false, 0));
        }

        //Lists the SCENE files of the PAKs, double clicking one closes the list and passes the path to onPick
        private void showSceneList(string title, Func<string, Task> onPick)
        {
            //I need to make a custom window for previewing the entire list of SCENE Files from the PAK files
            List<PakSceneEntry> paths = new List<PakSceneEntry>();

            foreach (KeyValuePair<string, string> file in RenderState.activeResMgr.NMSFileToArchiveMap)
            {
                if (!file.Key.EndsWith(".SCENE.MBIN", StringComparison.OrdinalIgnoreCase))
                    continue;

                long size = 0;
                try
                {
                    libHGPAK.HGPAK.Archive archive = NMSUtils.loadNMSArchive(file.Value);
                    int fileIndex = archive.GetFileIndex(file.Key);
                    if (fileIndex >= 0)
                    {
                        ulong archiveSize = archive.fileEntries[fileIndex].uncompressedFileSize;
                        size = archiveSize > long.MaxValue ? long.MaxValue : (long)archiveSize;
                    }
                }
                catch (Exception ex)
                {
                    CallBacks.Verbose("Could not read PAK entry size for " + file.Key + ": " + ex.Message);
                }
                paths.Add(new PakSceneEntry { Name = file.Key, SizeBytes = size });
            }
            paths.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name));

            Window win = new Window();
            win.Title = title;
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

            GridView listColumns = new GridView();
            GridViewColumnHeader nameHeader = new GridViewColumnHeader { Content = "Name" };
            GridViewColumnHeader sizeHeader = new GridViewColumnHeader { Content = "Size" };
            listColumns.Columns.Add(new GridViewColumn
            {
                Header = nameHeader,
                DisplayMemberBinding = new Binding(nameof(PakSceneEntry.Name)),
                Width = 650
            });
            listColumns.Columns.Add(new GridViewColumn
            {
                Header = sizeHeader,
                DisplayMemberBinding = new Binding(nameof(PakSceneEntry.Size)),
                Width = 100
            });
            lb.View = listColumns;

            //Add items
            lb.ItemsSource = paths;
            lb.MouseDoubleClick += async delegate
            {
                string selected = (lb.SelectedItem as PakSceneEntry)?.Name;
                if (selected == null)
                    return;
                win.Close();
                await onPick(selected);
            };

            CollectionView viewSource = (CollectionView)CollectionViewSource.GetDefaultView(lb.ItemsSource);
            viewSource.SortDescriptions.Add(new SortDescription(nameof(PakSceneEntry.Name), ListSortDirection.Ascending));

            ListSortDirection nameDirection = ListSortDirection.Ascending;
            ListSortDirection sizeDirection = ListSortDirection.Ascending;
            nameHeader.Click += delegate
            {
                nameDirection = nameDirection == ListSortDirection.Ascending
                    ? ListSortDirection.Descending : ListSortDirection.Ascending;
                viewSource.SortDescriptions.Clear();
                viewSource.SortDescriptions.Add(new SortDescription(nameof(PakSceneEntry.Name), nameDirection));
            };
            sizeHeader.Click += delegate
            {
                sizeDirection = sizeDirection == ListSortDirection.Ascending
                    ? ListSortDirection.Descending : ListSortDirection.Ascending;
                viewSource.SortDescriptions.Clear();
                viewSource.SortDescriptions.Add(new SortDescription(nameof(PakSceneEntry.SizeBytes), sizeDirection));
                viewSource.SortDescriptions.Add(new SortDescription(nameof(PakSceneEntry.Name), ListSortDirection.Ascending));
            };


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
                    return (obj as PakSceneEntry)?.Name.IndexOf(searchBox.Text,
                        StringComparison.OrdinalIgnoreCase) >= 0;
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

            //Detach WPF collection views before the render thread disposes the scene graph.
            ObjectInfoBox.Content = null;
            SceneTreeView.Items.Clear();

            //Stop rendering first, then free the GL resources (a queued terminate request would only run on the
            //next frame, after the resources were already gone)
            glControl.shutdown();
        }

        private void MainWindow_OnClosed(object sender, EventArgs e)
        {
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

            SceneTreeView.Items.Clear();
            SceneTreeView.Items.Add(RenderState.rootObject);


            //Check if Temp folder exists
#if DEBUG
            if (!Directory.Exists("Temp")) Directory.CreateDirectory("Temp");
#endif
            //Set active Components
            Util.activeStatusStrip = StatusLabel;
            Util.activeWindow = this;

            //Bind Settings
            RenderViewOptionsControl.Content = RenderState.renderViewSettings;
            //Loading the settings replaces the renderViewSettings instance, so bind to the current one here
            ActionEmulationBox.DataContext = RenderState.renderViewSettings;
            SystemsPanel.Attach(glControl.engine);
            SystemsPanel.OwnerSelected += selectSceneNode;
            RenderOptionsControl.Content = RenderState.renderSettings;
            DefaultLightOptionsControl.DataContext = RenderState.activeResMgr.GLlights[0];

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

        void setTestComponents()
        {
            //The debug test sliders had no connected behavior; retain the functional scene button only.
            Button bt = new Button { Content = "Test Scene 1" };
            bt.Click += new RoutedEventHandler(async delegate (object s, RoutedEventArgs ee)
            {
                await OpenFileAsync("", true, 0);
            });

            TestOptions.Content = bt;
        }
#endif

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            NMSUtils.ProcGen();
        }

        private void SelectLOD_OnClick(object sender, RoutedEventArgs e)
        {
            //The first entry is Auto, which picks the level by camera distance
            int selected = Math.Max(tb_LODLevel.SelectedIndex, 0) - 1;
            RenderState.forcedLOD = selected;
            RenderState.instancesDirty = true;
            CallBacks.Log("[LODSelection] UI requested " + (selected < 0 ? "Auto" : $"LOD{selected}"));
        }

        private async void RegenTextures_Click(object sender, RoutedEventArgs e)
        {
            //Mixing needs the GL context, so it runs on the next frame
            await glControl.engine.RegenerateProceduralTexturesAsync();
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

        private void RestartActions_Click(object sender, RoutedEventArgs e)
        {
            glControl.engine.actionSys.RequestRestart();
        }

        private void CameraResetPos(object sender, RoutedEventArgs e)
        {
            //Frame the loaded model, or go back to the origin when there is none
            if (RenderState.rootObject != null)
                RenderState.activeCam.frameScene(RenderState.rootObject, true);
            else
                RenderState.activeCam.Position = Vector3.Zero;
        }

        private void SceneResetRotation(object sender, RoutedEventArgs e)
        {
            RenderState.rotAngles = new Vector3(0.0f);
        }

        private void SceneTreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            Model node = (Model)SceneTreeView.SelectedItem;
            if (node != null)
                setActiveModel(node);
        }

        private void GizmoToolbarModeChanged(object sender, RoutedEventArgs e)
        {
            if (glControl == null || sender is not RadioButton button)
                return;
            GIZMO_MODE mode = button.Tag?.ToString() switch
            {
                "Rotate" => GIZMO_MODE.ROTATE,
                "Scale" => GIZMO_MODE.SCALE,
                _ => GIZMO_MODE.TRANSLATE
            };
            glControl.SetGizmoMode(mode);
        }

        private void setActiveModel(Model node)
        {
            //Swap activeModels
            Model prev_activeModel = RenderState.activeModel;
            glControl.SetActiveModel(node);

            //Set binding to objectinfo box
            ObjectInfoBox.Content = node;

            //Set Selected
            RenderState.activeModel.selected = 1;

            //Deselect Previews model
            if (prev_activeModel != null && prev_activeModel != node)
                prev_activeModel.selected = 0;
            RenderState.instancesDirty = true;
        }

        //Selects a node in the scene tree, expanding its parents. Used by the Systems panel
        private void selectSceneNode(Model node)
        {
            List<Model> path = new List<Model>();
            for (Model n = node; n != null; n = n.parent)
                path.Insert(0, n);

            ItemsControl container = SceneTreeView;
            TreeViewItem item = null;
            foreach (Model n in path)
            {
                container.UpdateLayout();
                TreeViewItem next = container.ItemContainerGenerator.ContainerFromItem(n) as TreeViewItem;
                if (next == null)
                    continue; //Ancestors above the tree's root
                item = next;
                if (n != node)
                    item.IsExpanded = true;
                container = item;
            }

            if (item != null && item.DataContext == node)
            {
                item.IsSelected = true; //Updates the active model through SelectedItemChanged
                item.BringIntoView();
            }
            else
                setActiveModel(node);
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

        private async void SceneTreeView_Drop(object sender, DragEventArgs e)
        {
            //Only nodes dragged from this tree can be dropped. The target is the node under the cursor now
            Model source = init_drag;
            Model target = (SceneTreeView.InputHitTest(e.GetPosition(SceneTreeView)) as FrameworkElement)?.DataContext as Model;

            if (source == null || target == null || source == target || source.parent == target)
                return;

            //Moves happen in the scene file too, so they stay inside the node's own file
            if (!SceneEdit.canReparent(source, target, out string reason))
            {
                CallBacks.Log("Cannot move " + source.Name + ": " + reason);
                Util.setStatus("Cannot move " + source.Name + ": " + reason);
                return;
            }

            await glControl.engine.ReparentAsync(source, target);
            init_drag = null;
            target_drag = null;
            e.Handled = true;

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
            Model node = (e.Source as Control).DataContext as Model;
            if (node == null)
                return;
            //A file root saves as its file, any other node as a scene of its own
            string name = (node.SourceFile != "") ? node.SourceFile : node.EditableName + ".SCENE.MBIN";
            Util.saveNode(node, name);
        }

        private void SceneTreeView_Ctx_ExportGeometry(object sender, RoutedEventArgs e)
        {
            Model node = (sender as Control)?.DataContext as Model;
            if (node == null)
                return;

            try
            {
                if (!node.IsRenderable)
                {
                    Util.showError("The selected node is unchecked. Check it before exporting its geometry.",
                        "Export Geometry");
                    return;
                }

                using Assimp.AssimpContext context = new Assimp.AssimpContext();
                Assimp.ExportFormatDescription[] formats = context.GetSupportedExportFormats();
                if (formats.Length == 0)
                {
                    Util.showError("Assimp did not report any supported export formats.", "Export Geometry");
                    return;
                }

                AssimpExportDialog dialog = new AssimpExportDialog(formats, makeSafeFileName(node.Name))
                {
                    Owner = this
                };
                if (dialog.ShowDialog() != true)
                    return;

                Assimp.ExportFormatDescription format = dialog.SelectedFormat;
                string outputFile = dialog.DestinationFile;
                CallBacks.Log($"Preparing Assimp export from {node.Name}; animations requested: " +
                    dialog.IncludeAnimations);
                Assimp.Scene scene = new AssimpGeometryExporter().Export(node, dialog.IncludeAnimations);

                int skippedProceduralTextures = 0;
                Dictionary<(Material Material, Vector3 Color), int> materialIndices =
                    new Dictionary<(Material, Vector3), int>();
                addAssimpMaterials(node, scene.RootNode, scene, materialIndices, outputFile,
                    ref skippedProceduralTextures);

                if (scene.MeshCount == 0)
                {
                    Util.showError("The selected node and its descendants do not contain exportable geometry.",
                        "Export Geometry");
                    return;
                }

                CallBacks.Log($"Starting Assimp {format.FormatId} export: {scene.MeshCount} mesh(es), " +
                    $"{scene.MaterialCount} material(s), {scene.AnimationCount} animation(s), " +
                    $"{scene.Meshes.Sum(mesh => mesh.BoneCount)} bones, " +
                    $"{scene.Meshes.Sum(mesh => mesh.VertexCount)} vertices, " +
                    $"{scene.Meshes.Sum(mesh => mesh.FaceCount)} faces");
                //The native exporter can terminate the process, so force this diagnostic checkpoint to disk.
                Util.closeLog();
                if (!context.ExportFile(scene, outputFile, format.FormatId))
                    throw new InvalidOperationException("Assimp could not export the scene in the selected format.");
                if (format.FormatId == "collada")
                    AssimpGeometryExporter.AddColladaAnimationClips(outputFile, scene);
                string message = $"Exported {scene.MeshCount} mesh(es), {scene.MaterialCount} material(s), " +
                    $"and {scene.AnimationCount} animation clip(s) " +
                    $"from {node.Name} to {outputFile}";
                if (skippedProceduralTextures > 0)
                    message += $". Skipped {skippedProceduralTextures} procedural texture reference(s)";
                CallBacks.Log(message);
            }
            catch (Exception ex)
            {
                CallBacks.Log("Geometry export failed: " + ex);
                Util.showError("Geometry export failed: " + ex.Message, "Export Geometry");
            }
        }

        private static string makeSafeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "geometry";
            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');
            return name;
        }

        private static void addAssimpMaterials(Model model, Assimp.Node assimpNode, Assimp.Scene scene,
            Dictionary<(Material Material, Vector3 Color), int> materialIndices, string outputFile,
            ref int skippedProceduralTextures)
        {
            if (!model.IsRenderable)
                return;

            if (model is Mesh mesh && assimpNode.MeshCount > 0)
            {
                Material material = mesh.material;
                var materialKey = (material, mesh.color);
                if (!materialIndices.TryGetValue(materialKey, out int materialIndex))
                {
                    materialIndex = scene.MaterialCount;
                    scene.Materials.Add(createAssimpMaterial(material, mesh.color, outputFile,
                        ref skippedProceduralTextures));
                    materialIndices.Add(materialKey, materialIndex);
                }

                foreach (int meshIndex in assimpNode.MeshIndices)
                    scene.Meshes[meshIndex].MaterialIndex = materialIndex;
            }

            int exportedChild = 0;
            foreach (Model child in model.Children)
            {
                if (!child.IsRenderable)
                    continue;
                if (exportedChild >= assimpNode.ChildCount)
                    break;
                addAssimpMaterials(child, assimpNode.Children[exportedChild++], scene, materialIndices,
                    outputFile, ref skippedProceduralTextures);
            }
        }

        private static Assimp.Material createAssimpMaterial(Material source, Vector3 fallbackColor,
            string outputFile, ref int skippedProceduralTextures)
        {
            Assimp.Material result = new Assimp.Material
            {
                Name = source?.Name ?? "Default material",
                ColorDiffuse = new System.Numerics.Vector4(fallbackColor.X, fallbackColor.Y, fallbackColor.Z, 1.0f)
            };

            if (source == null)
                return result;

            if (source.CustomPerMaterialUniforms.TryGetValue(
                "mpCustomPerMaterial.gMaterialColourVec4", out Uniform colour))
            {
                Vector4 value = colour.Vec.Vec;
                result.ColorDiffuse = new System.Numerics.Vector4(value.X, value.Y, value.Z, value.W);
                result.Opacity = value.W;
            }
            result.IsTwoSided = source.DoubleSided;

            bool hasProceduralSet = source.PSamplers.Values.Any(sampler => sampler.isProcGen);
            foreach (Sampler sampler in source.PSamplers.Values)
            {
                if (string.IsNullOrWhiteSpace(sampler.Map))
                    continue;

                bool procedural = sampler.isProcGen ||
                    sampler.Map.IndexOf(".MIXED.", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (hasProceduralSet && (sampler.Name.EndsWith("gDiffuseMap") ||
                        sampler.Name.EndsWith("gMasksMap") || sampler.Name.EndsWith("gNormalMap")));
                if (procedural)
                {
                    skippedProceduralTextures++;
                    continue;
                }

                Assimp.TextureType textureType;
                switch (sampler.Name)
                {
                    case "mpCustomPerMaterial.gDiffuseMap":
                    case "mpCustomPerMaterial.gDiffuse2Map":
                        textureType = Assimp.TextureType.Diffuse;
                        break;
                    case "mpCustomPerMaterial.gNormalMap":
                        textureType = Assimp.TextureType.Normals;
                        break;
                    case "mpCustomPerMaterial.gOcclusionMap":
                        textureType = Assimp.TextureType.Lightmap;
                        break;
                    default:
                        continue; //Packed masks and NMS-specific samplers have no standard Assimp meaning.
                }

                string relativePath = copyExportTexture(sampler.Map, outputFile);
                if (relativePath == null)
                    continue;

                int textureIndex = sampler.Name == "mpCustomPerMaterial.gDiffuse2Map" ? 1 : 0;
                Assimp.TextureSlot slot = new Assimp.TextureSlot(relativePath, textureType, textureIndex,
                    Assimp.TextureMapping.FromUV, 0, 1.0f, Assimp.TextureOperation.Multiply,
                    Assimp.TextureWrapMode.Wrap, Assimp.TextureWrapMode.Wrap, 0);
                result.AddMaterialTexture(in slot);
            }
            return result;
        }

        private static string copyExportTexture(string sourcePath, string outputFile)
        {
            try
            {
                string textureFolderName = Path.GetFileNameWithoutExtension(outputFile) + "_textures";
                string textureFolder = Path.Combine(Path.GetDirectoryName(outputFile), textureFolderName);
                Directory.CreateDirectory(textureFolder);

                string fileName = makeSafeFileName(sourcePath.Replace('/', '_').Replace('\\', '_'));
                string destination = Path.Combine(textureFolder, fileName);
                if (!File.Exists(destination))
                {
                    using Stream input = NMSUtils.LoadNMSFileStream(sourcePath,
                        ref RenderState.activeResMgr, false);
                    using FileStream output = File.Create(destination);
                    input.CopyTo(output);
                }
                return textureFolderName + "/" + fileName;
            }
            catch (Exception ex)
            {
                CallBacks.Log("Could not export texture " + sourcePath + ": " + ex.Message);
                return null;
            }
        }

        private async Task addNodeAsync(object sender, SceneEdit.NODE_KIND kind, string scenePath)
        {
            Model parent = (sender as Control)?.DataContext as Model;
            if (parent == null || !SceneEdit.canAdd(parent))
                return;

            await glControl.engine.AddNodeAsync(parent, kind, scenePath);
        }

        private async void SceneTreeView_Ctx_AddLocator(object sender, RoutedEventArgs e)
        {
            await addNodeAsync(sender, SceneEdit.NODE_KIND.LOCATOR, "");
        }

        private async void SceneTreeView_Ctx_AddLight(object sender, RoutedEventArgs e)
        {
            await addNodeAsync(sender, SceneEdit.NODE_KIND.LIGHT, "");
        }

        private void SceneTreeView_Ctx_AddReference(object sender, RoutedEventArgs e)
        {
            if (activateIfOpen(sceneListWindow))
                return;
            showSceneList("Select the referenced SCENE file",
                path => addNodeAsync(sender, SceneEdit.NODE_KIND.REFERENCE, path));
        }

        private async void SceneTreeView_Ctx_ImportGeometry(object sender, RoutedEventArgs e)
        {
            Model parent = (sender as Control)?.DataContext as Model;
            if (parent?.CanImportGeometry != true)
                return;

            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Import Geometry",
                InitialDirectory = @"C:\Users\Greg\Downloads\glTF-Sample-Models-master\2.0\Duck\glTF-Binary\",
                Filter = AssimpGeometryImporter.BuildFileFilter(),
                CheckFileExists = true,
                Multiselect = false
            };
            if (dialog.ShowDialog(this) != true)
                return;

            try
            {
                CallBacks.Log("[Import] Reading geometry hierarchy from {0}", dialog.FileName);
                AssimpGeometryImportResult imported = AssimpGeometryImporter.Import(dialog.FileName);
                await glControl.engine.AddImportedGeometryAsync(parent, imported.Root);
                CallBacks.Log("[Import] Prepared {0} node(s), {1} mesh(es), {2} vertices and {3} triangles from {4}",
                    imported.NodeCount, imported.MeshCount, imported.VertexCount, imported.TriangleCount,
                    Path.GetFileName(dialog.FileName));
            }
            catch (Exception ex)
            {
                CallBacks.Log("Geometry import failed: " + ex);
                Util.showError("Geometry import failed\n" + ex.Message, "Geometry Import");
            }
        }

        private async void SceneTreeView_Ctx_Remove(object sender, RoutedEventArgs e)
        {
            Model node = (sender as Control)?.DataContext as Model;
            if (node == null)
                return;
            if (!SceneEdit.canRemove(node, out string reason))
            {
                Util.showError("Cannot remove " + node.Name + ": " + reason, "Remove");
                return;
            }
            if (MessageBox.Show(this, "Remove " + node.Name + " and everything under it?", "Remove",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            await glControl.engine.RemoveNodeAsync(node);
        }

        private void SceneTreeView_Ctx_ExportEntity(object sender, RoutedEventArgs e)
        {
            Model node = (e.Source as Control).DataContext as Model;
            if (node == null)
                return;
            Util.saveTemplate(node.ExportAttachmentTemplate(), Path.GetFileName(node.AttachmentPath));
        }

        private void SceneTreeView_Ctx_ExportMaterial(object sender, RoutedEventArgs e)
        {
            Mesh node = (e.Source as Control).DataContext as Mesh;
            Material mat = node?.meshVao?.material;
            if (mat == null || !mat.fromFile)
            {
                Util.showError("The material of this mesh was not loaded from a file", "Export");
                return;
            }
            Util.saveTemplate(mat.ExportTemplate(), Path.GetFileName(mat.name_key));
        }

        private async void SceneTreeView_Ctx_LoadReference(object sender, RoutedEventArgs e)
        {
            Reference root_node = (e.Source as Control).DataContext as Reference;
            //Try to load the reference model and add it as a child to the reference

            if (root_node.ref_scene_filepath is null)
            {
                CallBacks.Log($"Null Reference of {root_node.Name}");
                return;
            }

            CallBacks.Log("Loading Reference " + root_node.ref_scene_filepath);
            await glControl.engine.LoadReferenceAsync(root_node.ref_scene_filepath);

            //Bind new camera to the controls
            CameraOptionsView.Content = RenderState.activeCam.settings;

        }


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

}


