namespace Mishi.Battle.Effects
{
    public sealed class AttachPlayersEffect : KernelEffect
    {
        public override string Id => "AttachPlayers";
        public override string Description => "将己方两张玩家卡放在发动契约下方并随行，来源失效或离场时归位";
        public override string LuaExample => "{ op = \"AttachPlayers\" }";
        public override void Execute(EffectExecutionContext context, EffectInstruction instruction) => context.AttachPlayers();
    }
}
