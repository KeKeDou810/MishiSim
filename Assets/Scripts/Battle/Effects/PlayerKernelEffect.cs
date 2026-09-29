using System;
namespace Mishi.Battle.Effects
{
    public abstract class PlayerKernelEffect : KernelEffect
    {
        public override void Validate(EffectInstruction instruction, ICardEffectProvider definitions)
        {
            if (instruction.Side == "any") throw new FormatException(Id + " requires own or opponent, not any.");
        }
        public sealed override void Execute(EffectExecutionContext context, EffectInstruction instruction) =>
            Apply(context, instruction, context.Player);
        protected abstract void Apply(EffectExecutionContext context, EffectInstruction instruction, int player);
    }
}
