using System;
namespace Mishi.Battle.Effects
{
    public sealed class ContinueEffect : KernelEffect
    {
        public override string Id => "Continue";
        public override string Description => "以最新选择与变量上下文继续 Lua 回调";
        public override string LuaExample => @"{ op = ""Continue"", callback = ""afterChoice"" }";
        public override void Validate(EffectInstruction step, ICardEffectProvider definitions) { if (string.IsNullOrWhiteSpace(step.Callback)) throw new FormatException("Continue requires callback."); }
        public override void Execute(EffectExecutionContext context, EffectInstruction step) => context.Continue(step);
    }
}
