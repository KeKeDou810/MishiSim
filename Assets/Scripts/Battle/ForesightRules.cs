using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Mishi.Battle
{
    public enum ForesightMark { None, Draw, Heal, Destroy, Special }
    public static class ForesightRules
    {
        public static int MaximumChecks(int marks, int attackTargets)
        {
            if (marks < 0 || marks > 32 || attackTargets < 1 || attackTargets > 32) throw new ArgumentOutOfRangeException();
            return checked(marks * attackTargets);
        }
        public static int Count(string sign, bool contract)
        {
            var match = Regex.Match(sign ?? "", @"未来视\s*(\d*)");
            if (!match.Success) return contract ? 1 : 0;
            int count = match.Groups[1].Value.Length == 0 ? 1 : int.Parse(match.Groups[1].Value);
            if (count < 1 || count > 32) throw new FormatException("Foresight count must be 1..32.");
            return count;
        }
        public static ForesightMark Mark(string sign)
        {
            if (string.IsNullOrEmpty(sign) || !sign.Contains("特效标记")) return ForesightMark.None;
            if (sign.Contains("抽卡标记")) return ForesightMark.Draw;
            if (sign.Contains("回复标记")) return ForesightMark.Heal;
            if (sign.Contains("炸裂标记")) return ForesightMark.Destroy;
            if (sign.Contains("特殊标记")) return ForesightMark.Special;
            throw new FormatException("Unknown foresight mark: " + sign);
        }
    }
    public interface IForesightEffectProvider
    {
        // null means no override; an empty plan explicitly resolves without additional operations.
        IReadOnlyList<EffectInstruction> BuildForesight(EffectContext context);
    }
}
