using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using libMBIN.NMS.Toolkit;
using MVCore.Utils;
using OpenTK;

namespace MVCore.GMDL
{
    public class AnimData : TkAnimationData, INotifyPropertyChanged
    {
        public AnimMetadata animMeta;
        public float animationTime = 0.0f;
        public bool _animationToggle = false;
        private int prevFrameIndex = 0;
        private int nextFrameIndex = 0;
        private float LERP_coeff = 0.0f;
        public bool loaded = false;

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

        public bool PActive
        {
            get { return Active; }
            set { Active = value; }
        }

        public bool AnimationToggle
        {
            get { return _animationToggle; }
            set
            {
                _animationToggle = value;
                NotifyPropertyChanged("AnimationToggle");
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
                animMeta = new AnimMetadata(amd);
                animMeta.load(); //Load data as well
                Common.RenderState.activeResMgr.Animations[Filename] = animMeta;
            }
        }


        public void animate(float dt) //time in milliseconds
        {
            if (!loaded)
            {
                fetchAnimMetaData();
                loaded = true;
            }

            if (animMeta != null)
            {
                float activeAnimDuration = animMeta.FrameCount * 1000.0f / Common.RenderState.renderSettings.animFPS; // In ms
                float activeAnimInterval = activeAnimDuration / (animMeta.FrameCount - 1);

                animationTime += dt; //Progress time

                if ((AnimType == AnimTypeEnum.OneShot) && animationTime > activeAnimDuration)
                {
                    animationTime = 0.0f;
                    AnimationToggle = false;
                    return;
                }
                else
                    animationTime = animationTime % activeAnimDuration; //Clamp to correct time span

                //Find frames
                prevFrameIndex = (int) Math.Floor(animationTime * animMeta.FrameCount / activeAnimDuration) % animMeta.FrameCount;
                nextFrameIndex = (prevFrameIndex + 1) % animMeta.FrameCount;

                LERP_coeff = (animationTime % activeAnimInterval) / activeAnimInterval;
            }
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

    }

}
