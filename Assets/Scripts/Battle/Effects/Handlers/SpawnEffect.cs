namespace Mishi.Battle.Effects
{
    public sealed class SpawnEffect : KernelEffect
    {
        public override string Id => "Spawn";
        public override string Description => "生成衍生物，等待玩家选择合法场外区";
        public override string LuaExample => @"{ op = ""Spawn"", definitionId = ""PD03-T01-C"" }";
        public override void Validate(EffectInstruction step, ICardEffectProvider definitions)
        {
            if (step.Side == "any") throw new System.FormatException("Spawn requires own or opponent.");
            if (definitions == null || string.IsNullOrEmpty(step.DefinitionId) || !definitions.Rules(step.DefinitionId).IsToken)
                throw new System.FormatException("Spawn requires a known token definition.");
        }
        public override void Execute(EffectExecutionContext context, EffectInstruction instruction) => context.Spawn(instruction);
    }
}
