namespace Mishi.Battle.Effects
{
    public sealed class ModifyEffect : CardKernelEffect
    {
        public override string Id => "Modify";
        public override string Description => "增加或设置力量／时间修正器";
        public override string LuaExample => @"{ op = ""Modify"", target = ""selected"", stat = ""power"", amount = 1000, duration = ""turn"" }";
        
        protected override void Apply(EffectExecutionContext context, EffectInstruction instruction, NetworkTestMatch.Card card)
        {
            if (instruction.Duration == "source" && context.Source.Zone != TestCardZone.Board && context.Source.Zone != TestCardZone.OffField)
                throw new System.InvalidOperationException("Source-bound modifiers require a source on Board or OffField.");
            card.Modifiers.Add(new StatModifier {
                Source = context.Source.Id, Stat = instruction.Stat, Amount = instruction.Amount, SetValue = instruction.Mode == "set",
                BattleBound = instruction.Duration == "battle", SourceBound = instruction.Duration == "source", SourceGeneration = context.Source.FieldGeneration, SourceZone = context.Source.Zone,
                ExpiresAfterTurn = instruction.Duration == "permanent" || instruction.Duration == "source" ? int.MaxValue : instruction.Duration == "nextTurn" ? context.Turn + 1 : context.Turn
            }); }
    }
}
