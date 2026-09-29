using System;
using System.Collections.Generic;

namespace Mishi.Battle
{
    public sealed class AutomaticEffectPlan
    {
        public string Id, Label;
        public ActivationCost[] Costs = Array.Empty<ActivationCost>();
        public IReadOnlyList<EffectInstruction> Steps;
    }
    public interface IAutomaticEffectProvider
    {
        IReadOnlyList<AutomaticEffectPlan> BuildAutomatic(EffectContext context);
    }
    public sealed class TriggerOption
    {
        public Guid Id;
        public string DefinitionId, Label;
        public int Owner;
    }
}
