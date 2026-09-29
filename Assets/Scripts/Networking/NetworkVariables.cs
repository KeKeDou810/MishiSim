using System;
using Mishi.Battle;

namespace Mishi.Networking
{
    public struct NetworkVariableInfo
    {
        public int Scope, Owner, Type;
        public string CardId, Key, Text;
        public double Number;
        public bool Boolean;
    }
    public static class NetworkVariables
    {
        public static NetworkVariableInfo Encode(VariableSnapshot value) => new NetworkVariableInfo {
            Scope = (int)value.Scope, Owner = value.Owner, CardId = value.CardId.ToString("N"), Key = value.Key,
            Type = (int)value.Value.Type, Number = value.Value.Number, Boolean = value.Value.Boolean, Text = value.Value.Text
        };
        public static VariableSnapshot Decode(NetworkVariableInfo value)
        {
            EffectValue typed;
            switch ((EffectValueType)value.Type)
            {
                case EffectValueType.Integer:
                    if (value.Number != Math.Truncate(value.Number) || Math.Abs(value.Number) > 1000000000) throw new FormatException("Invalid integer snapshot.");
                    typed = EffectValue.Integer(checked((int)value.Number)); break;
                case EffectValueType.Number: typed = EffectValue.Decimal(value.Number); break;
                case EffectValueType.Boolean: typed = EffectValue.Bool(value.Boolean); break;
                case EffectValueType.String: typed = EffectValue.String(value.Text); break;
                default: throw new FormatException("Invalid variable snapshot type.");
            }
            EffectVariableStore.ValidateKey(value.Key);
            if (!Enum.IsDefined(typeof(VariableScope), value.Scope)) throw new FormatException("Invalid variable snapshot scope.");
            return new VariableSnapshot { Scope = (VariableScope)value.Scope, Owner = value.Owner,
                CardId = Guid.Parse(value.CardId), Key = value.Key, Value = typed };
        }
    }
}
