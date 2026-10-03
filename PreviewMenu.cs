using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace PracticeMode;

internal static class PreviewMenu
{
    private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const string Namespace = "NLEFMLCDFFEMGDOKIGDNOCDMEOMFOPLNJOOD.";
    private const float RowSpacing = 38f;
    private const float RowTop = -190f; // Original audio offset row is at -152.
    private static FieldInfo _panelField, _fontField, _materialField, _hoverHeight;
    private static MethodInfo _rowLabel, _chip, _createButton, _buttonText, _buttonNavigation, _fill;
    private static object _onKind, _offKind;
    private static RectTransform _panel;
    private static Button _toggle;
    private static Text _caption, _shortcut;
    private static GameObject _labelObject;
    private static float _originalHeight, _originalHoverHeight;

    internal static void Bind(Assembly core, HarmonyLib.Harmony harmony)
    {
        Type controls = core.GetType(Namespace + "BDAIIJKHAKOCKBINBCKCDPMMAKFOFPPJJCLO", true);
        Type overlay = core.GetType(Namespace + "BFNEPGDHFAKBJIMAPJEACHPCFKJDEMMMKAAE", true);
        Type theme = core.GetType("HFDLDOLAMACBIIHMBFPHDFHDMGHEHHDPCMJP.DDJNACMJFNFCMLPFDKGBMCDFGLPKGHHMPONG", true);
        _panelField = Field(controls, "PGAHJJHHNNMEAEGIAKNNCBMGALIKAEIBLDBC");
        _fontField = Field(controls, "KBLIEBJLNJKCOHEKAGODMKHELFOEIBNIHJPJ");
        _materialField = Field(controls, "GOILAIMHHBHGMNPMMJJCFMANHFDNPMOFAMKD");
        _hoverHeight = Field(overlay, "BKAFCBMFOJCOHGBGOHGOHLADJLPPOMIIKDMI");
        _rowLabel = Method(controls, "JNHBPPPONAOMMFEGNBJBPMCCIFAKCCEMCAAE", typeof(string), typeof(float), typeof(string));
        _chip = Method(controls, "Chip", typeof(string), typeof(float));
        _createButton = theme.GetMethods(Static).Single(m => m.Name == "Button" && m.GetParameters().Length == 10);
        Type kind = _createButton.GetParameters()[4].ParameterType;
        _onKind = Enum.ToObject(kind, 4);
        _offKind = Enum.ToObject(kind, 3);
        _buttonText = Method(theme, "CLMDGHOOHOEHFEDKEIPOGABBOCOHCBDBBADL", typeof(Button));
        _buttonNavigation = Method(theme, "KPDNBMAMDPCINAMIJDFFIFGOPALMPGLONLMH", typeof(Button));
        _fill = Method(theme, "Fill", kind);
        harmony.Patch(Method(controls, "JCFAJJMNKGGKKMLILDCNPBINMLKDBBHFBMDM"),
            postfix: new HarmonyMethod(typeof(PreviewMenu), nameof(EnsureRow)));
        harmony.Patch(Method(controls, "DEHHAGNHLOKIIINIGOAGHNIBLGHHNDGHKAJK",
            typeof(bool), typeof(float), typeof(bool), typeof(bool), typeof(float), typeof(float)),
            postfix: new HarmonyMethod(typeof(PreviewMenu), nameof(Refresh)));
    }

    private static FieldInfo Field(Type type, string name)
        => type.GetField(name, Static) ?? throw new MissingFieldException(type.FullName, name);
    private static MethodInfo Method(Type type, string name, params Type[] args)
        => type.GetMethod(name, Static, null, args, null) ?? throw new MissingMethodException(type.FullName, name);

    private static void EnsureRow()
    {
        var panel = (RectTransform)_panelField.GetValue(null);
        if (panel == null || (_panel != null && panel.Pointer == _panel.Pointer && _toggle != null)) return;
        _panel = panel;
        _originalHeight = panel.sizeDelta.y;
        _originalHoverHeight = (float)_hoverHeight.GetValue(null);
        panel.sizeDelta = new Vector2(panel.sizeDelta.x, _originalHeight + RowSpacing);
        _hoverHeight.SetValue(null, _originalHoverHeight + RowSpacing);

        _rowLabel.Invoke(null, new object[] { "PreviewAutoPlayLabel", RowTop, "自动游玩" });
        _labelObject = panel.transform.Find("PreviewAutoPlayLabel")?.gameObject;
        _toggle = (Button)_createButton.Invoke(null, new object[] {
            panel.transform, _fontField.GetValue(null), _materialField.GetValue(null), "",
            _onKind, new Vector2(-60f, RowTop), new Vector2(222f, 32f), Vector2.one,
            (Action)(() => Main.Instance?.Toggle()), 15
        });
        _toggle.gameObject.name = "PracticeMode";
        _buttonNavigation.Invoke(null, new object[] { _toggle });
        _caption = (Text)_buttonText.Invoke(null, new object[] { _toggle });
        _shortcut = (Text)_chip.Invoke(null, new object[] { "PreviewAutoPlayShortcut", RowTop });
        _shortcut.text = SettingManager.ToggleKey.ToString();
        Refresh();
    }

    internal static void Refresh()
    {
        if (_toggle == null) return;
        _toggle.interactable = PreviewBridge.Active && !PreviewBridge.Seeking;
        string caption = SettingManager.AutoPlay ? "开启" : "关闭";
        if (_caption != null && _caption.text != caption) _caption.text = caption;
        var image = _toggle.GetComponent<Image>();
        if (image != null) image.color = (Color)_fill.Invoke(null, new[] { SettingManager.AutoPlay ? _onKind : _offKind });
    }

    internal static void Shutdown()
    {
        if (_panel != null)
        {
            _panel.sizeDelta = new Vector2(_panel.sizeDelta.x, _originalHeight);
            _hoverHeight.SetValue(null, _originalHoverHeight);
        }
        if (_toggle != null) UnityEngine.Object.Destroy(_toggle.gameObject);
        if (_shortcut != null) UnityEngine.Object.Destroy(_shortcut.gameObject);
        if (_labelObject != null) UnityEngine.Object.Destroy(_labelObject);
        _panel = null;
        _toggle = null;
        _caption = _shortcut = null;
        _labelObject = null;
    }
}
