namespace Mishi.Battle.Effects
{
    public sealed class TapEffect : CardKernelEffect
    {
        public override string Description => "横置场上卡片";
        public override string LuaExample => "{ op = \"Tap\", target = \"selected\" }";
        public override string Id => "Tap";
        protected override void Apply(EffectExecutionContext context, EffectInstruction step, NetworkTestMatch.Card card)
        { if (card.Zone == TestCardZone.Board && !card.Covered) card.Tapped = true; }
    }
}
