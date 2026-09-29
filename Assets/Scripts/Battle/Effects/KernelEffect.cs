namespace Mishi.Battle.Effects
{
    // Stateless strategy: one operation per class. Match state is accessed through the execution port.
    public abstract class KernelEffect
    {
        public abstract string Id { get; }
        public abstract string Description { get; }
        public abstract string LuaExample { get; }
        public virtual void Validate(EffectInstruction instruction, ICardEffectProvider definitions) { }
        public abstract void Execute(EffectExecutionContext context, EffectInstruction instruction);
    }
}
