using System;
using System.Collections.Generic;
using System.Linq;
using Mishi.Battle;

namespace Mishi.Networking
{
    // Runs on each client using its snapshot and local Lua definitions. No host capability query.
    public static class BattleMenuPolicy
    {
        public static CardActions Evaluate(NetworkSnapshot state, NetworkCardInfo card,
            IReadOnlyDictionary<string, CardDefinition> definitions, ICardEffectProvider effects,
            BattleBoard board, IEnumerable<int> nodes)
        {
            if (state.Spectator || card.Owner != state.You || state.Winner >= 0 || state.ChoicePlayer >= 0 ||
                state.OccupationNode >= 0 || card.Covered || card.FaceDown) return CardActions.None;
            var definition = definitions[card.DefinitionId];
            var rule = effects.Rules(card.DefinitionId);
            var zone = (TestCardZone)card.Zone;
            var phase = (TestTurnPhase)state.Phase;
            int cost = state.CostPointers[card.Owner] == 12 ? 0 : state.CostPointers[card.Owner];
            var field = state.Board.Concat(state.PublicPiles).Where(c => !c.FaceDown && !string.IsNullOrEmpty(c.DefinitionId)).ToArray();
            var context = new EffectContext {
                SourceZone = (TestCardZone)card.Zone,
                InstanceId = card.InstanceId, Time = card.Time, Power = card.Power,
                DefinitionId = card.DefinitionId, Owner = card.Owner, ActivePlayer = state.ActivePlayer,
                Turn = state.Turn, Cost = cost, Damage = state.DamagePointers[card.Owner],
                Clock = (ClockKind)state.ClockKinds[card.Owner], HandCount = state.OwnHand.Length,
                OpponentClock = (ClockKind)state.ClockKinds[1 - card.Owner], OpponentCost = state.CostPointers[1 - card.Owner] == 12 ? 0 : state.CostPointers[1 - card.Owner],
                OpponentDamage = state.DamagePointers[1 - card.Owner], OpponentHandCount = state.OpponentHandCount,
                PublicCards = field.Concat(state.PlayerCards ?? Array.Empty<NetworkCardInfo>()).Where(c => !c.FaceDown && definitions.ContainsKey(c.DefinitionId)).Select(c => new ScryCardInfo {
                    InstanceId = c.InstanceId, DefinitionId = c.DefinitionId, Owner = c.Owner, Node = c.NodeId, Zone = (TestCardZone)c.Zone, ZoneEnteredTurn = c.ZoneEnteredTurn,
                    Name = definitions[c.DefinitionId].Name, Type = definitions[c.DefinitionId].Type, Race = definitions[c.DefinitionId].Race, Sign = definitions[c.DefinitionId].Sign, Time = c.Time, Power = c.Power }).ToArray(),
                EmptyBoardCount = nodes.Count(n => !board.IsOpposingProtectedNode(n, card.Owner) && !state.Board.Any(c => c.NodeId == n)),
                ContractZoneCount = field.Count(c => c.Owner == card.Owner && definitions[c.DefinitionId].IsContract && (TestCardZone)c.Zone == TestCardZone.Contract),
                ContractName = field.Where(c => c.Owner == card.Owner && definitions[c.DefinitionId].IsContract &&
                    ((TestCardZone)c.Zone == TestCardZone.Board || (TestCardZone)c.Zone == TestCardZone.Contract))
                    .Select(c => definitions[c.DefinitionId].Name).FirstOrDefault() ?? "",
                OwnFieldNameCounts = field.Where(c => c.Owner == card.Owner &&
                    ((TestCardZone)c.Zone == TestCardZone.Board || (TestCardZone)c.Zone == TestCardZone.OffField))
                    .GroupBy(c => definitions[c.DefinitionId].Name).ToDictionary(g => g.Key, g => g.Count())
            };
            context.Variables = VariableContexts.FromSnapshots((state.Variables ?? Array.Empty<NetworkVariableInfo>()).Select(NetworkVariables.Decode),
                state.You, Guid.TryParse(card.InstanceId, out var sourceId) ? sourceId : Guid.Empty);
            bool Condition(EffectEvent kind)
            {
                context.Event = kind;
                try { return kind == EffectEvent.Played ? effects.CanPlay(context) : effects.CanActivate(context); }
                catch { return false; }
            }
            bool Empty(int node) => !board.IsOpposingProtectedNode(node, card.Owner) && !state.Board.Any(c => c.NodeId == node);
            bool CanPayCards(ActivationCost[] costs, bool playing)
            {
                var resources = field.Concat(state.OwnHand).Concat(state.PlayerCards ?? Array.Empty<NetworkCardInfo>())
                    .Where(c => c.Owner == card.Owner && !c.FaceDown && !c.Covered && (!playing || c.InstanceId != card.InstanceId)).ToArray();
                return costs.Where(c => c.Kind != ActivationCostKind.Time).All(component => resources.Count(c => {
                    if (!definitions.TryGetValue(c.DefinitionId, out var d)) return false;
                    var resourceZone = (TestCardZone)c.Zone;
                    if (component.Kind == ActivationCostKind.MoveSelf ? c.InstanceId != card.InstanceId :
                        component.Zones.Length > 0 ? !component.Zones.Contains(resourceZone) : resourceZone != (component.Zone ?? (component.Kind == ActivationCostKind.Discard ? TestCardZone.Hand : TestCardZone.Board))) return false;
                    if (component.Kind == ActivationCostKind.Destroy && resourceZone != TestCardZone.Board) return false;
                    if ((component.Kind == ActivationCostKind.RemoveToken || component.Kind == ActivationCostKind.TokenTime) && d.Type != "衍生物") return false;
                    if (component.Kind == ActivationCostKind.TokenTime && c.Time < component.Minimum) return false;
                    if (!string.IsNullOrEmpty(component.Type) && component.Type != d.Type || !string.IsNullOrEmpty(component.Race) && component.Race != d.Race) return false;
                    return string.IsNullOrEmpty(component.Name) || (component.NameMatch == "fuzzy" ? d.Name.IndexOf(component.Name, StringComparison.OrdinalIgnoreCase) >= 0 : d.Name == component.Name);
                }) >= (component.Kind == ActivationCostKind.TokenTime ? 1 : component.Minimum));
            }
            CardActions result = CardActions.None;
            bool ownTurn = state.ActivePlayer == card.Owner;
            bool decisionWindow = state.ResponsePlayer >= 0 ? state.ResponsePlayer == card.Owner : ownTurn && phase == TestTurnPhase.Main;
            if (!card.NameEffectsBlocked && definition.IsDecision && zone == TestCardZone.Hand && decisionWindow && Math.Max(0, card.Time + card.PlayCostAdjustment) + rule.PlayCosts.Where(c => c.Kind == ActivationCostKind.Time).Sum(c => c.Minimum) <= cost &&
                CanPayCards(rule.PlayCosts, true) &&
                (!ownTurn || state.DecisionsUsed == 0) && rule.HasPlay && (!rule.OwnTurnOnly || ownTurn) &&
                (!rule.OncePerTurn || card.PlayUses == 0) && (!rule.OncePerNamePerTurn || card.NamedPlayUses == 0) && Condition(EffectEvent.Played))
                result |= CardActions.Decision;
            if (!ownTurn || state.ResponsePlayer >= 0) return result;
            if (phase == TestTurnPhase.Main)
            {
                if (!card.NameEffectsBlocked && !card.EffectsSuppressed && rule.HasActivate && zone == rule.ActivateZone && cost >= rule.ActivateCost + rule.ActivationCosts.Where(c => c.Kind == ActivationCostKind.Time).Sum(c => c.Minimum) &&
                    CanPayCards(rule.ActivationCosts, false) &&
                    (!rule.OncePerTurn || card.ActivationUses == 0) && (!rule.OncePerNamePerTurn || card.NamedActivationUses == 0) && Condition(EffectEvent.Activated))
                    result |= CardActions.Activate;
                if (zone == TestCardZone.Hand && !definition.IsPlayer && !definition.IsDecision && !definition.IsContract)
                {
                    if (card.Time <= cost && nodes.Any(Empty)) result |= CardActions.Summon;
                    if (state.Board.Any(c => c.Owner == card.Owner && !c.Covered && !board.IsOpposingProtectedNode(c.NodeId, card.Owner) &&
                        card.Time > c.Time && card.Time - c.Time <= cost)) result |= CardActions.Overclock;
                }
                if (zone == TestCardZone.Board && definition.IsContract && !state.ContractMoved &&
                    nodes.Any(n => board.AreAdjacent(card.NodeId, n) && Empty(n))) result |= CardActions.Move;
            }
            if (phase == TestTurnPhase.Combat && zone == TestCardZone.Board && !card.Tapped && !definition.IsPlayer && !definition.IsDecision)
            {
                bool unit = state.Board.Any(c => c.Owner != card.Owner && !c.Covered && (card.EffectsSuppressed || c.Time <= rule.AttackTimeLimit) && board.InRange(card.NodeId, c.NodeId, card.AttackRange));
                bool player = nodes.Any(n => board.PlayerAt(n) == 1 - card.Owner && board.InRange(card.NodeId, n, card.AttackRange)) &&
                    !(state.PlayerCards ?? Array.Empty<NetworkCardInfo>()).Any(c => c.Owner != card.Owner && !string.IsNullOrEmpty(c.AttachedTo)) &&
                    !state.Board.Any(c => c.Owner != card.Owner && board.IsProtectedBy(c.NodeId, c.Owner));
                if (unit || player) result |= CardActions.Attack;
            }
            return result;
        }
    }
}
