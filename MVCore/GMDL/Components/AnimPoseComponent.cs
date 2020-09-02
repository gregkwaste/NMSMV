using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;
using libMBIN.NMS.Toolkit;
using MVCore.Utils;

namespace MVCore.GMDL
{
    public class AnimPoseComponent : Component
    {
        public Model ref_object = null;
        public TkAnimMetadata animMeta = null;
        //AnimationPoseData
        public List<AnimPoseData> _poseData = new List<AnimPoseData>();
        public TkAnimMetadata _poseFrameData = null; //Stores the actual poseFrameData
        public List<AnimPoseData> poseData
        {
            get
            {
                return _poseData;
            }
        }

        public ICommand ApplyPose
        {
            get { return new ApplyPoseCommand(); }
        }

        public ICommand ResetPose
        {
            get { return new ResetPoseCommand(); }
        }

        //Default Constructor
        public AnimPoseComponent()
        {

        }

        public AnimPoseComponent(TkAnimPoseComponentData apcd)
        {
            _poseFrameData = (TkAnimMetadata) NMSUtils.LoadNMSTemplate(apcd.Filename,
                ref Common.RenderState.activeResMgr);

            //Load PoseAnims
            for (int i = 0; i < apcd.PoseAnims.Count; i++)
            {
                AnimPoseData my_apd = new AnimPoseData(apcd.PoseAnims[i]);
                poseData.Add(my_apd);
            }
        }

        public override Component Clone()
        {
            return new AnimPoseComponent();
        }

        //ICommands

        private class ApplyPoseCommand : ICommand
        {
            event EventHandler ICommand.CanExecuteChanged
            {
                add { }
                remove { }
            }

            bool ICommand.CanExecute(object parameter)
            {
                return true;
            }

            void ICommand.Execute(object parameter)
            {
                AnimPoseComponent apc = parameter as AnimPoseComponent;
                apc.ref_object.parentScene.applyPoses(apc.ref_object.loadPose());
            }
        }

        private class ResetPoseCommand : ICommand
        {
            event EventHandler ICommand.CanExecuteChanged
            {
                add { }
                remove { }
            }

            bool ICommand.CanExecute(object parameter)
            {
                return true;
            }

            void ICommand.Execute(object parameter)
            {
                AnimPoseComponent apc = parameter as AnimPoseComponent;
                apc.ref_object.parentScene.resetPoses();
            }
        }

    }
}
