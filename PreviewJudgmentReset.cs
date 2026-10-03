using Il2CppFormulaBase;
using Il2CppGameLogic;
using Il2CppAssets.Scripts.PeroTools.Commons;

namespace PracticeMode;

internal static class PreviewJudgmentReset
{
    private static readonly Dictionary<nint, TimeNodeOrder> Consumed = new();
    private static nint _stage;

    internal static void CaptureInput()
    {
        if (!PreviewBridge.Active) return;
        var stage = Singleton<StageBattleComponent>.instance;
        var touch = GameGlobal.gTouch;
        if (stage == null || touch == null) return;
        if (_stage != stage.Pointer)
        {
            Clear();
            _stage = stage.Pointer;
        }
        // This is the same lookup used by native TouchActionResult. Observe only
        // candidates touched by this input, rather than scanning the whole song.
        var nodes = stage.GetTimeNodeByTick(touch.tickTime);
        if (nodes == null) return;
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (node != null && node.isFucked) Consumed[node.Pointer] = node;
        }
    }

    internal static void AfterSeek()
    {
        var stage = Singleton<StageBattleComponent>.instance;
        // Native TouchActionResult marks individual timing candidates as consumed.
        // Euterpe resets BattleEnemyManager.m_Evaluates, but leaves these flags set.
        // Reset the existing candidates instead of rebuilding or widening the chart.
        if (stage != null && stage.Pointer == _stage)
        {
            foreach (var node in Consumed.Values)
            {
                if (!node.isFucked) continue;
                node.isFucked = false;
            }
        }
        Clear();

        // This list also remembers note indices consumed by input optimization.
        // A seek starts a fresh pass through those same indices.
        var optimizedNotes = GameGlobal.gGameOptimization?.m_OptimizationList;
        optimizedNotes?.Clear();
    }

    internal static void Clear()
    {
        Consumed.Clear();
        _stage = 0;
    }
}
