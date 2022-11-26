﻿using System;
using System.Collections.Generic;
using System.Security.Policy;
using System.Security.RightsManagement;
using System.Text;
using MVCore.GMDL;
using OpenTK.Mathematics;

namespace MVCore.Engine.Systems
{
    public class AnimationSystem
    {
        public List<Model> AnimScenes = new List<Model>();
        public Dictionary<Model, List<AnimData>> AnimQueues = new Dictionary<Model, List<AnimData>>();
        public Engine engine;
        private float timeInterval = 20.0f;
        private float time = 0.0f;

        public AnimationSystem()
        {
            
        }

        public void CleanUp()
        {
            AnimScenes.Clear();
            AnimQueues.Clear();
        }

        public void SetEngine(Engine e)
        {
            engine = e;
        }

        public void update(float dt)
        {
            //Clear queues for all the joints
            foreach (Model anim_model in AnimScenes)
            {
                foreach (Model m in anim_model.parentScene.nodeDict.Values)
                {
                    m.PositionQueue.Clear();
                    m.ScaleQueue.Clear();
                    m.RotationQueue.Clear();
                }
            }
                
            foreach (Model anim_model in AnimScenes)
            {
                AnimComponent ac = anim_model._components[anim_model.hasComponent(typeof(AnimComponent))] as AnimComponent;
                bool found_first_active_anim = false;

                foreach (AnimData ad in ac.Animations)
                {
                    if (ad.IsPlaying)
                    {
                        if (!ad.loaded)
                            ad.loadData();
                        
                        found_first_active_anim = true;
                        //Load updated local joint transforms
                        foreach (libMBIN.NMS.Toolkit.TkAnimNodeData node in ad.animMeta.NodeData)
                        {
                            if (!anim_model.parentScene.nodeDict.ContainsKey(node.Node))
                                continue;

                            Model m = anim_model.parentScene.nodeDict[node.Node];

                            //Transforms
                            Vector3 p = new Vector3();
                            Vector3 s = new Vector3();
                            Quaternion q = new Quaternion();

                            ad.getCurrentTransform(ref p, ref s, ref q, node.Node);

                            m.RotationQueue.Add(q);
                            m.PositionQueue.Add(p);
                            m.ScaleQueue.Add(s);

                            //ad.applyNodeTransform(tj, node.Node);
                        }

                        //Once the current frame data is fetched, progress to the next frame
                        ad.update(dt);
                    } 
                }

                //Calculate Blending Factors
                List<float> blendingFactors = new List<float>();
                float totalWeight = 1.0f;
                foreach (AnimData ad in ac.Animations)
                {
                    if (ad.AnimType == libMBIN.NMS.Toolkit.TkAnimationData.AnimTypeEnum.OneShot)
                    {
                        //Calculate blending factor based on the animation progress
                        //float bF = ad.ActiveFrame / (ad.FrameEnd - ad.FrameStart);
                        float bF = 0.0f;
                        blendingFactors.Add(bF);
                        totalWeight -= bF;
                    }
                    else
                    {
                        blendingFactors.Add(1.0f);
                    }
                        
                }

                //Blend Transforms and apply
                foreach (Model m in anim_model.parentScene.jointDict.Values)
                {
                    if (m.PositionQueue.Count == 0)
                    {
                        //Keep last transforms
                        m.localPosition = m._localPosition;
                        m.localScale = m._localScale;
                        m.localRotation = m._localRotation;
                        continue;
                    }
                        
                    float blendFactor = 1.0f / m.PositionQueue.Count;

                    Vector3 p = new Vector3();
                    Vector3 s = new Vector3();
                    Quaternion q = new Quaternion();

                    for (int i = 0; i < m.PositionQueue.Count; i++)
                    {
                        q += blendFactor * m.RotationQueue[i];
                        p += blendFactor * m.PositionQueue[i];
                        s += blendFactor * m.ScaleQueue[i];
                    }

                    m.localRotation = Matrix4.CreateFromQuaternion(q);
                    m.localPosition = p;
                    m.localScale = s;
                    
                }
            }
        }

        public void StartAnimation(Model anim_model, string Anim)
        {
            AnimComponent ac = anim_model.Components[anim_model.animComponentID] as AnimComponent;
            AnimData ad = ac.getAnimation(Anim);

            if (ad != null)
            {
                if (!ad.IsPlaying)
                    ad.IsPlaying = true;
            }
        }

        public void StopActiveAnimations(Model anim_model)
        {
            AnimComponent ac = anim_model.Components[anim_model.animComponentID] as AnimComponent;
            List<AnimData> ad_list = ac.getActiveAnimations();
          
            foreach (AnimData ad in ad_list)
                ad.IsPlaying = false;
        }

        public void StopActiveLoopAnimations(Model anim_model)
        {
            AnimComponent ac = anim_model.Components[anim_model.animComponentID] as AnimComponent;
            List<AnimData> ad_list = ac.getActiveAnimations();

            foreach (AnimData ad in ad_list)
            {
                if (ad.AnimType == libMBIN.NMS.Toolkit.TkAnimationData.AnimTypeEnum.Loop)
                    ad.IsPlaying = false;
            }
                
        }

        public int queryAnimationFrame(Model anim_model, string Anim)
        {
            AnimComponent ac = anim_model.Components[anim_model.animComponentID] as AnimComponent;
            AnimData ad = ac.getAnimation(Anim);

            if (ad != null)
            {
                return ad.ActiveFrame;
            }
            return -1;
        }

        public int queryAnimationFrameCount(Model anim_model, string Anim)
        {
            AnimComponent ac = anim_model.Components[anim_model.animComponentID] as AnimComponent;
            AnimData ad = ac.getAnimation(Anim);

            if (ad != null)
            {
                return (ad.FrameEnd == 0 ? ad.animMeta.FrameCount : ad.FrameEnd) - (ad.FrameStart != 0 ? ad.FrameStart : 0);
            }
            return -1;
        }

        public void Add(Model m)
        {
            AnimScenes.Add(m);
        }
    }
}
