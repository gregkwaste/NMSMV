using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;
using libMBIN.NMS.Toolkit;
using MVCore.Utils;
using OpenTK.Mathematics;

namespace MVCore.GMDL
{
    public class AnimPoseComponent : Component
    {
        public Model ref_object = null;
        //AnimationPoseData
        public List<AnimPoseData> _poseData = new List<AnimPoseData>();
        public AnimMetadata poseAnim = null; //The pose file, one frame range per pose
        public List<AnimPoseData> poseData
        {
            get
            {
                return _poseData;
            }
        }

        //The CorrelationMat is a 64x64 array. Its top-left block is the correlation matrix of SCALE, ADULT
        //and a few more global items, followed by one row per PoseAnim in order. Only the pose block is
        //needed to draw correlated random poses
        private const int CORRELATION_STRIDE = 64;
        private float[,] corrL = null; //Cholesky factor of the pose block, null samples the poses independently
        private bool suspendApply = false;
        private static readonly Random rng = new Random();

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
            //The pose file is shared by every node and instance that uses it, like regular animations
            if (Common.RenderState.activeResMgr.Animations.TryGetValue(apcd.Filename, out AnimMetadata cached))
                poseAnim = cached;
            else
            {
                TkAnimMetadata amd = NMSUtils.LoadNMSTemplate(apcd.Filename,
                    ref Common.RenderState.activeResMgr) as TkAnimMetadata;
                if (amd != null)
                {
                    poseAnim = new AnimMetadata(amd);
                    poseAnim.load();
                    Common.RenderState.activeResMgr.Animations[apcd.Filename] = poseAnim;
                }
            }

            //Load PoseAnims
            for (int i = 0; i < apcd.PoseAnims.Count; i++)
            {
                AnimPoseData my_apd = new AnimPoseData(apcd.PoseAnims[i]);
                my_apd.changed = onPoseChanged;
                poseData.Add(my_apd);
            }

            setupCorrelation(apcd.CorrelationMat);
        }

        private void setupCorrelation(IList<float> mat)
        {
            int count = poseData.Count;
            if (mat == null || count == 0 || mat.Count < CORRELATION_STRIDE * CORRELATION_STRIDE)
                return;

            //The used block is the run of ones on the diagonal, the rest of the array is uninitialised
            int n = 0;
            while (n < CORRELATION_STRIDE && Math.Abs(mat[n * CORRELATION_STRIDE + n] - 1.0f) < 1e-3f)
                n++;
            if (n < count)
            {
                Common.CallBacks.Log("Pose correlation matrix has an unexpected layout, poses will be independent");
                return;
            }

            int offset = n - count;
            float[,] L = new float[count, count];
            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    double sum = mat[(offset + i) * CORRELATION_STRIDE + offset + j];
                    for (int k = 0; k < j; k++)
                        sum -= L[i, k] * L[j, k];

                    if (i == j)
                        L[i, i] = (float)Math.Sqrt(Math.Max(sum, 1e-4)); //Guards against a matrix that is not quite positive definite
                    else
                        L[i, j] = (float)(sum / L[j, j]);
                }
            }
            corrL = L;
        }

        private Scene getScene()
        {
            if (ref_object == null)
                return null;
            //A scene's parentScene is the scene it was loaded into, its joints are its own
            return (ref_object as Scene) ?? ref_object.parentScene;
        }

        private void onPoseChanged()
        {
            if (!suspendApply)
                applyPose();
        }

        //Each pose frame is turned into the change from the pose file's frame 0 (the ranges start at 1),
        //the changes of all poses are added up per node and applied on top of the current transforms.
        //The animation system adds the same offsets on top of the animated transforms
        public void applyPose(bool clear = false)
        {
            Scene scene = getScene();
            if (poseAnim == null || scene == null)
                return;

            int lastFrame = Math.Max(poseAnim.FrameCount - 1, 0);

            foreach (TkAnimNodeData node in poseAnim.NodeData)
            {
                Model m;
                if (scene.jointDict.TryGetValue(node.Node, out Joint j))
                    m = j;
                else if (!scene.nodeDict.TryGetValue(node.Node, out m))
                    continue;

                Vector3 dp = Vector3.Zero;
                Vector3 ds = Vector3.One;
                Quaternion dq = Quaternion.Identity;

                if (!clear)
                {
                    Vector3[] positions = poseAnim.anim_positions[node.Node];
                    Vector3[] scales = poseAnim.anim_scales[node.Node];
                    Quaternion[] rotations = poseAnim.anim_rotations[node.Node];
                    Vector3 ref_p = positions[0];
                    Vector3 ref_s = scales[0];
                    Quaternion ref_qi = Quaternion.Invert(rotations[0]);

                    foreach (AnimPoseData pd in poseData)
                    {
                        int f = Math.Clamp(pd.PActivePoseFrame, 0, lastFrame);
                        dp += positions[f] - ref_p;
                        ds *= new Vector3(ref_s.X != 0.0f ? scales[f].X / ref_s.X : 1.0f,
                                          ref_s.Y != 0.0f ? scales[f].Y / ref_s.Y : 1.0f,
                                          ref_s.Z != 0.0f ? scales[f].Z / ref_s.Z : 1.0f);
                        dq = dq * (ref_qi * rotations[f]);
                    }
                    dq = Quaternion.Normalize(dq);
                }

                //Swap the old offsets for the new ones, so a paused animation keeps its frame
                Quaternion q = m.localRotation.ExtractRotation() * Quaternion.Invert(m.poseRotation) * dq;
                Vector3 s = m.localScale;
                s = new Vector3(m.poseScale.X != 0.0f ? s.X / m.poseScale.X : s.X,
                                m.poseScale.Y != 0.0f ? s.Y / m.poseScale.Y : s.Y,
                                m.poseScale.Z != 0.0f ? s.Z / m.poseScale.Z : s.Z) * ds;

                m.localPosition = m.localPosition - m.posePosition + dp;
                m.localScale = s;
                m.localRotation = Matrix4.CreateFromQuaternion(Quaternion.Normalize(q));

                m.posePosition = dp;
                m.poseScale = ds;
                m.poseRotation = dq;
            }

            scene.update();
        }

        //Draws a new set of pose values, correlated like the game's CorrelationMat
        public void randomise()
        {
            int count = poseData.Count;
            if (count == 0)
                return;

            double[] n = new double[count];
            for (int i = 0; i < count; i++)
            {
                //Box-Muller
                double u1 = 1.0 - rng.NextDouble();
                double u2 = rng.NextDouble();
                n[i] = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            }

            suspendApply = true;
            for (int i = 0; i < count; i++)
            {
                double z = n[i];
                if (corrL != null)
                {
                    z = 0.0;
                    for (int k = 0; k <= i; k++)
                        z += corrL[i, k] * n[k];
                }

                //The normal value becomes a position along the pose's frame range
                AnimPoseData pd = poseData[i];
                double v = normalCdf(z);
                pd.PActivePoseFrame = (int)Math.Round(pd.FrameStart + v * (pd.FrameEnd - pd.FrameStart));
            }
            suspendApply = false;

            applyPose();
        }

        private static double normalCdf(double z)
        {
            //Abramowitz-Stegun 7.1.26 for erf
            double x = Math.Abs(z) / Math.Sqrt(2.0);
            double t = 1.0 / (1.0 + 0.3275911 * x);
            double erf = 1.0 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
            return 0.5 * (1.0 + (z < 0 ? -erf : erf));
        }

        public override Component Clone()
        {
            //The pose file and the correlation factor are read-only and shared, the slider values are copied
            AnimPoseComponent apc = new AnimPoseComponent();
            apc.poseAnim = poseAnim;
            apc.corrL = corrL;
            foreach (AnimPoseData pd in poseData)
            {
                AnimPoseData copy = new AnimPoseData(pd);
                copy.PActivePoseFrame = pd.PActivePoseFrame;
                copy.changed = apc.onPoseChanged;
                apc.poseData.Add(copy);
            }
            return apc;
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
                apc?.applyPose();
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
                apc?.applyPose(true);
            }
        }

    }
}
