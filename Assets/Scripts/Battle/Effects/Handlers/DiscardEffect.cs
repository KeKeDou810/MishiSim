namespace Mishi.Battle.Effects
{
    public sealed class DiscardEffect : CardKernelEffect
    {
        public override string Id => "Discard";
        public override string Description => "将目标手牌送入弃牌区";
        public override string LuaExample => @"{ op = ""Discard"", target = ""selected"" }";
        
        protected override void Apply(EffectExecutionContext context, EffectInstruction instruction, NetworkTestMatch.Card card)
        { if (card.Zone == TestCardZone.Hand) context.Move(card, TestCardZone.Discard); }
    }
}
