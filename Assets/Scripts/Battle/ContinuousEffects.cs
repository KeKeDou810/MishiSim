using System;
using System.Collections.Generic;

namespace Mishi.Battle
{
    public sealed class ContinuousModifier
    {
        public string Id, Stat = "power", Target = "self", Side = "own", Race, Type, Name;
        public int Amount;
        public TestCardZone SourceZone = TestCardZone.Board, TargetZone = TestCardZone.Board;
        public void Validate()
        {
            EffectVariableStore.ValidateKey(Id);
            if ((Stat != "power" && Stat != "time" && Stat != "range" && Stat != "damage") || (Target != "self" && Target != "all") ||
                (Side != "own" && Side != "opponent" && Side != "any") || Math.Abs((long)Amount) > 100000 ||
                (SourceZone != TestCardZone.Board && SourceZone != TestCardZone.OffField) ||
                (TargetZone != TestCardZone.Board && TargetZone != TestCardZone.OffField))
                throw new FormatException("Invalid continuous modifier.");
        }
    }
    public interface IContinuousEffectProvider
    {
        IReadOnlyList<ContinuousModifier> Continuous(EffectContext context);
    }
    public sealed class ContinuousContribution
    {
        public Guid Source;
        public string EffectId, Stat;
        public int Amount;
    }
}
