using System;
using System.Linq;
namespace Mishi.Battle.Effects
{
    public static class EffectInstructionValidation
    {
        public static void Common(EffectInstruction step)
        {
            if (step == null) throw new ArgumentNullException(nameof(step));
            if (!new[] { "self", "selected", "all", "scry", "set", "event", "cause", "attacker", "defender" }.Contains(step.Target) ||
                !new[] { "own", "opponent", "any" }.Contains(step.Side) ||
                !new[] { "add", "set" }.Contains(step.Mode) ||
                !new[] { "power", "time", "range" }.Contains(step.Stat) ||
                !new[] { "turn", "nextTurn", "permanent", "source", "battle" }.Contains(step.Duration) ||
                !Enum.IsDefined(typeof(TestCardZone), step.Zone) || step.Minimum < 0 || step.Minimum > step.Maximum ||
                step.Amount < -100000 || step.Amount > 100000 || step.MaximumCost < 0 || step.MaximumCost > 11 ||
                step.MinimumDamage < 0 || step.MinimumDamage > 11 || step.ScryIndex < 0 || step.ScryIndex > 32 ||
                step.ScryIndex > 0 && step.Target != "scry")
                throw new FormatException("Invalid effect selector, modifier, or clock bounds.");
            new Events.EventCardFilter { Type = step.Type, Types = step.Types, Name = step.Name, NameMatch = step.NameMatch, Race = step.Race }.Validate();
            if (step.Placement != "any" && step.Placement != "player" && step.Placement != "defense") throw new FormatException("placement must be any/player/defense.");
            MemoryKey(step.StoreAs, false);
            step.AmountReference?.Validate(); step.ValueReference?.Validate();
            if (step.Target == "set") MemoryReference(step.FromSet);
            if (!string.IsNullOrEmpty(step.Except)) MemoryReference(step.Except);
        }
        public static void VariableWrite(EffectInstruction step)
        {
            EffectVariableStore.ValidateKey(step.Key);
            if (!Enum.IsDefined(typeof(VariableScope), step.Scope) || step.Side == "any" || step.Visibility != "private" && step.Visibility != "public")
                throw new FormatException("Invalid variable scope, side or visibility.");
        }
        public static void MemoryKey(string key, bool required)
        {
            if (string.IsNullOrEmpty(key) && !required) return;
            if (string.IsNullOrEmpty(key) || key.Length > 32 || key == "scry" || key == "selected" ||
                !System.Text.RegularExpressions.Regex.IsMatch(key, @"^[A-Za-z_][A-Za-z0-9_]*$"))
                throw new FormatException("Memory keys must be 1..32 ASCII identifier characters; scry/selected are reserved.");
        }
        private static void MemoryReference(string key)
        {
            if (key == "scry" || key == "selected") return;
            MemoryKey(key, true);
        }
        public static void NonNegativeAmount(EffectInstruction step)
        {
            if (step.Amount < 0 || step.Amount > 100) throw new FormatException(step.OperationId + " amount must be 0..100.");
        }
    }
}
