namespace Mishi.Battle.Effects
{
    public sealed class ClearValueEffect : KernelEffect
    {
        public override string Id => "ClearValue";
        public override string Description => "删除指定作用域中的变量，允许以后重新指定类型";
        public override string LuaExample => @"{ op = ""ClearValue"", scope = ""card"", key = ""marker"" }";
        public override void Validate(EffectInstruction step, ICardEffectProvider definitions) => EffectInstructionValidation.VariableWrite(step);
        public override void Execute(EffectExecutionContext context, EffectInstruction instruction) => context.ClearValue(instruction);
    }
}
