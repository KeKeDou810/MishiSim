using System;

namespace Mishi.Battle.Effects
{
    public sealed class SetValueEffect : KernelEffect
    {
        public override string Id => "SetValue";
        public override string Description => "在指定作用域保存带类型的变量";
        public override string LuaExample => @"{ op = ""SetValue"", scope = ""effect"", key = ""bonus"", value = 1000 }";
        public override void Validate(EffectInstruction step, ICardEffectProvider definitions)
        {
            EffectInstructionValidation.VariableWrite(step);
            if (step.Value == null && step.ValueReference == null) throw new FormatException("SetValue requires a typed value or reference.");
        }
        public override void Execute(EffectExecutionContext context, EffectInstruction instruction) => context.WriteValue(instruction, false);
    }
}
