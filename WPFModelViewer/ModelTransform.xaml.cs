using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MVCore;
using MVCore.GMDL;
using OpenTK;

namespace WPFModelViewer
{
    /// <summary>
    /// Interaction logic for ModelTransform.xaml
    /// </summary>
    public partial class ModelTransform : UserControl
    {
        public model mdl;
        //Old Transforms
        private Vector3 oldtranslation;
        private Matrix3 oldrotation;
        private Vector3 oldscale;

        public ModelTransform()
        {
            InitializeComponent();
        }

        public void loadModel(model m)
        {
            mdl = m;

            //Save old Transforms
            oldtranslation = m.localPosition;
            oldrotation = m.localRotation;
            oldscale = m.localScale;

            //Setup Values to the control
            translationX.Value = m.localPosition.X;
            translationY.Value = m.localPosition.Y;
            translationZ.Value = m.localPosition.Z;

            Quaternion q = Quaternion.FromMatrix(oldrotation);
            Vector3 q_euler = quaternionToEuler(q);
            rotationX.Value = q_euler.X;
            rotationY.Value = q_euler.Y;
            rotationZ.Value = q_euler.Z;

            scaleX.Value = m.localScale.X;
            scaleY.Value = m.localScale.Y;
            scaleZ.Value = m.localScale.Z;

        }

        private void applyTransform(object sender, RoutedEventArgs e)
        {
            //Apply translation
            mdl.localPosition = new Vector3((float) translationX.Value,
                (float) translationY.Value,
                (float) translationZ.Value);

            //Apply rotation
            Matrix3 rotx, roty, rotz;
            Matrix3.CreateRotationX(MathUtils.radians((float) rotationX.Value), out rotx);
            Matrix3.CreateRotationY(MathUtils.radians((float) rotationY.Value), out roty);
            Matrix3.CreateRotationZ(MathUtils.radians((float) rotationZ.Value), out rotz);
            mdl.localRotation = rotz * rotx * roty;

            //Apply scale
            mdl.localScale = new Vector3((float)scaleX.Value,
                (float) scaleY.Value,
                (float) scaleZ.Value);

            //Save values to underlying SceneNode
            if (mdl.mbin_scene != null)
            {
                mdl.mbin_scene.Transform.RotX = (float)rotationX.Value;
                mdl.mbin_scene.Transform.RotY = (float)rotationY.Value;
                mdl.mbin_scene.Transform.RotZ = (float)rotationZ.Value;
                mdl.mbin_scene.Transform.TransX = (float)translationX.Value;
                mdl.mbin_scene.Transform.TransY = (float)translationY.Value;
                mdl.mbin_scene.Transform.TransZ = (float)translationZ.Value;
                mdl.mbin_scene.Transform.ScaleX = (float)scaleX.Value;
                mdl.mbin_scene.Transform.ScaleY = (float)scaleY.Value;
                mdl.mbin_scene.Transform.ScaleZ = (float)scaleZ.Value;
            }
            

        }

        //Reset transform
        private void resetTransform(object sender, RoutedEventArgs e)
        {
            mdl.localPosition = oldtranslation;
            mdl.localRotation = oldrotation;
            mdl.localScale = oldscale;

            //Reload Values to the control
            loadModel(mdl);
            
            //Save values to underlying SceneNode
            if (mdl.mbin_scene != null)
            {
                mdl.mbin_scene.Transform.ScaleX = oldscale.X;
                mdl.mbin_scene.Transform.ScaleY = oldscale.Y;
                mdl.mbin_scene.Transform.ScaleZ = oldscale.Z;
                mdl.mbin_scene.Transform.TransX = oldtranslation.X;
                mdl.mbin_scene.Transform.TransY = oldtranslation.Y;
                mdl.mbin_scene.Transform.TransZ = oldtranslation.Z;
                //Convert rotation from matrix to angles
                Quaternion q = Quaternion.FromMatrix(oldrotation);
                Vector3 q_euler = quaternionToEuler(q);

                mdl.mbin_scene.Transform.RotX = q_euler.X;
                mdl.mbin_scene.Transform.RotY = q_euler.Y;
                mdl.mbin_scene.Transform.RotZ = q_euler.Z;
                
            }
        }

        //Export to EXML
        private void exportToEXML(object sender, RoutedEventArgs e)
        {
            if (mdl?.mbin_scene != null)
            {
                var exmlstring = libMBIN.EXmlFile.WriteTemplate(mdl.mbin_scene);
                //Fetch scene name
                string[] split = mdl.mbin_scene.Name.Split('\\');
                string scnName = split[split.Length - 1];
                File.WriteAllText(scnName + ".exml", exmlstring);
                Console.WriteLine("Scene successfully exported to " + scnName + ".exml");
            }
        }

        private Vector3 quaternionToEuler(Quaternion q)
        {
            Vector3 euler = new Vector3();
            var t0 = +2.0 * (q.W * q.X + q.Y * q.Z);
            var t1 = +1.0 - 2.0 * (q.X * q.X + q.Y * q.Y);
            euler.X = MathUtils.degrees((float) Math.Atan2(t0, t1));

            var t2 = +2.0 * (q.W * q.Y - q.Z * q.X);

            if (t2 > 1.0)
                t2 = 1.0f;

            if (t2 < -1.0)
                t2 = -1.0f;

            euler.Y = MathUtils.degrees((float)Math.Asin(t2));


            var t3 = +2.0 * (q.W * q.Z + q.X * q.Y);

            var t4 = +1.0 - 2.0 * (q.Y * q.Y + q.Z * q.Z);

            euler.Z = MathUtils.degrees((float) Math.Atan2(t3, t4));

            return euler;
        }
    }
}
