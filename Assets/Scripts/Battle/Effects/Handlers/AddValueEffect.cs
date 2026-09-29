using System;

namespace Mishi.Battle.Effects
{
    public sealed class AddValueEffect : KernelEffect
    {
        public override string Id => "AddValue";
        public override string Description => "累加已存在的同类型数值变量，保留其可见性";
        public override string LuaExample => @"{ op = ""AddValue"", scope = ""player"", key = ""counter"", value = 1 }";
        public override void Validate(EffectInstruction step, ICardEffectProvider definitions)
        {
            EffectInstructionValidation.VariableWrite(step);
            if (step.ValueReference == null && (step.Value == null || step.Value.Type != EffectValueType.Integer && step.Value.Type != EffectValueType.Number))
                throw new FormatException("AddValue requires a number.");
        }
        public override void Execute(EffectExecutionContext context, EffectInstruction instruction) => context.WriteValue(instruction, true);
    }
}
