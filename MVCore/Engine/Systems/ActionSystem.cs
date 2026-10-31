using System;
using System.Collections.Generic;
using MVCore.Common;
using MVCore.GMDL;
using OpenTK.Mathematics;

namespace MVCore.Engine.Systems
{
    public class ActionSystem
    {
        //Runtime state of one node with a TriggerActionComponent
        private class ActionSceneState
        {
            public TriggerActionComponent Component;
            public ActionTriggerState State; //null when the component has no states
            public float StateTime = 0.0f; //ms since the state was entered
            public bool Active = true;
            //Each trigger fires once per state entry
            public readonly HashSet<ActionTrigger> Fired = new HashSet<ActionTrigger>();
            //StateTime targets in ms, rolled on state entry
            public readonly Dictionary<ActionTrigger, float> TimerTargets = new Dictionary<ActionTrigger, float>();
            //CompletedCount of the AnimFrame triggers' clips on state entry
            public readonly Dictionary<ActionTrigger, int> AnimCompletions = new Dictionary<ActionTrigger, int>();
        }

        //A state sent to an entity from outside its own triggers: an interact press or a broadcast GoToState
        private class SentState
        {
            public Model Sender;
            public string State;
            public bool Broadcast; //To every entity that has the state, otherwise only to the sender
            public bool ExcludeSender;
            public string Reason;
        }

        private readonly List<Model> actionScenes = new List<Model>();
        private readonly List<Model> interactScenes = new List<Model>();
        public IReadOnlyList<Model> ActionScenes => actionScenes;
        public IReadOnlyList<Model> InteractScenes => interactScenes; //Nodes with an InteractionComponent
        private readonly Dictionary<Model, ActionSceneState> sceneStates = new Dictionary<Model, ActionSceneState>();
        //Visibility of the nodes the actions changed, restored when emulation stops
        private readonly Dictionary<Model, bool> originalVisibility = new Dictionary<Model, bool>();
        private readonly HashSet<string> loggedOnce = new HashSet<string>();
        private readonly Random rng = new Random();
        private readonly AnimationSystem animationSystem;
        private readonly AudioSystem audioSystem;
        public bool Running { get; private set; } = false;
        private volatile bool restartRequested = false; //Set from the UI thread, handled on the next update
        private volatile bool playerInteractRequested = false;
        //States forced from the UI, applied on the next update
        private readonly System.Collections.Concurrent.ConcurrentQueue<ActionTriggerState> forcedStates =
            new System.Collections.Concurrent.ConcurrentQueue<ActionTriggerState>();
        private readonly System.Collections.Concurrent.ConcurrentQueue<SentState> sentStates =
            new System.Collections.Concurrent.ConcurrentQueue<SentState>();
        //Triggers fired from the UI, for events the viewer can't emulate
        private readonly System.Collections.Concurrent.ConcurrentQueue<ActionTrigger> firedTriggers =
            new System.Collections.Concurrent.ConcurrentQueue<ActionTrigger>();

        //Every [Actions] line, for the event log of the Scene Systems panel
        public event System.Action<string> EventLogged;

        public ActionSystem(AnimationSystem animationSystem, AudioSystem audioSystem)
        {
            this.animationSystem = animationSystem ?? throw new ArgumentNullException(nameof(animationSystem));
            this.audioSystem = audioSystem ?? throw new ArgumentNullException(nameof(audioSystem));
        }

        public void CleanUp()
        {
            actionScenes.Clear();
            interactScenes.Clear();
            sentStates.Clear();
            sceneStates.Clear();
            originalVisibility.Clear();
            loggedOnce.Clear();
            Running = false;
        }

        //Turning emulation on restarts every entity from its initial state, turning it off restores the
        //visibility the actions changed
        public void SetRunning(bool run)
        {
            if (run == Running)
                return;
            Running = run;

            restoreVisibility();
            if (run)
            {
                log("[Actions] Emulation started, " + actionScenes.Count + " entities");
                foreach (Model m in actionScenes)
                    initState(m);
            }
            else
            {
                log("[Actions] Emulation stopped");
                audioSystem.StopActionSounds(); //Sounds the actions started end with them
                foreach (Model m in actionScenes)
                    clearUIState(sceneStates[m].Component, "Not running");
            }
        }

        //Sends every entity back to its initial state
        public void RequestRestart()
        {
            restartRequested = true;
        }

        //Puts the state's entity in that state, the way a game-side GoToState would
        public void RequestState(ActionTriggerState state)
        {
            forcedStates.Enqueue(state);
        }

        //Runs a trigger's actions as if its event had happened
        public void RequestFire(ActionTrigger at)
        {
            firedTriggers.Enqueue(at);
        }

        //Live state of a trigger of the entity's current state, for the Systems panel
        public string TriggerProgress(Model m, ActionTrigger at)
        {
            if (!Running || !sceneStates.TryGetValue(m, out ActionSceneState st) || st.State == null ||
                !st.State.Triggers.Contains(at))
                return "";
            if (st.Fired.Contains(at))
                return "fired";
            if (!st.Active)
                return "inactive";

            switch (at.Trigger)
            {
                case null:
                    return at.FiresImmediately ? "fires immediately" : "not emulated, use Fire";
                case PlayerNearbyEventTrigger t:
                    return distanceFromCamera(m).ToString("0.0") + "m, needs " + (t.Inverse ? "> " : "< ") + t.Distance + "m";
                case StateTimeEventTrigger _:
                    float left = Math.Max(st.TimerTargets[at] - st.StateTime, 0.0f) / 1000.0f;
                    return left.ToString("0.0") + "s left";
                case AnimFrameEventTrigger t:
                    AnimData ad = findAnimation(m, t.Anim);
                    if (ad == null)
                        return t.Anim + " not found";
                    if (!ad.IsPlaying)
                        return t.Anim + " not playing";
                    int target = t.StartFromEnd ? Math.Max(ad.clipFrameCount - 1 - t.FrameStart, 0) : t.FrameStart;
                    return t.Anim + " frame " + ad.ActiveFrame + " / " + target;
            }
            return "";
        }

        //The player's interact key: every interaction within its InteractDistance of the camera
        public void RequestPlayerInteract()
        {
            playerInteractRequested = true;
        }

        //Interactions the camera is close enough to use
        public List<Model> InteractablesInRange()
        {
            List<Model> result = new List<Model>();
            foreach (Model m in interactScenes)
            {
                if (m.Components[m.hasComponent(typeof(InteractionComponent))] is InteractionComponent ic &&
                    distanceFromCamera(m) <= interactRange(ic))
                    result.Add(m);
            }
            return result;
        }

        private static float interactRange(InteractionComponent ic)
        {
            return ic.InteractDistance > 0.0f ? ic.InteractDistance : 5.0f;
        }

        //Distance of the node to the camera, the player's stand-in. The scene is drawn with worldMat * rotMat
        //(the Q/E scene rotation), so the node's position is rotated the same way first
        public static float DistanceFromCamera(Model m)
        {
            Vector3 pos = (new Vector4(m.worldPosition, 1.0f) * RenderState.rotMat).Xyz;
            return (RenderState.activeCam.Position - pos).Length;
        }

        private static float distanceFromCamera(Model m)
        {
            return DistanceFromCamera(m);
        }

        //An interact press on one entity: sends the interaction's TriggerAction as a state
        public void RequestInteract(InteractionComponent ic)
        {
            Model m = interactScenes.Find(n => n.Components.Contains(ic));
            if (m == null || string.IsNullOrEmpty(ic.TriggerAction))
            {
                log("[Actions] Interaction has no TriggerAction, nothing to send");
                return;
            }
            sentStates.Enqueue(new SentState
            {
                Sender = m, State = ic.TriggerAction, Broadcast = ic.BroadcastTriggerAction, Reason = "interact"
            });
        }

        public void update(float dt)
        {
            if (!Running)
            {
                if (!forcedStates.IsEmpty)
                    log("[Actions] Emulation is off, forced states are dropped");
                forcedStates.Clear();
                sentStates.Clear();
                firedTriggers.Clear();
                playerInteractRequested = false;
                return;
            }

            if (restartRequested)
            {
                restartRequested = false;
                restoreVisibility();
                log("[Actions] Emulation restarted");
                audioSystem.StopActionSounds(); //Sounds the actions started end with them

                //Several TriggerActionComponents can use the same animation node. Reset each component
                //once before its actions return to their initial states.
                HashSet<Model> resetAnimNodes = new HashSet<Model>();
                foreach (Model m in actionScenes)
                {
                    Model animNode = findAnimNode(m);
                    if (animNode != null && resetAnimNodes.Add(animNode))
                        animationSystem.ResetAnimations(animNode);
                }

                foreach (Model m in actionScenes)
                    initState(m);
            }

            while (forcedStates.TryDequeue(out ActionTriggerState forced))
            {
                foreach (Model m in actionScenes)
                {
                    ActionSceneState fst = sceneStates[m];
                    if (fst.Component != forced.Owner)
                        continue;
                    fst.Active = true; //Forcing a state also wakes an inactive component
                    enterState(m, fst, forced.StateID, "forced from the UI");
                }
            }

            if (playerInteractRequested)
            {
                playerInteractRequested = false;
                playerInteract();
            }

            while (sentStates.TryDequeue(out SentState sent))
                deliverState(sent);

            while (firedTriggers.TryDequeue(out ActionTrigger fire))
                fireTrigger(fire);

            foreach (Model m in actionScenes)
            {
                ActionSceneState st = sceneStates[m];
                if (!st.Active || st.State == null)
                    continue;

                st.StateTime += dt;

                string nextState = null;
                foreach (ActionTrigger at in st.State.Triggers)
                {
                    if (st.Fired.Contains(at) || !TestTrigger(m, st, at))
                        continue;

                    st.Fired.Add(at);
                    at.Fired = true;
                    log("[Actions] " + m.Name + " [" + st.State.StateID + "] " + at.Description + " fired");
                    foreach (GMDL.Action a in at.Actions)
                        ExecuteAction(m, a, ref nextState);

                    //The state changes after the trigger's actions, the old state's other triggers don't run
                    if (nextState != null)
                    {
                        enterState(m, st, nextState, at.Description);
                        break;
                    }
                }
            }
        }

        private void initState(Model m)
        {
            ActionSceneState st = sceneStates[m];
            TriggerActionComponent tac = st.Component;
            st.Active = !tac.StartInactive;
            st.State = null;
            clearUIState(tac, "NONE");

            if (tac.HideModel)
                setVisible(m, false);

            string initial = null;
            if (tac.StateMap.ContainsKey("BOOT"))
                initial = "BOOT";
            else if (tac.States.Count > 0)
                initial = tac.States[0].StateID;

            if (initial == null)
            {
                log("[Actions] " + m.Name + " has no states");
                tac.CurrentStateID = "No states";
                return;
            }

            enterState(m, st, initial, "start");
            if (!st.Active)
            {
                log("[Actions] " + m.Name + " starts inactive, waiting for a NodeActivation");
                tac.CurrentStateID = initial + " (inactive)";
            }
        }

        private void enterState(Model m, ActionSceneState st, string stateID, string reason)
        {
            if (!st.Component.StateMap.TryGetValue(stateID, out ActionTriggerState next))
            {
                log("[Actions] " + m.Name + ": unknown state " + stateID + ", staying in " +
                              (st.State?.StateID ?? "NONE"));
                return;
            }

            log("[Actions] " + m.Name + ": " + (st.State?.StateID ?? "NONE") + " -> " + stateID +
                          " (" + reason + ")");
            if (st.State != null)
                st.State.IsCurrent = false;
            st.State = next;
            next.IsCurrent = true;
            foreach (ActionTrigger at in next.Triggers)
                at.Fired = false;
            st.Component.CurrentStateID = stateID;
            st.StateTime = 0.0f;
            st.Fired.Clear();
            st.TimerTargets.Clear();
            st.AnimCompletions.Clear();

            foreach (ActionTrigger at in next.Triggers)
            {
                if (at.Trigger is StateTimeEventTrigger stt)
                    st.TimerTargets[at] = 1000.0f * (stt.Seconds + (float) rng.NextDouble() * stt.RandomSeconds);
                else if (at.Trigger is AnimFrameEventTrigger aft)
                    st.AnimCompletions[at] = findAnimation(m, aft.Anim)?.CompletedCount ?? 0;
            }
        }

        private bool TestTrigger(Model m, ActionSceneState st, ActionTrigger at)
        {
            switch (at.Trigger)
            {
                case null:
                    return at.FiresImmediately; //Events the viewer can't emulate never fire
                case PlayerNearbyEventTrigger t:
                    return TestPlayerNearbyEventTrigger(m, t);
                case AnimFrameEventTrigger t:
                    return TestAnimFrameEventTrigger(m, st, at, t);
                case StateTimeEventTrigger _:
                    return st.StateTime >= st.TimerTargets[at];
            }
            return false;
        }

        private bool TestAnimFrameEventTrigger(Model m, ActionSceneState st, ActionTrigger at, AnimFrameEventTrigger t)
        {
            AnimData ad = findAnimation(m, t.Anim);
            if (ad == null)
            {
                logOnce(m.Name + "/anim/" + t.Anim, "[Actions] " + m.Name + ": animation " + t.Anim +
                        " not found, its AnimFrame trigger never fires");
                return false;
            }

            if (ad.IsPlaying)
            {
                int target = t.StartFromEnd ? Math.Max(ad.clipFrameCount - 1 - t.FrameStart, 0) : t.FrameStart;
                return ad.ActiveFrame >= target;
            }

            //A one-shot that ran to its end since the state was entered went past every frame
            st.AnimCompletions.TryGetValue(at, out int completions);
            return ad.CompletedCount > completions;
        }

        private bool TestPlayerNearbyEventTrigger(Model m, PlayerNearbyEventTrigger t)
        {
            //The camera stands in for the player
            float distanceFromCam = distanceFromCamera(m);

            //TODO: Angle, AngleOffset, AnglePlayerRelative, DistanceCheckType and RequirePlayerAction
            if (t.Inverse)
                return distanceFromCam > t.Distance;
            return distanceFromCam < t.Distance;
        }


        private void ExecuteAction(Model m, GMDL.Action action, ref string nextState)
        {
            log("[Actions]   -> " + action.Description);

            if (action is NodeActivationAction na)
                ExecuteNodeActivationAction(m, na);
            else if (action is GoToStateAction gs)
            {
                nextState = gs.State;
                //A broadcast also sends the state to the other entities, on the next update
                //TODO: BroadcastLevel, every broadcast reaches the whole loaded scene
                if (gs.Broadcast)
                    sentStates.Enqueue(new SentState
                    {
                        Sender = m, State = gs.State, Broadcast = true, ExcludeSender = true, Reason = "broadcast"
                    });
            }
            else if (action is PlayAnimAction pa)
                ExecutePlayAnimAction(m, pa);
            else if (action is PlayAudioAction audio)
                audioSystem.PlayAction(m, audio);
        }

        private void ExecutePlayAnimAction(Model m, PlayAnimAction action)
        {
            Model animNode = findAnimNode(m);
            AnimComponent ac = animNode?.Components[animNode.animComponentID] as AnimComponent;
            AnimData ad = ac?.getAnimation(action.Anim);
            if (ad == null)
            {
                log("[Actions] " + m.Name + ": animation " + action.Anim + " not found");
                return;
            }

            animationSystem.Add(animNode);

            //A new loop replaces the playing loops, one-shots and additive clips play on top
            if (!ad.IsOneShot && !ad.Additive)
            {
                foreach (AnimData other in ac.Animations)
                {
                    if (other != ad && other.IsPlaying && !other.IsOneShot && !other.Additive)
                        other.IsPlaying = false;
                }
            }

            //One-shots restart, loops keep running
            if (ad.IsOneShot || !ad.IsPlaying)
                ad.IsPlaying = true;
            sceneStates.TryGetValue(m, out ActionSceneState st);
            ad.StartedBy = m.Name + " [" + (st?.State?.StateID ?? "NONE") + "]";

            if (!RenderState.renderSettings.ToggleAnimations)
                logOnce("toggleanims", "[Actions] Animations are toggled off, PlayAnim actions won't be visible");
        }

        private void ExecuteNodeActivationAction(Model m, NodeActivationAction action)
        {
            Model target = null;

            if (action.UseMasterModel && m.parentScene != null)
                m.parentScene.findNode(action.Name, ref target);
            else
                m.findNode(action.Name, ref target);

            if (target == null)
            {
                log("[Actions] " + m.Name + ": node " + action.Name + " not found");
                return;
            }

            switch (action.NodeActiveState)
            {
                case "Activate":
                    setVisible(target, true);
                    //Activating a node also wakes its TriggerActionComponent if it started inactive
                    if (sceneStates.TryGetValue(target, out ActionSceneState tst) && !tst.Active)
                    {
                        tst.Active = true;
                        tst.Component.CurrentStateID = tst.State?.StateID ?? "NONE";
                        log("[Actions] " + target.Name + " activated");
                    }
                    break;
                case "Deactivate":
                case "Remove":
                    setVisible(target, false);
                    break;
                case "Toggle":
                    setVisible(target, !target.IsRenderable);
                    break;
                case "RemoveChildren":
                    foreach (Model child in target.Children)
                        setVisible(child, false);
                    break;
                default:
                    logOnce(m.Name + "/nodestate/" + action.NodeActiveState, "[Actions] NodeActivation " +
                            action.NodeActiveState + " is not emulated");
                    break;
            }
        }

        private void fireTrigger(ActionTrigger fire)
        {
            foreach (Model m in actionScenes)
            {
                ActionSceneState st = sceneStates[m];
                ActionTriggerState owner = st.Component.States.Find(s => s.Triggers.Contains(fire));
                if (owner == null)
                    continue;

                log("[Actions] " + m.Name + " [" + owner.StateID + "] " + fire.Description + " fired from the UI");
                st.Active = true; //Like a forced state, firing wakes an inactive component
                if (st.State == owner)
                {
                    st.Fired.Add(fire);
                    fire.Fired = true;
                }

                string nextState = null;
                foreach (GMDL.Action a in fire.Actions)
                    ExecuteAction(m, a, ref nextState);
                if (nextState != null)
                    enterState(m, st, nextState, fire.Description + ", fired from the UI");
                return;
            }
        }

        private void playerInteract()
        {
            List<Model> inRange = InteractablesInRange();
            if (inRange.Count == 0)
            {
                Model nearest = null;
                foreach (Model m in interactScenes)
                {
                    if (nearest == null || distanceFromCamera(m) < distanceFromCamera(nearest))
                        nearest = m;
                }
                log("[Actions] Player interacts: nothing in range" + (nearest == null ? "" :
                    ", nearest is " + nearest.Name + " at " + distanceFromCamera(nearest).ToString("0.0") + "m"));
                return;
            }

            log("[Actions] Player interacts: " + inRange.Count + " in range (" + string.Join(", ", inRange.ConvertAll(m => m.Name)) + ")");
            //Interactions stacked on one spot often broadcast the same state, it's sent once
            HashSet<string> broadcasted = new HashSet<string>();
            foreach (Model m in inRange)
            {
                InteractionComponent ic = (InteractionComponent) m.Components[m.hasComponent(typeof(InteractionComponent))];
                if (string.IsNullOrEmpty(ic.TriggerAction))
                    continue;
                if (ic.BroadcastTriggerAction && !broadcasted.Add(ic.TriggerAction))
                    continue;
                sentStates.Enqueue(new SentState
                {
                    Sender = m, State = ic.TriggerAction, Broadcast = ic.BroadcastTriggerAction, Reason = "player interact"
                });
            }
        }

        private void deliverState(SentState sent)
        {
            IEnumerable<Model> targets = sent.Broadcast ? actionScenes : new[] { sent.Sender };
            List<Model> receivers = new List<Model>();
            foreach (Model t in targets)
            {
                if (sent.ExcludeSender && t == sent.Sender)
                    continue;
                if (!sceneStates.TryGetValue(t, out ActionSceneState st) || !st.Component.StateMap.ContainsKey(sent.State))
                    continue;
                if (!st.Active)
                {
                    log("[Actions] " + t.Name + " is inactive and ignores " + sent.State);
                    continue;
                }
                receivers.Add(t);
            }

            log("[Actions] " + sent.Sender.Name + (sent.Broadcast ? " broadcasts " : " sends ") + sent.State + " (" +
                sent.Reason + "): " + (receivers.Count == 0 ? "no entity has that state" : receivers.Count + " receiver(s)"));
            foreach (Model t in receivers)
                enterState(t, sceneStates[t], sent.State, sent.Reason + " from " + sent.Sender.Name);
        }

        private void setVisible(Model node, bool visible)
        {
            if (!originalVisibility.ContainsKey(node))
                originalVisibility[node] = node.IsRenderable;
            node.IsRenderable = visible;
        }

        private void restoreVisibility()
        {
            foreach (KeyValuePair<Model, bool> kv in originalVisibility)
                kv.Key.IsRenderable = kv.Value;
            originalVisibility.Clear();
        }

        //The animations usually sit on the same node as the actions, otherwise on the closest ancestor
        private static Model findAnimNode(Model m)
        {
            for (Model n = m; n != null; n = n.parent)
            {
                if (n.animComponentID >= 0)
                    return n;
            }
            return null;
        }

        private static AnimData findAnimation(Model m, string anim)
        {
            Model animNode = findAnimNode(m);
            AnimComponent ac = animNode?.Components[animNode.animComponentID] as AnimComponent;
            return ac?.getAnimation(anim);
        }

        private static void clearUIState(TriggerActionComponent tac, string status)
        {
            tac.CurrentStateID = status;
            foreach (ActionTriggerState s in tac.States)
            {
                s.IsCurrent = false;
                foreach (ActionTrigger at in s.Triggers)
                    at.Fired = false;
            }
        }

        private void logOnce(string key, string message)
        {
            if (loggedOnce.Add(key))
                log(message);
        }

        private void log(string message)
        {
            CallBacks.Log(message);
            EventLogged?.Invoke(message);
        }

        public void Remove(Model scn)
        {
            actionScenes.Remove(scn);
            interactScenes.Remove(scn);
            sceneStates.Remove(scn);
            originalVisibility.Remove(scn);
        }

        public void Add(Model scn)
        {
            if (scn.hasComponent(typeof(InteractionComponent)) >= 0 && !interactScenes.Contains(scn))
                interactScenes.Add(scn);

            if (scn.actionComponentID < 0 || actionScenes.Contains(scn)) //Scenes are re-scanned after a reference loads
                return;

            if (!(scn.Components[scn.actionComponentID] is TriggerActionComponent tac))
                return;

            actionScenes.Add(scn);
            sceneStates[scn] = new ActionSceneState { Component = tac };
            if (Running)
                initState(scn);
        }
    }
}
