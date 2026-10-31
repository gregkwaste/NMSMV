using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MVCore.Common;
using MVCore.Engine;
using MVCore.GMDL;

namespace WPFModelViewer
{
    //One row of the Scene Systems tree: a group, an owner, a TriggerAction state or an animation clip
    public class SystemsNode : INotifyPropertyChanged
    {
        public string Key;
        public Model Owner;
        public object Content; //Shown in the details pane
        public ActionTriggerState State { get; set; }
        public AnimData Clip { get; set; }
        public InteractionComponent Interaction { get; set; }
        public ActionTrigger Trigger { get; set; }
        public ObservableCollection<SystemsNode> Children { get; } = new ObservableCollection<SystemsNode>();

        public bool HasGo => State != null;
        public bool HasPlay => Clip != null;
        public bool HasInteract => Interaction != null;
        public bool HasFire => Trigger != null;

        private string _title = "", _status = "", _detail = "", _toolTip = null;
        private bool _highlight, _strong, _expanded, _selected;

        public string Title { get => _title; set => set(ref _title, value); }
        public string Status { get => _status; set => set(ref _status, value); }
        public string Detail { get => _detail; set => set(ref _detail, value); }
        public string ToolTip { get => _toolTip; set => set(ref _toolTip, value); }
        public bool Highlight { get => _highlight; set => set(ref _highlight, value); }
        public bool Strong { get => _strong; set => set(ref _strong, value); }
        public bool IsExpanded { get => _expanded; set => set(ref _expanded, value); }
        public bool IsSelected { get => _selected; set => set(ref _selected, value); }

        //Takes the live values of a freshly built node with the same key
        public void CopyFrom(SystemsNode n)
        {
            Title = n.Title;
            Status = n.Status;
            Detail = n.Detail;
            ToolTip = n.ToolTip;
            Highlight = n.Highlight;
            Strong = n.Strong;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    //Every TriggerAction and animation component of the loaded scene in one place, with their live state
    public partial class SceneSystemsPanel : UserControl
    {
        private const int MAX_LOG_LINES = 500;
        private const double HIGHLIGHT_SECONDS = 2.0;

        private Engine engine;
        private readonly DispatcherTimer timer;
        private readonly ObservableCollection<SystemsNode> roots = new ObservableCollection<SystemsNode>();
        private readonly ObservableCollection<string> logLines = new ObservableCollection<string>();
        private readonly Dictionary<string, SystemsNode> nodeIndex = new Dictionary<string, SystemsNode>();
        private readonly HashSet<string> expandedKeys = new HashSet<string>();
        private string selectedKey = null;
        private string lastSignature = null;

        //When the state of an action entity or the play state of a clip last changed, for the highlight
        private readonly Dictionary<TriggerActionComponent, (string state, DateTime time)> stateSeen =
            new Dictionary<TriggerActionComponent, (string, DateTime)>();
        private readonly Dictionary<AnimData, (bool playing, DateTime time)> clipSeen =
            new Dictionary<AnimData, (bool, DateTime)>();

        //Raised when a row is selected, MainWindow selects the owner in the scene tree
        public event System.Action<Model> OwnerSelected;

        public SceneSystemsPanel()
        {
            InitializeComponent();
            SystemsTree.ItemsSource = roots;
            EventLogList.ItemsSource = logLines;
            timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
            timer.Tick += (s, e) => refresh();
        }

        public void Attach(Engine e)
        {
            engine = e;
            engine.actionSys.EventLogged += onActionEvent;
            timer.Start();
        }

        private void onActionEvent(string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new System.Action(() => onActionEvent(message)));
                return;
            }

            const string prefix = "[Actions] ";
            if (message.StartsWith(prefix))
                message = message.Substring(prefix.Length);
            logLines.Add(DateTime.Now.ToString("HH:mm:ss.f") + "  " + message);
            while (logLines.Count > MAX_LOG_LINES)
                logLines.RemoveAt(0);
            EventLogList.ScrollIntoView(logLines[logLines.Count - 1]);
        }

        #region Tree

        private void refresh()
        {
            if (engine == null)
                return;

            bool running = engine.actionSys.Running;
            EmulationToggle.IsChecked = RenderState.renderViewSettings.EmulateActions;
            EmulationToggle.Content = RenderState.renderViewSettings.EmulateActions ? "Actions: ON" : "Actions: OFF";
            EmulationToggle.FontWeight = RenderState.renderViewSettings.EmulateActions ? FontWeights.Bold : FontWeights.Normal;
            WarningText.Visibility = RenderState.renderSettings.ToggleAnimations ? Visibility.Collapsed : Visibility.Visible;

            if (!IsVisible)
                return; //Nothing to show, the log keeps filling

            List<SystemsNode> fresh = build(running);
            string signature = signatureOf(fresh);
            if (signature == lastSignature)
            {
                //Same rows, only the live values change, so expansion and selection are kept
                foreach (SystemsNode n in flatten(fresh))
                {
                    if (nodeIndex.TryGetValue(n.Key, out SystemsNode old))
                        old.CopyFrom(n);
                }
                return;
            }

            lastSignature = signature;
            nodeIndex.Clear();
            foreach (SystemsNode n in flatten(fresh))
            {
                nodeIndex[n.Key] = n;
                n.IsExpanded = expandedKeys.Contains(n.Key);
                n.PropertyChanged += onNodePropertyChanged;
            }
            roots.Clear();
            foreach (SystemsNode n in fresh)
                roots.Add(n);
            if (selectedKey != null && nodeIndex.TryGetValue(selectedKey, out SystemsNode sel))
                sel.IsSelected = true;
        }

        private void onNodePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            SystemsNode n = (SystemsNode) sender;
            if (e.PropertyName == nameof(SystemsNode.IsExpanded))
            {
                if (n.IsExpanded)
                    expandedKeys.Add(n.Key);
                else
                    expandedKeys.Remove(n.Key);
            }
        }

        private List<SystemsNode> build(bool running)
        {
            string query = SearchBox.Text.Trim();
            bool onlyRunning = OnlyRunningBox.IsChecked == true;
            bool byOwner = GroupByBox.SelectedIndex == 1;

            List<SystemsNode> actionNodes = new List<SystemsNode>();
            foreach (Model m in engine.actionSys.ActionScenes)
            {
                SystemsNode n = actionNode(m, running, onlyRunning, query);
                if (n != null)
                    actionNodes.Add(n);
            }

            List<SystemsNode> animNodes = new List<SystemsNode>();
            foreach (Model m in engine.animationSys.AnimScenes)
            {
                SystemsNode n = animNode(m, onlyRunning, query);
                if (n != null)
                    animNodes.Add(n);
            }

            List<SystemsNode> interactNodes = new List<SystemsNode>();
            HashSet<Model> inRange = new HashSet<Model>(engine.actionSys.InteractablesInRange());
            foreach (Model m in engine.actionSys.InteractScenes)
            {
                SystemsNode n = interactNode(m, query, inRange.Contains(m));
                if (n != null)
                    interactNodes.Add(n);
            }

            List<SystemsNode> rotationNodes = new List<SystemsNode>();
            foreach (Model m in engine.rotationSys.RotationScenes)
            {
                SystemsNode n = rotationNode(m, onlyRunning, query);
                if (n != null)
                    rotationNodes.Add(n);
            }

            List<SystemsNode> result = new List<SystemsNode>();
            if (!byOwner)
            {
                result.Add(group("grp|interact", "Interactions", interactNodes, inRange.Count + " in range"));
                result.Add(group("grp|actions", "Actions", actionNodes, running ? "running" : "emulation off"));
                result.Add(group("grp|anims", "Animations", animNodes,
                                 animNodes.Count(n => n.Status != "idle") + " playing"));
                result.Add(group("grp|rotations", "Rotations", rotationNodes,
                                 RenderState.renderSettings.ToggleAnimations ? "" : "animations off"));
                return result;
            }

            //By owner: an entity's actions and animations side by side
            foreach (IGrouping<Model, SystemsNode> g in interactNodes.Concat(actionNodes).Concat(animNodes).Concat(rotationNodes).GroupBy(n => n.Owner))
            {
                SystemsNode owner = new SystemsNode
                {
                    Key = "own|" + ownerKey(g.Key), Owner = g.Key, Title = ownerLabel(g.Key),
                    ToolTip = ownerPath(g.Key), Strong = true
                };
                foreach (SystemsNode child in g)
                {
                    child.Title = child.Content is InteractionComponent ? "Interaction" :
                                  child.Content is TriggerActionComponent ? "Actions" :
                                  child.Content is RotationComponent ? "Rotation" : "Animations";
                    child.Key = "own|" + child.Key;
                    rekeyChildren(child);
                    owner.Status = owner.Status.Length == 0 ? child.Status : owner.Status + ", " + child.Status;
                    owner.Highlight |= child.Highlight;
                    owner.Children.Add(child);
                }
                result.Add(owner);
            }
            return result;
        }

        private static void rekeyChildren(SystemsNode n)
        {
            foreach (SystemsNode c in n.Children)
            {
                c.Key = "own|" + c.Key;
                rekeyChildren(c);
            }
        }

        private static SystemsNode group(string key, string title, List<SystemsNode> children, string status)
        {
            SystemsNode g = new SystemsNode { Key = key, Title = title + " (" + children.Count + ")", Status = status, Strong = true };
            foreach (SystemsNode c in children)
                g.Children.Add(c);
            return g;
        }

        private SystemsNode actionNode(Model m, bool running, bool onlyRunning, string query)
        {
            if (!(m.Components[m.actionComponentID] is TriggerActionComponent tac))
                return null;

            string state = tac.CurrentStateID ?? "";
            bool idle = !running || state.EndsWith("(inactive)") || state == "No states" || state == "Not running";
            if (onlyRunning && idle)
                return null;

            DateTime now = DateTime.Now;
            if (!stateSeen.TryGetValue(tac, out var seen) || seen.state != state)
                stateSeen[tac] = seen = (state, now);
            double age = (now - seen.time).TotalSeconds;

            string key = "act|" + ownerKey(m);
            SystemsNode n = new SystemsNode
            {
                Key = key, Owner = m, Content = tac, Title = ownerLabel(m), ToolTip = ownerPath(m),
                Status = state, Highlight = running && age < HIGHLIGHT_SECONDS,
                Detail = running && age < 60.0 ? "changed " + (int) age + "s ago" : ""
            };

            bool ownerMatches = matches(query, n.Title, state);
            foreach (ActionTriggerState s in tac.States)
            {
                if (!ownerMatches && !matches(query, s.StateID))
                    continue;
                int fired = s.Triggers.Count(t => t.Fired);
                SystemsNode sn = new SystemsNode
                {
                    Key = key + "|" + s.StateID, Owner = m, Content = s, State = s, Title = s.StateID,
                    Strong = s.IsCurrent, Highlight = s.IsCurrent && n.Highlight,
                    Status = s.IsCurrent ? "current" : "",
                    Detail = s.Triggers.Count + " triggers" + (s.IsCurrent && fired > 0 ? ", " + fired + " fired" : "")
                };

                //Triggers sit under their state, collapsed until the state is expanded. The current state's show their live value
                for (int i = 0; i < s.Triggers.Count; i++)
                {
                    ActionTrigger at = s.Triggers[i];
                    sn.Children.Add(new SystemsNode
                    {
                        Key = sn.Key + "|" + i, Owner = m, Content = at, Trigger = at, Title = at.Description,
                        Status = engine.actionSys.TriggerProgress(m, at),
                        ToolTip = string.Join("\n", at.Actions.Select(a => a.Description))
                    });
                }
                n.Children.Add(sn);
            }
            if (!ownerMatches && n.Children.Count == 0)
                return null;
            return n;
        }

        private SystemsNode interactNode(Model m, string query, bool inRange)
        {
            int id = m.hasComponent(typeof(InteractionComponent));
            if (id < 0 || !(m.Components[id] is InteractionComponent ic))
                return null;

            SystemsNode n = new SystemsNode
            {
                Key = "int|" + ownerKey(m), Owner = m, Content = ic, Interaction = ic,
                Title = ownerLabel(m), ToolTip = ownerPath(m), Status = ic.Description,
                Detail = MVCore.Engine.Systems.ActionSystem.DistanceFromCamera(m).ToString("0.0") + "m away" + (inRange ? ", in range" : ""),
                Highlight = inRange
            };
            return matches(query, n.Title, ic.TriggerAction) ? n : null;
        }

        private SystemsNode rotationNode(Model m, bool onlyRunning, string query)
        {
            int id = m.hasComponent(typeof(RotationComponent));
            if (id < 0 || !(m.Components[id] is RotationComponent rc))
                return null;

            bool spinning = RenderState.renderSettings.ToggleAnimations && rc.Speed != 0.0f;
            if (onlyRunning && !spinning)
                return null;

            SystemsNode n = new SystemsNode
            {
                Key = "rot|" + ownerKey(m), Owner = m, Content = rc, Title = ownerLabel(m), ToolTip = ownerPath(m),
                Status = rc.Description, Detail = spinning ? "spinning" : "stopped"
            };
            return matches(query, n.Title) ? n : null;
        }

        private SystemsNode animNode(Model m, bool onlyRunning, string query)
        {
            if (m.animComponentID < 0 || !(m.Components[m.animComponentID] is AnimComponent ac))
                return null;

            DateTime now = DateTime.Now;
            string key = "anim|" + ownerKey(m);
            SystemsNode n = new SystemsNode { Key = key, Owner = m, Content = ac, Title = ownerLabel(m), ToolTip = ownerPath(m) };
            bool ownerMatches = matches(query, n.Title);
            int playing = 0;

            foreach (AnimData ad in ac.Animations)
            {
                if (!clipSeen.TryGetValue(ad, out var seen) || seen.playing != ad.IsPlaying)
                    clipSeen[ad] = seen = (ad.IsPlaying, now);
                if (ad.IsPlaying)
                    playing++;
                if (onlyRunning && !ad.IsPlaying)
                    continue;
                if (!ownerMatches && !matches(query, ad.PName, ad.StartedBy))
                    continue;

                bool recent = ad.IsPlaying && (now - seen.time).TotalSeconds < HIGHLIGHT_SECONDS;
                n.Children.Add(new SystemsNode
                {
                    Key = key + "|" + ad.PName, Owner = m, Content = ad, Clip = ad, Title = ad.PName,
                    Strong = ad.IsPlaying, Highlight = recent,
                    Status = ad.IsPlaying ? ad.ActiveFrame + "/" + ad.FrameCount : "",
                    Detail = ad.StartedBy != null ? "started by " + ad.StartedBy : "",
                    ToolTip = ad.AnimType + (ad.Additive ? ", additive" : "")
                });
                n.Highlight |= recent;
            }

            if (n.Children.Count == 0 && (onlyRunning || !ownerMatches))
                return null;
            n.Status = playing > 0 ? playing + " playing" : "idle";
            return n;
        }

        private static bool matches(string query, params string[] values)
        {
            if (query.Length == 0)
                return true;
            foreach (string v in values)
            {
                if (v != null && v.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static string ownerKey(Model m)
        {
            return m.Name + "#" + RuntimeHelpers.GetHashCode(m);
        }

        private static string ownerLabel(Model m)
        {
            return m.parent != null ? m.parent.Name + "/" + m.Name : m.Name;
        }

        private static string ownerPath(Model m)
        {
            List<string> names = new List<string>();
            for (Model n = m; n != null; n = n.parent)
                names.Insert(0, n.Name);
            return string.Join("/", names);
        }

        private static IEnumerable<SystemsNode> flatten(IEnumerable<SystemsNode> nodes)
        {
            foreach (SystemsNode n in nodes)
            {
                yield return n;
                foreach (SystemsNode c in flatten(n.Children))
                    yield return c;
            }
        }

        private static string signatureOf(List<SystemsNode> nodes)
        {
            StringBuilder sb = new StringBuilder();
            foreach (SystemsNode n in flatten(nodes))
                sb.Append(n.Key).Append('\n');
            return sb.ToString();
        }

        #endregion

        #region UI events

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;
            refresh();
        }

        private void SystemsTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (!(SystemsTree.SelectedItem is SystemsNode n))
                return;
            bool changed = n.Key != selectedKey; //A rebuild reselects the same row
            selectedKey = n.Key;
            DetailsBox.Content = n.Content;
            if (changed && n.Owner != null)
                OwnerSelected?.Invoke(n.Owner);
        }

        private void GoState_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is SystemsNode n && n.State != null)
            {
                RenderState.renderViewSettings.EmulateActions = true; //Emulation starts on the next frame, then the state is applied
                engine.actionSys.RequestState(n.State);
            }
        }

        private void Interact_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is SystemsNode n && n.Interaction != null)
            {
                RenderState.renderViewSettings.EmulateActions = true; //Emulation starts on the next frame, then the state is sent
                engine.actionSys.RequestInteract(n.Interaction);
            }
        }

        private void PlayerInteract_Click(object sender, RoutedEventArgs e)
        {
            RenderState.renderViewSettings.EmulateActions = true; //Emulation starts on the next frame, then the interact runs
            engine.actionSys.RequestPlayerInteract();
        }

        private void Fire_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is SystemsNode n && n.Trigger != null)
            {
                RenderState.renderViewSettings.EmulateActions = true; //Emulation starts on the next frame, then the trigger runs
                engine.actionSys.RequestFire(n.Trigger);
            }
        }

        private void EmulationToggle_Click(object sender, RoutedEventArgs e)
        {
            RenderState.renderViewSettings.EmulateActions = !RenderState.renderViewSettings.EmulateActions;
            refresh();
        }

        private void Restart_Click(object sender, RoutedEventArgs e)
        {
            engine?.actionSys.RequestRestart();
        }

        private void StopClips_Click(object sender, RoutedEventArgs e)
        {
            if (engine == null)
                return;
            foreach (Model m in engine.animationSys.AnimScenes)
                engine.animationSys.StopActiveAnimations(m);
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            logLines.Clear();
        }

        //Filters the tree by the entity the line is about
        private void EventLogList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!(EventLogList.SelectedItem is string line))
                return;
            string text = line.Substring(line.IndexOf("  ", StringComparison.Ordinal) + 2).TrimStart();
            int end = text.IndexOfAny(new[] { ' ', ':' });
            SearchBox.Text = end > 0 ? text.Substring(0, end) : text;
        }

        #endregion
    }
}
