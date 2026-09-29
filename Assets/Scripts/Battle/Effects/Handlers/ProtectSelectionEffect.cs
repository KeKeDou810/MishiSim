namespace Mishi.Battle.Effects
{
    public sealed class ProtectSelectionEffect : KernelEffect
    {
        public override string Id => "ProtectSelection";
        public override string Description => "目标不再能被指定玩家的效果选择";
        public override string LuaExample => @"{ op = ""ProtectSelection"", side = ""own"", duration = ""turn"" }";
        public override void Execute(EffectExecutionContext context, EffectInstruction step) => context.ProtectSelection(step);
    }
}
