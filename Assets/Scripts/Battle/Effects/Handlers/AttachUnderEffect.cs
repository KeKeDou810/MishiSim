namespace Mishi.Battle.Effects
{
    public sealed class AttachUnderEffect : KernelEffect
    {
        public override string Id => "AttachUnder";
        public override string Description => "将所选手牌放到己方时魔下方，保留原始所有者";
        public override string LuaExample => @"{ op = ""AttachUnder"", target = ""self"", set = ""selected"" }";
        public override void Execute(EffectExecutionContext context, EffectInstruction step) => context.AttachUnder(step);
    }
}
