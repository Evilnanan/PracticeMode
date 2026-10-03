using System.Reflection;
using Il2Cpp;
using Il2CppDG.Tweening;
using Il2CppGameLogic;
using UnityEngine;

namespace PracticeMode;

internal static class PreviewLongVisuals
{
    private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private static readonly Dictionary<nint, Fade> Fades = new();
    private static Func<int, float> _endTime;
    private static Fade _capturing;

    private sealed class Fade
    {
        internal SpineActionController Controller;
        internal int Index;
        internal float End, Alpha;
        internal Color StartColor, EndColor;
        internal readonly List<Tween> Tweens = new();
    }

    internal static void Bind(Assembly core, HarmonyLib.Harmony harmony)
    {
        var cache = core.GetType("NLEFMLCDFFEMGDOKIGDNOCDMEOMFOPLNJOOD.DEJLCLIPJNOHOAOEJBGHDPNEOKGIMNPKEOLE", true);
        _endTime = cache.GetMethod("BGJGFMOBNLGHFNLILDFLLFBDHPOFDJNHCJOI", Static, null, new[] { typeof(int) }, null)
            .CreateDelegate<Func<int, float>>();
        harmony.Patch(AccessTools.DeclaredMethod(typeof(SpineActionController), "SetAlpha", new[] { typeof(float) }),
            prefix: new HarmonyMethod(typeof(PreviewLongVisuals), nameof(BeforeFade)),
            finalizer: new HarmonyMethod(typeof(PreviewLongVisuals), nameof(AfterFade)));

        // Native SetAlpha creates three untagged tweens. Capture their exact handles
        // during that call so a rollback can cancel only this note's old fade.
        foreach (var method in AccessTools.GetDeclaredMethods(typeof(DOTween)))
        {
            var parameters = method.GetParameters();
            if (method.Name == "To" && parameters.Length == 4 &&
                (parameters[2].ParameterType == typeof(float) || parameters[2].ParameterType == typeof(Color)))
                harmony.Patch(method, postfix: new HarmonyMethod(typeof(PreviewLongVisuals), nameof(CaptureTween)));
        }
    }

    private static void BeforeFade(SpineActionController __instance, out Fade __state)
    {
        __state = _capturing;
        _capturing = null;
        if (!PreviewBridge.Active || __instance.m_HasAlpha || __instance.m_Mtrl == null) return;
        var controller = __instance.objController;
        if (controller == null || controller.TryCast<LongPressController>() == null) return;
        int index = __instance.m_Idx;
        float end = _endTime(index);
        if (end < 0f) return;
        if (!Fades.TryGetValue(__instance.Pointer, out var fade) || fade.Index != index)
        {
            fade = new Fade
            {
                Controller = __instance, Index = index, End = end,
                Alpha = __instance._SetAlpha_b__72_0(),
                StartColor = __instance.m_StartStar != null ? __instance.m_StartStar.color : Color.white,
                EndColor = __instance.m_EndStar != null ? __instance.m_EndStar.color : Color.white
            };
            Fades[__instance.Pointer] = fade;
        }
        _capturing = fade;
    }

    private static void AfterFade(Fade __state) => _capturing = __state;
    private static void CaptureTween(Tween __result)
    {
        if (_capturing != null && __result != null) _capturing.Tweens.Add(__result);
    }

    internal static void AfterSeek(float time)
    {
        var scene = GameGlobal.gGameMusicScene;
        var controllers = scene?.spineActionCtrls;
        if (controllers == null) return;
        foreach (var fade in Fades.Values)
        {
            if (fade.End <= time || fade.Index < 0 || fade.Index >= controllers.Length) continue;
            var current = controllers[fade.Index];
            if (current == null || current.Pointer != fade.Controller.Pointer || current.m_Idx != fade.Index) continue;
            foreach (var tween in fade.Tweens)
            {
                TweenExtensions.Kill(tween, false);
            }
            fade.Tweens.Clear();
            if (current.m_Mtrl != null) current._SetAlpha_b__72_1(fade.Alpha);
            if (current.m_StartStar != null) current.m_StartStar.color = fade.StartColor;
            if (current.m_EndStar != null) current.m_EndStar.color = fade.EndColor;
            current.m_HasAlpha = false;
            var note = scene.objCtrls?[fade.Index]?.TryCast<LongPressController>();
            if (note != null)
            {
                note.m_IsHurt = false;
                var children = note.m_LongChild;
                if (children != null)
                    for (int i = 0; i < children.Count; i++)
                        if (children[i] != null) children[i].m_IsHurt = false;
            }
        }
    }

    internal static void Clear()
    {
        Fades.Clear();
        _capturing = null;
    }
}
