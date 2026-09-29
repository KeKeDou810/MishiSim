namespace Mishi.Battle.Effects
{
    public sealed class SuppressEffectsEffect : CardKernelEffect
    {
        public override string Id => "SuppressEffects";
        public override string Description => "目标本回合失去卡片效果";
        public override string LuaExample => "{ op = \"SuppressEffects\", target = \"selected\" }";
        protected override void Apply(EffectExecutionContext context, EffectInstruction instruction, NetworkTestMatch.Card card)
        { if (card.Zone == TestCardZone.Board) context.SuppressEffects(card); }
    }
}
