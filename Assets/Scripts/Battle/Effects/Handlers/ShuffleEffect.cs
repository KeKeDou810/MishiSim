namespace Mishi.Battle.Effects
{
    public sealed class ShuffleEffect : PlayerKernelEffect
    {
        public override string Id => "Shuffle";
        public override string Description => "由主机洗切指定玩家现有牌库，不公开牌序";
        public override string LuaExample => "{ op = \"Shuffle\", side = \"own\" }";
        protected override void Apply(EffectExecutionContext context, EffectInstruction instruction, int player) => context.Shuffle(player);
    }
}
