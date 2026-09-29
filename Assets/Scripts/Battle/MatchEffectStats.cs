using System;
using System.Collections.Generic;
using System.Linq;

namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        private void RefreshStats()
        {
            RefreshPlayerAttachments();
            ExpireInvalidBattleSchedules();
            foreach (var card in cards)
            {
                card.ContinuousContributions.Clear();
                card.Power = ClampStat(ModifiedValue(card, "power", card.BasePower));
                card.Time = ClampStat(ModifiedValue(card, "time", card.BaseTime));
                card.AttackRange = ClampStat(ModifiedValue(card, "range", 1));
            }
            if (effects == null) return;
            try
            {
                foreach (var source in cards.Where(c => !c.Covered && EffectsActive(c) && (c.Zone == TestCardZone.Board || c.Zone == TestCardZone.OffField)))
                {
                    var aura = effects.Rules(source.DefinitionId);
                    if (aura.AuraPower != 0 && source.Zone == aura.AuraZone && (!aura.AuraOwnTurn || source.Owner == ActivePlayer))
                        AddContinuous(source, new ContinuousModifier { Id = "legacy_aura", Target = "all", Amount = aura.AuraPower, Race = aura.AuraRace, SourceZone = aura.AuraZone });
                    if (!(effects is IContinuousEffectProvider provider)) continue;
                    var modifiers = provider.Continuous(Context(source, EffectEvent.Activated));
                    if (modifiers == null || modifiers.Count > 32) throw new FormatException("At most 32 continuous modifiers per source.");
                    var ids = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var modifier in modifiers)
                    {
                        modifier.Validate();
                        if (!ids.Add(modifier.Id)) throw new FormatException("Duplicate continuous modifier id: " + modifier.Id);
                        if (modifier.Stat != "damage") AddContinuous(source, modifier);
                    }
                }
                foreach (var card in cards)
                {
                    card.Power = ClampStat(ModifiedValue(card, "power", card.BasePower) + card.ContinuousContributions.Where(m => m.Stat == "power").Sum(m => (long)m.Amount));
                    card.Time = ClampStat(ModifiedValue(card, "time", card.BaseTime) + card.ContinuousContributions.Where(m => m.Stat == "time").Sum(m => (long)m.Amount));
                    card.AttackRange = ClampStat(ModifiedValue(card, "range", 1) + card.ContinuousContributions.Where(m => m.Stat == "range").Sum(m => (long)m.Amount));
                }
                // Damage conditions see the final power/time, including other cards' auras.
                if (effects is IContinuousEffectProvider damageProvider)
                    foreach (var source in cards.Where(c => !c.Covered && EffectsActive(c) && (c.Zone == TestCardZone.Board || c.Zone == TestCardZone.OffField)))
                        foreach (var modifier in damageProvider.Continuous(Context(source, EffectEvent.Activated)).Where(m => m.Stat == "damage"))
                        {
                            modifier.Validate();
                            AddContinuous(source, modifier);
                        }
            }
            catch (Exception e) { StopForScriptError(e); }
            LogStatChanges();
        }
        private void AddContinuous(Card source, ContinuousModifier modifier)
        {
            if (source.Zone != modifier.SourceZone) return;
            foreach (var card in cards.Where(c => !c.Covered && c.Zone == modifier.TargetZone &&
                (modifier.Target != "self" || c == source) &&
                (modifier.Side == "any" || c.Owner == (modifier.Side == "own" ? source.Owner : 1 - source.Owner))))
            {
                var rule = effects.Rules(card.DefinitionId);
                if (!string.IsNullOrEmpty(modifier.Race) && rule.Race != modifier.Race ||
                    !string.IsNullOrEmpty(modifier.Type) && rule.Type != modifier.Type ||
                    !string.IsNullOrEmpty(modifier.Name) && rule.Name != modifier.Name) continue;
                card.ContinuousContributions.Add(new ContinuousContribution { Source = source.Id, EffectId = modifier.Id, Stat = modifier.Stat, Amount = modifier.Amount });
            }
        }
        private long ModifiedValue(Card card, string stat, int basis)
        {
            var modifiers = card.Modifiers.Where(m => m.Stat == stat && ModifierActive(m)).ToArray();
            return (long)(modifiers.LastOrDefault(m => m.SetValue)?.Amount ?? basis) + modifiers.Where(m => !m.SetValue).Sum(m => (long)m.Amount);
        }
        private bool ModifierActive(StatModifier modifier) => !modifier.SourceBound || cards.Any(s => s.Id == modifier.Source && !s.Covered && EffectsActive(s) &&
            s.Zone == modifier.SourceZone && s.FieldGeneration == modifier.SourceGeneration && (s.Zone == TestCardZone.Board || s.Zone == TestCardZone.OffField));
        private static int ClampStat(long value) => (int)Math.Max(0, Math.Min(int.MaxValue, value));
        private IEnumerable<Card> Candidates(Card source, EffectInstruction step) => cards.Where(c => c.Zone == step.Zone && !c.Covered &&
            (step.Side == "any" || c.Owner == (step.Side == "own" ? source.Owner : 1 - source.Owner)) &&
            CardMatches(c, step));
        private bool CardMatches(Card c, EffectInstruction step) =>
            c.Time >= step.Minimum && c.Time <= step.Maximum && (!step.NotRebuiltThisTurn || c.LastRebuiltTurn != Turn) &&
            (!step.EnteredThisTurn || c.ZoneEnteredTurn == Turn) &&
            (string.IsNullOrEmpty(step.Type) || effects.Rules(c.DefinitionId).Type == step.Type) &&
            (string.IsNullOrEmpty(step.Race) || effects.Rules(c.DefinitionId).Race == step.Race) &&
            (step.Types.Length == 0 || step.Types.Contains(effects.Rules(c.DefinitionId).Type)) &&
            (string.IsNullOrEmpty(step.Name) || (step.NameMatch == "fuzzy" ? (effects.Rules(c.DefinitionId).Name ?? "").IndexOf(step.Name, StringComparison.OrdinalIgnoreCase) >= 0 : effects.Rules(c.DefinitionId).Name == step.Name));
    }
}
