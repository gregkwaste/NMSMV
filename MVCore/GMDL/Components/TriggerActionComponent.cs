using libMBIN;
using System;
using System.Collections.Generic;
using System.Text;
using libMBIN.NMS.GameComponents;
using System.Security.RightsManagement;
using libMBIN.NMS;

namespace MVCore.GMDL
{
    public abstract class Trigger
    {
        public NMSTemplate _template;
        public Trigger() { }

        public Trigger(NMSTemplate t)
        {
            _template = t;
        }
    }

    public class StateTimeEventTrigger : Trigger
    {
        public StateTimeEventTrigger() { }

        public StateTimeEventTrigger(NMSTemplate t) : base(t)
        {

        }

        //Exposed Properties
        public float Seconds
        {
            get {
                return ((GcStateTimeEvent)_template).Seconds;
                }
        }

        public float RandomSeconds
        {
            get
            {
                return ((GcStateTimeEvent)_template).RandomSeconds;
            }
        }
    }

    public class AnimFrameEventTrigger : Trigger
    {
        public AnimFrameEventTrigger() { }

        public AnimFrameEventTrigger(NMSTemplate t) : base(t)
        {

        }

        //Exposed Properties
        public string Anim
        {
            get
            {
                return ((GcAnimFrameEvent)_template).Anim;
            }
        }

        public int FrameStart
        {
            get
            {
                return ((GcAnimFrameEvent)_template).FrameStart;
            }
        }

        public bool StartFromEnd
        {
            get
            {
                return ((GcAnimFrameEvent)_template).StartFromEnd;
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

        //Exposed Properties

        public string RequirePLayerAction
        {
            get
            {
                return ((GcPlayerNearbyEvent)_template).RequirePlayerAction.ToString();
            }
        }

        public float Angle
        {
            get
            {
                return ((GcPlayerNearbyEvent)_template).Angle;
            }
        }

        public bool AnglePlayerRelative
        {
            get
            {
                return ((GcPlayerNearbyEvent)_template).AnglePlayerRelative;
            }
        }

        public float AngleOffset
        {
            get
            {
                return ((GcPlayerNearbyEvent)_template).AngleOffset;
            }
        }

        public string DistanceCheckType
        {
            get
            {
                return ((GcPlayerNearbyEvent)_template).DistanceCheckType.ToString();
            }
        }

        public bool Inverse
        {
            get
            {
                return ((GcPlayerNearbyEvent)_template).Inverse;
            }
        }

        public float Distance
        {
            get
            {
                return ((GcPlayerNearbyEvent)_template).Distance;
            }
        }
        


    }





    public abstract class Action
    {
        public NMSTemplate _template;
        public Action() { }

        public Action(NMSTemplate t)
        {
            _template = t;
        }
    }

    public class NodeActivationAction:Action
    {
        public NodeActivationAction()
        {

        }

        public NodeActivationAction(NMSTemplate t): base(t)
        {
            
        }

        //Exposed Properties
        public string Name
        {
            get 
            {
                return ((GcNodeActivationAction) _template).Name;
            }
        }

        public string NodeActiveState
        {
            get
            {
                return ((GcNodeActivationAction)_template).NodeActiveState.ToString();
            }
        }

        public bool UseMasterModel
        {
            get
            {
                return ((GcNodeActivationAction) _template).UseMasterModel;
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

        //Expose Properties
        
        public string Anim
        {
            get { return ((GcPlayAnimAction)_template).Anim; }
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

        //Exposed Properties
        public string State
        {
            get
            {
                return ((GcGoToStateAction)_template).State;
            }
        }

        public bool Broadcast
        {
            get
            {
                return ((GcGoToStateAction)_template).Broadcast;
            }
        }

        public string BroadcastLevel
        {
            get
            {
                return ((GcGoToStateAction)_template).BroadcastLevel.ToString();
            }
        }

    }

    public class ActionTrigger
    {
        NMSTemplate _template;
        public List<Action> Actions;
        public Trigger Trigger;
        
        public ActionTrigger() {
            Actions = new List<Action>();
        }

        public ActionTrigger(GcActionTrigger at)
        {
            _template = at;
            
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
                else if (t is EmptyNode)
                    continue;
                else
                    Console.WriteLine("Non Implemented Action");
            }
            
            //Set Trigger
            if (at.Trigger is GcPlayerNearbyEvent)
            {
                Trigger = new PlayerNearbyEventTrigger(at.Trigger);
            } 
            else if (at.Trigger is GcStateTimeEvent)
            {
                Trigger = new StateTimeEventTrigger(at.Trigger);
            }
            else if (at.Trigger is GcAnimFrameEvent)
            {
                Trigger = new AnimFrameEventTrigger(at.Trigger);
            }
            else
            {
                Console.WriteLine("Non Implemented Trigger");
                Trigger = null;
            }
            


        }

        //Exposed Properties


    }

    public class ActionTriggerState
    {
        GcActionTriggerState _template;
        public List<ActionTrigger> Triggers;
        public ActionTriggerState() { }

        public ActionTriggerState(GcActionTriggerState ats)
        {
            _template = ats;
            Triggers = new List<ActionTrigger>();
            
            //Populate Triggers
            foreach (GcActionTrigger at in _template.Triggers)
            {
                Triggers.Add(new ActionTrigger(at));
            }
        }

        //Exposed Properties
        public string StateID
        {
            get
            {
                return _template.StateID;
            }
        }

    }


   public class TriggerActionComponent : Component
    {
        public GcTriggerActionComponentData _template;
        public List<ActionTriggerState> States;
        public Dictionary<string, ActionTriggerState> StateMap;

        public TriggerActionComponent()
        {
            States = new List<ActionTriggerState>();
            StateMap = new Dictionary<string, ActionTriggerState>();
        }

        public TriggerActionComponent(GcTriggerActionComponentData tacd)
        {
            _template = tacd;
            //Populate States
            States = new List<ActionTriggerState>();
            StateMap = new Dictionary<string, ActionTriggerState>();
            foreach (GcActionTriggerState s in tacd.States)
            {
                ActionTriggerState ats = new ActionTriggerState(s);
                States.Add(ats);
                StateMap[ats.StateID] = ats;
            }
        }

        public override Component Clone()
        {
            //TODO: Make sure to properly populate the new object
            return new TriggerActionComponent();
        }

        //Exposed Properties

        public bool HideModel
        {
            get { return _template.HideModel; }
        }

        public bool StartInteractive
        {
            get { return _template.StartInactive; }
        }

    }
}
