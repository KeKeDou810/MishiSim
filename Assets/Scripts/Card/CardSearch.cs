using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// Pure query logic, independent of Unity and deck legality.
public static class CardSearch
{
    public static int TypeOrder(CardDefinition card) => card.IsContract ? 0 : card.Type == "通常时魔" ? 1 : card.IsDecision ? 2 : card.IsPlayer ? 3 : 4;
    public static IEnumerable<CardDefinition> Organize(IEnumerable<CardDefinition> cards, bool byTime, bool reverse)
    {
        var ordered = cards.OrderBy(c => byTime ? c.Level : TypeOrder(c))
            .ThenBy(c => byTime ? TypeOrder(c) : c.Level).ThenBy(c => c.Id, StringComparer.Ordinal);
        return reverse ? ordered.Reverse() : ordered;
    }
    public static bool MatchesTime(CardDefinition card, int? min, int? max) =>
        (!min.HasValue || card.Level >= min.Value) && (!max.HasValue || card.Level <= max.Value);
    public static bool TryPowerRange(string minimum, string maximum, out int? min, out int? max)
    {
        min = max = null;
        if (!string.IsNullOrWhiteSpace(minimum)) { if (!int.TryParse(minimum, out int n) || n < 0) return false; min = n; }
        if (!string.IsNullOrWhiteSpace(maximum)) { if (!int.TryParse(maximum, out int n) || n < 0) return false; max = n; }
        return !min.HasValue || !max.HasValue || min <= max;
    }
    public static bool MatchesPower(CardDefinition card, int? min, int? max)
    {
        if (!min.HasValue && !max.HasValue) return true;
        if (card.IsPlayer || card.IsDecision) return false;
        return (!min.HasValue || card.Power >= min.Value) && (!max.HasValue || card.Power <= max.Value);
    }
    public static int Score(CardDefinition card, string query)
    {
        var tokens = (query ?? "").Normalize(NormalizationForm.FormKC).Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        var fields = new[] { card.Name, card.Id, card.RulesId, card.Faction, card.Type, card.Race, card.Sign, card.EffectText, card.Rarity }.Select(Normalize).ToArray();
        int total = 0;
        foreach (string token in tokens)
        {
            string needle = Normalize(token);
            if (needle.Length == 0) continue;
            int best = int.MaxValue;
            foreach (string field in fields)
            {
                if (field == needle) best = 0;
                else if (field.StartsWith(needle, StringComparison.Ordinal)) best = Math.Min(best, 1);
                else if (field.Contains(needle)) best = Math.Min(best, 2);
                else if (needle.Length >= 2 && IsSubsequence(needle, field)) best = Math.Min(best, 3);
            }
            if (best == int.MaxValue) return -1;
            total += best;
        }
        return total;
    }
    private static string Normalize(string value) => new string((value ?? "").Normalize(NormalizationForm.FormKC).ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
    private static bool IsSubsequence(string needle, string field)
    {
        int index = 0;
        foreach (char c in field) if (c == needle[index] && ++index == needle.Length) return true;
        return false;
    }
}
