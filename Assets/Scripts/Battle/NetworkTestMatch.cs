using System;
using System.Collections.Generic;
using System.Linq;

namespace Mishi.Battle
{
    public enum TestCommandKind { MoveContract, EndTurn, NextPhase, Summon, Attack, Occupy, Stay, Overclock }
    public enum TestTurnPhase { TurnStart, Draw, Rebuild, TimeReset, Main, Combat, End }
    public enum TestCardZone { Board, Hand, Discard, Contract, Deck }

    // Authoritative action test. Costs, decisions, foresight and Lua effects are deliberately disabled.
    public sealed class NetworkTestMatch
    {
        public sealed class Card
        {
            public Guid Id { get; }
            public string DefinitionId { get; }
            public int Owner { get; }
            public int Power { get; }
            public bool IsContract { get; }
            public bool IsDecision { get; }
            public int NodeId { get; internal set; }
            public bool Tapped { get; internal set; }
            public TestCardZone Zone { get; internal set; }
            public Card(Guid id, string definitionId, int owner, int nodeId, int power = 0,
                bool contract = false, TestCardZone zone = TestCardZone.Board, bool decision = false)
            { Id = id; DefinitionId = definitionId; Owner = owner; NodeId = nodeId; Power = power; IsContract = contract; Zone = zone; IsDecision = decision; }
            internal Card Copy() => new Card(Id, DefinitionId, Owner, NodeId, Power, IsContract, Zone, IsDecision) { Tapped = Tapped };
        }
        public sealed class View
        {
            public Guid MatchId;
            public int Revision, ActivePlayer, Turn;
            public TestTurnPhase Phase;
            public bool ContractMoved;
            public Card[] Board, OwnHand, PublicPiles;
            public int OpponentHandCount;
            public int[] DeckCounts;
            public int OccupationNode;
            public string LastAction;
        }
        public Guid MatchId { get; } = Guid.NewGuid();
        public int Revision { get; private set; }
        public int ActivePlayer { get; private set; }
        public int Turn { get; private set; } = 1;
        public TestTurnPhase Phase { get; private set; } = TestTurnPhase.TurnStart;
        public bool ContractMoved { get; private set; }
        private readonly BattleBoard board;
        private readonly HashSet<int> nodes;
        private readonly List<Card> cards = new List<Card>();
        private readonly long[] lastSequence = new long[2];
        private Guid occupationCard;
        private int occupationNode = -1;
        private string lastAction = "Action test: costs / draws / Lua effects disabled.";
        private MatchDrawRules drawRules;
        public bool OpeningPending { get; private set; }

        // Input is already validated by the room against its selected external mode.
        public static NetworkTestMatch FromDecks(BattleBoard board, IEnumerable<int> nodes, int hostStart, int guestStart,
            Card[][] decks, MatchDrawRules rules, Random random)
        {
            if (decks == null || decks.Length != 2 || rules == null || random == null) throw new ArgumentException("Invalid match setup.");
            var match = new NetworkTestMatch(board, nodes, hostStart, guestStart, "fixture", "fixture");
            match.cards.Clear(); match.drawRules = rules;
            for (int player = 0; player < 2; player++)
            {
                if (decks[player] == null || decks[player].Count(c => c.IsContract) != 1 || decks[player].Length <= rules.OpeningHand)
                    throw new ArgumentException("Deck needs one contract and enough cards for the opening hand.");
                var contract = decks[player].Single(c => c.IsContract);
                match.cards.Add(new Card(Guid.NewGuid(), contract.DefinitionId, player, player == 0 ? hostStart : guestStart, contract.Power, true));
                var pile = decks[player].Where(c => !c.IsContract).ToArray();
                for (int i = pile.Length - 1; i > 0; i--)
                { int j = random.Next(i + 1); var temp = pile[i]; pile[i] = pile[j]; pile[j] = temp; }
                foreach (var card in pile)
                    match.cards.Add(new Card(Guid.NewGuid(), card.DefinitionId, player, -1, card.Power, false, TestCardZone.Deck, card.IsDecision));
            }
            match.OpeningPending = true;
            match.lastAction = "Decks ready; dealing opening hands...";
            return match;
        }
        public void DealOpeningHands()
        {
            if (!OpeningPending) return;
            Draw(0, drawRules.OpeningHand); Draw(1, drawRules.OpeningHand);
            OpeningPending = false; Revision++;
            lastAction = "Opening hands dealt: " + drawRules.OpeningHand + " each.";
        }
        private int Draw(int player, int count)
        {
            var drawn = cards.Where(c => c.Owner == player && c.Zone == TestCardZone.Deck).Take(count).ToArray();
            foreach (var card in drawn) card.Zone = TestCardZone.Hand;
            return drawn.Length;
        }

        public NetworkTestMatch(BattleBoard board, IEnumerable<int> nodes, int hostStart, int guestStart,
            string contractDefinition, string handDefinition, int contractPower = 1000, int handPower = 4000)
        {
            this.board = board ?? throw new ArgumentNullException(nameof(board));
            this.nodes = new HashSet<int>(nodes);
            if (hostStart == guestStart || !this.nodes.Contains(hostStart) || !this.nodes.Contains(guestStart))
                throw new ArgumentException("Invalid starting nodes.");
            cards.Add(new Card(Guid.NewGuid(), contractDefinition, 0, hostStart, contractPower, true));
            cards.Add(new Card(Guid.NewGuid(), contractDefinition, 1, guestStart, contractPower, true));
            for (int player = 0; player < 2; player++)
                for (int i = 0; i < 4; i++)
                    cards.Add(new Card(Guid.NewGuid(), handDefinition, player, -1, handPower, false, TestCardZone.Hand));
        }
        public bool TryCommand(int player, Guid matchId, long sequence, int expectedRevision,
            TestCommandKind kind, Guid cardId, int targetNode, out string error)
        {
            error = null;
            if (player < 0 || player > 1) return Fail("Unknown player.", out error);
            if (matchId != MatchId) return Fail("Old match request.", out error);
            if (sequence <= lastSequence[player]) return Fail("Duplicate request.", out error);
            lastSequence[player] = sequence;
            if (expectedRevision != Revision) return Fail("State changed; try again.", out error);
            if (OpeningPending) return Fail("Wait for opening hands.", out error);
            if (player != ActivePlayer) return Fail("Not your turn.", out error);
            if (occupationNode >= 0)
            {
                if (kind != TestCommandKind.Occupy && kind != TestCommandKind.Stay)
                    return Fail("Choose Occupy or Stay before another action.", out error);
                var attacker = cards.Single(c => c.Id == occupationCard);
                if (kind == TestCommandKind.Occupy) attacker.NodeId = occupationNode;
                lastAction = kind == TestCommandKind.Occupy ? "Attacker occupied the defeated unit's node." : "Attacker stayed in place.";
                occupationNode = -1; occupationCard = Guid.Empty;
                Revision++; return true;
            }
            if (kind == TestCommandKind.NextPhase)
            {
                if (Phase == TestTurnPhase.End) return Fail("Use End Turn in the End phase.", out error);
                do { Phase++; } while (drawRules != null && drawRules.Skip(Turn, Phase));
                if (Phase == TestTurnPhase.Rebuild)
                    foreach (var unit in cards.Where(c => c.Zone == TestCardZone.Board && c.Owner == player)) unit.Tapped = false;
                lastAction = Phase == TestTurnPhase.Rebuild ? "Your units untapped." : "Phase: " + Phase;
                if (Phase == TestTurnPhase.Draw && drawRules != null)
                {
                    int drawn = Draw(player, drawRules.DrawPerTurn);
                    lastAction = $"Drew {drawn} card(s).";
                    if (drawn < drawRules.DrawPerTurn) lastAction += " Deck empty; recycling/damage is not implemented in this test.";
                }
                Revision++; return true;
            }
            if (kind == TestCommandKind.EndTurn)
            {
                if (Phase != TestTurnPhase.End) return Fail("Advance to End phase first.", out error);
                ActivePlayer = 1 - ActivePlayer; Turn++; Phase = TestTurnPhase.TurnStart; ContractMoved = false;
                lastAction = "Turn passed to P" + (ActivePlayer + 1); Revision++; return true;
            }
            if (kind == TestCommandKind.Overclock)
            {
                if (!ValidateOverclock(player, cardId, targetNode, out _, out error)) return false;
                return Fail("Overclock entry reserved; stacking and costs are not implemented yet.", out error);
            }
            var card = cards.FirstOrDefault(c => c.Id == cardId);
            if (card == null || card.Owner != player) return Fail("You do not control this card.", out error);
            if (kind == TestCommandKind.Summon)
            {
                if (Phase != TestTurnPhase.Main) return Fail("Summon only in Main phase.", out error);
                if (card.Zone != TestCardZone.Hand || card.IsContract || card.IsDecision) return Fail("Select a normal unit in your hand.", out error);
                if (!ValidEmptyNode(player, targetNode, out error)) return false;
                card.Zone = TestCardZone.Board; card.NodeId = targetNode; card.Tapped = false;
                lastAction = "Summoned " + card.DefinitionId + " (no cost).";
            }
            else if (kind == TestCommandKind.MoveContract)
            {
                if (Phase != TestTurnPhase.Main) return Fail("Move only in Main phase.", out error);
                if (card.Zone != TestCardZone.Board || !card.IsContract) return Fail("Only a contract on the board can move.", out error);
                if (ContractMoved) return Fail("Contract already moved this turn.", out error);
                if (!board.AreAdjacent(card.NodeId, targetNode)) return Fail("Target must be adjacent.", out error);
                if (!ValidEmptyNode(player, targetNode, out error)) return false;
                card.NodeId = targetNode; ContractMoved = true;
                lastAction = "Contract moved.";
            }
            else if (kind == TestCommandKind.Attack)
            {
                if (Phase != TestTurnPhase.Combat) return Fail("Attack only in Combat phase.", out error);
                if (card.Zone != TestCardZone.Board) return Fail("Attacker must be on the board.", out error);
                if (card.Tapped) return Fail("Tapped units cannot attack.", out error);
                var target = cards.FirstOrDefault(c => c.Zone == TestCardZone.Board && c.NodeId == targetNode);
                if (target == null || target.Owner == player) return Fail("Select an enemy unit (player attacks not implemented).", out error);
                if (!board.AreAdjacent(card.NodeId, targetNode)) return Fail("Target must be adjacent.", out error);
                // Reuse the existing tested no-effects combat slice for comparison/contract rules.
                var combat = new BattleState(board, player, BattlePhase.Combat, new[] {
                    new BattleUnit(1, card.DefinitionId, card.Owner, card.NodeId, card.Power, card.IsContract),
                    new BattleUnit(2, target.DefinitionId, target.Owner, target.NodeId, target.Power, target.IsContract) });
                if (combat.DeclareAttack(player, 1, 2) != AttackError.None)
                    return Fail("Attack rejected by battle rules.", out error);
                combat.PassResponse(target.Owner); combat.PassResponse(player);
                combat.ResolveWithoutCardEffects();
                card.Tapped = true;
                lastAction = "Attack: attacker tapped; target survived.";
                if (combat.TargetDestroyed)
                {
                    target.Zone = target.IsContract ? TestCardZone.Contract : TestCardZone.Discard;
                    target.NodeId = -1;
                    lastAction = target.IsContract ? "Contract defeated -> contract zone (draw/flip omitted in test)." : "Target defeated -> discard.";
                    if (combat.AwaitingOccupation) { occupationNode = targetNode; occupationCard = card.Id; }
                }
            }
            else return Fail("Unsupported command.", out error);
            Revision++; return true;
        }
        private bool ValidEmptyNode(int player, int node, out string error)
        {
            error = null;
            if (!nodes.Contains(node)) return Fail("Unknown board node.", out error);
            if (board.IsOpposingProtectedNode(node, player)) return Fail("Cannot use enemy player/defense node.", out error);
            if (cards.Any(c => c.Zone == TestCardZone.Board && c.NodeId == node)) return Fail("Node occupied. Use the reserved Overclock action for friendly units.", out error);
            return true;
        }
        // Shared validation seam for the future overclock resolver. No mutation/payment occurs here.
        public bool ValidateOverclock(int player, Guid handCardId, int targetNode, out OverclockRequest request, out string error)
        {
            request = null; error = null;
            if (player != ActivePlayer || Phase != TestTurnPhase.Main || occupationNode >= 0)
                return Fail("Overclock only in your Main phase with no pending battle.", out error);
            var source = cards.FirstOrDefault(c => c.Id == handCardId && c.Owner == player && c.Zone == TestCardZone.Hand && !c.IsContract && !c.IsDecision);
            var target = cards.FirstOrDefault(c => c.NodeId == targetNode && c.Owner == player && c.Zone == TestCardZone.Board);
            if (source == null || target == null) return Fail("Overclock needs a normal unit in hand and a friendly unit on the board.", out error);
            request = new OverclockRequest(MatchId, Revision, player, source.Id, target.Id, targetNode);
            return true;
        }
        public View ForPlayer(int player)
        {
            if (player < 0 || player > 1) throw new ArgumentOutOfRangeException(nameof(player));
            return new View { MatchId = MatchId, Revision = Revision, ActivePlayer = ActivePlayer, Phase = Phase,
                Turn = Turn, ContractMoved = ContractMoved, Board = cards.Where(c => c.Zone == TestCardZone.Board).Select(c => c.Copy()).ToArray(),
                OwnHand = cards.Where(c => c.Zone == TestCardZone.Hand && c.Owner == player).Select(c => c.Copy()).ToArray(),
                PublicPiles = cards.Where(c => c.Zone == TestCardZone.Discard || c.Zone == TestCardZone.Contract).Select(c => c.Copy()).ToArray(),
                OpponentHandCount = cards.Count(c => c.Zone == TestCardZone.Hand && c.Owner != player),
                DeckCounts = new[] { cards.Count(c => c.Owner == 0 && c.Zone == TestCardZone.Deck), cards.Count(c => c.Owner == 1 && c.Zone == TestCardZone.Deck) }, OccupationNode = occupationNode, LastAction = lastAction };
        }
        private static bool Fail(string message, out string error) { error = message; return false; }
    }
    public sealed class OverclockRequest
    {
        public Guid MatchId { get; }
        public int Revision { get; }
        public int Player { get; }
        public Guid SourceCardId { get; }
        public Guid TargetCardId { get; }
        public int TargetNode { get; }
        public OverclockRequest(Guid matchId, int revision, int player, Guid sourceCardId, Guid targetCardId, int targetNode)
        { MatchId = matchId; Revision = revision; Player = player; SourceCardId = sourceCardId; TargetCardId = targetCardId; TargetNode = targetNode; }
    }
}
