using System;
namespace Mishi.Battle.Effects
{
    public sealed class PreventDestructionEffect : KernelEffect
    {
        public override string Id => "PreventDestruction";
        public override string Description => "给予防破坏保护";
        public override string LuaExample => @"{ op = ""PreventDestruction"", target = ""event"", from = ""battle"", duration = ""battle"" }";
        public override void Validate(EffectInstruction step, ICardEffectProvider definitions) { if (step.From != "battle" && step.From != "effect" && step.From != "any") throw new FormatException("Prevention from must be battle/effect/any."); }
        public override void Execute(EffectExecutionContext context, EffectInstruction step) => context.PreventDestruction(step);
    }
}
