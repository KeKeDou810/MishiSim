using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Mishi.Battle
{
    public enum VariableScope { Effect, Card, Player, Turn, Match }
    public enum EffectValueType { Integer, Number, Boolean, String }

    // Immutable values keep Lua context copies and network snapshots from mutating kernel state.
    public sealed class EffectValue
    {
        public EffectValueType Type { get; }
        public double Number { get; }
        public bool Boolean { get; }
        public string Text { get; }
        private EffectValue(EffectValueType type, double number = 0, bool boolean = false, string text = null)
        { Type = type; Number = number; Boolean = boolean; Text = text; }
        public static EffectValue Integer(int value)
        { CheckNumber(value); return new EffectValue(EffectValueType.Integer, value); }
        public static EffectValue Decimal(double value)
        { CheckNumber(value); return new EffectValue(EffectValueType.Number, value); }
        public static EffectValue Bool(bool value) => new EffectValue(EffectValueType.Boolean, boolean: value);
        public static EffectValue String(string value)
        {
            if (value == null || value.Length > 1024) throw new FormatException("Variable strings must contain at most 1024 characters.");
            return new EffectValue(EffectValueType.String, text: value);
        }
        private static void CheckNumber(double value)
        { if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) > 1000000000) throw new FormatException("Variable numbers must be finite and within +/-1e9."); }
        public int RequireInteger() => Type == EffectValueType.Integer ? (int)Number : throw new FormatException("An integer variable is required for this parameter.");
        public EffectValue Add(EffectValue delta)
        {
            if (delta == null || Type != delta.Type || Type != EffectValueType.Integer && Type != EffectValueType.Number)
                throw new FormatException("AddValue requires matching numeric types.");
            return Type == EffectValueType.Integer ? Integer(checked((int)Number + (int)delta.Number)) : Decimal(Number + delta.Number);
        }
    }
    public sealed class VariableReference
    {
        public string Key, Side = "own", Target = "self";
        public VariableScope Scope;
        public void Validate()
        {
            EffectVariableStore.ValidateKey(Key);
            if (!Enum.IsDefined(typeof(VariableScope), Scope) || (Side != "own" && Side != "opponent") || (Target != "self" && Target != "selected"))
                throw new FormatException("Invalid variable reference scope, side or target.");
        }
    }
    public sealed class StoredEffectValue
    {
        public EffectValue Value { get; }
        public int Owner { get; }
        public bool Public { get; }
        public StoredEffectValue(EffectValue value, int owner, bool isPublic) { Value = value; Owner = owner; Public = isPublic; }
    }
    public sealed class EffectVariableStore
    {
        private readonly Dictionary<string, StoredEffectValue> values = new Dictionary<string, StoredEffectValue>(StringComparer.Ordinal);
        public static void ValidateKey(string key)
        { if (string.IsNullOrEmpty(key) || key.Length > 32 || !Regex.IsMatch(key, @"^[A-Za-z_][A-Za-z0-9_]*$")) throw new FormatException("Variable key must be a 1..32 character ASCII identifier."); }
        public EffectValue Read(string key) => values.TryGetValue(key, out var value) ? value.Value : throw new FormatException("Undefined variable: " + key);
        public void Set(string key, EffectValue value, int owner, bool isPublic)
        {
            ValidateKey(key);
            if (value == null || owner < 0 || owner > 1) throw new FormatException("Invalid variable value or owner.");
            if (values.TryGetValue(key, out var existing) && existing.Value.Type != value.Type)
                throw new FormatException("Variable type cannot change before ClearValue: " + key);
            if (!values.ContainsKey(key) && values.Count >= 128) throw new FormatException("Variable store exceeded 128 keys.");
            values[key] = new StoredEffectValue(value, owner, isPublic);
        }
        public void Add(string key, EffectValue delta)
        {
            var next = Read(key).Add(delta); var old = values[key];
            Set(key, next, old.Owner, old.Public);
        }
        public void Clear(string key) { ValidateKey(key); values.Remove(key); }
        public void ClearAll() => values.Clear();
        public KeyValuePair<string, StoredEffectValue>[] VisibleTo(int player) => values.Where(p => p.Value.Public || p.Value.Owner == player).ToArray();
    }
    public sealed class VariableSnapshot
    {
        public VariableScope Scope;
        public Guid CardId;
        public int Owner;
        public string Key;
        public EffectValue Value;
    }
    public static class VariableContexts
    {
        public static Dictionary<VariableScope, Dictionary<string, EffectValue>> Empty() =>
            Enum.GetValues(typeof(VariableScope)).Cast<VariableScope>().ToDictionary(s => s, _ => new Dictionary<string, EffectValue>(StringComparer.Ordinal));
        public static Dictionary<VariableScope, Dictionary<string, EffectValue>> FromSnapshots(IEnumerable<VariableSnapshot> values, int player, Guid source)
        {
            var result = Empty();
            foreach (var value in values)
            {
                if (value.Scope == VariableScope.Card && value.CardId != source || value.Scope == VariableScope.Player && value.Owner != player) continue;
                result[value.Scope][value.Key] = value.Value;
            }
            return result;
        }
    }
}
