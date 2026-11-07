using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MiniJSON;
using UnityEngine;

namespace screen_filter
{
    // Kept next to the game's settings rather than in the mod folder, which Workshop updates can replace.
    internal sealed class filter_settings
    {
        private static string file_path => FilePaths.getFolderPath("screen_filter.settings.json");

        public float brightness;
        public float contrast;
        public float saturation = 1f;
        public float gamma = 1f;
        public float temperature;
        public float tint;
        public string base_filter = "";

        public bool use_effects = true;
        // Relative to the game's own vignette and bloom, so the defaults leave them as they are.
        public float vignette;
        public float vignette_softness;
        public float grain;
        public float grain_size = 1f;
        public float chromatic_aberration;
        public float bloom_strength = 1f;
        public float bloom_threshold;

        public filter_settings copy()
        {
            return (filter_settings)MemberwiseClone();
        }

        // Only what goes into the color table, which is slow to rebuild.
        public bool same_table_as(filter_settings other)
        {
            return brightness == other.brightness
                && contrast == other.contrast
                && saturation == other.saturation
                && gamma == other.gamma
                && temperature == other.temperature
                && tint == other.tint
                && base_filter == other.base_filter;
        }

        public bool same_effects_as(filter_settings other)
        {
            return use_effects == other.use_effects
                && vignette == other.vignette
                && vignette_softness == other.vignette_softness
                && grain == other.grain
                && grain_size == other.grain_size
                && chromatic_aberration == other.chromatic_aberration
                && bloom_strength == other.bloom_strength
                && bloom_threshold == other.bloom_threshold;
        }

        public static filter_settings load()
        {
            filter_settings settings = new filter_settings();
            try
            {
                if (File.Exists(file_path) && Json.Deserialize(File.ReadAllText(file_path)) is Dictionary<string, object> values)
                {
                    settings.brightness = read_float(values, "brightness", settings.brightness);
                    settings.contrast = read_float(values, "contrast", settings.contrast);
                    settings.saturation = read_float(values, "saturation", settings.saturation);
                    settings.gamma = read_float(values, "gamma", settings.gamma);
                    settings.temperature = read_float(values, "temperature", settings.temperature);
                    settings.tint = read_float(values, "tint", settings.tint);
                    if (values.TryGetValue("use_effects", out object use_effects) && use_effects is bool use_effects_value)
                    {
                        settings.use_effects = use_effects_value;
                    }
                    settings.vignette = read_float(values, "vignette", settings.vignette);
                    settings.vignette_softness = read_float(values, "vignette_softness", settings.vignette_softness);
                    settings.grain = read_float(values, "grain", settings.grain);
                    settings.grain_size = read_float(values, "grain_size", settings.grain_size);
                    settings.chromatic_aberration = read_float(values, "chromatic_aberration", settings.chromatic_aberration);
                    settings.bloom_strength = read_float(values, "bloom_strength", settings.bloom_strength);
                    settings.bloom_threshold = read_float(values, "bloom_threshold", settings.bloom_threshold);
                    if (values.TryGetValue("base_filter", out object base_filter) && base_filter is string base_filter_name)
                    {
                        settings.base_filter = base_filter_name;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("[screen_filter] Couldn't read " + file_path + ": " + exception);
            }
            return settings;
        }

        public void save()
        {
            try
            {
                Dictionary<string, object> values = new Dictionary<string, object>
                {
                    { "brightness", brightness },
                    { "contrast", contrast },
                    { "saturation", saturation },
                    { "gamma", gamma },
                    { "temperature", temperature },
                    { "tint", tint },
                    { "use_effects", use_effects },
                    { "vignette", vignette },
                    { "vignette_softness", vignette_softness },
                    { "grain", grain },
                    { "grain_size", grain_size },
                    { "chromatic_aberration", chromatic_aberration },
                    { "bloom_strength", bloom_strength },
                    { "bloom_threshold", bloom_threshold },
                    { "base_filter", base_filter }
                };
                File.WriteAllText(file_path, Json.Serialize(values));
            }
            catch (Exception exception)
            {
                Debug.LogError("[screen_filter] Couldn't write " + file_path + ": " + exception);
            }
        }

        private static float read_float(Dictionary<string, object> values, string key, float fallback)
        {
            return values.TryGetValue(key, out object value) ? Convert.ToSingle(value, CultureInfo.InvariantCulture) : fallback;
        }
    }
}
