namespace Mishi.Battle.Effects
{
    public sealed class DestroyEffect : CardKernelEffect
    {
        public override string Id => "Destroy";
        public override string Description => "破坏目标圆阵的整个卡片堆叠";
        public override string LuaExample => @"{ op = ""Destroy"", target = ""selected"" }";
        
        protected override void Apply(EffectExecutionContext context, EffectInstruction instruction, NetworkTestMatch.Card card)
        { if (card.Zone == TestCardZone.Board) context.Destroy(card); }
    }
}
