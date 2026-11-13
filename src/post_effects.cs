using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace screen_filter
{
    // Adjusts the effects in the game's post processing profile. The game ships with its own bloom and vignette, so
    // those sliders are relative to the game's values. With every slider at its default, the original values are back
    // in place and the image is exactly the game's.
    internal static class post_effects
    {
        private const float default_vignette_intensity = 0f;
        private const float default_vignette_smoothness = 0.2f;
        private const float default_bloom_intensity = 0f;
        private const float default_bloom_threshold = 1f;
        private const float minimum_vignette_smoothness = 0.01f;

        private sealed class saved_parameter<T>
        {
            private readonly ParameterOverride<T> parameter;
            private readonly T value;
            private readonly bool override_state;

            public saved_parameter(ParameterOverride<T> parameter)
            {
                this.parameter = parameter;
                value = parameter.value;
                override_state = parameter.overrideState;
            }

            // A parameter that isn't overridden renders with the effect's default, whatever its value says.
            public T effective(T default_value)
            {
                return override_state ? value : default_value;
            }

            public void restore()
            {
                parameter.value = value;
                parameter.overrideState = override_state;
            }
        }

        private static PostProcessProfile profile;

        private static Bloom bloom;
        private static saved_parameter<float> bloom_intensity;
        private static saved_parameter<float> bloom_threshold;

        private static Vignette vignette;
        private static saved_parameter<bool> vignette_enabled;
        private static saved_parameter<float> vignette_intensity;
        private static saved_parameter<float> vignette_smoothness;

        private static Grain grain;
        private static saved_parameter<bool> grain_enabled;
        private static saved_parameter<float> grain_intensity;
        private static saved_parameter<float> grain_size;

        private static ChromaticAberration aberration;
        private static saved_parameter<bool> aberration_enabled;
        private static saved_parameter<float> aberration_intensity;

        // Does nothing outside a park. The values get applied when the next park starts.
        public static void apply(filter_settings settings)
        {
            if (!settings.use_effects)
            {
                restore();
                return;
            }

            if (!capture_profile())
            {
                return;
            }

            if (bloom != null)
            {
                set_or_restore(bloom.intensity, bloom_intensity, settings.bloom_strength != 1f, bloom_intensity.effective(default_bloom_intensity) * settings.bloom_strength);
                set_or_restore(bloom.threshold, bloom_threshold, settings.bloom_threshold != 0f, bloom_threshold.effective(default_bloom_threshold) + settings.bloom_threshold);
            }

            bool use_vignette = settings.vignette != 0f || settings.vignette_softness != 0f;
            set_or_restore(vignette.enabled, vignette_enabled, use_vignette, true);
            set_or_restore(vignette.intensity, vignette_intensity, use_vignette, Mathf.Clamp01(vignette_intensity.effective(default_vignette_intensity) + settings.vignette));
            set_or_restore(vignette.smoothness, vignette_smoothness, use_vignette, Mathf.Clamp(vignette_smoothness.effective(default_vignette_smoothness) + settings.vignette_softness, minimum_vignette_smoothness, 1f));

            bool use_grain = settings.grain > 0f;
            set_or_restore(grain.enabled, grain_enabled, use_grain, true);
            set_or_restore(grain.intensity, grain_intensity, use_grain, settings.grain);
            set_or_restore(grain.size, grain_size, use_grain, settings.grain_size);

            bool use_aberration = settings.chromatic_aberration > 0f;
            set_or_restore(aberration.enabled, aberration_enabled, use_aberration, true);
            set_or_restore(aberration.intensity, aberration_intensity, use_aberration, settings.chromatic_aberration);
        }

        public static void restore()
        {
            // The game destroys the profile along with the park, and then there's nothing left to put back.
            if (profile != null)
            {
                if (bloom != null)
                {
                    bloom_intensity.restore();
                    bloom_threshold.restore();
                }

                vignette_enabled.restore();
                vignette_intensity.restore();
                vignette_smoothness.restore();
                grain_enabled.restore();
                grain_intensity.restore();
                grain_size.restore();
                aberration_enabled.restore();
                aberration_intensity.restore();

                // Effects the mod added stay in the profile, switched off by the restore above. Taken out of it, the
                // camera would go on drawing them with their last values: the game's post processing only updates
                // an effect while some profile holds it. The profile goes away with the park.
            }
            profile = null;
        }

        // Each park has its own copy of the profile, so the original values are read again when it changes.
        private static bool capture_profile()
        {
            PostProcessingController controller = PostProcessingController.Instance;
            PostProcessVolume volume = controller != null ? controller.GetComponent<PostProcessVolume>() : null;
            if (volume == null)
            {
                return false;
            }

            PostProcessProfile current = volume.profile;
            if (current == profile)
            {
                return true;
            }
            profile = current;

            if (profile.TryGetSettings(out bloom))
            {
                bloom_intensity = new saved_parameter<float>(bloom.intensity);
                bloom_threshold = new saved_parameter<float>(bloom.threshold);
            }

            vignette = find_or_add<Vignette>();
            vignette_enabled = new saved_parameter<bool>(vignette.enabled);
            vignette_intensity = new saved_parameter<float>(vignette.intensity);
            vignette_smoothness = new saved_parameter<float>(vignette.smoothness);

            grain = find_or_add<Grain>();
            grain_enabled = new saved_parameter<bool>(grain.enabled);
            grain_intensity = new saved_parameter<float>(grain.intensity);
            grain_size = new saved_parameter<float>(grain.size);

            aberration = find_or_add<ChromaticAberration>();
            aberration_enabled = new saved_parameter<bool>(aberration.enabled);
            aberration_intensity = new saved_parameter<float>(aberration.intensity);
            return true;
        }

        private static T find_or_add<T>() where T : PostProcessEffectSettings
        {
            if (!profile.TryGetSettings(out T effect))
            {
                effect = profile.AddSettings<T>();
                // It stays off until a slider asks for it.
                effect.enabled.Override(false);
            }
            return effect;
        }

        private static void set_or_restore<T>(ParameterOverride<T> parameter, saved_parameter<T> saved, bool use, T value)
        {
            if (use)
            {
                parameter.Override(value);
            }
            else
            {
                saved.restore();
            }
        }
    }
}
