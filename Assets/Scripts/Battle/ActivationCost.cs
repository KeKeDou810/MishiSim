using System;
namespace Mishi.Battle
{
    public enum ActivationCostKind { Time, Discard, Destroy, Exile, ReturnToDeck, RemoveToken, MoveSelf, TokenTime }
    public sealed class ActivationCost
    {
        public ActivationCostKind Kind;
        public int Minimum, Maximum;
        public string Subject = "any";
        public string StoreAs, Type, Race, Name, NameMatch = "exact", Position = "bottom";
        public TestCardZone? Zone;
        public TestCardZone[] Zones = Array.Empty<TestCardZone>();
        public TestCardZone Destination = TestCardZone.Hand;
        public bool PaySelectedTime, StoreTime;
        public void Validate()
        {
            if (!Enum.IsDefined(typeof(ActivationCostKind), Kind) || Minimum < 0 || Maximum < Minimum || Maximum > 32)
                throw new FormatException("Invalid activation cost bounds.");
            if (Subject != "any" && Subject != "self" && Subject != "other") throw new FormatException("Invalid cost subject.");
            if (NameMatch != "exact" && NameMatch != "fuzzy" || Position != "top" && Position != "bottom") throw new FormatException("Invalid cost filter/position.");
            if (Zones == null || Zones.Length > 8 || Zone.HasValue && Zones.Length > 0) throw new FormatException("Use zone or zones in a cost.");
            foreach (var zone in Zones) if (!Enum.IsDefined(typeof(TestCardZone), zone)) throw new FormatException("Unknown cost zone.");
            if (Kind == ActivationCostKind.MoveSelf && (Minimum != 1 || Maximum != 1 || Destination != TestCardZone.Hand && Destination != TestCardZone.Discard && Destination != TestCardZone.Exile)) throw new FormatException("MoveSelf requires one self and Hand/Discard/Exile destination.");
            if (Kind == ActivationCostKind.TokenTime && Minimum != Maximum) throw new FormatException("TokenTime requires a fixed time amount.");
            if (!string.IsNullOrEmpty(StoreAs)) EffectVariableStore.ValidateKey(StoreAs);
        }
    }
}
