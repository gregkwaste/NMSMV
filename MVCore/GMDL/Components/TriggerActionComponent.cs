using libMBIN;
using System;
using System.Collections.Generic;
using System.Text;
using libMBIN.NMS.GameComponents;
using System.Security.RightsManagement;
using libMBIN.NMS;

namespace MVCore.GMDL
{
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

        public ActionTrigger() {
            Actions = new List<Action>();
        }

        public ActionTrigger(GcActionTrigger at)
        {
            _template = at;
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
            throw new NotImplementedException();
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
