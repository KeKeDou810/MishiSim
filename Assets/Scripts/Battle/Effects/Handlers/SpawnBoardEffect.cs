using System;
namespace Mishi.Battle.Effects
{
    public sealed class SpawnBoardEffect : KernelEffect
    {
        public override string Id => "SpawnBoard";
        public override string Description => "生成衍生物并选择空圆阵";
        public override string LuaExample => @"{ op = ""SpawnBoard"", definitionId = ""PD03-T02-C"", amount = 3 }";
        public override void Validate(EffectInstruction step, ICardEffectProvider definitions) { if (step.Side != "own" || string.IsNullOrEmpty(step.DefinitionId) || !definitions.Rules(step.DefinitionId).IsToken) throw new FormatException("SpawnBoard needs own token definition."); }
        public override void Execute(EffectExecutionContext context, EffectInstruction step) => context.SpawnBoard(step);
    }
}
