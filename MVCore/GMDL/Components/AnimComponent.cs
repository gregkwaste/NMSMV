using System;
using System.Collections.Generic;
using System.Text;
using libMBIN.NMS.Toolkit;
using MVCore.Utils;

namespace MVCore.GMDL
{
    public class AnimComponent : IComponent
    {
        //The component entry of the entity file
        public TkAnimationComponentData Template;
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
            Template = new TkAnimationComponentData();
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


        public AnimComponent(TkAnimationComponentData data, string default_anim_name)
        {
            Template = data;
            //Load Animations
            if (data.Idle.Anim != "")
            {
                _animations.Add(new AnimData(data.Idle)); //Add Idle Animation
                _animDict[data.Idle.Anim] = _animations[0];
            } else
            {
                _animations.Add(new AnimData(data.Idle));
                _animations[0].Filename = default_anim_name; //Override FileName
                _animDict["Idle"] = _animations[0];
            }
                

            for (int i = 0; i < data.Anims.Count; i++)
            {
                //Check if the animation is already loaded
                AnimData my_ad = new AnimData(data.Anims[i]);
                _animations.Add(my_ad);
                _animDict[my_ad.PName] = my_ad;
            }

            linkAdditiveBases();
        }

        //Additive clips measure their motion from a frame of a clip of this component
        private void linkAdditiveBases()
        {
            foreach (AnimData ad in _animations)
            {
                string name = ad.AdditiveBaseAnim;
                ad.AdditiveBase = null;
                if (name == "")
                    continue;
                if (_animDict.TryGetValue(name, out AnimData base_ad))
                    ad.AdditiveBase = base_ad;
                else
                    Common.CallBacks.Log("Additive base animation " + name + " of " + ad.Anim + " not found");
            }
        }

        public IComponent Clone()
        {
            AnimComponent ac = new AnimComponent();
            ac.Template = Template;

            //Copy Animations
            foreach (AnimData ad in _animations)
            {
                AnimData clone = ad.Clone();
                ac.Animations.Add(clone);
                ac._animDict[clone.PName] = clone;
            }

            ac.linkAdditiveBases();
            return ac;
        }

    }
}
