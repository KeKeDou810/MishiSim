namespace Mishi.Battle.Effects
{
    public abstract class CardKernelEffect : KernelEffect
    {
        public sealed override void Execute(EffectExecutionContext context, EffectInstruction instruction)
        {
            foreach (var card in context.Targets)
                if (card.Zone != TestCardZone.Removed) Apply(context, instruction, card);
        }
        protected abstract void Apply(EffectExecutionContext context, EffectInstruction instruction, NetworkTestMatch.Card card);
    }
}
