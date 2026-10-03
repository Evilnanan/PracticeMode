using System.Reflection;
using Il2Cpp;
using Il2CppAssets.Scripts.Database;
using Il2CppAssets.Scripts.GameCore.CharacterMovement;
using Il2CppAssets.Scripts.GameCore.HostComponent;
using Il2CppAssets.Scripts.GameCore.GameObjectLogics.GameObjectManager;
using Il2CppAssets.Scripts.GameCore.Managers;
using Il2CppFormulaBase;
using UnityEngine;

namespace PracticeMode;

internal static class PreviewBridge
{
    // These names are private implementation details of the locally inspected 0.10.7 build.
    // A different build must be re-inspected; silently guessing obfuscated members is unsafe.
    private static readonly Guid SupportedCore = new("e512ef15-5b5c-45ef-889c-3e6e667b6660");
    private const string Prefix = "NLEFMLCDFFEMGDOKIGDNOCDMEOMFOPLNJOOD.";
    private const string RuntimeName = Prefix + "NOCBOJEDNHBHDJMEKABFAPIIKMBEGDMGMHMI";
    private const string StateName = Prefix + "HMCFGKGBNHGKANEMBJNMHAJBNCNPEMCJEJFC";
    private const string PlaybackName = Prefix + "PONHPBKDMNDNAIIGFFPMFHLGBKDGEHFDNEFN";
    private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    private static FieldInfo _prepared, _running, _seeking, _rebuilding, _clock, _clockAnchor;
    private static Func<float> _elapsed;
    private static Action _clearPresses;
    private static DBSkill _ownedSkill;
    private static GameObject _sleepIndicator;
    private static bool _originalSleepVisible;
    private static bool _originalGmAuto, _gameStarted;
    internal static bool Ready { get; private set; }
    internal static bool Failed { get; private set; }
    internal static bool Active { get; private set; }
    internal static bool Seeking { get; private set; }
    internal static bool Manual => Ready && Active && !SettingManager.AutoPlay;

    internal static void TryBind(HarmonyLib.Harmony harmony)
    {
        var core = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Euterpe.Core");
        if (core == null) return;
        try
        {
            if (core.ManifestModule.ModuleVersionId != SupportedCore)
                throw new NotSupportedException("This Euterpe.Core build is not supported; expected the inspected Euterpe 0.10.7 build.");
            Type state = core.GetType(StateName, true);
            Type runtime = core.GetType(RuntimeName, true);
            Type playback = core.GetType(PlaybackName, true);
            _prepared = RequireField(state, "MFJEOOKOCLGODCAIOCBCFMHCNGCHAIHCOOPA", typeof(bool));
            _running = RequireField(state, "PEEBBIINFPJGJLPDNDIEELEONKAPJNKMLMJJ", typeof(bool));
            _seeking = RequireField(state, "NCMJNAJDFHNJMOLDCHFEHADJOHCMLEAJNAIL", typeof(bool));
            _rebuilding = RequireField(state, "EAOGCJOINDDGLNPEBHHMKNJOAGPCAOPCNKPD", typeof(bool));
            _clock = RequireField(state, "EMMOKJIALCDEJMEOGHNAEBCAAAKCAIBHHENP", typeof(float));
            _clockAnchor = RequireField(runtime, "LIGBJEPFFBKPCKGJNLOCNCCGJADNEEFCEJAL", typeof(float));
            _elapsed = RequireMethod(state, "EOJEGBAPELKGNDEMFKDGECICDNOKKFOBNPDO").CreateDelegate<Func<float>>();
            _clearPresses = RequireMethod(playback, "PBBNHJCMLIFEAFGELLKKLCOAODLFKCBHBAHP").CreateDelegate<Action>();

            // Patch managed Euterpe methods rather than replacing its seek/clock machinery.
            Patch(harmony, RequireMethod(runtime, "Tick"), nameof(BeforeTick), nameof(AfterTick));
            Patch(harmony, RequireMethod(runtime, "OAHKOJAHGLGBCHGMAHDFNDMGAJLIJKGPIHPA", typeof(float)),
                postfix: nameof(AfterSeekAndAudioResume));
            Patch(harmony, RequireMethod(core.GetType("Euterpe.Preview.PreviewGameStartHook", true), "Postfix"),
                postfix: nameof(AfterGameStart));
            Patch(harmony, RequireMethod(runtime, "MHPKHEHFBCDAMIDLIDPACKJGLDFIEFJBKHJF"), nameof(BeforeExit), nameof(AfterExit));
            Patch(harmony, RequireMethod(runtime, "NPCNCJODCDHFEBDJBKEHICCNNBKMFOPNPPFC"), nameof(BeforeExit), nameof(AfterExit));
            Patch(harmony, RequireMethod(playback, "OPMAMGFEKPBMDHPHBGLOCEBBMHFOEJCPCILP",
                    typeof(Il2CppGameLogic.GameMusicScene), typeof(BattleEnemyManager), typeof(float)), nameof(AllowLongRestore));
            Patch(harmony, RequireMethod(playback, "JEBGKFLMGILOHBKMNMAECIAPJGFAJLNCFLDC", typeof(float)),
                postfix: nameof(AfterSeek));

            Type[] autoSignature = { typeof(TimeNodeOrder), typeof(Il2CppSystem.Decimal).MakeByRefType(), typeof(bool).MakeByRefType() };
            // The current native GeneralGirlManager delegates through the movement behavior interface.
            // Patch both concrete behaviors too, covering callers that bypass the manager wrapper.
            foreach (Type type in new[] { typeof(GeneralGirlManager), typeof(NormalMovementBehavior), typeof(PersistentFloatMovementBehavior) })
            {
                var method = AccessTools.DeclaredMethod(type, "AutoPlay", autoSignature)
                    ?? throw new MissingMethodException(type.FullName, "AutoPlay");
                Patch(harmony, method, nameof(AllowNativeAutoPlay));
            }
            Patch(harmony, AccessTools.DeclaredMethod(typeof(Il2CppGameLogic.GameTouchPlay), "TouchActionResult", new[] { typeof(uint) }),
                postfix: nameof(AfterInput));
            PreviewLongVisuals.Bind(core, harmony);
            PreviewMenu.Bind(core, harmony);
            Ready = true;
            MelonLogger.Msg("PracticeMode ready (Euterpe 0.10.7).");
        }
        catch (Exception ex)
        {
            harmony.UnpatchSelf();
            Failed = true;
            MelonLogger.Error("PracticeMode disabled: " + ex);
        }
    }

    private static FieldInfo RequireField(Type type, string name, Type fieldType)
    {
        var field = type.GetField(name, Static);
        if (field == null || field.FieldType != fieldType) throw new MissingFieldException(type.FullName, name);
        return field;
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] arguments)
        => type.GetMethod(name, Static, null, arguments, null) ?? throw new MissingMethodException(type.FullName, name);

    private static void Patch(HarmonyLib.Harmony harmony, MethodInfo method, string prefix = null, string postfix = null)
        => harmony.Patch(method,
            prefix == null ? null : new HarmonyMethod(typeof(PreviewBridge), prefix),
            postfix == null ? null : new HarmonyMethod(typeof(PreviewBridge), postfix));

    internal static void Refresh()
    {
        if (!Ready) return;
        bool active = (bool)_prepared.GetValue(null) || (bool)_running.GetValue(null);
        Seeking = active && ((bool)_seeking.GetValue(null) || (bool)_rebuilding.GetValue(null));
        if (Active && !active) ReleaseOwnership();
        Active = active;
    }

    internal static void Apply()
    {
        if (!Ready || !Active || !_gameStarted) return;
        DBSkill skill = GlobalDataBase.s_DbSkill;
        if (skill == null) return;
        if (_ownedSkill == null || _ownedSkill.Pointer != skill.Pointer)
        {
            _ownedSkill = skill;
            _originalGmAuto = skill.m_IsGMAutoPlay;
        }
        // Euterpe owns/restores m_IsAutoPlay. We only override its value inside a preview.
        skill.m_IsAutoPlay = SettingManager.AutoPlay;
        if (!SettingManager.AutoPlay) skill.m_IsGMAutoPlay = false;
        else skill.m_IsGMAutoPlay = _originalGmAuto;
        ApplySleepIndicator();
    }

    private static void ApplySleepIndicator()
    {
        var indicator = FindSleepIndicator();
        if (indicator == null) return;
        if (_sleepIndicator == null || _sleepIndicator.Pointer != indicator.Pointer)
        {
            RestoreSleepIndicator();
            _sleepIndicator = indicator;
            _originalSleepVisible = indicator.activeSelf;
        }
        // This is the dedicated ZZZ effect, not the character or other skill effects.
        if (indicator.activeSelf != SettingManager.AutoPlay)
            indicator.SetActive(SettingManager.AutoPlay);
    }

    private static void RestoreSleepIndicator()
    {
        var current = FindSleepIndicator();
        if (_sleepIndicator != null && current != null && current.Pointer == _sleepIndicator.Pointer)
            current.SetActive(_originalSleepVisible);
        _sleepIndicator = null;
    }

    private static GameObject FindSleepIndicator()
    {
        var effects = AttackEffectManager.instance;
        var indicator = effects != null ? effects.sleepSkillEffect : null;
        if (indicator != null) return indicator;

        // Euterpe may clear the effect manager's reference while the instance remains
        // attached to the current character. Resolve only the observed dedicated prefab.
        var girl = GirlActionController.instance;
        if (girl == null) return null;
        var root = girl.transform;
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child.name is "fx_sleep_skill(Clone)" or "fx_sleep_skill") return child.gameObject;
        }
        return null;
    }

    internal static void ClearPresses()
    {
        if (Ready && Active) _clearPresses();
    }

    private static void ReleaseOwnership()
    {
        PreviewJudgmentReset.Clear();
        PreviewLongVisuals.Clear();
        RestoreSleepIndicator();
        if (_ownedSkill != null)
        {
            DBSkill current = GlobalDataBase.s_DbSkill;
            if (current != null && current.Pointer == _ownedSkill.Pointer) current.m_IsGMAutoPlay = _originalGmAuto;
            _ownedSkill = null;
        }
    }

    private static void BeforeTick() => Refresh();
    private static void AfterTick() { Refresh(); if ((bool)_running.GetValue(null)) _gameStarted = true; Apply(); }
    private static void AfterGameStart() { Refresh(); _gameStarted = Active; Apply(); }
    private static void BeforeExit()
    {
        ReleaseOwnership();
        _gameStarted = false;
    }
    private static void AfterExit() => Refresh();
    private static bool AllowNativeAutoPlay() => !Manual;
    private static bool AllowLongRestore() => !Manual;
    private static void AfterSeek()
    {
        if (!Active) return;
        PreviewJudgmentReset.AfterSeek();
        PreviewLongVisuals.AfterSeek((float)_clock.GetValue(null));
        if (Manual) ClearPresses();
        Apply();
    }
    private static void AfterSeekAndAudioResume()
    {
        if (!Active) return;
        // Euterpe sets this anchor before rebuilding, but resumes BGM afterwards.
        // Exclude the rebuild duration from the next frame's chart advancement.
        _clockAnchor.SetValue(null, _elapsed());
    }
    private static void AfterInput() => PreviewJudgmentReset.CaptureInput();

    internal static void Shutdown(HarmonyLib.Harmony harmony)
    {
        if (Ready && Active)
        {
            ClearPresses();
            var current = GlobalDataBase.s_DbSkill;
            if (_ownedSkill != null && current != null && current.Pointer == _ownedSkill.Pointer)
                current.m_IsAutoPlay = true; // Return control to Euterpe while it remains in preview.
        }
        ReleaseOwnership();
        Ready = Active = Seeking = false;
        harmony.UnpatchSelf();
    }
}
