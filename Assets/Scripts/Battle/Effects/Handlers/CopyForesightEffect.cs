namespace Mishi.Battle.Effects
{
    public sealed class CopyForesightEffect : KernelEffect
    {
        public override string Id => "CopyForesight";
        public override string Description => "复制刚处理的未来视特效标记";
        public override string LuaExample => @"{ op = ""CopyForesight"" }";
        public override void Validate(EffectInstruction step, ICardEffectProvider definitions) {  }
        public override void Execute(EffectExecutionContext context, EffectInstruction step) => context.CopyForesight(step);
    }
}
