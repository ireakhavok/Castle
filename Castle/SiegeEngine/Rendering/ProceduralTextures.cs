using SiegeEngine.Rendering.Definitions;
using System;
namespace SiegeEngine.Rendering
{
    public class ProceduralTextures
    {
        public struct Color
        {
            public byte r, g, b, a;
            public Color(byte r, byte g, byte b, byte a = 255) => (this.r, this.g, this.b, this.a) = (r, g, b, a);
        }
        private static readonly int[] Perm = new int[256] {
            151,160,137,91,90,15,131,13,201,95,96,53,194,233,7,225,140,36,103,30,69,142,
            8,99,37,240,21,10,23,190,6,148,247,120,234,75,0,26,197,62,94,252,219,203,117,
            35,11,32,57,177,33,88,237,149,56,87,174,20,125,136,171,168,68,175,74,165,71,
            134,139,48,27,166,77,146,158,231,83,111,229,122,60,211,133,230,220,105,92,41,
            55,46,245,40,244,102,143,54,65,25,63,161,1,216,80,73,209,76,132,187,208,89,
            18,169,200,196,135,130,116,188,159,86,164,100,109,198,173,186,3,64,52,217,226,
            250,124,123,5,202,38,147,118,126,255,82,85,212,207,206,59,227,47,16,58,17,182,
            189,28,42,223,183,170,213,119,248,152,2,44,154,163,70,221,153,101,155,167,43,
            172,9,129,22,39,253,19,98,108,110,79,113,224,232,178,185,112,104,218,246,97,
            228,251,34,242,193,238,210,144,12,191,179,162,241,81,51,145,235,249,14,239,
            107,49,192,214,31,181,199,106,157,184,84,204,176,115,121,50,45,127,4,150,254,
            138,236,205,93,222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180
        };
        private static readonly int[] p = new int[512];
        static ProceduralTextures()
        {
            for (int i = 0; i < 512; i++)
            {
                p[i] = Perm[i % 256];
            }
        }
        private static float PerlinNoise(float x, float y)
        {
            int xi = (int)x & 255;
            int yi = (int)y & 255;
            float xf = x - (int)x;
            float yf = y - (int)y;
            float u = Fade(xf);
            float v = Fade(yf);
            int aa = p[xi + yi];
            int ab = p[xi + yi + 1];
            int ba = p[xi + 1 + yi];
            int bb = p[xi + 1 + yi + 1];
            float x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
            float x2 = Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);
            return (Lerp(x1, x2, v) + 1) * 0.5f;
        }
        private static float OctaveNoise(float x, float y, int octaves = 4)
        {
            float total = 0f;
            float frequency = 0.1f;
            float amplitude = 1f;
            float maxValue = 0f;
            for (int i = 0; i < octaves; i++)
            {
                total += PerlinNoise(x * frequency, y * frequency) * amplitude;
                maxValue += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }
            return total / maxValue;
        }
        private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
        private static float Lerp(float a, float b, float t) => a + t * (b - a);
        private static float Grad(int hash, float x, float y)
        {
            int h = hash & 15;
            float u = h < 8 ? x : y;
            float v = h < 4 ? y : h == 12 || h == 14 ? x : 0;
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }
        public static Color[] GenerateTexture(int id, int width = 256, int height = 256)
        {
            Color[] pixels = new Color[width * height];
            if (id == TextureDefinitions.Grass)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = OctaveNoise(x, y);
                        byte g = (byte)(n * 100 + 100);
                        pixels[y * width + x] = new Color((byte)(g * 0.3f), g, (byte)(g * 0.2f));
                    }
            }
            else if (id == TextureDefinitions.Dirt)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = OctaveNoise(x, y, 6);
                        byte b = (byte)(n * 80 + 80);
                        pixels[y * width + x] = new Color(b, (byte)(b * 0.8f), (byte)(b * 0.6f));
                    }
            }
            else if (id == TextureDefinitions.Stone)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = OctaveNoise(x, y, 5);
                        byte g = (byte)(n * 60 + 100);
                        pixels[y * width + x] = new Color(g, g, g);
                    }
            }
            else if (id == TextureDefinitions.Sand)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = PerlinNoise(x * 0.12f, y * 0.12f);
                        byte s = (byte)(n * 127 + 128);
                        pixels[y * width + x] = new Color(s, (byte)(s * 0.9f), (byte)(s * 0.7f));
                    }
            }
            else if (id == TextureDefinitions.Water)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = OctaveNoise(x, y, 3);
                        byte b = (byte)(n * 100 + 100);
                        pixels[y * width + x] = new Color(20, 80, b, (byte)(n * 155 + 100));
                    }
            }
            else if (id == TextureDefinitions.Snow)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = PerlinNoise(x * 0.07f, y * 0.07f);
                        byte w = (byte)(n * 127 + 128);
                        pixels[y * width + x] = new Color(w, w, w);
                    }
            }
            else if (id == TextureDefinitions.Wood)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = PerlinNoise(x * 0.03f, y * 0.2f);
                        byte b = (byte)(n * 102 + 51);
                        pixels[y * width + x] = new Color(b, (byte)(b * 0.7f), (byte)(b * 0.4f));
                    }
            }
            else if (id == TextureDefinitions.Bark)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = PerlinNoise(x * 0.06f, y * 0.06f);
                        byte b = (byte)(n * 76 + 51);
                        pixels[y * width + x] = new Color(b, (byte)(b * 0.8f), (byte)(b * 0.6f));
                    }
            }
            else if (id == TextureDefinitions.Leaves)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = PerlinNoise(x * 0.09f, y * 0.09f);
                        pixels[y * width + x] = new Color(20, (byte)(n * 153 + 102), 30);
                    }
            }
            else if (id == TextureDefinitions.Mud)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = PerlinNoise(x * 0.04f, y * 0.04f);
                        byte m = (byte)(n * 102 + 51);
                        pixels[y * width + x] = new Color(m, (byte)(m * 0.9f), (byte)(m * 0.7f));
                    }
            }
            else if (id == TextureDefinitions.Gravel)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = PerlinNoise(x * 0.11f, y * 0.11f);
                        byte g = (byte)(n * 153 + 51);
                        pixels[y * width + x] = new Color(g, g, (byte)(g * 0.9f));
                    }
            }
            else if (id == TextureDefinitions.Clay)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = PerlinNoise(x * 0.06f, y * 0.06f);
                        byte c = (byte)(n * 102 + 76);
                        pixels[y * width + x] = new Color(c, (byte)(c * 0.8f), (byte)(c * 0.6f));
                    }
            }
            else if (id == TextureDefinitions.Lava)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = PerlinNoise(x * 0.13f, y * 0.13f);
                        pixels[y * width + x] = new Color((byte)(n * 204 + 51), 20, 10);
                    }
            }
            else if (id == TextureDefinitions.Ice)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = PerlinNoise(x * 0.08f, y * 0.08f);
                        byte i = (byte)(n * 127 + 128);
                        pixels[y * width + x] = new Color(i, i, (byte)(i * 1.1f > 255 ? 255 : i * 1.1f));
                    }
            }
            else if (id == TextureDefinitions.Moss)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = PerlinNoise(x * 0.1f, y * 0.1f);
                        pixels[y * width + x] = new Color(40, (byte)(n * 153 + 76), 20);
                    }
            }
            else if (id == TextureDefinitions.Coal)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = PerlinNoise(x * 0.07f, y * 0.07f);
                        byte c = (byte)(n * 76 + 25);
                        pixels[y * width + x] = new Color(c, c, c);
                    }
            }
            else if (id == TextureDefinitions.Iron)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = PerlinNoise(x * 0.09f, y * 0.09f);
                        byte i = (byte)(n * 102 + 76);
                        pixels[y * width + x] = new Color(i, (byte)(i * 0.9f), (byte)(i * 0.8f));
                    }
            }
            else if (id == TextureDefinitions.Gold)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = PerlinNoise(x * 0.11f, y * 0.11f);
                        pixels[y * width + x] = new Color((byte)(n * 153 + 102), (byte)(n * 127 + 76), 20);
                    }
            }
            else if (id == TextureDefinitions.Copper)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = PerlinNoise(x * 0.06f, y * 0.06f);
                        pixels[y * width + x] = new Color((byte)(n * 153 + 76), (byte)(n * 102 + 51), 30);
                    }
            }
            else if (id == TextureDefinitions.Brick)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = PerlinNoise(x * 0.05f, y * 0.05f);
                        byte b = (byte)(n * 127 + 76);
                        pixels[y * width + x] = new Color(b, (byte)(b * 0.7f), (byte)(b * 0.5f));
                    }
            }
            else if (id == TextureDefinitions.Concrete)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = PerlinNoise(x * 0.08f, y * 0.08f);
                        byte c = (byte)(n * 102 + 102);
                        pixels[y * width + x] = new Color(c, c, c);
                    }
            }
            else if (id == TextureDefinitions.Ash)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = PerlinNoise(x * 0.07f, y * 0.07f);
                        byte a = (byte)(n * 76 + 76);
                        pixels[y * width + x] = new Color(a, a, a);
                    }
            }
            else if (id == TextureDefinitions.Obsidian)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = PerlinNoise(x * 0.09f, y * 0.09f);
                        byte o = (byte)(n * 51 + 25);
                        pixels[y * width + x] = new Color(o, o, (byte)(o * 1.1f > 255 ? 255 : o * 1.1f));
                    }
            }
            else if (id == TextureDefinitions.Slate)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = PerlinNoise(x * 0.06f, y * 0.06f);
                        byte s = (byte)(n * 102 + 76);
                        pixels[y * width + x] = new Color(s, (byte)(s * 0.95f), s);
                    }
            }
            else if (id == TextureDefinitions.Marble)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = PerlinNoise(x * 0.04f, y * 0.04f);
                        byte m = (byte)(n * 127 + 128);
                        pixels[y * width + x] = new Color(m, m, (byte)(m * 0.9f));
                    }
            }
            else if (id == TextureDefinitions.Rust)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = PerlinNoise(x * 0.1f, y * 0.1f);
                        pixels[y * width + x] = new Color((byte)(n * 153 + 76), (byte)(n * 102 + 51), 20);
                    }
            }
            else if (id == TextureDefinitions.Pebble)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = PerlinNoise(x * 0.13f, y * 0.13f);
                        byte p = (byte)(n * 127 + 76);
                        pixels[y * width + x] = new Color(p, (byte)(p * 0.9f), (byte)(p * 0.8f));
                    }
            }
            else if (id == TextureDefinitions.Chalk)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = PerlinNoise(x * 0.07f, y * 0.07f);
                        byte c = (byte)(n * 102 + 153);
                        pixels[y * width + x] = new Color(c, c, c);
                    }
            }
            else if (id == TextureDefinitions.Salt)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = PerlinNoise(x * 0.11f, y * 0.11f);
                        byte s = (byte)(n * 76 + 179);
                        pixels[y * width + x] = new Color(s, s, s);
                    }
            }
            else if (id == TextureDefinitions.Coral)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = PerlinNoise(x * 0.09f, y * 0.09f);
                        pixels[y * width + x] = new Color((byte)(n * 153 + 102), 50, (byte)(n * 102 + 76));
                    }
            }
            else if (id == TextureDefinitions.Algae)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = PerlinNoise(x * 0.08f, y * 0.08f);
                        pixels[y * width + x] = new Color(20, (byte)(n * 127 + 76), (byte)(n * 76 + 51));
                    }
            }
            else if (id == TextureDefinitions.Crystal)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = PerlinNoise(x * 0.14f, y * 0.14f);
                        byte c = (byte)(n * 127 + 128);
                        pixels[y * width + x] = new Color((byte)(c * 0.8f), c, (byte)(c * 1.1f > 255 ? 255 : c * 1.1f));
                    }
            }
            else if (id == TextureDefinitions.Door)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = OctaveNoise(x, y, 3);
                        byte w = (byte)(n * 80 + 100);
                        pixels[y * width + x] = new Color(w, (byte)(w * 0.6f), (byte)(w * 0.4f));
                    }
            }
            else if (id == TextureDefinitions.Trap)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = OctaveNoise(x, y, 5);
                        pixels[y * width + x] = new Color((byte)(n * 100 + 100), 50, 50);
                    }
            }
            else if (id == TextureDefinitions.Light)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = OctaveNoise(x, y, 2);
                        pixels[y * width + x] = new Color(255, 255, (byte)(n * 155 + 100));
                    }
            }
            else if (id == TextureDefinitions.Fire)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = OctaveNoise(x, y, 4);
                        pixels[y * width + x] = new Color((byte)(n * 155 + 100), (byte)(n * 100 + 50), 0);
                    }
            }
            else if (id == TextureDefinitions.Roof)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = OctaveNoise(x, y, 3);
                        byte r = (byte)(n * 80 + 80);
                        pixels[y * width + x] = new Color(r, (byte)(r * 0.9f), (byte)(r * 0.8f));
                    }
            }
            else if (id == TextureDefinitions.Window)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = OctaveNoise(x, y, 2);
                        pixels[y * width + x] = new Color(150, 180, (byte)(n * 100 + 150));
                    }
            }
            else if (id == TextureDefinitions.Pathway)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = OctaveNoise(x, y, 4);
                        byte p = (byte)(n * 70 + 90);
                        pixels[y * width + x] = new Color(p, p, (byte)(p * 0.95f));
                    }
            }
            else if (id == TextureDefinitions.Road)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = OctaveNoise(x, y, 3);
                        byte r = (byte)(n * 60 + 100);
                        pixels[y * width + x] = new Color(r, r, r);
                    }
            }
            else if (id == TextureDefinitions.Bridge)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = OctaveNoise(x, y, 3);
                        byte b = (byte)(n * 80 + 100);
                        pixels[y * width + x] = new Color(b, (byte)(b * 0.7f), (byte)(b * 0.5f));
                    }
            }
            else if (id == TextureDefinitions.Monster)
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        float n = OctaveNoise(x, y, 5);
                        pixels[y * width + x] = new Color((byte)(n * 100 + 100), 20, 20);
                    }
            }
            else
            {
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        pixels[y * width + x] = new Color(0, 0, 0);
                    }
            }
            return pixels;
        }
    }
}