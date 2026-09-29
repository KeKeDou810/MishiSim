using System;
using System.Collections.Generic;
using System.Linq;
namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        private sealed class SelectionState
        {
            public EffectInstruction Step;
            public readonly Dictionary<Guid, Card> Options = new Dictionary<Guid, Card>();
            public readonly List<Card> Selected = new List<Card>();
            public bool Anonymous;
            public double Seconds;
        }
        private void BeginSelection(EffectExecution execution, EffectInstruction step, bool anonymous)
        {
            var source = execution.Source;
            var targets = anonymous ? cards.Where(c => c.Owner != source.Owner && c.Zone == TestCardZone.Hand).ToArray() :
                (step.Target == "scry" || step.Target == "set" || step.Target == "event" ? ResolveEffectTargets(execution, step) : ExcludeTargets(execution, Candidates(source, step), step.Except).ToArray())
                .Where(c => CardMatches(c, step) && CanChooseByEffect(source, c)).ToArray();
            var state = new SelectionState { Step = step, Anonymous = anonymous, Seconds = ChoiceBudget };
            foreach (var card in targets) state.Options.Add(anonymous ? Guid.NewGuid() : card.Id, card);
            execution.Selection = state; execution.Selected = Guid.Empty;
            SaveSet(execution, step.StoreAs, Array.Empty<Guid>());
            RequestSelection(execution);
        }
        private void RequestSelection(EffectExecution execution)
        {
            var state = execution.Selection;
            if (state.Selected.Count >= state.Step.MaximumCount || state.Options.Count == 0)
            { CompleteSelection(execution); return; }
            effectChoice = new EffectChoice { Player = execution.Source.Owner,
                Prompt = (state.Step.Prompt ?? "选择卡片") + (state.Step.MaximumCount > 1 ? $"（已选 {state.Selected.Count}/{state.Step.MaximumCount}；放弃可结束选择）" : ""),
                Candidates = state.Options.Keys.ToArray(), Optional = state.Step.Optional || state.Selected.Count >= state.Step.MinimumCount,
                ViewedCards = state.Options.Select(p => state.Anonymous ? new Card(p.Key, "", p.Value.Owner, -1, zone: TestCardZone.Hand) : p.Value.Copy()).ToArray(), Seconds = state.Seconds };
        }
        private void ResolveSelection(EffectExecution execution, Guid handle)
        {
            var state = execution.Selection; state.Seconds = effectChoice.Seconds;
            if (handle == Guid.Empty) { CompleteSelection(execution); return; }
            var card = state.Options[handle]; state.Options.Remove(handle); state.Selected.Add(card);
            execution.Selected = card.Id;
            AddLog(execution.Source.Owner, state.Anonymous ? "选择一张对方背面手牌。" : "选择目标：" + CardLabel(card), PublicZone(card.Zone) && !card.HiddenAttachment ? -1 : execution.Source.Owner);
            RequestSelection(execution);
        }
        private void CompleteSelection(EffectExecution execution)
        {
            SaveSet(execution, execution.Selection.Step.StoreAs, execution.Selection.Selected.Select(c => c.Id).ToArray());
            execution.Selection = null; effectChoice = null;
        }
        private void ChooseHiddenHand(EffectExecution execution, EffectInstruction step) => BeginSelection(execution, step, true);
    }
}
