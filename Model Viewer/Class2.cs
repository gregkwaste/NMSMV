using System;
using System.Collections.Generic;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;

namespace Model_Viewer
{
    class CGLControl : GLControl
    {
        public List<GMDL.model> objects = new List<GMDL.model>();

        private Vector3 rot = new Vector3(0.0f, 0.0f, 0.0f);
        private Vector3 target = new Vector3(0.0f, 0.0f, 0.0f);
        private Camera cam = new Camera(60);

        private float light_angle_y = 0.0f;
        private float light_angle_x = 0.0f;
        private float light_distance = 5.0f;
        private float scale = 1.0f;
        public int[] shader_programs;
        //Mouse Pos
        private int mouse_x;
        private int mouse_y;

        //Constructor
        public CGLControl()
        {
            this.Load += new System.EventHandler(this.genericLoad);
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.genericPaint);
            this.MouseHover += new System.EventHandler(this.hover);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.genericMouseMove);
            this.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(this.generic_KeyDown);

            //Set Camera position
            for (int i = 0; i < 20; i++)
                cam.Move(0.0f, -0.1f, 0.0f);
            this.rot.Y = 131;
            this.light_angle_y = 190;
            
        }

        private void render()
        {
            this.MakeCurrent();
            foreach (GMDL.model m in objects)
            {
                GL.UseProgram(m.ShaderProgram);
                if (m.ShaderProgram == -1)
                    throw new ApplicationException("Shit program");
                Matrix4 look = cam.GetViewMatrix();
                //Matrix4 look = Matrix4.Identity;
                float aspect = (float)this.ClientSize.Width / this.ClientSize.Height;
                Matrix4 proj = Matrix4.CreatePerspectiveFieldOfView(cam.fov, aspect,
                                                                    0.1f, 300.0f);
                int loc;
                //Send LookAt matrix to all shaders
                loc = GL.GetUniformLocation(m.ShaderProgram, "look");
                GL.UniformMatrix4(loc, false, ref look);
                //Send projection matrix to all shaders
                loc = GL.GetUniformLocation(m.ShaderProgram, "proj");
                GL.UniformMatrix4(loc, false, ref proj);
                //Send theta to all shaders
                loc = GL.GetUniformLocation(m.ShaderProgram, "theta");
                GL.Uniform3(loc, this.rot);

                if (m.ShaderProgram == shader_programs[0])
                {
                    //Object program
                    //Local Transformation is the same for all objects 
                    //Pending - Personalize local matrix on each object
                    loc = GL.GetUniformLocation(m.ShaderProgram, "scale");
                    GL.Uniform1(loc, this.scale);

                    loc = GL.GetUniformLocation(m.ShaderProgram, "light");

                    GL.Uniform3(loc, new Vector3((float)(light_distance * Math.Cos(this.light_angle_x * Math.PI / 180.0) *
                                                                Math.Sin(this.light_angle_y * Math.PI / 180.0)),
                                                 (float)(light_distance * Math.Sin(this.light_angle_x * Math.PI / 180.0)),
                                                 (float)(light_distance * Math.Cos(this.light_angle_x * Math.PI / 180.0) *
                                                                Math.Cos(this.light_angle_y * Math.PI / 180.0))));

                }
                else if (m.ShaderProgram == shader_programs[1])
                {
                    //Locator Program
                }
                GL.ClearColor(System.Drawing.Color.Black);
                m.render();
            }

        }

        private void genericLoad(object sender, EventArgs e)
        {
            
            this.MakeCurrent();
            this.Size = new System.Drawing.Size(320, 240);
            GL.Viewport(0, 0, this.ClientSize.Width, this.ClientSize.Height);
            GL.ClearColor(System.Drawing.Color.Black);
            GL.Enable(EnableCap.DepthTest);
            //glControl1.SwapBuffers();
            //glControl1.Invalidate();
            //Debug.WriteLine("GL Cleared");
            //Debug.WriteLine(GL.GetError());
        }

        private void genericPaint(object sender, EventArgs e)
        {
            this.MakeCurrent();
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            render();

            this.SwapBuffers();
        }

        private void hover(object sender, EventArgs e)
        {
            this.Focus();
            this.Invalidate();
        }

        private void genericMouseMove(object sender, MouseEventArgs e)
        {
            //Debug.WriteLine("Mouse moving on {0}", this.TabIndex);
            //int delta_x = (int) (Math.Pow(cam.fov, 4) * (e.X - mouse_x));
            //int delta_y = (int) (Math.Pow(cam.fov, 4) * (e.Y - mouse_y));
            int delta_x = (e.X - mouse_x);
            int delta_y = (e.Y - mouse_y);

            delta_x = Math.Min(Math.Max(delta_x, -10), 10);
            delta_y = Math.Min(Math.Max(delta_y, -10), 10);

            if (e.Button == MouseButtons.Left)
            {
                //Debug.WriteLine("Deltas {0} {1} {2}", delta_x, delta_y, e.Button);
                cam.AddRotation(delta_x, delta_y);
                this.Invalidate();
            }

            mouse_x = e.X;
            mouse_y = e.Y;

        }

        private void generic_KeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            //Debug.WriteLine("Key pressed {0}",e.KeyCode);
            switch (e.KeyCode)
            {
                //Local Transformation
                case Keys.Q:
                    this.rot.Y -= 4.0f;
                    break;
                case Keys.E:
                    this.rot.Y += 4.0f;
                    break;
                case Keys.Z:
                    this.rot.X -= 4.0f;
                    break;
                case Keys.C:
                    this.rot.X += 4.0f;
                    break;
                //Camera Movement
                case Keys.W:
                    cam.Move(0.0f, 0.1f, 0.0f);
                    break;
                case Keys.S:
                    cam.Move(0.0f, -0.1f, 0.0f);
                    break;
                case (Keys.D):
                    cam.Move(+0.1f, 0.0f, 0.0f);
                    break;
                case Keys.A:
                    cam.Move(-0.1f, 0.0f, 0.0f);
                    break;
                case (Keys.R):
                    cam.Move(0.0f, 0.0f, 0.1f);
                    break;
                case Keys.F:
                    cam.Move(0.0f, 0.0f, -0.1f);
                    break;
                //Light Rotation
                case Keys.N:
                    this.light_angle_y -= 1;
                    break;
                case Keys.M:
                    this.light_angle_y += 1;
                    break;
                case Keys.Oemcomma:
                    this.light_angle_x -= 1;
                    break;
                case Keys.OemPeriod:
                    this.light_angle_x += 1;
                    break;
                default:
                    Debug.WriteLine("Not Implemented Yet");
                    break;
            }
            this.Invalidate();
        }
    }

    
}
