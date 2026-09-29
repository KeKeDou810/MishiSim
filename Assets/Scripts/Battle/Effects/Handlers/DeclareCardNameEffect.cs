using System;

namespace Mishi.Battle.Effects
{
    public sealed class DeclareCardNameEffect : KernelEffect
    {
        public override string Id => "DeclareCardName";
        public override string Description => "先锁定卡名宣言，再继续后续效果";
        public override string LuaExample => @"{ op = ""DeclareCardName"", storeAs = ""declaredName"" }";
        public override void Validate(EffectInstruction step, ICardEffectProvider definitions)
        {
            EffectInstructionValidation.MemoryKey(step.StoreAs, true);
            if (definitions is not ICardNameProvider) throw new FormatException("Card name catalog is required.");
        }
        public override void Execute(EffectExecutionContext context, EffectInstruction instruction) => context.DeclareCardName(instruction);
    }
}
