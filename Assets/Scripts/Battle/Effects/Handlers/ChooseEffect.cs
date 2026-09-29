namespace Mishi.Battle.Effects
{
    public sealed class ChooseEffect : KernelEffect
    {
        public override string Id => "Choose";
        public override string Description => "选择一张合法卡片，暂停结算等待玩家输入";
        public override string LuaExample => @"{ op = ""Choose"", zone = ""Board"", side = ""own"" }";
        public override void Validate(EffectInstruction step, ICardEffectProvider definitions)
        {
            if (step.MinimumCount < 0 || step.MaximumCount < step.MinimumCount || step.MaximumCount > 32) throw new System.FormatException("Choose count must be 0..32.");
            if (step.Target == "scry" || step.Target == "set") return;
            if (step.Zone != TestCardZone.Board && step.Zone != TestCardZone.Discard && step.Zone != TestCardZone.Contract &&
                step.Zone != TestCardZone.OffField && step.Zone != TestCardZone.Exile && !(step.Zone == TestCardZone.Hand && step.Side == "own"))
                throw new System.FormatException("Choose only allows public zones or your own hand.");
        }
        public override void Execute(EffectExecutionContext context, EffectInstruction instruction) => context.Choose(instruction);
    }
}
