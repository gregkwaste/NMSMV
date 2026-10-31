using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using GLSLHelper;
using MVCore.Common;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace MVCore.Text
{
    //Small SDF bitmap-font renderer for viewport information and future engine labels.
    public sealed class EngineTextRenderer : IDisposable
    {
        private struct Glyph
        {
            public int x, y, width, height, xOffset, yOffset, advance;
        }

        private readonly Dictionary<int, Glyph> glyphs = new();
        private readonly GLSLShaderConfig shader;
        private int atlasWidth, atlasHeight, fontSize, lineHeight;
        private int texture, vao, vbo, bufferBytes;
        private string uploadedText = null;
        private float uploadedX, uploadedY, uploadedHeight;
        private int vertexCount;

        public EngineTextRenderer(ResourceManager resources, string descriptorPath, string atlasPath)
        {
            shader = resources.GLShaders[SHADER_TYPE.TEXT_SHADER];
            loadDescriptor(descriptorPath);
            loadAtlas(atlasPath);

            vao = GL.GenVertexArray();
            vbo = GL.GenBuffer();
            GL.BindVertexArray(vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, 4096, IntPtr.Zero, BufferUsageHint.DynamicDraw);
            bufferBytes = 4096;
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        }

        private static Dictionary<string, string> fields(string line)
        {
            Dictionary<string, string> result = new();
            foreach (Match match in Regex.Matches(line, "([A-Za-z]+)=((?:\\\"[^\\\"]*\\\")|-?\\d+(?:,-?\\d+)*)"))
                result[match.Groups[1].Value] = match.Groups[2].Value.Trim('"');
            return result;
        }

        private void loadDescriptor(string path)
        {
            foreach (string line in File.ReadLines(path))
            {
                Dictionary<string, string> f = fields(line);
                if (line.StartsWith("info ", StringComparison.Ordinal))
                    fontSize = Math.Abs(int.Parse(f["size"], CultureInfo.InvariantCulture));
                else if (line.StartsWith("common ", StringComparison.Ordinal))
                {
                    lineHeight = int.Parse(f["lineHeight"], CultureInfo.InvariantCulture);
                    atlasWidth = int.Parse(f["scaleW"], CultureInfo.InvariantCulture);
                    atlasHeight = int.Parse(f["scaleH"], CultureInfo.InvariantCulture);
                }
                else if (line.StartsWith("char ", StringComparison.Ordinal))
                {
                    int id = int.Parse(f["id"], CultureInfo.InvariantCulture);
                    glyphs[id] = new Glyph
                    {
                        x = int.Parse(f["x"], CultureInfo.InvariantCulture),
                        y = int.Parse(f["y"], CultureInfo.InvariantCulture),
                        width = int.Parse(f["width"], CultureInfo.InvariantCulture),
                        height = int.Parse(f["height"], CultureInfo.InvariantCulture),
                        xOffset = int.Parse(f["xoffset"], CultureInfo.InvariantCulture),
                        yOffset = int.Parse(f["yoffset"], CultureInfo.InvariantCulture),
                        advance = int.Parse(f["xadvance"], CultureInfo.InvariantCulture)
                    };
                }
            }
            if (fontSize <= 0 || atlasWidth <= 0 || atlasHeight <= 0 || glyphs.Count == 0)
                throw new InvalidDataException("Invalid bitmap font descriptor: " + path);
        }

        private void loadAtlas(string path)
        {
            using Bitmap source = new Bitmap(path);
            using Bitmap bitmap = new Bitmap(source.Width, source.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
                graphics.DrawImageUnscaled(source, 0, 0);

            Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            texture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, bitmap.Width, bitmap.Height,
                0, OpenTK.Graphics.OpenGL4.PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);
            bitmap.UnlockBits(data);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        private static void addVertex(List<float> data, float x, float y, float u, float v)
        {
            data.Add(x); data.Add(y); data.Add(u); data.Add(v);
        }

        private void upload(string text, float x, float y, float pixelHeight)
        {
            if (text == uploadedText && x == uploadedX && y == uploadedY && pixelHeight == uploadedHeight)
                return;

            float scale = pixelHeight / fontSize;
            float caretX = x, caretY = y;
            List<float> vertices = new List<float>(text.Length * 24);
            foreach (char character in text)
            {
                if (character == '\r')
                    continue;
                if (character == '\n')
                {
                    caretX = x;
                    caretY += lineHeight * scale;
                    continue;
                }
                if (!glyphs.TryGetValue(character, out Glyph glyph))
                    glyph = glyphs.TryGetValue('?', out Glyph fallback) ? fallback : default;

                float left = caretX + glyph.xOffset * scale;
                float top = caretY + glyph.yOffset * scale;
                float right = left + glyph.width * scale;
                float bottom = top + glyph.height * scale;
                float u0 = glyph.x / (float)atlasWidth;
                float v0 = glyph.y / (float)atlasHeight;
                float u1 = (glyph.x + glyph.width) / (float)atlasWidth;
                float v1 = (glyph.y + glyph.height) / (float)atlasHeight;

                addVertex(vertices, left, top, u0, v0);
                addVertex(vertices, right, top, u1, v0);
                addVertex(vertices, left, bottom, u0, v1);
                addVertex(vertices, right, top, u1, v0);
                addVertex(vertices, right, bottom, u1, v1);
                addVertex(vertices, left, bottom, u0, v1);
                caretX += glyph.advance * scale;
            }

            float[] data = vertices.ToArray();
            int bytes = data.Length * sizeof(float);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            if (bytes > bufferBytes)
            {
                bufferBytes = Math.Max(bytes, bufferBytes * 2);
                GL.BufferData(BufferTarget.ArrayBuffer, bufferBytes, IntPtr.Zero, BufferUsageHint.DynamicDraw);
            }
            if (bytes > 0)
                GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, bytes, data);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            vertexCount = data.Length / 4;
            uploadedText = text;
            uploadedX = x;
            uploadedY = y;
            uploadedHeight = pixelHeight;
        }

        public void Render(string text, float x, float y, float pixelHeight, Vector3 color, int viewportWidth, int viewportHeight)
        {
            if (string.IsNullOrEmpty(text) || viewportWidth <= 0 || viewportHeight <= 0)
                return;
            upload(text, x, y, pixelHeight);
            if (vertexCount == 0)
                return;

            GL.UseProgram(shader.program_id);
            GL.Uniform2(shader.getLocation("viewportSize"), new Vector2(viewportWidth, viewportHeight));
            GL.Uniform3(shader.getLocation("textColor"), color);
            GL.Uniform1(shader.getLocation("fontAtlas"), 0);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
            GL.BindVertexArray(vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, vertexCount);
            GL.BindVertexArray(0);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            GL.PolygonMode(TriangleFace.FrontAndBack, RenderState.renderSettings.RENDERMODE);
        }

        public void Dispose()
        {
            if (vbo != 0) GL.DeleteBuffer(vbo);
            if (vao != 0) GL.DeleteVertexArray(vao);
            if (texture != 0) GL.DeleteTexture(texture);
            vbo = vao = texture = 0;
        }
    }
}
