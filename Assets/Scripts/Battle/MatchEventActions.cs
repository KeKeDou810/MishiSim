using Mishi.Battle.Events;
using System.Linq;

namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        // Host pump: retain all phase rules/events, but only Main and Combat wait for a player action.
        public bool AdvanceAutomaticPhases()
        {
            bool changed = false;
            for (int i = 0; i < 16; i++)
            {
                if (OpeningPending || Winner >= 0 || effectChoice != null || responseAttack != null || occupationNode >= 0 ||
                    drainingEffects || executions.Count > 0 || eventContinuations.Count > 0 || automaticBatches.Count > 0 ||
                    orderingBatch != null || pendingAutomatic.Count > 0 || pendingEvents.Count > 0 ||
                    Phase == TestTurnPhase.Main || Phase == TestTurnPhase.Combat) break;
                if (Phase == TestTurnPhase.End) BeginNextTurn();
                else AdvancePhaseWithEvents(ActivePlayer);
                Revision++; changed = true;
            }
            return changed;
        }
        private void AdvancePhaseWithEvents(int player)
        {
            Publish(new PhaseEndedEvent(player, Turn, Phase));
            eventContinuations.Enqueue(() => {
                do { Phase++; } while (drawRules != null && drawRules.Skip(Turn, Phase));
                Publish(new PhaseStartedEvent(player, Turn, Phase));
                if (Phase == TestTurnPhase.End) RunSchedules("turnEnd", player);
                if (Phase == TestTurnPhase.Main) RunSchedules("nextOwnMain", player);
                eventContinuations.Enqueue(() => ApplyPhaseRules(player));
            });
            DrainEffects();
        }
        private void ApplyPhaseRules(int player)
        {
            if (Phase == TestTurnPhase.TimeReset)
            {
                int before = clocks[player].RemainingTime;
                clocks[player].ReconstructTime(); PublishCost(player, before, "timeReset");
            }
            if (Phase == TestTurnPhase.Rebuild)
                foreach (var unit in cards.Where(c => c.Zone == TestCardZone.Board && c.Owner == player && !c.Covered)) { if (unit.Tapped) unit.LastRebuiltTurn = Turn; unit.Tapped = false; }
            lastAction = Phase == TestTurnPhase.Rebuild ? "Your units untapped." : "Phase: " + Phase;
            if (Phase == TestTurnPhase.Draw && drawRules != null)
            {
                int drawn = Draw(player, drawRules.DrawPerTurn);
                lastAction = $"Drew {drawn} card(s).";
                if (drawn < drawRules.DrawPerTurn) lastAction += " 牌库已空，无法继续抽卡。";
            }
        }
        private void PublishCardAction(Card card, EffectEvent kind, string reason)
        {
            if (effects == null) return;
            var subject = EventCard(card, true);
            if (kind == EffectEvent.Summoned)
            { PublishMove(card, TestCardZone.Hand, reason); Publish(new SummonedEvent(subject, TestCardZone.Hand, card.Zone, reason, EventCard(eventCause))); }
            else if (kind == EffectEvent.Played)
            { PublishMove(card, TestCardZone.Hand, reason); Publish(new PlayedEvent(subject, TestCardZone.Hand, card.Zone, reason, subject)); }
            else if (kind == EffectEvent.Activated) Publish(new ActivatedEvent(subject, card.Zone, card.Zone, reason, subject));
        }
        private void PublishCost(int player, int before, string reason, Card source = null)
        {
            int after = clocks[player].RemainingTime;
            if (before != after) Publish(new CostChangedEvent(player, after - before, before, after, reason, EventCard(source ?? eventCause)));
        }
        private bool PayTime(int player, int amount, Card source)
        {
            int before = clocks[player].RemainingTime;
            if (!clocks[player].Pay(amount)) return false;
            PublishCost(player, before, "payment", source); return true;
        }
        private void HealPlayer(int player, int amount, bool moveCost, int minimumDamage)
        {
            int before = clocks[player].DamagePointer, cost = clocks[player].RemainingTime;
            clocks[player].Heal(amount, moveCost, minimumDamage);
            int after = clocks[player].DamagePointer;
            if (before != after) Publish(new HealedEvent(player, before - after, before, after, "effect", EventCard(eventCause)));
            PublishCost(player, cost, "heal");
        }
        private void ChangeCost(int player, int amount, int maximum)
        {
            int before = clocks[player].RemainingTime;
            clocks[player].AdjustCost(amount, maximum); PublishCost(player, before, "effect");
        }
        private void ApplyDamage(int player, int amount, bool moveCost, string reason, Card source = null)
        {
            if (Winner >= 0) return;
            int before = clocks[player].DamagePointer, cost = clocks[player].RemainingTime;
            clocks[player].TakeDamage(amount, moveCost);
            int after = clocks[player].DamagePointer;
            if (before != after) Publish(new DamageTakenEvent(player, after - before, before, after, reason, EventCard(source ?? eventCause)));
            PublishCost(player, cost, "damage", source); CheckResult(player);
        }
        private void QueueAttack(Card attacker, Card target, int node, int defender)
        {
            redirectedTarget = null;
            attacker.Tapped = true;
            declaredCombat = new ForesightCombat { Attacker = attacker, Target = target, Node = node, Defender = defender,
                AttackerGeneration = attacker.FieldGeneration, TargetGeneration = target?.FieldGeneration ?? 0,
                Remaining = ForesightRules.MaximumChecks(effects?.Rules(attacker.DefinitionId).ForesightCount ?? 0, 1) };
            int generation = attacker.FieldGeneration, targetGeneration = target?.FieldGeneration ?? 0;
            if (effects != null) Publish(new AttackDeclaredEvent(EventCard(attacker, true), EventCard(target, true), defender, node));
            eventContinuations.Enqueue(() => {
                if (redirectedTarget != null) { target = redirectedTarget; node = target.NodeId; targetGeneration = target.FieldGeneration; redirectedTarget = null; }
                if (attacker.FieldGeneration != generation || attacker.Zone != TestCardZone.Board || attacker.Covered || target == null && !PlayerIsExposed(defender) || target != null && (target.FieldGeneration != targetGeneration || target.Zone != TestCardZone.Board || target.Covered || target.NodeId != node))
                { if (declaredCombat != null) EndCombat(declaredCombat, true); return; }
                if (drawRules != null && drawRules.ResponseTimeSeconds > 0) StartResponse(attacker, target, node, defender);
                else BeginForesightCombat(attacker, target, node, defender);
            });
            DrainEffects();
        }
    }
}
