using System;
using System.Collections.Generic;
using MVCore.GMDL;
using OpenTK.Mathematics;

namespace MVCore.Engine.Systems
{
    public class AnimationSystem
    {
        private readonly List<Model> animScenes = new List<Model>();
        private readonly List<Model> activeScenes = new List<Model>();
        public IReadOnlyList<Model> AnimScenes => animScenes;
        public IReadOnlyList<Model> ActiveScenes => activeScenes; //Scenes whose pose changed in the last update
        private readonly HashSet<Model> pendingPoseResets = new HashSet<Model>();
        private readonly HashSet<Model> activeSceneSet = new HashSet<Model>();

        //Per-joint working state for one animation update. Models store only the final transform; clip
        //weights and partial transforms belong to the system that performs the blend.
        private sealed class JointBlendAccumulator
        {
            public readonly Model Joint;
            public int Generation;
            public float RegularWeight;
            public Vector3 RegularPosition;
            public Vector3 RegularScale;
            public Quaternion RegularRotation;
            public Quaternion RotationReference;
            public Vector3 AdditivePosition;
            public Vector3 AdditiveScale;
            public Quaternion AdditiveRotation;

            public JointBlendAccumulator(Model joint)
            {
                Joint = joint;
            }

            public void Reset(int generation)
            {
                Generation = generation;
                RegularWeight = 0.0f;
                RegularPosition = Vector3.Zero;
                RegularScale = Vector3.Zero;
                RegularRotation = new Quaternion(0.0f, 0.0f, 0.0f, 0.0f);
                RotationReference = Quaternion.Identity;
                AdditivePosition = Vector3.Zero;
                AdditiveScale = Vector3.One;
                AdditiveRotation = Quaternion.Identity;
            }

            public void Add(Vector3 position, Vector3 scale, Quaternion rotation, float weight, bool additive)
            {
                if (additive)
                {
                    AdditiveRotation *= Quaternion.Slerp(Quaternion.Identity, rotation, weight);
                    AdditivePosition += weight * position;
                    AdditiveScale *= Vector3.Lerp(Vector3.One, scale, weight);
                    return;
                }

                if (RegularWeight == 0.0f)
                    RotationReference = rotation;
                else if (RotationReference.X * rotation.X + RotationReference.Y * rotation.Y +
                         RotationReference.Z * rotation.Z + RotationReference.W * rotation.W < 0.0f)
                    rotation = new Quaternion(-rotation.X, -rotation.Y, -rotation.Z, -rotation.W);

                RegularRotation += weight * rotation;
                RegularPosition += weight * position;
                RegularScale += weight * scale;
                RegularWeight += weight;
            }
        }

        private readonly Dictionary<Model, JointBlendAccumulator> jointBlendScratch =
            new Dictionary<Model, JointBlendAccumulator>();
        private readonly List<JointBlendAccumulator> activeJointBlends = new List<JointBlendAccumulator>();
        private int blendGeneration;

        public void CleanUp()
        {
            animScenes.Clear();
            activeScenes.Clear();
            activeSceneSet.Clear();
            pendingPoseResets.Clear();
            jointBlendScratch.Clear();
            activeJointBlends.Clear();
        }

        private readonly List<AnimData> playingClips = new List<AnimData>();
        private readonly List<float> clipWeights = new List<float>();

        private void markSceneActive(Model model)
        {
            if (activeSceneSet.Add(model))
                activeScenes.Add(model);
        }

        private JointBlendAccumulator getJointBlend(Model joint)
        {
            if (!jointBlendScratch.TryGetValue(joint, out JointBlendAccumulator blend))
            {
                blend = new JointBlendAccumulator(joint);
                jointBlendScratch.Add(joint, blend);
            }

            if (blend.Generation != blendGeneration)
            {
                blend.Reset(blendGeneration);
                activeJointBlends.Add(blend);
            }
            return blend;
        }

        public void update(float dt)
        {
            activeScenes.Clear();
            activeSceneSet.Clear();
            activeJointBlends.Clear();
            if (++blendGeneration == 0)
                blendGeneration = 1;

            //Action emulation can request a restart after this system has already updated for the frame.
            //Apply the rest pose here so CGLControl sees it in ActiveScenes and refreshes the matrices/skin.
            foreach (Model anim_model in pendingPoseResets)
            {
                restoreBasePose(anim_model);
                markSceneActive(anim_model);
            }
            pendingPoseResets.Clear();

            foreach (Model anim_model in animScenes)
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
                    if (ad.IsOneShot)
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
                    if (ad.IsOneShot)
                        clipWeights[i] *= oneShotScale;
                    else
                        clipWeights[i] = loopWeight;
                }

                if (playingClips.Count > 0)
                    markSceneActive(anim_model);

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
                        if (!ad.drivesNode(node.Node, nodeDict))
                            continue; //Outside the clip's StartNode subtrees

                        Vector3 p = new Vector3();
                        Vector3 s = new Vector3();
                        Quaternion q = new Quaternion();

                        ad.getCurrentTransform(ref p, ref s, ref q, node.Node);
                        if (ad.Additive)
                            ad.toAdditiveDelta(ref p, ref s, ref q, node.Node);

                        getJointBlend(m).Add(p, s, q, w, ad.Additive);
                    }
                }
            }

            foreach (JointBlendAccumulator blend in activeJointBlends)
                applyBlend(blend);
        }

        private static void applyBlend(JointBlendAccumulator blend)
        {
            Model joint = blend.Joint;
            Quaternion rotation;
            Vector3 position;
            Vector3 scale;

            if (blend.RegularWeight > 0.0f)
            {
                rotation = Quaternion.Normalize(blend.RegularRotation);
                position = blend.RegularPosition / blend.RegularWeight;
                scale = blend.RegularScale / blend.RegularWeight;
            }
            else
            {
                //Only additive clips drive this joint, so apply them on top of the bind pose.
                rotation = joint.__localRotation.ExtractRotation();
                position = joint.__localPosition;
                scale = joint.__localScale;
            }

            rotation *= blend.AdditiveRotation;
            position += blend.AdditivePosition;
            scale *= blend.AdditiveScale;

            //Creature pose from the AnimPoseComponent is applied after all clip blending.
            rotation *= joint.poseRotation;
            position += joint.posePosition;
            scale *= joint.poseScale;

            //Write only the final pose. These internal fields avoid invalidating scene membership and
            //forcing a full instance rebuild; CGLControl refreshes the affected matrices and skin data.
            joint._localRotation = Matrix4.CreateFromQuaternion(Quaternion.Normalize(rotation));
            joint._localPosition = position;
            joint._localScale = scale;
            joint.updated = true;
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

        //Restores the animation component to the state declared by its entity metadata. Used by the
        //action-system restart button so stale one-shots, completion counts and poses do not survive.
        public void ResetAnimations(Model anim_model)
        {
            if (anim_model?.animComponentID < 0)
                return;

            AnimComponent ac = anim_model.Components[anim_model.animComponentID] as AnimComponent;
            if (ac == null)
                return;

            foreach (AnimData ad in ac.Animations)
            {
                ad.IsPlaying = false; //Also rewinds the clip to its first frame
                ad.CompletedCount = 0;
                ad.StartedBy = null;
            }

            foreach (AnimData ad in ac.Animations)
                if (ad.Active)
                    ad.IsPlaying = true;

            Add(anim_model);
            pendingPoseResets.Add(anim_model);
        }

        private static void restoreBasePose(Model anim_model)
        {
            if (anim_model?.parentScene == null)
                return;

            foreach (Model m in anim_model.parentScene.jointDict.Values)
            {
                m._localRotation = m.__localRotation * Matrix4.CreateFromQuaternion(m.poseRotation);
                m._localPosition = m.__localPosition + m.posePosition;
                m._localScale = m.__localScale * m.poseScale;
                m.updated = true;
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

        public void Remove(Model m)
        {
            animScenes.Remove(m);
            activeScenes.Remove(m);
            pendingPoseResets.Remove(m);
        }

        public void Add(Model m)
        {
            if (!animScenes.Contains(m)) //Scenes are re-scanned after a reference loads
                animScenes.Add(m);
        }
    }
}
