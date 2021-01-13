using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows.Forms;
using System.Windows.Media.Animation;
using libMBIN.NMS.Toolkit;
using MVCore.Utils;
using OpenTK;

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
        private int prevFrameIndex = 0;
        private int activeFrameIndex = 0;
        private int nextFrameIndex = 0;
        //private AnimTransform prevFrameTransform;
        //private AnimTransform nextFrameTransform;
        //public AnimTransform activeFrameTransform;
        private float animationTime = 0.0f;
        private float prevFrameTime = 0.0f;
        private float nextFrameTime = 0.0f;
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
                prevFrameIndex = 0; //Reset frame counter on animation
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
                activeFrameIndex = value;
                NotifyPropertyChanged("ActiveFrame");
            }
        }

        public int FrameCount
        {
            get { return (animMeta != null) ?  animMeta.FrameCount - 1 : 0;}
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
            
            animationTime += dt;
            progress();
        }


        public void progress() 
        {
            //Override frame based on the GUI
            if (Override)
            {
                //Find frames
                prevFrameIndex = ActiveFrame;
                nextFrameIndex = ActiveFrame;
                LERP_coeff = 0.0f;
                return;
            }

            int activeFrameCount = (FrameEnd == 0 ? animMeta.FrameCount : Math.Min(FrameEnd, animMeta.FrameCount)) - (FrameStart != 0 ? FrameStart : 0);
            float activeAnimDuration = activeFrameCount * 1000.0f / Common.RenderState.renderSettings.animFPS; // In ms TOTAL
            float activeAnimInterval = activeAnimDuration / (activeFrameCount - 1); // Per frame time

            if (animationTime > activeAnimDuration)
            {
                if ((AnimType == AnimTypeEnum.OneShot) && animationTime > activeAnimDuration)
                {
                    animationTime = 0.0f;
                    prevFrameTime = 0.0f;
                    nextFrameTime = 0.0f;
                    IsPlaying = false;
                    return;
                }
                else
                {
                    animationTime %= activeAnimDuration; //Clamp to correct time span

                    //Properly calculate previous and nextFrameTimes
                    prevFrameIndex = (int) Math.Floor(animationTime / activeAnimInterval);
                    nextFrameIndex = (prevFrameIndex + 1) % activeFrameCount;
                    prevFrameTime = activeAnimInterval * prevFrameIndex;
                    nextFrameTime = prevFrameTime + activeAnimInterval;
                }
                    
            }


            if (animationTime > nextFrameTime)
            {
                //Progress animation
                prevFrameIndex = nextFrameIndex;
                ActiveFrame = prevFrameIndex;
                prevFrameTime = nextFrameTime;
                
                nextFrameIndex = (prevFrameIndex + 1) % activeFrameCount;
                nextFrameTime = prevFrameTime + activeAnimInterval;
            }

            LERP_coeff = (animationTime - prevFrameTime) / activeAnimInterval;

            //Console.WriteLine("AnimationTime {0} PrevAnimationTime {1} NextAnimationTime {2} LERP Coeff {3}",
            //    animationTime, prevFrameTime, nextFrameTime, LERP_coeff);

        }

        //TODO: Use this new definition for animation blending
        //public void applyNodeTransform(model m, string node, out Quaternion q, out Vector3 p)
        public void applyNodeTransform(Model m, string node)
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
            Quaternion q = Quaternion.Slerp(next_q, prev_q, LERP_coeff);
            Vector3 p = next_p * LERP_coeff + prev_p * (1.0f - LERP_coeff);
            Vector3 s = next_s * LERP_coeff + prev_s * (1.0f - LERP_coeff);

            //Convert transforms
            m.localRotation = Matrix4.CreateFromQuaternion(q);
            m.localPosition = p;
            m.localScale = s;
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
            q = Quaternion.Slerp(next_q, prev_q, LERP_coeff);
            p = next_p * LERP_coeff + prev_p * (1.0f - LERP_coeff);
            s = next_s * LERP_coeff + prev_s * (1.0f - LERP_coeff);

            
        }

    }

}
