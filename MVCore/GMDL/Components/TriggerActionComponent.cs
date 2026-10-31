using libMBIN;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using libMBIN.NMS.GameComponents;
using System.Security.RightsManagement;
using libMBIN.NMS;

namespace MVCore.GMDL
{
    public abstract class Trigger
    {
        public NMSTemplate Template;
        public Trigger() { }

        public Trigger(NMSTemplate t)
        {
            Template = t;
        }

        //One line summary for the UI and the action log
        public abstract string Description { get; }
    }

    public class StateTimeEventTrigger : Trigger
    {
        public StateTimeEventTrigger() { }

        public StateTimeEventTrigger(NMSTemplate t) : base(t)
        {

        }

        public override string Description =>
            "StateTime " + Seconds + "s" + (RandomSeconds > 0.0f ? " + rand " + RandomSeconds + "s" : "");

        //Exposed Properties
        public float Seconds
        {
            get {
                return ((GcStateTimeEvent)Template).Seconds;
                }
        }

        public float RandomSeconds
        {
            get
            {
                return ((GcStateTimeEvent)Template).RandomSeconds;
            }
        }
    }

    public class AnimFrameEventTrigger : Trigger
    {
        public AnimFrameEventTrigger() { }

        public AnimFrameEventTrigger(NMSTemplate t) : base(t)
        {

        }

        public override string Description =>
            "AnimFrame " + Anim + " @" + (StartFromEnd ? "end-" : "") + FrameStart;

        //Exposed Properties
        public string Anim
        {
            get
            {
                return ((GcAnimFrameEvent)Template).Anim;
            }
        }

        public int FrameStart
        {
            get
            {
                return ((GcAnimFrameEvent)Template).FrameStart;
            }
        }

        public bool StartFromEnd
        {
            get
            {
                return ((GcAnimFrameEvent)Template).StartFromEnd;
            }
        }
    }

    public class PlayerNearbyEventTrigger : Trigger
    {
        public PlayerNearbyEventTrigger()
        {

        }

        public PlayerNearbyEventTrigger(NMSTemplate t) : base(t)
        {
            
        }

        public override string Description => "PlayerNearby " + (Inverse ? "> " : "< ") + Distance + "m";

        //Exposed Properties

        public string RequirePLayerAction
        {
            get
            {
                return ((GcPlayerNearbyEvent)Template).RequirePlayerAction.ToString();
            }
        }

        public float Angle
        {
            get
            {
                return ((GcPlayerNearbyEvent)Template).Angle;
            }
        }

        public bool AnglePlayerRelative
        {
            get
            {
                return ((GcPlayerNearbyEvent)Template).AnglePlayerRelative;
            }
        }

        public float AngleOffset
        {
            get
            {
                return ((GcPlayerNearbyEvent)Template).AngleOffset;
            }
        }

        public string DistanceCheckType
        {
            get
            {
                return ((GcPlayerNearbyEvent)Template).DistanceCheckType.ToString();
            }
        }

        public bool Inverse
        {
            get
            {
                return ((GcPlayerNearbyEvent)Template).Inverse;
            }
        }

        public float Distance
        {
            get
            {
                return ((GcPlayerNearbyEvent)Template).Distance;
            }
        }
        


    }





    public abstract class Action
    {
        public NMSTemplate Template;
        public Action() { }

        public Action(NMSTemplate t)
        {
            Template = t;
        }

        //One line summary for the UI and the action log
        public abstract string Description { get; }
    }

    //An action the viewer doesn't emulate, kept so the UI lists every action of a trigger
    public class UnsupportedAction : Action
    {
        public UnsupportedAction(NMSTemplate t) : base(t)
        {

        }

        public override string Description => Template.GetType().Name.Replace("Gc", "") + " (not emulated)";
    }

    public class NodeActivationAction:Action
    {
        public NodeActivationAction()
        {

        }

        public NodeActivationAction(NMSTemplate t): base(t)
        {
            
        }

        public override string Description =>
            "NodeActivation " + NodeActiveState + " " + Name + (UseMasterModel ? " (master)" : "");

        //Exposed Properties
        public string Name
        {
            get 
            {
                return ((GcNodeActivationAction) Template).Name;
            }
        }

        public string NodeActiveState
        {
            get
            {
                return ((GcNodeActivationAction)Template).NodeActiveState.ToString();
            }
        }

        public bool UseMasterModel
        {
            get
            {
                return ((GcNodeActivationAction) Template).UseMasterModel;
            }
        }

    }

    public class PlayAnimAction: Action
    {
        public PlayAnimAction()
        {

        }

        public PlayAnimAction(NMSTemplate t): base(t)
        {

        }

        public override string Description => "PlayAnim " + Anim;

        //Expose Properties
        
        public string Anim
        {
            get { return ((GcPlayAnimAction)Template).Anim; }
        }
    }

    public class GoToStateAction: Action
    {
        public GoToStateAction()
        {

        }

        public GoToStateAction(NMSTemplate t): base(t)
        {

        }

        public override string Description => "GoToState " + State + (Broadcast ? " (broadcast)" : "");

        //Exposed Properties
        public string State
        {
            get
            {
                return ((GcGoToStateAction)Template).State;
            }
        }

        public bool Broadcast
        {
            get
            {
                return ((GcGoToStateAction)Template).Broadcast;
            }
        }

        public string BroadcastLevel
        {
            get
            {
                return ((GcGoToStateAction)Template).BroadcastLevel.ToString();
            }
        }

    }

    public class ActionTrigger : INotifyPropertyChanged
    {
        NMSTemplate Template;
        public List<Action> Actions { get; private set; }
        public Trigger Trigger { get; private set; }
        //Set when Trigger is null: triggers without an event fire as soon as their state is entered,
        //events the viewer doesn't emulate never fire
        public bool FiresImmediately { get; private set; } = false;
        public string EventName { get; private set; } = "None";

        public event PropertyChangedEventHandler PropertyChanged;

        public string Description
        {
            get
            {
                if (Trigger != null)
                    return Trigger.Description;
                return FiresImmediately ? "On enter" : EventName.Replace("Gc", "") + " (not emulated)";
            }
        }

        //Fired since its state was last entered, set by the ActionSystem
        private bool _fired = false;
        public bool Fired
        {
            get { return _fired; }
            set
            {
                if (_fired == value)
                    return;
                _fired = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Fired"));
            }
        }
        
        public ActionTrigger() {
            Actions = new List<Action>();
        }

        public ActionTrigger(GcActionTrigger at)
        {
            Template = at;
            
            //Populate Actions
            Actions = new List<Action>();
            foreach (NMSTemplate t in at.Action)
            {
                if (t is GcNodeActivationAction)
                    Actions.Add(new NodeActivationAction(t));
                else if (t is GcGoToStateAction)
                    Actions.Add(new GoToStateAction(t));
                else if (t is GcPlayAnimAction)
                    Actions.Add(new PlayAnimAction(t));
                else if (t.GetType().Name == "GcPlayAudioAction") //By name, like the AudioComponent
                    Actions.Add(new PlayAudioAction(t));
                else if (t is EmptyNode)
                    continue;
                else
                {
                    Actions.Add(new UnsupportedAction(t));
                    Common.CallBacks.Log("Action " + t.GetType().Name + " is not emulated");
                }
            }
            
            //Set Trigger
            if (at.Event is GcPlayerNearbyEvent)
            {
                Trigger = new PlayerNearbyEventTrigger(at.Event);
            } 
            else if (at.Event is GcStateTimeEvent)
            {
                Trigger = new StateTimeEventTrigger(at.Event);
            }
            else if (at.Event is GcAnimFrameEvent)
            {
                Trigger = new AnimFrameEventTrigger(at.Event);
            }
            else
            {
                Trigger = null;
                EventName = at.Event?.GetType().Name ?? "None";
                FiresImmediately = at.Event == null || at.Event is EmptyNode;
                if (!FiresImmediately)
                    Common.CallBacks.Log("Event " + EventName + " is not emulated, its trigger never fires");
            }
            


        }

        //Exposed Properties


    }

    public class ActionTriggerState : INotifyPropertyChanged
    {
        GcActionTriggerState Template;
        public List<ActionTrigger> Triggers { get; private set; }
        public TriggerActionComponent Owner; //Lets the UI force this state on its component
        public ActionTriggerState() { }

        public event PropertyChangedEventHandler PropertyChanged;

        //The entity is in this state, set by the ActionSystem
        private bool _isCurrent = false;
        public bool IsCurrent
        {
            get { return _isCurrent; }
            set
            {
                if (_isCurrent == value)
                    return;
                _isCurrent = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsCurrent"));
            }
        }

        public ActionTriggerState(GcActionTriggerState ats)
        {
            Template = ats;
            Triggers = new List<ActionTrigger>();
            
            //Populate Triggers
            foreach (GcActionTrigger at in Template.Triggers)
            {
                Triggers.Add(new ActionTrigger(at));
            }
        }

        //Exposed Properties
        public string StateID
        {
            get
            {
                return Template.StateID;
            }
        }

    }


   public class TriggerActionComponent : IComponent, INotifyPropertyChanged
    {
        public GcTriggerActionComponentData Template;
        public List<ActionTriggerState> States { get; private set; }
        public Dictionary<string, ActionTriggerState> StateMap;

        public event PropertyChangedEventHandler PropertyChanged;

        //Emulation status shown in the UI, set by the ActionSystem
        private string _currentStateID = "Not running";
        public string CurrentStateID
        {
            get { return _currentStateID; }
            set
            {
                if (_currentStateID == value)
                    return;
                _currentStateID = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CurrentStateID"));
            }
        }

        public TriggerActionComponent()
        {
            Template = new GcTriggerActionComponentData();
            States = new List<ActionTriggerState>();
            StateMap = new Dictionary<string, ActionTriggerState>();
        }

        public TriggerActionComponent(GcTriggerActionComponentData tacd)
        {
            Template = tacd;
            //Populate States
            States = new List<ActionTriggerState>();
            StateMap = new Dictionary<string, ActionTriggerState>();
            foreach (GcActionTriggerState s in tacd.States)
            {
                ActionTriggerState ats = new ActionTriggerState(s);
                ats.Owner = this;
                States.Add(ats);
                StateMap[ats.StateID] = ats;
            }
        }

        public IComponent Clone()
        {
            //Copies share the entity data
            return new TriggerActionComponent(Template);
        }

        //Exposed Properties

        public bool HideModel
        {
            get { return Template.HideModel; }
        }

        public bool StartInactive
        {
            get { return Template.StartInactive; }
        }

    }
}
