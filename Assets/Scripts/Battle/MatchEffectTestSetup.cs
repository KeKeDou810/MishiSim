using System;
using System.Collections.Generic;
using System.Linq;

namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        // Fixture construction only. Never exposed as a command in a live network match.
        public static NetworkTestMatch FromEffectTest(BattleBoard board, IEnumerable<int> nodes, int hostHome, int guestHome,
            EffectTestScenario scenario, ICardEffectProvider provider, IEnumerable<int> offFieldZones)
        {
            if (scenario == null || provider == null || scenario.ActivePlayer < 0 || scenario.ActivePlayer > 1 || scenario.Turn < 1 ||
                !Enum.IsDefined(typeof(TestTurnPhase), scenario.Phase) || scenario.Cards == null || scenario.Cards.Length > 200 ||
                scenario.Cost == null || scenario.Cost.Length != 2 || scenario.Cost.Any(v => v < 0 || v > 11) ||
                scenario.Damage == null || scenario.Damage.Length != 2 || scenario.Damage.Any(v => v < 1 || v > 11) ||
                scenario.Clocks == null || scenario.Clocks.Length != 2) throw new ArgumentException("Invalid effect test scenario.");
            var match = new NetworkTestMatch(board, nodes, hostHome, guestHome, "fixture", "fixture");
            match.cards.Clear(); match.random = new Random(scenario.Seed);
            match.Turn = scenario.Turn; match.ActivePlayer = scenario.ActivePlayer; match.Phase = scenario.Phase;
            match.drawRules = new MatchDrawRules(0, 1, Array.Empty<TestTurnPhase>(), 120, 5, 20);
            match.RemainingTurnSeconds = 120;
            var offField = offFieldZones.ToArray();
            foreach (var item in scenario.Cards)
            {
                if (item == null || item.Owner < 0 || item.Owner > 1 || item.Count < 1 || item.Count > 100 ||
                    item.StackOrder < 0 || !Enum.IsDefined(typeof(TestCardZone), item.Zone) || item.Zone == TestCardZone.Removed || item.Zone == TestCardZone.Revealed)
                    throw new ArgumentException("Invalid test card.");
                var rule = provider.Rules(item.Id);
                if (item.Zone == TestCardZone.Player || rule.Type == "玩家卡") throw new ArgumentException("Players are derived from the contract pair.");
                if (rule.Type == "契约时魔" && item.Zone != TestCardZone.Board && item.Zone != TestCardZone.Contract ||
                    rule.Type == "决策卡" && item.Zone == TestCardZone.Board || rule.IsToken && item.Zone != TestCardZone.Board && item.Zone != TestCardZone.OffField)
                    throw new ArgumentException("Invalid zone for test card type: " + item.Id);
                int node = item.Node;
                if (item.Zone == TestCardZone.Board && rule.Type == "契约时魔" && node < 0) node = item.Owner == 0 ? hostHome : guestHome;
                if (item.Zone == TestCardZone.Board && (!match.nodes.Contains(node) || board.IsOpposingProtectedNode(node, item.Owner)) ||
                    item.Zone == TestCardZone.OffField && !offField.Contains(node)) throw new ArgumentException("Invalid test placement: " + item.Id);
                if ((item.Zone == TestCardZone.Board || item.Zone == TestCardZone.OffField) && item.Count != 1) throw new ArgumentException("Specify field stack members separately.");
                for (int i = 0; i < item.Count; i++) match.cards.Add(new Card(Guid.NewGuid(), item.Id, item.Owner, node, rule.Power,
                    rule.Type == "契约时魔", item.Zone, rule.Type == "决策卡", rule.Time) { Tapped = item.Tapped, StackOrder = item.StackOrder });
            }
            if (match.cards.Count > 400) throw new ArgumentException("Test scenario is too large.");
            foreach (int player in new[] { 0, 1 })
            {
                if (match.cards.Count(c => c.Owner == player && c.IsContract) != 1) throw new ArgumentException("Each test player needs exactly one contract.");
                if (!match.cards.Any(c => c.Owner == player && c.Zone == TestCardZone.Deck)) throw new ArgumentException("Each test player needs a nonempty deck.");
                if (scenario.Damage[player] >= 4) match.clocks[player].TakeDamage(scenario.Damage[player] - 4, false);
                else match.clocks[player].Heal(4 - scenario.Damage[player], false);
                match.clocks[player].AdjustCost(scenario.Cost[player] - 4);
            }
            foreach (var pile in match.cards.Where(c => c.Zone == TestCardZone.Board || c.Zone == TestCardZone.OffField).GroupBy(c => (c.Zone, c.NodeId)))
            {
                if (pile.Select(c => c.Owner).Distinct().Count() > 1 || pile.Select(c => c.StackOrder).Distinct().Count() != pile.Count()) throw new ArgumentException("Invalid mixed-owner or duplicate stack order.");
                int top = pile.Max(c => c.StackOrder);
                foreach (var card in pile) { card.Covered = card.Zone == TestCardZone.Board && card.StackOrder < top; if (card.StackOrder < top) card.Tapped = true; }
            }
            match.AttachEffects(provider, scenario.Clocks[0], scenario.Clocks[1], offField);
            if (match.Winner >= 0) throw new InvalidOperationException(match.ResultReason);
            match.AddLog(scenario.ActivePlayer, "效果实验室：载入预设局面，不触发初始摆放卡的登场效果。");
            return match;
        }
    }
}
