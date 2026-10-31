using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Windows.Media.Animation;
using libMBIN.NMS.Toolkit;
using MVCore.Utils;
using OpenTK.Mathematics;

namespace MVCore.GMDL
{
    public class AnimTransform
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;

        public AnimTransform()
        {
            position = new Vector3();
            rotation = new Quaternion();
            scale = new Vector3();
        }

        public static AnimTransform Lerp(AnimTransform prev, AnimTransform next, float x)
        {
            AnimTransform t = new AnimTransform();
            t.position = Vector3.Lerp(prev.position, next.position, x);
            t.rotation = Quaternion.Slerp(prev.rotation, next.rotation, x);
            t.scale = Vector3.Lerp(prev.scale, next.scale, x);

            return t;
        }
    }



    public class AnimData : INotifyPropertyChanged
    {
        //The animation entry of the entity file
        public TkAnimationData Template;
        //The animation file the viewer plays. Usually the template's file, the Idle animation of entities
        //that name none plays the scene's default .ANIM.MBIN
        public string Filename = "";
        public AnimMetadata animMeta;
        private int prevFrameIndex = 0; //Keyframe indices into the whole anim file (FrameStart included)
        private int nextFrameIndex = 0;
        private int activeFrameIndex = 0; //Frame within the clip, as shown in the UI
        private float animationTime = 0.0f; //Time since the clip started in ms
        private float LERP_coeff = 0.0f;
        public bool loaded = false;
        private bool _playing = false;
        public int CompletedCount = 0; //Times a one-shot ran to its end, AnimFrame triggers use it
        public string StartedBy = null; //The action that last started the clip, shown in the Scene Systems panel

        public event PropertyChangedEventHandler PropertyChanged;

        //Constructors
        public AnimData(TkAnimationData ad)
        {
            Template = ad;
            Filename = ad.Filename?.Value ?? "";
        }

        public AnimData()
        {
            Template = new TkAnimationData();
        }

        //Template data
        public string Anim
        {
            get { return Template.Anim; }
            set { Template.Anim = value; }
        }
        public int FrameStart => Template.FrameStart;
        public int FrameEnd => Template.FrameEnd;
        public TkAnimationData.AnimTypeEnum AnimType => Template.AnimType;
        public float Speed
        {
            get { return Template.Speed; }
            set { Template.Speed = value; }
        }
        public bool Additive
        {
            get { return Template.Additive; }
            set { Template.Additive = value; }
        }
        public bool Active
        {
            get { return Template.Active; }
            set { Template.Active = value; }
        }

        //OneShotBlendable clips play once like OneShot ones, they only differ in how the game blends them
        public bool IsOneShot => AnimType == TkAnimationData.AnimTypeEnum.OneShot ||
                                 AnimType == TkAnimationData.AnimTypeEnum.OneShotBlendable;

        //Rest pose of an additive clip: frame AdditiveBaseFrame (within the clip) of the AdditiveBaseAnim
        //clip, set by the AnimComponent. Null measures from this clip
        public AnimData AdditiveBase;
        public string AdditiveBaseAnim => Template.AdditiveBaseAnim?.Value ?? "";
        public float AdditiveBaseFrame => Template.AdditiveBaseFrame;

        //Joints the clip drives, null when it drives the whole skeleton. Clips that name a StartNode only
        //animate that joint's subtree, plus the subtrees of their ExtraStartNodes
        private HashSet<string> nodeFilter;
        private Dictionary<string, Model> nodeFilterScene; //The nodeDict the filter was built for

        public AnimData Clone()
        {
            //Copies share the entity data. The AnimComponent links the copy's AdditiveBase
            AnimData ad = new AnimData(Template);
            ad.Filename = Filename;
            ad.animMeta = animMeta;
            ad.loaded = loaded;

            return ad;
        }

        //Properties

        public string PName
        {
            get { return Anim; }
            set { Anim = value; }
        }

        public bool IsPlaying
        {
            get { return _playing; }
            set
            {
                _playing = value;
                if (value)
                    StartedBy = null; //The ActionSystem sets it again after starting the clip
                //Restart the clip from its first frame
                animationTime = 0.0f;
                prevFrameIndex = clipStart;
                nextFrameIndex = clipStart;
                LERP_coeff = 0.0f;
                NotifyPropertyChanged("IsPlaying");
            }
        }

        public bool PActive
        {
            get { return Active; }
            set 
            { 
                Active = value;
                NotifyPropertyChanged("Active");
            }
        }

        public bool _override = false;
        public bool Override
        {
            get { return _override; }
            set
            {
                _override = value;
                NotifyPropertyChanged("Override");
            }
        }

        public int ActiveFrame
        {
            get { return activeFrameIndex; }
            set
            {
                if (activeFrameIndex == value)
                    return;
                activeFrameIndex = value;
                NotifyPropertyChanged("ActiveFrame");
            }
        }

        //Last frame index of the clip, the UI slider range
        public int FrameCount
        {
            get { return Math.Max(clipFrameCount - 1, 0); }
        }

        //First keyframe of the clip in the anim file
        private int clipStart
        {
            get { return (animMeta != null) ? Math.Clamp(FrameStart, 0, Math.Max(animMeta.FrameCount - 1, 0)) : 0; }
        }

        //Number of keyframes in the clip
        public int clipFrameCount
        {
            get
            {
                if (animMeta == null)
                    return 0;
                int end = (FrameEnd == 0) ? animMeta.FrameCount : Math.Min(FrameEnd, animMeta.FrameCount);
                return Math.Max(end - clipStart, 1);
            }
        }

        private static float frameInterval
        {
            get { return 1000.0f / Math.Max(Common.RenderState.renderSettings.AnimFPS, 1); }
        }

        //Loops wrap from the last keyframe back to the first, one-shots stop on the last keyframe
        private float clipDuration
        {
            get
            {
                int count = clipFrameCount;
                return (IsOneShot ? count - 1 : count) * frameInterval;
            }
        }

        public bool isValid
        {
            get { return Filename != ""; }
        }

        public string PAnimType
        {
            get
            {
                return AnimType.ToString();
            }
        }

        public bool PAdditive
        {
            get { return Additive; }
            set { Additive = value; }
        }

        public float PSpeed
        {
            get { return Speed; }
            set { Speed = value; }
        }

        //UI update
        private void NotifyPropertyChanged(String info)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }

        public void loadData()
        {
            if (Filename != "")
                fetchAnimMetaData();
        }


        private void fetchAnimMetaData()
        {
            if (Common.RenderState.activeResMgr.Animations.ContainsKey(Filename))
            {
                animMeta = Common.RenderState.activeResMgr.Animations[Filename];
            }
            else
            {
                TkAnimMetadata amd = NMSUtils.LoadNMSTemplate(Filename,
                    ref Common.RenderState.activeResMgr) as TkAnimMetadata;
                if (amd == null)
                {
                    Common.CallBacks.Log("Animation file " + Filename + " could not be loaded");
                    return;
                }
                animMeta = new AnimMetadata(amd);
                animMeta.load(); //Load data as well
                Common.RenderState.activeResMgr.Animations[Filename] = animMeta;
            }
            NotifyPropertyChanged("FrameCount");
        }


        public void ensureLoaded()
        {
            if (!loaded)
            {
                fetchAnimMetaData();
                loaded = true;
            }
        }

        public void update(float dt) //time in milliseconds
        {
            ensureLoaded();

            if (Speed > 0.0f)
                dt *= Speed;

            animationTime += dt;
            progress();
        }


        public void progress()
        {
            if (animMeta == null)
                return;

            int start = clipStart;
            int count = clipFrameCount;

            //Override frame based on the GUI
            if (Override)
            {
                int frame = Math.Clamp(ActiveFrame, 0, count - 1);
                prevFrameIndex = start + frame;
                nextFrameIndex = start + frame;
                LERP_coeff = 0.0f;
                return;
            }

            bool oneShot = IsOneShot;
            float duration = clipDuration;

            if (animationTime >= duration)
            {
                if (oneShot)
                {
                    CompletedCount++;
                    IsPlaying = false;
                    return;
                }
                animationTime = (duration > 0.0f) ? animationTime % duration : 0.0f;
            }

            //The frame pair and the blend come straight from the clock, so long frames never leave the
            //indices behind the time
            float f = animationTime / frameInterval;
            int prev = Math.Min((int) f, count - 1);
            int next = oneShot ? Math.Min(prev + 1, count - 1) : (prev + 1) % count;

            prevFrameIndex = start + prev;
            nextFrameIndex = start + next;
            LERP_coeff = Math.Clamp(f - prev, 0.0f, 1.0f);
            ActiveFrame = prev;
        }

        //Weight of the clip in the blend. One-shots fade in and out so they don't pop over the loops
        public float getBlendWeight()
        {
            if (!IsOneShot)
                return 1.0f;

            float duration = clipDuration;
            float fade = Math.Min(150.0f, 0.25f * duration);
            if (fade <= 0.0f)
                return 1.0f;

            return Math.Clamp(Math.Min(animationTime, duration - animationTime) / fade, 0.0f, 1.0f);
        }

        public void getCurrentTransform(ref Vector3 p, ref Vector3 s, ref Quaternion q, string node)
        {
            //Fetch prevFrame stuff
            Quaternion prev_q = animMeta.anim_rotations[node][prevFrameIndex];
            Vector3 prev_p = animMeta.anim_positions[node][prevFrameIndex];
            Vector3 prev_s = animMeta.anim_scales[node][prevFrameIndex];

            //Fetch nextFrame stuff
            Quaternion next_q = animMeta.anim_rotations[node][nextFrameIndex];
            Vector3 next_p = animMeta.anim_positions[node][nextFrameIndex];
            Vector3 next_s = animMeta.anim_scales[node][nextFrameIndex];

            //Interpolate
            q = Quaternion.Slerp(prev_q, next_q, LERP_coeff);
            p = next_p * LERP_coeff + prev_p * (1.0f - LERP_coeff);
            s = next_s * LERP_coeff + prev_s * (1.0f - LERP_coeff);
        }

        //Transform of a node at a frame within the clip, blending between the keyframes around it
        private void getClipFrameTransform(float frame, string node, out Vector3 p, out Vector3 s, out Quaternion q)
        {
            frame = Math.Clamp(frame, 0.0f, clipFrameCount - 1);
            int prev = clipStart + (int) frame;
            int next = clipStart + Math.Min((int) frame + 1, clipFrameCount - 1);
            float x = frame - (int) frame;

            q = Quaternion.Slerp(animMeta.anim_rotations[node][prev], animMeta.anim_rotations[node][next], x);
            p = Vector3.Lerp(animMeta.anim_positions[node][prev], animMeta.anim_positions[node][next], x);
            s = Vector3.Lerp(animMeta.anim_scales[node][prev], animMeta.anim_scales[node][next], x);
        }

        //Turns a sampled transform into the change from the additive rest pose, which additive clips
        //apply on top of the blended pose
        public void toAdditiveDelta(ref Vector3 p, ref Vector3 s, ref Quaternion q, string node)
        {
            AnimData baseClip = AdditiveBase ?? this;
            baseClip.ensureLoaded();
            if (baseClip.animMeta == null || !baseClip.animMeta.anim_rotations.ContainsKey(node))
                baseClip = this; //The base clip doesn't animate this joint, measure from this clip

            baseClip.getClipFrameTransform(AdditiveBaseFrame, node, out Vector3 ref_p, out Vector3 ref_s, out Quaternion ref_q);

            p -= ref_p;
            s = new Vector3(ref_s.X != 0.0f ? s.X / ref_s.X : 1.0f,
                            ref_s.Y != 0.0f ? s.Y / ref_s.Y : 1.0f,
                            ref_s.Z != 0.0f ? s.Z / ref_s.Z : 1.0f);
            q = Quaternion.Invert(ref_q) * q;
        }

        public bool drivesNode(string node, Dictionary<string, Model> nodeDict)
        {
            if (nodeFilterScene != nodeDict)
            {
                buildNodeFilter(nodeDict);
                nodeFilterScene = nodeDict;
            }
            return nodeFilter == null || nodeFilter.Contains(node);
        }

        private void buildNodeFilter(Dictionary<string, Model> nodeDict)
        {
            List<string> roots = new List<string>();
            string start = Template.StartNode?.Value ?? "";
            if (start != "")
                roots.Add(start);
            if (Template.ExtraStartNodes != null)
            {
                foreach (var n in Template.ExtraStartNodes)
                    if (!string.IsNullOrEmpty(n?.Value))
                        roots.Add(n.Value);
            }

            if (!string.IsNullOrEmpty(Template.Mask?.Value) || Template.AdditionalMasks?.Count > 0)
                Common.CallBacks.Log("Animation " + Anim + " uses bone masks, which are not applied");

            nodeFilter = null;
            if (roots.Count == 0)
                return;

            nodeFilter = new HashSet<string>();
            foreach (string r in roots)
            {
                if (nodeDict.TryGetValue(r, out Model root))
                    addSubtree(root);
                else
                    Common.CallBacks.Log("Start node " + r + " of animation " + Anim + " not found");
            }

            if (nodeFilter.Count == 0)
                nodeFilter = null; //None of the start nodes exist, drive the whole skeleton rather than nothing
        }

        private void addSubtree(Model m)
        {
            nodeFilter.Add(m.Name);
            foreach (Model child in m.Children)
                addSubtree(child);
        }

    }

}
