namespace Mishi.Battle.Effects
{
    public sealed class MoveEffect : KernelEffect
    {
        public override string Id => "Move";
        public override string Description => "移动卡片；进入场外区时等待玩家选择位置";
        public override string LuaExample => @"{ op = ""Move"", zone = ""OffField"" }";
        public override void Validate(EffectInstruction step, ICardEffectProvider definitions)
        {
            if (step.Zone != TestCardZone.Hand && step.Zone != TestCardZone.Discard && step.Zone != TestCardZone.Exile && step.Zone != TestCardZone.OffField)
                throw new System.FormatException("Unsupported Move destination.");
        }
        public override void Execute(EffectExecutionContext context, EffectInstruction instruction)
        {
            if (instruction.Zone == TestCardZone.OffField) context.PlaceOffField(context.Targets);
            else foreach (var card in context.Targets)
                if (card.Zone != TestCardZone.Removed) context.Move(card, instruction.Zone);
        }
    }
}
