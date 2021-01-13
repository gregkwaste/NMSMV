using System;
using System.Collections.Generic;
using System.Text;
using libMBIN.NMS.Toolkit;

namespace MVCore.GMDL
{
    public class AnimComponent : Component
    {
        //animations list Contains all the animations bound to the locator through Tkanimationcomponent
        public List<AnimData> _animations = new List<AnimData>();
        public Dictionary<string, AnimData> _animDict = new Dictionary<string, AnimData>();

        public List<AnimData> Animations
        {
            get
            {
                return _animations;
            }
        }

        //Default Constructor
        public AnimComponent()
        {

        }

        public AnimData getAnimation(string Name)
        {
            if (!_animDict.ContainsKey(Name))
                return null;
            return _animDict[Name];
        }

        public List<AnimData> getActiveAnimations()
        {
            List<AnimData> animList = new List<AnimData>();
            
            foreach (AnimData ad in _animations)
            {
                if (ad.IsPlaying)
                    animList.Add(ad);
            }
                
            return animList;
        }


        public void assimpExport(ref Assimp.Scene scn)
        {
            foreach (AnimData ad in Animations)
            {
                Assimp.Animation anim = ad.assimpExport(ref scn);
                scn.Animations.Add(anim);
            }
        }

        public AnimComponent(TkAnimationComponentData data)
        {
            //Load Animations
            if (data.Idle.Anim != "")
            {
                _animations.Add(new AnimData(data.Idle)); //Add Idle Animation
                _animDict[data.Idle.Anim] = _animations[0];
            }
                

            for (int i = 0; i < data.Anims.Count; i++)
            {
                //Check if the animation is already loaded
                AnimData my_ad = new AnimData(data.Anims[i]);
                _animations.Add(my_ad);
                _animDict[my_ad.PName] = my_ad;
            }

        }

        public void copyFrom(AnimComponent input)
        {
            //Base class is dummy
            //base.copyFrom(input); //Copy stuff from base class

            //TODO: Copy Animations

        }

        public override Component Clone()
        {
            AnimComponent ac = new AnimComponent();

            //Copy Animations
            foreach (AnimData ad in _animations)
            {
                AnimData clone = ad.Clone();
                ac.Animations.Add(clone);
                ac._animDict[clone.PName] = clone;
            }
                
            return ac;
        }

        public void update()
        {

        }

        protected AnimComponent(AnimComponent input)
        {
            this.copyFrom(input);
        }

        #region IDisposable Support
        private bool disposed = false; // To detect redundant calls
        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    base.Dispose(disposing);
                }
                disposed = true;
            }
        }

        #endregion

    }
}
