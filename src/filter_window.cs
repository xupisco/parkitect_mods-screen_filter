using System;
using System.Collections.Generic;
using System.Reflection;
using Parkitect.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace screen_filter
{
    // The game's windows need a UIWindow as their content, and the class itself has nothing to add.
    internal sealed class filter_window_content : UIWindow
    {
    }

    // Reads the hotkey and builds the sliders as a native game window over the park, from the same parts as a ride's
    // settings, so that the effect can be seen while adjusting it.
    internal sealed class filter_window : MonoBehaviour
    {
        private const string window_tag = "screen_filter";
        // Also the name the game remembers the window's position under.
        private const string window_name = "ScreenFilterWindow";
        private const float window_width = 420f;
        private const float button_height = 30f;

        private const BindingFlags private_field = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo slider_field = typeof(AttractionSettingNumberSliderUI).GetField("numberSlider", private_field);
        private static readonly FieldInfo number_text_field = typeof(AttractionSettingNumberSliderUI).GetField("numberText", private_field);
        private static readonly FieldInfo toggle_field = typeof(AttractionSettingToggleUI).GetField("toggle", private_field);
        private static readonly FieldInfo dropdown_field = typeof(AttractionSettingDropdownUI).GetField("dropdown", private_field);

        private sealed class slider_row
        {
            public Slider slider;
            public TextMeshProUGUI number;
            public Func<filter_settings, float> read;
        }

        public screen_filter_mod mod;

        private UIWindowFrame frame;
        private GameObject parts_holder;
        private readonly List<slider_row> slider_rows = new List<slider_row>();
        private Toggle use_filter_toggle;
        private Toggle use_effects_toggle;
        private TMP_Dropdown base_filter_dropdown;

        private void Update()
        {
            mod.rebuild_if_changed();

            if (InputManager.getKeyDown(screen_filter_mod.window_hotkey_id) && !UIUtility.isInputFieldFocused())
            {
                if (frame != null)
                {
                    frame.close();
                }
                else
                {
                    open();
                }
            }

            // The filter can also be changed in the graphics options while this window is open.
            if (frame != null)
            {
                use_filter_toggle.SetIsOnWithoutNotify(mod.use_filter);
            }
        }

        private void open()
        {
            // Parts are prepared in here, because a game object that was never active doesn't run OnDestroy.
            parts_holder = new GameObject("screen_filter_parts");
            parts_holder.SetActive(false);
            parts_holder.transform.SetParent(transform, false);

            GameObject content_object = new GameObject(window_name, typeof(RectTransform));
            VerticalLayoutGroup layout = content_object.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            UIWindowSettings window_settings = content_object.AddComponent<UIWindowSettings>();
            window_settings.title = mod.getName();
            window_settings.resizeability = 0;
            window_settings.pinnable = false;
            window_settings.contentDeterminesWindowSize = true;

            add_width_spacer(content_object.transform);
            add_use_filter_row(content_object.transform);
            add_slider_row(content_object.transform, "Brightness", -1f, 1f, settings => settings.brightness, (settings, value) => settings.brightness = value);
            add_slider_row(content_object.transform, "Contrast", -1f, 1f, settings => settings.contrast, (settings, value) => settings.contrast = value);
            add_slider_row(content_object.transform, "Saturation", 0f, 2f, settings => settings.saturation, (settings, value) => settings.saturation = value);
            add_slider_row(content_object.transform, "Gamma", 0.5f, 2f, settings => settings.gamma, (settings, value) => settings.gamma = value);
            add_slider_row(content_object.transform, "Temperature", -1f, 1f, settings => settings.temperature, (settings, value) => settings.temperature = value);
            add_slider_row(content_object.transform, "Tint", -1f, 1f, settings => settings.tint, (settings, value) => settings.tint = value);
            add_base_filter_row(content_object.transform);
            add_header(content_object.transform, screen_filter_mod.effects_header);
            add_use_effects_row(content_object.transform);
            add_slider_row(content_object.transform, "Vignette", screen_filter_mod.minimum_vignette, screen_filter_mod.maximum_vignette, settings => settings.vignette, (settings, value) => settings.vignette = value);
            add_slider_row(content_object.transform, "Vignette softness", screen_filter_mod.minimum_vignette_softness, screen_filter_mod.maximum_vignette_softness, settings => settings.vignette_softness, (settings, value) => settings.vignette_softness = value);
            add_slider_row(content_object.transform, "Grain", 0f, 1f, settings => settings.grain, (settings, value) => settings.grain = value);
            add_slider_row(content_object.transform, "Grain size", 0.3f, 3f, settings => settings.grain_size, (settings, value) => settings.grain_size = value);
            add_slider_row(content_object.transform, "Chromatic aberration", 0f, 0.5f, settings => settings.chromatic_aberration, (settings, value) => settings.chromatic_aberration = value);
            add_slider_row(content_object.transform, "Bloom strength", 0f, 3f, settings => settings.bloom_strength, (settings, value) => settings.bloom_strength = value);
            add_slider_row(content_object.transform, "Bloom threshold", -0.5f, 0.5f, settings => settings.bloom_threshold, (settings, value) => settings.bloom_threshold = value);
            add_reset_button(content_object.transform);
            refresh();

            filter_window_content content = content_object.AddComponent<filter_window_content>();
            frame = UIWindowsController.Instance.spawnWindow(content, window_tag);
            frame.OnClose += on_frame_closed;

            Destroy(parts_holder);
        }

        private void on_frame_closed(UIWindowFrame closed_frame)
        {
            frame = null;
            slider_rows.Clear();
            mod.on_window_closed();
        }

        private static void add_width_spacer(Transform parent)
        {
            GameObject spacer = new GameObject("width", typeof(RectTransform));
            spacer.transform.SetParent(parent, false);
            spacer.AddComponent<LayoutElement>().minWidth = window_width;
        }

        private void add_use_filter_row(Transform parent)
        {
            use_filter_toggle = add_toggle_row(parent, "Use this filter", value => mod.use_filter = value);
        }

        private void add_use_effects_row(Transform parent)
        {
            use_effects_toggle = add_toggle_row(parent, screen_filter_mod.use_effects_label, value => mod.current_settings.use_effects = value);
        }

        private Toggle add_toggle_row(Transform parent, string label, UnityEngine.Events.UnityAction<bool> on_changed)
        {
            AttractionSettingToggleUI row = Instantiate(ScriptableSingleton<UIAssetManager>.Instance.attractionSettingToggleUIGO, parts_holder.transform);
            row.setLabelText(label);
            Toggle toggle = (Toggle)toggle_field.GetValue(row);
            toggle.onValueChanged = new Toggle.ToggleEvent();
            toggle.onValueChanged.AddListener(on_changed);
            take_part(row, parent);
            return toggle;
        }

        private void add_slider_row(Transform parent, string label, float minimum, float maximum, Func<filter_settings, float> read, Action<filter_settings, float> write)
        {
            AttractionSettingNumberSliderUI row = Instantiate(ScriptableSingleton<UIAssetManager>.Instance.attractionSettingNumberSliderUIGO, parts_holder.transform);
            row.setLabelText(label);
            Slider slider = (Slider)slider_field.GetValue(row);
            TextMeshProUGUI number = (TextMeshProUGUI)number_text_field.GetValue(row);
            slider.onValueChanged = new Slider.SliderEvent();
            slider.wholeNumbers = false;
            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.onValueChanged.AddListener(value =>
            {
                write(mod.current_settings, value);
                number.text = format(value);
            });
            slider_rows.Add(new slider_row { slider = slider, number = number, read = read });
            take_part(row, parent);
        }

        // The game's parts have no plain label, so this borrows the number text from a slider row. The row itself stays
        // behind in the holder, which is destroyed without ever having been active.
        private void add_header(Transform parent, string title)
        {
            AttractionSettingNumberSliderUI row = Instantiate(ScriptableSingleton<UIAssetManager>.Instance.attractionSettingNumberSliderUIGO, parts_holder.transform);
            TextMeshProUGUI source = (TextMeshProUGUI)number_text_field.GetValue(row);
            TextMeshProUGUI header = Instantiate(source, parts_holder.transform);
            header.text = title;
            header.alignment = TextAlignmentOptions.Left;
            header.raycastTarget = false;
            LayoutElement layout = header.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = header.gameObject.AddComponent<LayoutElement>();
            }
            layout.minHeight = button_height;
            header.transform.SetParent(parent, false);
        }

        private void add_base_filter_row(Transform parent)
        {
            AttractionSettingDropdownUI row = Instantiate(ScriptableSingleton<UIAssetManager>.Instance.attractionSettingDropdownUIGO, parts_holder.transform);
            row.setLabelText("Base filter");
            base_filter_dropdown = (TMP_Dropdown)dropdown_field.GetValue(row);
            base_filter_dropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
            base_filter_dropdown.ClearOptions();
            List<ColorCorrectionFilter> filters = mod.get_base_filters();
            foreach (ColorCorrectionFilter filter in filters)
            {
                base_filter_dropdown.options.Add(new TMP_Dropdown.OptionData(I18N.GetString(filter.name)));
            }
            base_filter_dropdown.onValueChanged.AddListener(index => mod.current_settings.base_filter = filters[index].identifier);
            take_part(row, parent);
        }

        // There's no standalone button among the game's parts, so this borrows the one from its confirmation window.
        private void add_reset_button(Transform parent)
        {
            Button source = ScriptableSingleton<UIAssetManager>.Instance.confirmationWindowGO.GetComponentInChildren<Button>(true);
            Button button = Instantiate(source, parts_holder.transform);
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() =>
            {
                mod.reset_settings();
                refresh();
            });
            TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
            {
                text.text = "Reset";
            }
            LayoutElement layout = button.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = button.gameObject.AddComponent<LayoutElement>();
            }
            layout.minHeight = button_height;
            button.transform.SetParent(parent, false);
        }

        // The part's own script expects a ride's setting, and would throw without one both when used and when destroyed.
        // Removing it while it has never been active means its OnDestroy doesn't run.
        private static void take_part(Component row_script, Transform parent)
        {
            Transform row = row_script.transform;
            DestroyImmediate(row_script);
            row.SetParent(parent, false);
        }

        private void refresh()
        {
            filter_settings settings = mod.current_settings;
            foreach (slider_row row in slider_rows)
            {
                float value = row.read(settings);
                row.slider.SetValueWithoutNotify(value);
                row.number.text = format(value);
            }
            base_filter_dropdown.SetValueWithoutNotify(Mathf.Max(0, mod.get_base_filters().FindIndex(item => item.identifier == settings.base_filter)));
            base_filter_dropdown.RefreshShownValue();
            use_filter_toggle.SetIsOnWithoutNotify(mod.use_filter);
            use_effects_toggle.SetIsOnWithoutNotify(settings.use_effects);
        }

        private static string format(float value)
        {
            return value.ToString("0.00");
        }

        private void OnDestroy()
        {
            if (frame != null)
            {
                frame.OnClose -= on_frame_closed;
                frame.close();
            }
        }
    }
}
