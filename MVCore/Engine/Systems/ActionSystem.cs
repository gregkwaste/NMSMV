using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MVCore.GMDL;

namespace MVCore.Engine.Systems
{
    public class ActionSystem
    {
        List<Scene> ActionScenes = new List<Scene>();
        Dictionary<Scene, string> ActionSceneStateMap = new Dictionary<Scene, string>();


        public ActionSystem()
        {

        }

        public void update()
        {
            foreach (Scene scn in ActionScenes)
            {
                TriggerActionComponent tac = (TriggerActionComponent) scn.Components[scn.actionComponentID];
                ActionTriggerState activeState = tac.StateMap[ActionSceneStateMap[scn]];

                //Apply Actions of state

                if (activeState.Triggers.Count > 1)
                    Console.WriteLine("CHECK WHAT IS GOING ON");

                ActionTrigger at = activeState.Triggers[0];

                foreach (GMDL.Action a in at.Actions)
                {
                    ExecuteAction(scn, a);
                }
            }
        }


        private void ExecuteAction(GMDL.Scene scn, GMDL.Action action)
        {
            
            if (action is NodeActivationAction)
            {
                ExecuteNodeActivationAction(scn, action as NodeActivationAction);
                
            } else
            {
                Console.WriteLine("unimplemented Action execution");
            }
        }

        private void ExecuteNodeActivationAction(GMDL.Scene scn, GMDL.NodeActivationAction action)
        {
            Model m = null;
            scn.findNode(action.Name, ref m);
            
            if (m == null)
            {
                Console.WriteLine("Node Not found");
            }


            switch (action.NodeActiveState)
            {
                case "Activate":
                    m.active = true;
                    break;
                case "Deactivate":
                    m.active = false;
                    break;
                case "Toggle":
                    m.active = !m.active;
                    break;
                default:
                    Console.WriteLine("Not implemented");
                    break;
            }

        }


        public void Add(Scene scn)
        {
            if (scn.actionComponentID >= 0)
            {
                ActionScenes.Add(scn);
                ActionSceneStateMap[scn] = "BOOT"; //Add Default State
            }
        }
    }
}
