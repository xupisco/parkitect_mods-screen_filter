using UnityEngine;

namespace screen_filter
{
    // Builds the color lookup table the game's Lutify effect applies to the final image.
    // Lutify expects a strip of square slices, one per level of blue: width = size * size, height = size.
    internal static class lut_builder
    {
        private const int size = 32;

        public static Texture2D build(filter_settings settings, Texture2D base_lut)
        {
            int base_size = 0;
            Color[] base_pixels = null;
            if (base_lut != null)
            {
                if (base_lut.isReadable && base_lut.width == base_lut.height * base_lut.height)
                {
                    base_size = base_lut.height;
                    base_pixels = base_lut.GetPixels();
                }
                else
                {
                    Debug.LogWarning("[screen_filter] Ignoring base filter " + base_lut.name + ": its table can't be read");
                }
            }

            Color[] pixels = new Color[size * size * size];
            float step = 1f / (size - 1);
            for (int blue = 0; blue < size; blue++)
            {
                for (int green = 0; green < size; green++)
                {
                    for (int red = 0; red < size; red++)
                    {
                        Color color = new Color(red * step, green * step, blue * step, 1f);
                        if (base_pixels != null)
                        {
                            color = sample(base_pixels, base_size, color);
                        }
                        pixels[pixel_index(size, red, green, blue)] = adjust(color, settings);
                    }
                }
            }

            Texture2D lut = new Texture2D(size * size, size, TextureFormat.RGBA32, mipChain: false, linear: true);
            lut.name = "ScreenFilterLut";
            lut.wrapMode = TextureWrapMode.Clamp;
            lut.SetPixels(pixels);
            lut.Apply();
            return lut;
        }

        // Green runs from the top of the texture, as in Lutify.ConvertBaseTexture3D.
        private static int pixel_index(int table_size, int red, int green, int blue)
        {
            return blue * table_size + red + (table_size - green - 1) * table_size * table_size;
        }

        private static Color adjust(Color color, filter_settings settings)
        {
            // Warmer moves red up and blue down, and the tint trades green against magenta.
            float red = color.r * (1f + settings.temperature * 0.2f + settings.tint * 0.1f);
            float green = color.g * (1f - settings.tint * 0.2f);
            float blue = color.b * (1f - settings.temperature * 0.2f + settings.tint * 0.1f);

            float brightness = 1f + settings.brightness;
            red *= brightness;
            green *= brightness;
            blue *= brightness;

            float contrast = 1f + settings.contrast;
            red = (red - 0.5f) * contrast + 0.5f;
            green = (green - 0.5f) * contrast + 0.5f;
            blue = (blue - 0.5f) * contrast + 0.5f;

            float luminance = red * 0.2126f + green * 0.7152f + blue * 0.0722f;
            red = Mathf.LerpUnclamped(luminance, red, settings.saturation);
            green = Mathf.LerpUnclamped(luminance, green, settings.saturation);
            blue = Mathf.LerpUnclamped(luminance, blue, settings.saturation);

            float inverse_gamma = 1f / settings.gamma;
            return new Color(
                Mathf.Pow(Mathf.Clamp01(red), inverse_gamma),
                Mathf.Pow(Mathf.Clamp01(green), inverse_gamma),
                Mathf.Pow(Mathf.Clamp01(blue), inverse_gamma),
                1f);
        }

        // Trilinear lookup, so that a base table of any size works.
        private static Color sample(Color[] pixels, int table_size, Color color)
        {
            float last = table_size - 1;
            float red = color.r * last, green = color.g * last, blue = color.b * last;
            int r0 = (int)red, g0 = (int)green, b0 = (int)blue;
            int r1 = Mathf.Min(r0 + 1, table_size - 1), g1 = Mathf.Min(g0 + 1, table_size - 1), b1 = Mathf.Min(b0 + 1, table_size - 1);
            float rt = red - r0, gt = green - g0, bt = blue - b0;

            Color low = Color.LerpUnclamped(
                Color.LerpUnclamped(pixels[pixel_index(table_size, r0, g0, b0)], pixels[pixel_index(table_size, r1, g0, b0)], rt),
                Color.LerpUnclamped(pixels[pixel_index(table_size, r0, g1, b0)], pixels[pixel_index(table_size, r1, g1, b0)], rt),
                gt);
            Color high = Color.LerpUnclamped(
                Color.LerpUnclamped(pixels[pixel_index(table_size, r0, g0, b1)], pixels[pixel_index(table_size, r1, g0, b1)], rt),
                Color.LerpUnclamped(pixels[pixel_index(table_size, r0, g1, b1)], pixels[pixel_index(table_size, r1, g1, b1)], rt),
                gt);
            return Color.LerpUnclamped(low, high, bt);
        }
    }
}
