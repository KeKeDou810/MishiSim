using System;
using System.Collections.Generic;
using System.Linq;

namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        private void MoveEffectCard(Card target, TestCardZone destination, string reason = "effect", bool deferContractDraw = false)
        {
            string deckPosition = target.Zone == TestCardZone.Deck && cards.LastOrDefault(c => c.Zone == TestCardZone.Deck && c.Owner == target.Owner) == target ? "bottom" : null;
            var observers = eventListenersBefore ?? CaptureListeners();
            var moving = target.Zone == TestCardZone.Board
                ? cards.Where(c => c.Zone == TestCardZone.Board && c.NodeId == target.NodeId).OrderByDescending(c => c.StackOrder).ToArray()
                : new[] { target };
            var previous = moving.ToDictionary(c => c.Id, c => c.Zone);
            var contracts = moving.Where(c => c.IsContract && c.Zone == TestCardZone.Board).ToArray();
            foreach (var member in moving)
            {
                bool contractLeft = contracts.Contains(member);
                var requested = member != moving[0] && (destination == TestCardZone.Hand || destination == TestCardZone.Deck)
                    ? TestCardZone.Discard : destination;
                var to = member.IsToken && requested != TestCardZone.Board && requested != TestCardZone.OffField ? TestCardZone.Removed : contractLeft ? TestCardZone.Contract : requested;
                if (member.Zone == TestCardZone.Board || (member.Zone == TestCardZone.OffField) != (to == TestCardZone.OffField)) ResetFieldInstance(member);
                if (to != TestCardZone.Board && to != TestCardZone.OffField) { member.Owner = member.OriginalOwner; member.HiddenAttachment = false; }
                member.Zone = to; member.NodeId = -1; member.Modifiers.Clear(); member.Tapped = false;
            }
            // All members have left before listeners or contract draws observe the new state.
            foreach (var member in moving) PublishMove(member, previous[member.Id], reason, observers, member == target ? deckPosition : null);
            foreach (var contract in contracts) { playerFlipped[contract.Owner] = !playerFlipped[contract.Owner]; if (drawRules != null && !deferContractDraw) Draw(contract.Owner, 1); }
        }
        private void DestroyPile(Card target, string reason, bool allowReplacement = true)
        {
            if (allowReplacement && TryReplaceDestruction(target, reason)) return;
            if (IsDestructionPrevented(target, reason)) return;
            var observers = eventListenersBefore ?? CaptureListeners();
            var destroyed = cards.Where(c => c.Zone == TestCardZone.Board && c.NodeId == target.NodeId).ToArray();
            var active = new HashSet<Guid>(destroyed.Where(EffectsActive).Select(c => c.Id));
            foreach (var member in destroyed)
            {
                ResetFieldInstance(member);
                member.Owner = member.OriginalOwner; member.HiddenAttachment = false;
                member.Zone = member.IsToken ? TestCardZone.Removed : member.IsContract ? TestCardZone.Contract : TestCardZone.Discard;
                member.NodeId = -1; member.Covered = false; member.StackOrder = 0; member.Tapped = false; member.Modifiers.Clear();
            }
            foreach (var member in destroyed)
            {
                if (active.Contains(member.Id)) QueueEffect(member, EffectEvent.Destroyed, reason);
                PublishMove(member, TestCardZone.Board, reason, observers);
                if (effects != null) Publish(new Events.DestroyedEvent(EventCard(member, true), TestCardZone.Board, member.Zone, reason, EventCard(eventCause)), observers);
            }
            if (destroyed.Any(c => c.IsContract)) { playerFlipped[target.Owner] = !playerFlipped[target.Owner]; if (drawRules != null) Draw(target.Owner, 1); }
            else if (drawRules != null && effects == null) RecycleEmptyDeck(target.Owner);
        }
        private int PlacementOwner(EffectExecution execution) => execution.PendingSpawn != null
            ? (execution.PendingSpawn.Side == "opponent" ? 1 - execution.Source.Owner : execution.Source.Owner)
            : execution.PendingMoves.Peek().Owner;
        private int[] AvailableOffFieldZones(int owner) => sharedOffFieldZones.Where(zone =>
            !cards.Any(c => c.Zone == TestCardZone.OffField && c.NodeId == zone && c.Owner != owner)).ToArray();
        private void RequestPlacement(EffectExecution execution)
        {
            while (execution.PendingSpawn != null || execution.PendingMoves.Count > 0)
            {
                var available = AvailableOffFieldZones(PlacementOwner(execution));
                if (available.Length > 0)
                {
                    effectChoice = new EffectChoice { Player = execution.Source.Owner, Prompt = "选择卡片放置的场外区",
                        ZoneCandidates = available, Seconds = drawRules?.ResponseTimeSeconds > 0 ? drawRules.ResponseTimeSeconds : 20 };
                    return;
                }
                // No legal destination: do not create a token or move the existing card.
                lastAction = "没有可用的场外区，无法完成这次放置。";
                if (execution.PendingSpawn != null) execution.PendingSpawn = null;
                else execution.PendingMoves.Dequeue();
            }
        }
        private void ResolveZoneChoice(int zone)
        {
            if (effectChoice.IsBoardPlacement) { ResolveSummonPlacement(zone); return; }
            var execution = executions.Peek();
            Card placed;
            if (execution.PendingSpawn != null)
            {
                var step = execution.PendingSpawn;
                var rule = effects.Rules(step.DefinitionId);
                placed = new Card(Guid.NewGuid(), step.DefinitionId, PlacementOwner(execution), zone, rule.Power, false, TestCardZone.OffField, false, rule.Time) { IsToken = true };
                if (step.Amount != 0) placed.Modifiers.Add(new StatModifier { Source = execution.Source.Id, Stat = "time", Amount = step.Amount, ExpiresAfterTurn = int.MaxValue });
                cards.Add(placed); execution.PendingSpawn = null;
                if (effects != null) Publish(new Events.SummonedEvent(EventCard(placed, true), TestCardZone.Removed, placed.Zone, "spawn", EventCard(execution.Source)));
            }
            else
            {
                placed = execution.PendingMoves.Dequeue();
                var members = placed.Zone == TestCardZone.Board ? cards.Where(c => c.Zone == TestCardZone.Board && c.NodeId == placed.NodeId).OrderBy(c => c.StackOrder).ToArray() : new[] { placed };
                int order = cards.Where(c => c.Zone == TestCardZone.OffField && c.NodeId == zone).Select(c => c.StackOrder).DefaultIfEmpty(-1).Max();
                MoveEffectCard(placed, TestCardZone.OffField);
                foreach (var member in members.Where(c => c.Zone == TestCardZone.OffField)) { member.NodeId = zone; member.StackOrder = ++order; member.Tapped = true; }
            }
            if (placed.Zone == TestCardZone.OffField)
            {
                var lower = cards.Where(c => c != placed && c.Zone == TestCardZone.OffField && c.NodeId == zone).ToArray();
                foreach (var card in lower) card.Tapped = true;
                placed.StackOrder = lower.Length == 0 ? 0 : lower.Max(c => c.StackOrder) + 1;
                placed.Tapped = false;
            }
            effectChoice = null;
            RequestPlacement(execution);
            DrainEffects(); RefreshStats();
        }
    }
}
