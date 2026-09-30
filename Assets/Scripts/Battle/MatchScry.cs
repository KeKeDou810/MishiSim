using System;
using System.Collections.Generic;
using System.Linq;

namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        // The inspected cards remain in the deck until resolution; looking is not drawing.
        private sealed class ScryState
        {
            public EffectInstruction Instruction;
            public Card[] Viewed;
            public bool ActivateMark = true;
        }
        private void BeginScry(EffectExecution execution, EffectInstruction step)
        {
            int owner = step.Side == "opponent" ? 1 - execution.Source.Owner : execution.Source.Owner;
            IEnumerable<Card> deck = cards.Where(c => c.Owner == owner && c.Zone == TestCardZone.Deck);
            if (step.From == "bottom") deck = deck.Reverse();
            var viewed = deck.Take(step.Amount).ToArray();
            AddLog(execution.Source.Owner, $"{(step.Reveal ? "公开" : "查看")}牌库 {viewed.Length} 张卡。");
            foreach (var card in viewed) AddLog(execution.Source.Owner, "查看卡片：" + CardLabel(card), step.Reveal ? -1 : execution.Source.Owner);
            if (execution.Foresight != null)
            {
                // A revealed foresight card is outside the deck while its mark resolves; Draw cannot draw itself.
                foreach (var card in viewed) card.Zone = TestCardZone.Revealed;
                if (drawRules != null) RecycleEmptyDeck(owner);
            }
            execution.ScryTargets = viewed;
            SaveSet(execution, step.StoreAs, viewed.Select(c => c.Id).ToArray());
            execution.Scry = new ScryState { Instruction = step, Viewed = viewed };
            if (step.Reveal) foreach (var card in viewed) Present(card, CardPresentationKind.Reveal);
            if (Winner >= 0) return;
            if (viewed.Length == 0) { FinishScry(execution); return; }
            effectChoice = new EffectChoice {
                Player = execution.Source.Owner, IsDeckView = true, PublicView = execution.Foresight != null && step.Reveal, Prompt = step.Prompt ?? "查看牌库",
                Candidates = viewed.Select(c => c.Id).ToArray(), ViewedCards = viewed.Select(c => c.Copy()).ToArray(),
                DeckPositions = execution.Foresight != null && effects.Rules(viewed[0].DefinitionId).ForesightMark != ForesightMark.None ? new[] { 0, 3 } : new[] { 0 },
                Seconds = drawRules?.ResponseTimeSeconds > 0 ? drawRules.ResponseTimeSeconds : 20
            };
        }
        private void FinishScry(EffectExecution execution)
        {
            try { FinishScryCore(execution); }
            catch (Exception e) { StopForScriptError(e); }
        }
        private void FinishScryCore(EffectExecution execution)
        {
            var scry = execution.Scry;
            effectChoice = null; execution.Scry = null;
            if (execution.Foresight != null) { FinishForesightScry(execution, scry.Viewed, scry.ActivateMark); return; }
            var after = scry.Instruction.After;
            if (!string.IsNullOrEmpty(scry.Instruction.AfterCallback))
            {
                var context = Context(execution.Source, EffectEvent.Activated, "scry");
                if (!string.IsNullOrEmpty(scry.Instruction.ScriptDefinitionId)) context.DefinitionId = scry.Instruction.ScriptDefinitionId;
                PopulateEffectMemory(execution, context);
                context.ScryCards = scry.Viewed.Select(MemoryCard).ToArray();
                after = BindScript(((IScryEffectProvider)effects).BuildScry(context, scry.Instruction.AfterCallback), context.DefinitionId);
            }
            ValidatePlan(after);
            if (after.Any(s => s.OperationId.Equals("Scry", StringComparison.OrdinalIgnoreCase)))
                throw new FormatException("Scry continuations cannot start another Scry; use separate steps.");
            // Insert immediately after Scry, ahead of the source's remaining steps and queued triggers.
            execution.Steps = execution.Steps.Take(execution.Index).Concat(after).Concat(execution.Steps.Skip(execution.Index)).ToArray();
        }
    }
}
