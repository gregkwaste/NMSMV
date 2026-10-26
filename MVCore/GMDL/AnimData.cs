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



    public class AnimData : TkAnimationData, INotifyPropertyChanged
    {
        public AnimMetadata animMeta;
        private int prevFrameIndex = 0; //Keyframe indices into the whole anim file (FrameStart included)
        private int nextFrameIndex = 0;
        private int activeFrameIndex = 0; //Frame within the clip, as shown in the UI
        private float animationTime = 0.0f; //Time since the clip started in ms
        private float LERP_coeff = 0.0f;
        public bool loaded = false;
        private bool _playing = false;

        public event PropertyChangedEventHandler PropertyChanged;

        //Constructors
        public AnimData(TkAnimationData ad)
        {
            Anim = ad.Anim;
            Filename = ad.Filename;
            FrameStart = ad.FrameStart;
            FrameEnd = ad.FrameEnd;
            StartNode = ad.StartNode;
            AnimType = ad.AnimType;
            Speed = ad.Speed;
            Additive = ad.Additive;
        }

        public AnimData()
        {

        }

        public Assimp.Animation assimpExport(ref Assimp.Scene scn)
        {
            Assimp.Animation asAnim = new Assimp.Animation();
            asAnim.Name = Anim;


            //Make sure keyframe data is loaded from the files
            if (!loaded)
            {
                fetchAnimMetaData();
                loaded = true;
            }



            asAnim.TicksPerSecond = 60;
            asAnim.DurationInTicks = animMeta.FrameCount;
            float time_interval = 1.0f / (float)asAnim.TicksPerSecond;


            //Add Node-Bone Channels
            for (int i = 0; i < animMeta.NodeCount; i++)
            {
                string name = animMeta.NodeData[i].Node;
                Assimp.NodeAnimationChannel mChannel = new Assimp.NodeAnimationChannel();
                mChannel.NodeName = name;

                //mChannel.PostState = Assimp.AnimationBehaviour.Linear;
                //mChannel.PreState = Assimp.AnimationBehaviour.Linear;


                //Export Keyframe Data
                for (int j = 0; j < animMeta.FrameCount; j++)
                {

                    //Position
                    Assimp.VectorKey vk = new Assimp.VectorKey(j * time_interval, MathUtils.convertVector(animMeta.anim_positions[name][j]));
                    mChannel.PositionKeys.Add(vk);
                    //Rotation
                    Assimp.QuaternionKey qk = new Assimp.QuaternionKey(j * time_interval, MathUtils.convertQuaternion(animMeta.anim_rotations[name][j]));
                    mChannel.RotationKeys.Add(qk);
                    //Scale
                    Assimp.VectorKey sk = new Assimp.VectorKey(j * time_interval, MathUtils.convertVector(animMeta.anim_scales[name][j]));
                    mChannel.ScalingKeys.Add(sk);

                }

                asAnim.NodeAnimationChannels.Add(mChannel);

            }

            return asAnim;

        }

        public AnimData Clone()
        {
            AnimData ad = new AnimData();

            ad.Anim = Anim;
            ad.Filename = Filename;
            ad.FrameStart = FrameStart;
            ad.FrameEnd = FrameEnd;
            ad.StartNode = StartNode;
            ad.AnimType = AnimType;
            ad.Speed = Speed;
            ad.Additive = Additive;
            ad.animMeta = animMeta;

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
                return (AnimType == AnimTypeEnum.OneShot ? count - 1 : count) * frameInterval;
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


        public void update(float dt) //time in milliseconds
        {
            if (!loaded)
            {
                fetchAnimMetaData();
                loaded = true;
            }
            
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

            bool oneShot = AnimType == AnimTypeEnum.OneShot;
            float duration = clipDuration;

            if (animationTime >= duration)
            {
                if (oneShot)
                {
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
            if (AnimType != AnimTypeEnum.OneShot)
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

        //Turns a sampled transform into the change from the clip's first frame, which additive clips
        //apply on top of the blended pose
        public void toAdditiveDelta(ref Vector3 p, ref Vector3 s, ref Quaternion q, string node)
        {
            int r = clipStart;
            Quaternion ref_q = animMeta.anim_rotations[node][r];
            Vector3 ref_p = animMeta.anim_positions[node][r];
            Vector3 ref_s = animMeta.anim_scales[node][r];

            p -= ref_p;
            s = new Vector3(ref_s.X != 0.0f ? s.X / ref_s.X : 1.0f,
                            ref_s.Y != 0.0f ? s.Y / ref_s.Y : 1.0f,
                            ref_s.Z != 0.0f ? s.Z / ref_s.Z : 1.0f);
            q = Quaternion.Invert(ref_q) * q;
        }

    }

}
