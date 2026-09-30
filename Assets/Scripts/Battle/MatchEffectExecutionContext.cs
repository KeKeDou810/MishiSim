using System;
using System.Collections.Generic;
using System.Linq;
using Mishi.Battle.Effects;

namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        // Adapter from isolated effect strategies to the match's invariant-preserving operations.
        private sealed class MatchEffectExecutionContext : EffectExecutionContext
        {
            private readonly NetworkTestMatch match;
            private readonly EffectExecution execution;
            private readonly string operation;
            public override Card Source => execution.Source;
            public override IReadOnlyList<Card> Targets { get; }
            public override int Player { get; }
            public override int Turn => match.Turn;
            public override void Pay(EffectInstruction step) => match.Pay(execution, step);
            public override void ChangeDestructionReason(EffectInstruction step) => match.ChangeDestructionReason(execution, step);
            public override void BlockName(EffectInstruction step) => match.BlockName(execution, step);
            public override void ProtectSelection(EffectInstruction step) => match.ProtectSelection(execution, step);
            public MatchEffectExecutionContext(NetworkTestMatch match, EffectExecution execution, EffectInstruction step)
            {
                this.match = match; this.execution = execution; operation = step.OperationId;
                Targets = match.ResolveEffectTargets(execution, step);
                if (new[] { "Destroy", "Modify", "Move", "Ready", "Tap", "Summon", "SuppressEffects", "ReturnToDeck", "ProtectFromEnemyUnitEffects", "PreventDestruction", "ProtectSelection", "GrantTrigger" }.Contains(step.OperationId))
                {
                    foreach (var target in Targets) execution.Affected[target.Id] = match.EventCard(target);
                    if (execution.ResolvedMark != null && Targets.Any(c => c.Zone == TestCardZone.Board))
                    {
                        var applied = step.Copy(); applied.Target = "set"; applied.FromSet = "copiedMarkTargets"; applied.Except = null;
                        execution.AppliedMarkSteps.Add(applied);
                    }
                }
                Player = step.Side == "opponent" ? 1 - Source.Owner : Source.Owner;
            }
            public override void WriteValue(EffectInstruction instruction, bool add) => match.WriteVariable(execution, instruction, Targets, add);
            public override void ClearValue(EffectInstruction instruction) => match.ClearVariable(execution, instruction, Targets);
            public override void DeclareCardName(EffectInstruction step) => match.BeginDeclaration(execution, step);
            public override void RememberCards(string key, IReadOnlyList<Card> targets) => SaveSet(execution, key, targets.Select(c => c.Id).ToArray());
            public override void ReturnToDeck(IReadOnlyList<Card> targets, string position) => match.BeginDeckReturn(execution, targets, position);
            public override void Scry(EffectInstruction step) => match.BeginScry(execution, step);
            public override void Choose(EffectInstruction step) => match.BeginSelection(execution, step, false);
            public override void Draw(int player, int amount) => match.Draw(player, amount);
            public override void Damage(int player, int amount, bool moveCost) => match.ApplyDamage(player, amount, moveCost, "effect", Source);
            public override void Heal(int player, int amount, bool moveCost, int minimumDamage) => match.HealPlayer(player, amount, moveCost, minimumDamage);
            public override void AdjustCost(int player, int amount, int maximumCost) => match.ChangeCost(player, amount, maximumCost);
            public override void Destroy(Card card) => match.RequestDestruction(card, "effect");
            public override void Move(Card card, TestCardZone destination)
            {
                if (destination == TestCardZone.OffField) throw new InvalidOperationException("Use PlaceOffField to validate occupancy and select a position.");
                match.MoveEffectCard(card, destination, operation == "Discard" ? "discard" : "effect");
            }
            public override void PlaceOffField(IReadOnlyList<Card> targets)
            {
                execution.PendingMoves = new Queue<Card>(targets.Where(c => c.Zone != TestCardZone.Removed));
                match.RequestPlacement(execution);
            }
            public override void Spawn(EffectInstruction instruction)
            {
                execution.PendingSpawn = instruction;
                match.RequestPlacement(execution);
            }
            public override void Summon(IReadOnlyList<Card> targets) => match.BeginEffectSummon(execution, targets);
            public override void Summon(IReadOnlyList<Card> targets, string placement) => match.BeginEffectSummon(execution, targets, placement);
            public override void Schedule(EffectInstruction instruction) => match.ScheduleEffect(execution, instruction, Targets);
            public override void AttachPlayers() => match.AttachPlayerCards(Source);
            public override void RollDice(EffectInstruction step) => match.RollDice(execution, step);
            public override void Continue(EffectInstruction step) => match.Continue(execution, step);
            public override void QueryCards(EffectInstruction step) => match.QueryCards(execution, step);
            public override void InvokeDecision(EffectInstruction step) => match.InvokeDecision(execution, step);
            public override void SpawnBoard(EffectInstruction step) => match.SpawnBoard(execution, step);
            public override void RemoveToken(EffectInstruction step) => match.RemoveToken(execution, step);
            public override void PreventDestruction(EffectInstruction step) => match.PreventDestruction(execution, step);
            public override void RedirectAttack(EffectInstruction step) => match.RedirectAttack(execution, step);
            public override void ChooseHiddenHand(EffectInstruction step) => match.ChooseHiddenHand(execution, step);
            public override void GrantTrigger(EffectInstruction step) => match.GrantTrigger(execution, step);
            public override void CopyForesight(EffectInstruction step) => match.CopyForesight(execution, step);
            public override void ReplaceDestruction(EffectInstruction step) => match.ReplaceDestruction(execution, step);
            public override void ModifyDecisionCost(EffectInstruction step) => match.ModifyDecisionCost(execution, step);
            public override void AttachUnder(EffectInstruction step) => match.AttachUnder(execution, step);
            public override void Shuffle(int player) => match.ShuffleDeck(player);
            public override void Reveal(Card card) => match.Present(card, CardPresentationKind.Reveal);
            public override void ModifyRevealTime(int amount) => match.revealTimeAdjustments.Add((Source.Owner, Turn, amount));
            public override void Ready(Card card) { card.Tapped = false; card.LastRebuiltTurn = card.LastEffectRebuiltTurn = Turn; }
            public override void SuppressEffects(Card card) { card.SuppressedUntilTurn = Turn; }
            public override void ProtectFromEnemyUnitEffects(Card card)
            { card.ProtectedAgainstPlayer = 1 - Source.Owner; card.ProtectedUntilTurn = Turn + (match.ActivePlayer == Source.Owner ? 1 : 2); }
            public override void ModifyBattleDamage(int amount, IReadOnlyList<Card> targets)
            {
                var combat = match.foresightCombat ?? match.declaredCombat;
                if (combat != null && targets.Contains(combat.Attacker)) ModifyBattleDamage(amount);
            }
            public override void ModifyBattleDamage(int amount)
            {
                var combat = match.foresightCombat ?? match.declaredCombat;
                if (combat != null) combat.Damage = Math.Max(0, combat.Damage + amount);
            }
        }
    }
}
