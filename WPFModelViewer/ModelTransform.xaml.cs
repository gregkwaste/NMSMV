using System;
using System.IO;
using System.Text.RegularExpressions;
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
        private Matrix4 oldrotation;
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
            translationX.Text = m.localPosition.X.ToString();
            translationY.Text = m.localPosition.Y.ToString();
            translationZ.Text = m.localPosition.Z.ToString();

            //Vector3 q_euler = quaternionToEuler(m.localRotationQuaternion);
            Vector3 q_euler = matrixToEuler(m.localRotation, "ZXY");
            //For some reason results are inverted
            rotationX.Text = (-q_euler.X).ToString();
            rotationY.Text = (-q_euler.Y).ToString();
            rotationZ.Text = (-q_euler.Z).ToString();

            scaleX.Text = m.localScale.X.ToString();
            scaleY.Text = m.localScale.Y.ToString();
            scaleZ.Text = m.localScale.Z.ToString();

        }

        private void applyTransformButtonTrigger(object sender, RoutedEventArgs e)
        {
            applyTransform();
        }


        private void applyTransform()
        {
            float lX, lY, lZ;
            //Apply translation
            float.TryParse(translationX.Text, out lX);
            float.TryParse(translationY.Text, out lY);
            float.TryParse(translationZ.Text, out lZ);
            mdl.localPosition = new Vector3(lX, lY, lZ);

            //Apply rotation
            float.TryParse(rotationX.Text, out lX);
            float.TryParse(rotationY.Text, out lY);
            float.TryParse(rotationZ.Text, out lZ);
            Matrix4 rotx, roty, rotz;
            Matrix4.CreateRotationX(MathUtils.radians(lX), out rotx);
            Matrix4.CreateRotationY(MathUtils.radians(lY), out roty);
            Matrix4.CreateRotationZ(MathUtils.radians(lZ), out rotz);

            //Quaternion q_euler = Quaternion.FromEulerAngles(MathUtils.radians((float)rotationX.Value),
            //                                            MathUtils.radians((float)rotationY.Value),
            //                                            MathUtils.radians((float)rotationZ.Value));

            mdl.localRotation = rotz * rotx * roty;

            //Apply scale
            float.TryParse(scaleX.Text, out lX);
            float.TryParse(scaleY.Text, out lY);
            float.TryParse(scaleZ.Text, out lZ);
            mdl.localScale = new Vector3(lX, lY, lZ);
            
            //Save values to underlying SceneNode
            if (mdl.mbin_scene != null)
            {
                float.TryParse(rotationX.Text, out mdl.mbin_scene.Transform.RotX);
                float.TryParse(rotationY.Text, out mdl.mbin_scene.Transform.RotY);
                float.TryParse(rotationZ.Text, out mdl.mbin_scene.Transform.RotZ);
                //mdl.mbin_scene.Transform.RotX = (float)rotationX.Value;
                //mdl.mbin_scene.Transform.RotY = (float)rotationY.Value;
                //mdl.mbin_scene.Transform.RotZ = (float)rotationZ.Value;
                float.TryParse(translationX.Text, out mdl.mbin_scene.Transform.TransX);
                float.TryParse(translationY.Text, out mdl.mbin_scene.Transform.TransY);
                float.TryParse(translationZ.Text, out mdl.mbin_scene.Transform.TransZ);
                //mdl.mbin_scene.Transform.TransX = (float)translationX.Value;
                //mdl.mbin_scene.Transform.TransY = (float)translationY.Value;
                //mdl.mbin_scene.Transform.TransZ = (float)translationZ.Value;
                float.TryParse(scaleX.Text, out mdl.mbin_scene.Transform.ScaleX);
                float.TryParse(scaleY.Text, out mdl.mbin_scene.Transform.ScaleY);
                float.TryParse(scaleZ.Text, out mdl.mbin_scene.Transform.ScaleZ);
                //mdl.mbin_scene.Transform.ScaleX = (float) scaleX.Value;
                //mdl.mbin_scene.Transform.ScaleY = (float)scaleY.Value;
                //mdl.mbin_scene.Transform.ScaleZ = (float)scaleZ.Value;
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
                //Vector3 q_euler = quaternionToEuler(oldrotation);
                Matrix4 tempMat = oldrotation;
                tempMat.Transpose();
                Vector3 q_euler = matrixToEuler(tempMat, "ZXY");

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
                //Fetch scene name
                string[] split = mdl.mbin_scene.Name.Split('\\');
                string scnName = split[split.Length - 1];
                mdl.mbin_scene.WriteToExml(scnName + ".SCENE.EXML");
                MessageBox.Show("Scene successfully exported to " + scnName + ".exml");
            }
        }

        //Export to MBIN
        private void exportToMBIN(object sender, RoutedEventArgs e)
        {
            if (mdl?.mbin_scene != null)
            {
                //Fetch scene name
                string[] split = mdl.mbin_scene.Name.Split('\\');
                string scnName = split[split.Length - 1];
                mdl.mbin_scene.WriteToMbin(scnName.ToUpper() + ".SCENE.MBIN");
                MessageBox.Show("Scene successfully exported to " + scnName.ToUpper() + ".MBIN");
            }
        }

        private Vector3 quaternionToEuler(Quaternion q)
        {
            Matrix4 rotMat = Matrix4.CreateFromQuaternion(q);
            rotMat.Transpose();
            Vector3 test;
            test = matrixToEuler(rotMat, "YZX");
            return test;
        }

        private Vector3 matrixToEuler(Matrix4 rotMat, string order)
        {
            Vector3 euler = new Vector3();
            
            //rotMat.Transpose();
            double m11 = rotMat[0, 0];
            double m12 = rotMat[0, 1];
            double m13 = rotMat[0, 2];
            double m21 = rotMat[1, 0];
            double m22 = rotMat[1, 1];
            double m23 = rotMat[1, 2];
            double m31 = rotMat[2, 0];
            double m32 = rotMat[2, 1];
            double m33 = rotMat[2, 2];
            
            if (order == "XYZ")
            {

                euler.Y = (float) Math.Asin(MathUtils.clamp(m13, -1, 1));
                if (Math.Abs(m13) < 0.99999)
                {

                    euler.X = (float) Math.Atan2(-m23, m33);
                    euler.Z = (float) Math.Atan2(-m12, m11);
                }
                else
                {

                    euler.X = (float) Math.Atan2(m32, m22);
                    euler.Z = 0;
                }

            }
            else if (order == "YXZ")
            {

                euler.X = (float) Math.Asin(-MathUtils.clamp(m23, -1, 1));

                if (Math.Abs(m23) < 0.99999)
                {

                    euler.Y = (float) Math.Atan2(m13, m33);
                    euler.Z = (float)Math.Atan2(m21, m22);
                }
                else
                {

                    euler.Y = (float) Math.Atan2(-m31, m11);
                    euler.Z = 0;
                }

            }
            else if (order == "ZXY")
            {

                euler.X = (float)Math.Asin(MathUtils.clamp(m32, -1, 1));
                
                if (Math.Abs(m32) < 0.99999)
                {

                    euler.Y = (float) Math.Atan2(-m31, m33);
                    euler.Z = (float) Math.Atan2(-m12, m22);

                }
                else
                {

                    euler.Y = 0;
                    euler.Z = (float)Math.Atan2(m21, m11);

                }

            }
            else if (order == "ZYX")
            {

                euler.Y = (float) Math.Asin(-MathUtils.clamp(m31, -1, 1));

                if (Math.Abs(m31) < 0.99999)
                {

                    euler.X = (float) Math.Atan2(m32, m33);
                    euler.Z = (float) Math.Atan2(m21, m11);

                }
                else
                {

                    euler.X = 0;
                    euler.Z = (float) Math.Atan2(-m12, m22);

                }

            }
            else if (order == "YZX")
            {

                euler.Z = (float) Math.Asin(MathUtils.clamp(m21, -1, 1));

                if (Math.Abs(m21) < 0.99999)
                {

                    euler.X = (float) Math.Atan2(-m23, m22);
                    euler.Y = (float) Math.Atan2(-m31, m11);

                }
                else
                {

                    euler.X = 0;
                    euler.Y = (float) Math.Atan2(m13, m33);

                }

            }
            else if (order == "XZY")
            {

                euler.Z = (float) Math.Asin(-MathUtils.clamp(m12, -1, 1));

                if (Math.Abs(m12) < 0.99999)
                {

                    euler.X = (float) Math.Atan2(m32, m22);
                    euler.Y = (float) Math.Atan2(m13, m11);

                }
                else
                {

                    euler.X = (float) Math.Atan2(-m23, m33);
                    euler.Y = 0;

                }

            }
            else
            {
                Console.WriteLine("Unsupported Order");
            }

            
            //Convert to degrees
            euler.X = MathUtils.degrees(euler.X);
            euler.Y = MathUtils.degrees(euler.Y);
            euler.Z = MathUtils.degrees(euler.Z);

            //Console.WriteLine("Converted Angles {0} {1} {2}", euler.X, euler.Y, euler.Z);

            return euler;
        }

        private void HandleKeyUpEvent(object sender, System.Windows.Input.KeyEventArgs e)
        {
            //I assume that the sender is always a textbox
            TextBox tb = (TextBox) sender;
            float val;
            float.TryParse(tb.Text, out val); //Load the value

            switch (e.Key)
            {
                case System.Windows.Input.Key.Up:
                    val += 0.5f;
                    tb.Text = val.ToString();
                    break;
                case System.Windows.Input.Key.Down:
                    val -= 0.5f;
                    tb.Text = val.ToString();
                    break;
                case System.Windows.Input.Key.Return:
                    applyTransform();
                    break;
            }
        }

        private void FilterTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            //Console.WriteLine("Writing Shit");
            //Regex regex = new Regex("^(0|[1-9][0-9]*)$");
            Regex regex = new Regex("[^0-9.-]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
}
