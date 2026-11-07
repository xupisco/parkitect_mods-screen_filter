using System.Collections.Generic;
using UnityEngine;

namespace screen_filter
{
    // Adds a "Custom" entry to the game's color correction filters, with a table built from the sliders in the mod's
    // settings, and adjusts the game's post processing effects. Everything goes through public API, so there are no
    // patches. Rendering is local to each player.
    public class screen_filter_mod : AbstractMod, IModSettings
    {
        private const string identifier = "xupisco@ScreenFilter";
        private const string filter_identifier = identifier + "/custom";
        internal const string window_hotkey_id = identifier + "/window";
        // Rebuilding the table on every mouse move would be wasteful while dragging a slider.
        private const float seconds_between_rebuilds = 0.1f;
        private const float row_height = 26f;
        internal const string effects_header = "Effects (these work without the filter)";
        internal const string use_effects_label = "Use these effects";
        // The game's vignette has an intensity and a smoothness of 0.2, so the lowest values remove it.
        internal const float minimum_vignette = -0.2f;
        internal const float maximum_vignette = 0.6f;
        internal const float minimum_vignette_softness = -0.2f;
        internal const float maximum_vignette_softness = 0.8f;

        private filter_settings settings;
        private filter_settings applied;
        private filter_settings applied_effects;
        private ColorCorrectionFilter filter;
        private float next_rebuild_time;
        private GUIStyle label_style;
        private KeyGroup key_group;
        private KeyMapping key_mapping;
        private GameObject window_object;

        public override string getName()
        {
            return "Screen Filter";
        }

        public override string getDescription()
        {
            return "Adds a \"Custom\" color filter to the graphics options, with adjustable brightness, contrast, saturation, gamma, temperature and tint, plus vignette, film grain, chromatic aberration and bloom controls.";
        }

        public override string getVersionNumber()
        {
            return "0.4.0";
        }

        public override string getIdentifier()
        {
            return identifier;
        }

        public override bool isMultiplayerModeCompatible()
        {
            return true;
        }

        public override bool isRequiredByAllPlayersInMultiplayerMode()
        {
            return false;
        }

        public override void onEnabled()
        {
            settings = filter_settings.load();
            filter = new ColorCorrectionFilter { name = "Custom", identifier = filter_identifier };
            ScriptableSingleton<AssetManager>.Instance.registerColorCorrectionFilter(filter);
            rebuild();
            apply_effects();
            EventManager.Instance.OnStartPlayingPark += on_start_playing_park;

            register_hotkey();
            window_object = new GameObject("screen_filter");
            window_object.AddComponent<filter_window>().mod = this;
        }

        // The group is registered explicitly, because registering one that already exists makes the game throw, which
        // the implicit per-mod group would hit when the mod is enabled again.
        private void register_hotkey()
        {
            InputManager input = ScriptableSingleton<InputManager>.Instance;

            key_group = new KeyGroup(identifier);
            key_group.keyGroupName = getName();
            input.registerKeyGroup(key_group);

            key_mapping = new KeyMapping(window_hotkey_id, KeyCode.J, KeyCode.None);
            key_mapping.keyName = "Open Screen Filter";
            key_mapping.keyDescription = "Open the window with the screen filter's settings.";
            key_mapping.keyGroupIdentifier = identifier;
            input.registerKeyMapping(key_mapping);
        }

        public override void onDisabled()
        {
            if (window_object != null)
            {
                Object.Destroy(window_object);
            }
            InputManager input = ScriptableSingleton<InputManager>.Instance;
            input.unregisterKeyMapping(key_mapping);
            input.unregisterKeyGroup(key_group);
            settings.save();

            if (EventManager.Exists)
            {
                EventManager.Instance.OnStartPlayingPark -= on_start_playing_park;
            }
            ScriptableSingleton<AssetManager>.Instance.unregisterColorCorrectionFilter(filter);
            // The saved choice stays, so it comes back when the mod does. Until then the game uses its first filter.
            reapply();
            post_effects.restore();
            Object.Destroy(filter.lut);
            filter = null;
        }

        private static bool is_selected => Settings.Instance.graphicsColorCorrectionFilterIdentifier == filter_identifier;

        // The game applied its filter before this one was registered, and a new camera starts without the table.
        private void reapply()
        {
            if (is_selected)
            {
                Settings.Instance.changeGraphicsColorCorrectionFilter(filter_identifier);
            }
        }

        private void on_start_playing_park()
        {
            reapply();
            apply_effects();
        }

        private void apply_effects()
        {
            applied_effects = settings.copy();
            post_effects.apply(settings);
        }

        private void rebuild()
        {
            applied = settings.copy();
            next_rebuild_time = Time.unscaledTime + seconds_between_rebuilds;
            if (filter == null)
            {
                return;
            }

            // A new texture each time: Lutify only reloads the table when the texture's instance changes.
            Texture2D previous = filter.lut;
            filter.lut = lut_builder.build(settings, find_base_filter()?.lut);
            reapply();
            if (previous != null)
            {
                Object.Destroy(previous);
            }
        }

        internal List<ColorCorrectionFilter> get_base_filters()
        {
            List<ColorCorrectionFilter> filters = new List<ColorCorrectionFilter>();
            foreach (ColorCorrectionFilter item in ScriptableSingleton<AssetManager>.Instance.getColorCorrectionFilters())
            {
                if (item.identifier != filter_identifier)
                {
                    filters.Add(item);
                }
            }
            return filters;
        }

        private ColorCorrectionFilter find_base_filter()
        {
            foreach (ColorCorrectionFilter item in get_base_filters())
            {
                if (item.identifier == settings.base_filter)
                {
                    return item;
                }
            }
            return null;
        }

        public void onSettingsOpened()
        {
            if (settings == null)
            {
                settings = filter_settings.load();
                applied = settings.copy();
            }
        }

        public void onSettingsClosed()
        {
            on_window_closed();
        }

        internal void on_window_closed()
        {
            if (!settings.same_table_as(applied))
            {
                rebuild();
            }
            settings.save();
        }

        public void onDrawSettingsUI()
        {
            draw_controls();
        }

        // Shared by the mod's settings screen and the in-game window.
        internal void draw_controls()
        {
            if (label_style == null)
            {
                label_style = new GUIStyle(GUI.skin.label);
                label_style.fontSize = GUI.skin.toggle.fontSize;
                // The game's skin gives labels and sliders different margins, which puts them on different rows.
                label_style.margin = new RectOffset();
                label_style.padding = new RectOffset();
                label_style.alignment = TextAnchor.MiddleLeft;
                label_style.fixedHeight = row_height;
            }

            GUILayout.BeginVertical();
            if (filter != null)
            {
                use_filter = GUILayout.Toggle(use_filter, " Use this filter");
            }

            settings.brightness = draw_slider("Brightness", settings.brightness, -1f, 1f);
            settings.contrast = draw_slider("Contrast", settings.contrast, -1f, 1f);
            settings.saturation = draw_slider("Saturation", settings.saturation, 0f, 2f);
            settings.gamma = draw_slider("Gamma", settings.gamma, 0.5f, 2f);
            settings.temperature = draw_slider("Temperature", settings.temperature, -1f, 1f);
            settings.tint = draw_slider("Tint", settings.tint, -1f, 1f);
            draw_base_filter();

            GUILayout.Label(effects_header, label_style);
            settings.use_effects = GUILayout.Toggle(settings.use_effects, " " + use_effects_label);
            settings.vignette = draw_slider("Vignette", settings.vignette, minimum_vignette, maximum_vignette);
            settings.vignette_softness = draw_slider("Vignette softness", settings.vignette_softness, minimum_vignette_softness, maximum_vignette_softness);
            settings.grain = draw_slider("Grain", settings.grain, 0f, 1f);
            settings.grain_size = draw_slider("Grain size", settings.grain_size, 0.3f, 3f);
            settings.chromatic_aberration = draw_slider("Chromatic aberration", settings.chromatic_aberration, 0f, 0.5f);
            settings.bloom_strength = draw_slider("Bloom strength", settings.bloom_strength, 0f, 3f);
            settings.bloom_threshold = draw_slider("Bloom threshold", settings.bloom_threshold, -0.5f, 0.5f);

            if (GUILayout.Button("Reset"))
            {
                reset_settings();
            }
            GUILayout.EndVertical();
        }

        internal filter_settings current_settings => settings;

        internal bool use_filter
        {
            get => is_selected;
            set
            {
                // Turning it off goes back to the base filter, the closest thing to what's being adjusted here.
                if (value != is_selected)
                {
                    Settings.Instance.changeGraphicsColorCorrectionFilter(value ? filter_identifier : settings.base_filter);
                }
            }
        }

        internal void rebuild_if_changed()
        {
            if (Time.unscaledTime >= next_rebuild_time && !settings.same_table_as(applied))
            {
                rebuild();
            }
            if (!settings.same_effects_as(applied_effects))
            {
                apply_effects();
            }
        }

        // The base filter and the effects toggle aren't adjustments, so they stay.
        internal void reset_settings()
        {
            settings = new filter_settings { base_filter = settings.base_filter, use_effects = settings.use_effects };
        }

        private float draw_slider(string label, float value, float minimum, float maximum)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + ": " + value.ToString("0.00"), label_style, GUILayout.Width(180f));
            GUILayout.BeginVertical(GUILayout.Height(row_height));
            GUILayout.FlexibleSpace();
            float result = GUILayout.HorizontalSlider(value, minimum, maximum);
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            return result;
        }

        // The first filter stands for no base at all when the game's first filter has no table.
        private void draw_base_filter()
        {
            List<ColorCorrectionFilter> filters = get_base_filters();
            if (filters.Count == 0)
            {
                return;
            }

            int index = Mathf.Max(0, filters.FindIndex(item => item.identifier == settings.base_filter));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Base filter: " + filters[index].name, label_style, GUILayout.Width(180f));
            if (GUILayout.Button("<", GUILayout.Height(row_height)))
            {
                index = (index + filters.Count - 1) % filters.Count;
            }
            if (GUILayout.Button(">", GUILayout.Height(row_height)))
            {
                index = (index + 1) % filters.Count;
            }
            GUILayout.EndHorizontal();
            settings.base_filter = filters[index].identifier;
        }
    }
}
