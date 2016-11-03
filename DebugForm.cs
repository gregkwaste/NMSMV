using System;

public class DebugForm : Form
{
    public GLControl cgl;


    public DebugForm()
    {
        this.cgl = new GLControl();
        setupCgl();
        this.MouseHover += new System.EventHandler(this.hover);
        this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.genericMouseMove);


        this.Controls.Add(cgl);
        this.Show();
    }

    private void setupCgl()
    {
        this.Load += new System.EventHandler(this.genericLoad);
        this.Paint += new System.Windows.Forms.PaintEventHandler(this.genericPaint);


        //this.Resize += new System.EventHandler(this.genericResize);
    }

    private void genericLoad(object sender, EventArgs e)
    {

        this.cgl.MakeCurrent();
        Debug.WriteLine("----GL Control Load");
        this.cgl.Size = new System.Drawing.Size(512, 512);

        GL.Viewport(0, 0, this.cgl.ClientSize.Width, this.cgl.ClientSize.Height);
        GL.ClearColor(System.Drawing.Color.Red);
        GL.Enable(EnableCap.DepthTest);

        //glControl1.SwapBuffers();
        //glControl1.Invalidate();
        //Debug.WriteLine("GL Cleared");
        //Debug.WriteLine(GL.GetError());
    }

    private void genericMouseMove(object sender, MouseEventArgs e)
    {
        /*
         * DOING ABSOLUTELY NOTHING
         */
        Debug.WriteLine("Moving Mouse in debug");
        this.cgl.Update();
        this.Update();

    }

    private void hover(object sender, EventArgs e)
    {
        Debug.WriteLine("Hovering Mouse in debug");
        this.Focus();
        this.cgl.Focus();
        this.cgl.Update();
    }

    private void genericPaint(object sender, EventArgs e)
    {
        this.cgl.MakeCurrent();
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        Debug.WriteLine("----Painting Debug");
        this.renderquad();
        this.cgl.SwapBuffers();
    }

    private void renderquad()
    {
        GL.UseProgram(ResourceMgmt.shader_programs[3]);
        int quad_vbo;
        int quad_ebo;

        //Define Quad
        float[] quad = new float[6 * 3] {
                -1.0f, -1.0f, 0.0f,
                1.0f, -1.0f, 0.0f,
                -1.0f,  1.0f, 0.0f,
                -1.0f,  1.0f, 0.0f,
                1.0f, -1.0f, 0.0f,
                1.0f,  1.0f, 0.0f };

        float[] quadcolors = new float[6 * 3]
        {
                1.0f, 0.0f, 0.0f,
                0.0f, 1.0f, 0.0f,
                1.0f,  1.0f, 0.0f,
                1.0f,  1.0f, 0.0f,
                0.0f, 1.0f, 0.0f,
                0.0f,  0.0f, 1.0f
        };

        //Indices
        int[] indices = new Int32[2 * 3] { 0, 1, 2, 3, 4, 5 };

        //Generate OpenGL buffers
        int arraysize = sizeof(float) * 6 * 3;
        GL.GenBuffers(1, out quad_vbo);
        GL.GenBuffers(1, out quad_ebo);

        //Upload vertex buffer
        GL.BindBuffer(BufferTarget.ArrayBuffer, quad_vbo);
        //Allocate to NULL
        GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(2 * arraysize), (IntPtr)null, BufferUsageHint.StaticDraw);
        //Add verts data
        GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)arraysize, quad);
        //Add color data
        GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)arraysize, (IntPtr)arraysize, quadcolors);

        //Upload index buffer
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, quad_ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(sizeof(int) * 6), indices, BufferUsageHint.StaticDraw);


        // Attach to Shaders
        int vpos, cpos;

        //Vertex attribute
        //Bind vertex buffer
        GL.BindBuffer(BufferTarget.ArrayBuffer, quad_vbo);
        vpos = GL.GetAttribLocation(ResourceMgmt.shader_programs[3], "vPosition");
        GL.VertexAttribPointer(vpos, 3, VertexAttribPointerType.Float, false, 0, 0);
        GL.EnableVertexAttribArray(vpos);

        cpos = GL.GetAttribLocation(ResourceMgmt.shader_programs[3], "vColor");
        GL.VertexAttribPointer(cpos, 3, VertexAttribPointerType.Float, false, 0, (IntPtr)arraysize);
        GL.EnableVertexAttribArray(cpos);

        //Bind elem buffer
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, quad_ebo);


        //Create Texture to save to
        int out_tex = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, out_tex);
        //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        //NULL means reserve texture memory, but texels are undefined
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb, 512, 512, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);


        /*
        //Create New RenderBuffer
        int fb = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, fb);
        //Attach Texture to this FBO
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, out_tex, 0);
        GL.DrawBuffer(DrawBufferMode.ColorAttachment0);

        //Check
        if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
            Debug.WriteLine("MALAKIES STO FRAMEBUFFER");

        */
        //Render to the FBO
        //GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

        GL.Viewport(0, 0, 128, 128);
        GL.ClearColor(System.Drawing.Color.Blue);

        //GL.ClearColor(1.0f, 0.0f, 1.0f, 1.0f);

        //GL.Clear(ClearBufferMask.DepthBufferBit);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

        //ResourceMgmt.DebugWin.cgl.SwapBuffers();
        ResourceMgmt.DebugWin.cgl.Update();
        //ResourceMgmt.DebugWin.cgl.Draw;
        //ResourceMgmt.DebugWin.cgl.Invalidate();


        GL.DisableVertexAttribArray(vpos);
        GL.DisableVertexAttribArray(cpos);
        GL.DeleteBuffer(quad_vbo);
        GL.DeleteBuffer(quad_ebo);
    }



}
