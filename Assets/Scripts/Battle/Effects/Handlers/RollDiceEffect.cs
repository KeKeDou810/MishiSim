using System;
namespace Mishi.Battle.Effects
{
    public sealed class RollDiceEffect : KernelEffect
    {
        public override string Id => "RollDice";
        public override string Description => "掷骰并保存整数结果";
        public override string LuaExample => @"{ op = ""RollDice"", amount = 6, storeAs = ""die"", reveal = true }";
        public override void Validate(EffectInstruction step, ICardEffectProvider definitions) { if (step.AmountReference == null && (step.Amount < 2 || step.Amount > 100)) throw new FormatException("Dice sides must be 2..100."); EffectVariableStore.ValidateKey(step.StoreAs); }
        public override void Execute(EffectExecutionContext context, EffectInstruction step) => context.RollDice(step);
    }
}
