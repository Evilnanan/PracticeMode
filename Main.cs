using UnityEngine;

namespace PracticeMode;

public sealed class Main : MelonMod
{
    private float _nextBindAttempt;
    internal static Main Instance { get; private set; }

    public override void OnInitializeMelon()
    {
        Instance = this;
        SettingManager.Register();
    }

    public override void OnUpdate()
    {
        if (!PreviewBridge.Ready && !PreviewBridge.Failed && Time.realtimeSinceStartup >= _nextBindAttempt)
        {
            _nextBindAttempt = Time.realtimeSinceStartup + 1f;
            PreviewBridge.TryBind(HarmonyInstance);
        }
        if (!PreviewBridge.Ready) return;
        PreviewBridge.Refresh();
        if (PreviewBridge.Active && Input.GetKeyDown(SettingManager.ToggleKey)) Toggle();
        PreviewBridge.Apply();
    }

    internal void Toggle()
    {
        if (!PreviewBridge.Active || PreviewBridge.Seeking) return;
        PreviewBridge.ClearPresses();
        SettingManager.Toggle();
        PreviewBridge.Apply();
        PreviewMenu.Refresh();
    }

    public override void OnDeinitializeMelon()
    {
        PreviewBridge.Shutdown(HarmonyInstance);
        PreviewMenu.Shutdown();
        Instance = null;
    }
}
