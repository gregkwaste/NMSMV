using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace WPFModelViewer
{
    //Dependency-free block encoders used by scene-bundle export. BC7 uses single-subset mode 6.
    internal static class DdsTextureConverter
    {
        private const uint DdsMagic = 0x20534444;
        private const uint FourCcDxt1 = 0x31545844;
        private const uint FourCcBc5U = 0x55354342;
        internal enum Compression { Bc1, Bc3, Bc5, Bc7 }

        public static Compression SelectCompression(string sourcePath, string samplerName)
        {
            string name = samplerName ?? "";
            if (name.EndsWith("NormalMap", StringComparison.OrdinalIgnoreCase))
                return Compression.Bc5;
            //Packed masks may carry useful alpha even when it is constant. Preserve RGBA.
            if (name.IndexOf("mask", StringComparison.OrdinalIgnoreCase) >= 0)
                return Compression.Bc7;
            return HasTransparency(sourcePath) ? Compression.Bc3 : Compression.Bc1;
        }

        public static bool HasTransparency(string sourcePath)
        {
            using Bitmap bitmap = loadArgb(sourcePath);
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                unsafe
                {
                    byte* row = (byte*)data.Scan0;
                    for (int y = 0; y < data.Height; y++, row += data.Stride)
                        for (int x = 0; x < data.Width; x++)
                            if (row[x * 4 + 3] < 255)
                                return true;
                }
                return false;
            }
            finally { bitmap.UnlockBits(data); }
        }

        public static void Convert(string sourcePath, string destinationPath, Compression compression)
        {
            using Bitmap bitmap = loadArgb(sourcePath);
            byte[] pixels = readRgba(bitmap);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            using BinaryWriter writer = new BinaryWriter(new FileStream(destinationPath,
                FileMode.Create, FileAccess.Write, FileShare.None));
            int blockBytes = compression == Compression.Bc1 ? 8 : 16;
            int linearSize = ((bitmap.Width + 3) / 4) * ((bitmap.Height + 3) / 4) * blockBytes;
            int mipCount = mipLevelCount(bitmap.Width, bitmap.Height);
            writeHeader(writer, bitmap.Width, bitmap.Height, linearSize, mipCount,
                compression switch
                {
                    Compression.Bc3 => 0x35545844u, //DXT5
                    Compression.Bc5 => FourCcBc5U,
                    Compression.Bc7 => 0x30315844u, //DX10
                    _ => FourCcDxt1
                });
            if (compression == Compression.Bc7)
            {
                writer.Write(98u); //DXGI_FORMAT_BC7_UNORM (masks are linear data)
                writer.Write(3u); //D3D10_RESOURCE_DIMENSION_TEXTURE2D
                writer.Write(0u);
                writer.Write(1u); //Array size
                writer.Write(0u);
            }

            Span<byte> block = stackalloc byte[16 * 4];
            int width = bitmap.Width, height = bitmap.Height;
            while (true)
            {
                for (int y = 0; y < height; y += 4)
                for (int x = 0; x < width; x += 4)
                {
                    readBlock(pixels, width, height, x, y, block);
                    if (compression == Compression.Bc5)
                    {
                        writeBc4(writer, block, 0);
                        writeBc4(writer, block, 1);
                    }
                    else if (compression == Compression.Bc7)
                        writeBc7(writer, block);
                    else
                    {
                        if (compression == Compression.Bc3)
                            writeBc4(writer, block, 3);
                        writeBc1(writer, block);
                    }
                }

                if (width == 1 && height == 1)
                    break;
                pixels = downsample(pixels, width, height, out width, out height);
            }
        }

        private static int mipLevelCount(int width, int height)
        {
            int count = 1;
            while (width > 1 || height > 1)
            {
                width = Math.Max(1, width / 2);
                height = Math.Max(1, height / 2);
                count++;
            }
            return count;
        }

        private static byte[] downsample(byte[] source, int sourceWidth, int sourceHeight,
            out int width, out int height)
        {
            width = Math.Max(1, sourceWidth / 2);
            height = Math.Max(1, sourceHeight / 2);
            byte[] result = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            for (int channel = 0; channel < 4; channel++)
            {
                int sum = 0, samples = 0;
                for (int oy = 0; oy < 2; oy++)
                for (int ox = 0; ox < 2; ox++)
                {
                    int sx = x * 2 + ox, sy = y * 2 + oy;
                    if (sx >= sourceWidth || sy >= sourceHeight)
                        continue;
                    sum += source[(sy * sourceWidth + sx) * 4 + channel];
                    samples++;
                }
                result[(y * width + x) * 4 + channel] = (byte)((sum + samples / 2) / samples);
            }
            return result;
        }

        private static Bitmap loadArgb(string path)
        {
            using Bitmap source = new Bitmap(path);
            Bitmap result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            using Graphics graphics = Graphics.FromImage(result);
            graphics.DrawImageUnscaled(source, 0, 0);
            return result;
        }

        private static byte[] readRgba(Bitmap bitmap)
        {
            byte[] result = new byte[bitmap.Width * bitmap.Height * 4];
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                unsafe
                {
                    byte* row = (byte*)data.Scan0;
                    for (int y = 0; y < bitmap.Height; y++, row += data.Stride)
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        int target = (y * bitmap.Width + x) * 4;
                        result[target] = row[x * 4 + 2];
                        result[target + 1] = row[x * 4 + 1];
                        result[target + 2] = row[x * 4];
                        result[target + 3] = row[x * 4 + 3];
                    }
                }
            }
            finally { bitmap.UnlockBits(data); }
            return result;
        }

        private static void readBlock(byte[] pixels, int width, int height, int startX, int startY, Span<byte> block)
        {
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
            {
                int sourceX = Math.Min(startX + x, width - 1);
                int sourceY = Math.Min(startY + y, height - 1);
                pixels.AsSpan((sourceY * width + sourceX) * 4, 4)
                    .CopyTo(block.Slice((y * 4 + x) * 4, 4));
            }
        }

        private static void writeBc1(BinaryWriter writer, ReadOnlySpan<byte> block)
        {
            byte minR = 255, minG = 255, minB = 255, maxR = 0, maxG = 0, maxB = 0;
            for (int i = 0; i < 16; i++)
            {
                int offset = i * 4;
                minR = Math.Min(minR, block[offset]); minG = Math.Min(minG, block[offset + 1]); minB = Math.Min(minB, block[offset + 2]);
                maxR = Math.Max(maxR, block[offset]); maxG = Math.Max(maxG, block[offset + 1]); maxB = Math.Max(maxB, block[offset + 2]);
            }
            ushort high = rgb565(maxR, maxG, maxB), low = rgb565(minR, minG, minB);
            if (high <= low)
            {
                if (low < UInt16.MaxValue) high = (ushort)(low + 1);
                else low--;
            }
            Span<byte> palette = stackalloc byte[12];
            expand565(high, palette.Slice(0, 3));
            expand565(low, palette.Slice(3, 3));
            for (int c = 0; c < 3; c++)
            {
                palette[6 + c] = (byte)((2 * palette[c] + palette[3 + c]) / 3);
                palette[9 + c] = (byte)((palette[c] + 2 * palette[3 + c]) / 3);
            }
            uint indices = 0;
            for (int i = 0; i < 16; i++)
            {
                int source = i * 4, best = 0, bestDistance = Int32.MaxValue;
                for (int p = 0; p < 4; p++)
                {
                    int po = p * 3;
                    int dr = block[source] - palette[po], dg = block[source + 1] - palette[po + 1], db = block[source + 2] - palette[po + 2];
                    int distance = dr * dr + dg * dg + db * db;
                    if (distance < bestDistance) { bestDistance = distance; best = p; }
                }
                indices |= (uint)best << (i * 2);
            }
            writer.Write(high); writer.Write(low); writer.Write(indices);
        }

        private static void writeBc4(BinaryWriter writer, ReadOnlySpan<byte> block, int channel)
        {
            byte minimum = 255, maximum = 0;
            for (int i = 0; i < 16; i++)
            {
                byte value = block[i * 4 + channel];
                minimum = Math.Min(minimum, value); maximum = Math.Max(maximum, value);
            }
            if (maximum == minimum)
            {
                if (minimum > 0) minimum--; else maximum++;
            }
            Span<byte> palette = stackalloc byte[8];
            palette[0] = maximum; palette[1] = minimum;
            for (int i = 1; i <= 6; i++)
                palette[i + 1] = (byte)(((7 - i) * maximum + i * minimum) / 7);
            ulong indices = 0;
            for (int i = 0; i < 16; i++)
            {
                int value = block[i * 4 + channel], best = 0, bestDistance = Int32.MaxValue;
                for (int p = 0; p < 8; p++)
                {
                    int distance = Math.Abs(value - palette[p]);
                    if (distance < bestDistance) { bestDistance = distance; best = p; }
                }
                indices |= (ulong)best << (i * 3);
            }
            writer.Write(maximum); writer.Write(minimum);
            for (int i = 0; i < 6; i++) writer.Write((byte)(indices >> (i * 8)));
        }

        //Mode 6: two RGBA endpoints, seven bits per channel plus one shared p-bit
        //per endpoint, and four-bit interpolation indices (the anchor has three).
        private static void writeBc7(BinaryWriter writer, ReadOnlySpan<byte> block)
        {
            ReadOnlySpan<int> weights = [0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64];
            Span<int> endpoints = stackalloc int[8];
            Span<int> pbits = stackalloc int[2];
            Span<int> indices = stackalloc int[16];
            for (int c = 0; c < 4; c++)
            {
                int minimum = 255, maximum = 0;
                for (int i = 0; i < 16; i++)
                {
                    minimum = Math.Min(minimum, block[i * 4 + c]);
                    maximum = Math.Max(maximum, block[i * 4 + c]);
                }
                endpoints[c] = minimum;
                endpoints[4 + c] = maximum;
            }
            for (int e = 0; e < 2; e++)
            {
                int bestError = Int32.MaxValue, bestP = 0;
                for (int p = 0; p < 2; p++)
                {
                    int error = 0;
                    for (int c = 0; c < 4; c++)
                    {
                        int value = endpoints[e * 4 + c];
                        int q = Math.Clamp((value - p + 1) / 2, 0, 127);
                        int delta = value - (q * 2 + p);
                        error += delta * delta;
                    }
                    if (error < bestError) { bestError = error; bestP = p; }
                }
                pbits[e] = bestP;
                for (int c = 0; c < 4; c++)
                    endpoints[e * 4 + c] = Math.Clamp((endpoints[e * 4 + c] - bestP + 1) / 2, 0, 127) * 2 + bestP;
            }
            for (int i = 0; i < 16; i++)
            {
                int bestError = Int32.MaxValue;
                for (int index = 0; index < 16; index++)
                {
                    int error = 0, weight = weights[index];
                    for (int c = 0; c < 4; c++)
                    {
                        int value = ((64 - weight) * endpoints[c] + weight * endpoints[4 + c] + 32) >> 6;
                        int delta = block[i * 4 + c] - value;
                        error += delta * delta;
                    }
                    if (error < bestError) { bestError = error; indices[i] = index; }
                }
            }
            if (indices[0] >= 8)
            {
                for (int c = 0; c < 4; c++)
                    (endpoints[c], endpoints[4 + c]) = (endpoints[4 + c], endpoints[c]);
                (pbits[0], pbits[1]) = (pbits[1], pbits[0]);
                for (int i = 0; i < 16; i++) indices[i] = 15 - indices[i];
            }
            Span<byte> encoded = stackalloc byte[16];
            encoded.Clear();
            int bit = 0;
            writeBits(encoded, ref bit, 64, 7); //Unary mode-6 selector
            for (int c = 0; c < 4; c++)
                for (int e = 0; e < 2; e++)
                    writeBits(encoded, ref bit, endpoints[e * 4 + c] >> 1, 7);
            for (int e = 0; e < 2; e++) writeBits(encoded, ref bit, pbits[e], 1);
            for (int i = 0; i < 16; i++) writeBits(encoded, ref bit, indices[i], i == 0 ? 3 : 4);
            writer.Write(encoded);
        }

        private static void writeBits(Span<byte> destination, ref int bit, int value, int count)
        {
            for (int i = 0; i < count; i++, bit++)
                destination[bit / 8] |= (byte)(((value >> i) & 1) << (bit % 8));
        }

        private static ushort rgb565(byte r, byte g, byte b) =>
            (ushort)(((r >> 3) << 11) | ((g >> 2) << 5) | (b >> 3));

        private static void expand565(ushort value, Span<byte> rgb)
        {
            int r = (value >> 11) & 31, g = (value >> 5) & 63, b = value & 31;
            rgb[0] = (byte)((r << 3) | (r >> 2));
            rgb[1] = (byte)((g << 2) | (g >> 4));
            rgb[2] = (byte)((b << 3) | (b >> 2));
        }

        private static void writeHeader(BinaryWriter writer, int width, int height, int linearSize,
            int mipCount, uint fourCc)
        {
            writer.Write(DdsMagic); writer.Write(124u);
            writer.Write(0x000A1007u); //CAPS | HEIGHT | WIDTH | PIXELFORMAT | LINEARSIZE | MIPMAPCOUNT
            writer.Write((uint)height); writer.Write((uint)width); writer.Write((uint)linearSize);
            writer.Write(0u); writer.Write((uint)mipCount);
            for (int i = 0; i < 11; i++) writer.Write(0u);
            writer.Write(32u); writer.Write(4u); writer.Write(fourCc);
            writer.Write(0u); writer.Write(0u); writer.Write(0u); writer.Write(0u); writer.Write(0u);
            writer.Write(0x00401008u); //DDSCAPS_TEXTURE | COMPLEX | MIPMAP
            writer.Write(0u); writer.Write(0u); writer.Write(0u); writer.Write(0u);
        }
    }
}
