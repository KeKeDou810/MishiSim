using System;
using System.Linq;

namespace Mishi.Battle.Effects
{
    public sealed class ScryEffect : KernelEffect
    {
        public override string Id => "Scry";
        public override string Description => "查看指定数量的卡顶或卡底，可公开并继续执行后续效果";
        public override string LuaExample => @"{
    op = ""Scry"",
    amount = 1,
    from = ""top"",
    reveal = false,
    after = {
        { op = ""ReturnToDeck"", target = ""scry"", position = ""choose"" }
    }
}";
        public override void Validate(EffectInstruction step, ICardEffectProvider definitions)
        {
            if (step.Amount < 1 || step.Amount > 32 || step.Side == "any" ||
                !new[] { "top", "bottom" }.Contains(step.From))
                throw new FormatException("Scry requires amount 1..32, own/opponent and from top/bottom.");
            if (step.After == null || step.After.Count > 32 || step.After.Any(s => s == null || s.OperationId.Equals("Scry", StringComparison.OrdinalIgnoreCase)))
                throw new FormatException("Scry after accepts at most 32 non-Scry steps. Use separate Scry instructions for another look.");
            if (!string.IsNullOrEmpty(step.AfterCallback) && definitions is not IScryEffectProvider)
                throw new FormatException("This effect provider does not support Scry callbacks.");
            var registry = KernelEffectRegistry.CreateDefault();
            foreach (var instruction in step.After) registry.Validate(instruction, definitions);
        }
        public override void Execute(EffectExecutionContext context, EffectInstruction instruction) => context.Scry(instruction);
    }
}
