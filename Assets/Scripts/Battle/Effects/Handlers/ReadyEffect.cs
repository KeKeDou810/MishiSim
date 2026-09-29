namespace Mishi.Battle.Effects
{
    public sealed class ReadyEffect : CardKernelEffect
    {
        public override string Id => "Ready";
        public override string Description => "竖置未被覆盖的场上卡片";
        public override string LuaExample => @"{ op = ""Ready"", target = ""selected"" }";
        
        protected override void Apply(EffectExecutionContext context, EffectInstruction instruction, NetworkTestMatch.Card card)
        { if (card.Zone == TestCardZone.Board && !card.Covered) context.Ready(card); }
    }
}
