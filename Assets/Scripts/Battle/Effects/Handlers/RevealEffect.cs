namespace Mishi.Battle.Effects
{
    public sealed class RevealEffect : CardKernelEffect
    {
        public override string Description => "公开指定卡片并应用公开修正";
        public override string LuaExample => "{ op = \"Reveal\", target = \"selected\" }";
        public override string Id => "Reveal";
        protected override void Apply(EffectExecutionContext context, EffectInstruction step, NetworkTestMatch.Card card) => context.Reveal(card);
    }
}
