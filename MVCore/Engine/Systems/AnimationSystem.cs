using System;
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
        public List<Model> ActiveScenes = new List<Model>(); //Scenes whose pose changed in the last update
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

        private readonly List<AnimData> playingClips = new List<AnimData>();
        private readonly List<float> clipWeights = new List<float>();

        public void update(float dt)
        {
            ActiveScenes.Clear();

            //Clear queues for all the joints
            foreach (Model anim_model in AnimScenes)
            {
                foreach (Model m in anim_model.parentScene.nodeDict.Values)
                {
                    m.PositionQueue.Clear();
                    m.ScaleQueue.Clear();
                    m.RotationQueue.Clear();
                    m.WeightQueue.Clear();
                    m.AdditiveQueue.Clear();
                }
            }

            foreach (Model anim_model in AnimScenes)
            {
                AnimComponent ac = anim_model._components[anim_model.hasComponent(typeof(AnimComponent))] as AnimComponent;
                Dictionary<string, Model> nodeDict = anim_model.parentScene.nodeDict;

                //Advance the clocks first, so the pose sampled below belongs to this update
                playingClips.Clear();
                clipWeights.Clear();
                float oneShotWeight = 0.0f;
                int loopCount = 0;

                foreach (AnimData ad in ac.Animations)
                {
                    if (!ad.IsPlaying)
                        continue;

                    ad.update(dt);
                    if (!ad.IsPlaying || ad.animMeta == null)
                        continue; //One-shot that just ended, or an animation file that failed to load

                    float w = ad.getBlendWeight();
                    playingClips.Add(ad);
                    clipWeights.Add(w);

                    if (ad.Additive)
                        continue;
                    if (ad.AnimType == libMBIN.NMS.Toolkit.TkAnimationData.AnimTypeEnum.OneShot)
                        oneShotWeight += w;
                    else
                        loopCount++;
                }

                //One-shots take over from the loops as they fade in, the loops share the weight that is left
                float oneShotShare = Math.Min(oneShotWeight, 1.0f);
                float oneShotScale = oneShotWeight > 0.0f ? oneShotShare / oneShotWeight : 0.0f;
                float loopWeight = loopCount > 0 ? (1.0f - oneShotShare) / loopCount : 0.0f;

                for (int i = 0; i < playingClips.Count; i++)
                {
                    AnimData ad = playingClips[i];
                    if (ad.Additive)
                        continue;
                    if (ad.AnimType == libMBIN.NMS.Toolkit.TkAnimationData.AnimTypeEnum.OneShot)
                        clipWeights[i] *= oneShotScale;
                    else
                        clipWeights[i] = loopWeight;
                }

                if (playingClips.Count > 0)
                    ActiveScenes.Add(anim_model);

                //Load updated local joint transforms
                for (int i = 0; i < playingClips.Count; i++)
                {
                    AnimData ad = playingClips[i];
                    float w = clipWeights[i];
                    if (w <= 0.0f)
                        continue;

                    foreach (libMBIN.NMS.Toolkit.TkAnimNodeData node in ad.animMeta.NodeData)
                    {
                        if (!nodeDict.TryGetValue(node.Node, out Model m))
                            continue;

                        Vector3 p = new Vector3();
                        Vector3 s = new Vector3();
                        Quaternion q = new Quaternion();

                        ad.getCurrentTransform(ref p, ref s, ref q, node.Node);
                        if (ad.Additive)
                            ad.toAdditiveDelta(ref p, ref s, ref q, node.Node);

                        m.RotationQueue.Add(q);
                        m.PositionQueue.Add(p);
                        m.ScaleQueue.Add(s);
                        m.WeightQueue.Add(w);
                        m.AdditiveQueue.Add(ad.Additive);
                    }
                }

                //Blend Transforms and apply
                foreach (Model m in anim_model.parentScene.jointDict.Values)
                {
                    if (m.PositionQueue.Count == 0)
                        continue; //Keep last transforms

                    //Weighted average of the regular clips
                    Vector3 p = Vector3.Zero;
                    Vector3 s = Vector3.Zero;
                    Quaternion q = new Quaternion(0.0f, 0.0f, 0.0f, 0.0f);
                    Quaternion qRef = Quaternion.Identity;
                    float totalWeight = 0.0f;

                    for (int i = 0; i < m.PositionQueue.Count; i++)
                    {
                        if (m.AdditiveQueue[i])
                            continue;

                        float w = m.WeightQueue[i];
                        Quaternion qi = m.RotationQueue[i];

                        //q and -q are the same rotation, keep them all on one side so they don't cancel out
                        if (totalWeight == 0.0f)
                            qRef = qi;
                        else if (qRef.X * qi.X + qRef.Y * qi.Y + qRef.Z * qi.Z + qRef.W * qi.W < 0.0f)
                            qi = new Quaternion(-qi.X, -qi.Y, -qi.Z, -qi.W);

                        q += w * qi;
                        p += w * m.PositionQueue[i];
                        s += w * m.ScaleQueue[i];
                        totalWeight += w;
                    }

                    if (totalWeight > 0.0f)
                    {
                        q = Quaternion.Normalize(q);
                        p /= totalWeight;
                        s /= totalWeight;
                    }
                    else
                    {
                        //Only additive clips on this joint, they go on top of the bind pose
                        q = m.__localRotation.ExtractRotation();
                        p = m.__localPosition;
                        s = m.__localScale;
                    }

                    //Additive clips on top
                    for (int i = 0; i < m.PositionQueue.Count; i++)
                    {
                        if (!m.AdditiveQueue[i])
                            continue;

                        float w = m.WeightQueue[i];
                        q = q * Quaternion.Slerp(Quaternion.Identity, m.RotationQueue[i], w);
                        p += w * m.PositionQueue[i];
                        s *= Vector3.Lerp(Vector3.One, m.ScaleQueue[i], w);
                    }

                    //Creature pose from the AnimPoseComponent
                    q = q * m.poseRotation;
                    p += m.posePosition;
                    s *= m.poseScale;

                    m.localRotation = Matrix4.CreateFromQuaternion(Quaternion.Normalize(q));
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
                return ad.clipFrameCount;
            }
            return -1;
        }

        public void Add(Model m)
        {
            if (!AnimScenes.Contains(m)) //Scenes are re-scanned after a reference loads
                AnimScenes.Add(m);
        }
    }
}
