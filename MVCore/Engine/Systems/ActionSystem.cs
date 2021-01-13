using System;
using System.Collections.Generic;
using System.Globalization;
using System.ServiceModel.Configuration;
using System.Text;
using libMBIN.NMS;
using libMBIN.NMS.GameComponents;
using MathNet.Numerics;
using MVCore.Common;
using MVCore.GMDL;

namespace MVCore.Engine.Systems
{
    public class ActionSystem
    {
        public List<Model> ActionScenes = new List<Model>();
        public Dictionary<Model, string> ActionSceneStateMap = new Dictionary<Model, string>();
        public Dictionary<Model, List<GMDL.Action>> ActionsExecutedInState = new Dictionary<Model, List<GMDL.Action>>();
        public Dictionary<Model, string> PrevActionSceneStateMap = new Dictionary<Model, string>();
        public Engine engine;
        private float timeInterval = 1000.0f / 60.0f;
        private float time = 0.0f;

        public ActionSystem()
        {

        }

        public void CleanUp()
        {
            ActionSceneStateMap.Clear();
            ActionScenes.Clear();
        }

        public void SetEngine(Engine e)
        {
            engine = e;
        }

        public void update(float dt)
        {
            time += dt;

            if (time < timeInterval)
                return;
            else
                time = 0.0f;
            
            foreach (Model m in ActionScenes)
            {
                TriggerActionComponent tac = (TriggerActionComponent) m.Components[m.actionComponentID];
                ActionTriggerState activeState = tac.StateMap[ActionSceneStateMap[m]];
                
                //Apply Actions of state
                Console.WriteLine("Current State {0}", activeState.StateID);

                foreach (ActionTrigger at in activeState.Triggers)
                {
                    //Check if Trigger is activated
                    bool trigger_active = TestTrigger(m, at.Trigger);

                    if (trigger_active)
                    {
                        //Execute actions
                        foreach (GMDL.Action a in at.Actions)
                        {
                            if (!ActionsExecutedInState[m].Contains(a))
                                ExecuteAction(m, a);
                        }
                    }   
                }
            }
        }
        
        private bool TestTrigger(Model m, Trigger t)
        {
            if (t is null)
            {
                return true;
            }
            else if (t is PlayerNearbyEventTrigger)
            {
                return TestPlayerNearbyEventTrigger(m, t as PlayerNearbyEventTrigger);
            }
            else if (t is AnimFrameEventTrigger)
            {
                return TestAnimFrameEventTrigger(m, t as AnimFrameEventTrigger);
            }
            else if (t is StateTimeEventTrigger)
            {
                return true; //I don't have the timers implemented to properly add support for that yet
            }
            return false;
        }

        private bool TestAnimFrameEventTrigger(Model m, AnimFrameEventTrigger t)
        {
            int target_frame = 0;
            int anim_frameCount = engine.animationSys.queryAnimationFrameCount(m, t.Anim);
            
            if (t.StartFromEnd)
            {
                target_frame = anim_frameCount - t.FrameStart;
            } 
            else
                target_frame = t.FrameStart;

            int active_frame = engine.animationSys.queryAnimationFrame(m, t.Anim);

            if (active_frame >= target_frame)
                return true;

            return false;
        }

        private bool TestPlayerNearbyEventTrigger(Model m, PlayerNearbyEventTrigger t)
        {
            //Check the distance of the activeCamera from the model
            float distanceFromCam = (RenderState.activeCam.Position - m.worldPosition).Length;

            //TODO: Check all the inverse shit and the rest trigger parameters

            if (t.Inverse)
            {
                if (distanceFromCam > t.Distance)
                    return true;
            } else
            {
                if (distanceFromCam < t.Distance)
                    return true;
            }
            
            return false;
        }


        private void ExecuteAction(Model m, GMDL.Action action)
        {
            
            if (action is NodeActivationAction)
            {
                ExecuteNodeActivationAction(m, action as NodeActivationAction);
            
            } 
            else if (action is GoToStateAction)
            {
                ExecuteGoToStateAction(m, action as GoToStateAction);
            }
            else if (action is PlayAnimAction)
            {
                ExecutePlayAnimAction(m, action as PlayAnimAction);
            }
            else
            {
                //Console.WriteLine("unimplemented Action execution");
            }
        }

        private void ExecuteGoToStateAction(Model m, GoToStateAction action)
        {
            //Change State
            PrevActionSceneStateMap[m] = ActionSceneStateMap[m];
            ActionSceneStateMap[m] = action.State;
            ActionsExecutedInState[m] = new List<GMDL.Action>(); //Reset executed actions 
        }

        private void ExecutePlayAnimAction(Model m, PlayAnimAction action)
        {
            engine.animationSys.StopActiveLoopAnimations(m); //Not sure if this is correct
            engine.animationSys.StartAnimation(m, action.Anim);
            ActionsExecutedInState[m].Add(action);
        }

        private void ExecuteNodeActivationAction(Model m, GMDL.NodeActivationAction action)
        {
            Model target = null;

            if (action.UseMasterModel)
                m.parentScene.findNode(action.Name, ref target);
            else
                m.findNode(action.Name, ref target);

            if (target == null)
            {
                Console.WriteLine("Node Not found");
            }


            switch (action.NodeActiveState)
            {
                case "Activate":
                    target.IsRenderable = true;
                    break;
                case "Deactivate":
                    target.IsRenderable = false;
                    break;
                case "Toggle":
                    target.IsRenderable = !target.IsRenderable;
                    break;
                default:
                    Console.WriteLine("Not implemented");
                    break;
            }

        }


        public void Add(Model scn)
        {
            if (scn.actionComponentID >= 0)
            {
                ActionScenes.Add(scn);
                ActionSceneStateMap[scn] = "BOOT"; //Add Default State
                PrevActionSceneStateMap[scn] = "NONE"; //Add Default State
                ActionsExecutedInState[scn] = new List<GMDL.Action>();
            }
        }
    }
}
