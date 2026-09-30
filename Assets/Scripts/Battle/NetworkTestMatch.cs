using System;
using System.Collections.Generic;
using System.Linq;

namespace Mishi.Battle
{
    public enum TestCommandKind { MoveContract, EndTurn, NextPhase, Summon, Attack, Occupy, Stay, Overclock, AttackPlayer, PlayDecision, PassResponse, ChooseEffect, ActivateEffect, ChooseEffectZone, ResolveDeckView, DeclareCardName, OrderTrigger, ChooseForesight, ChooseNumber }
    public enum TestTurnPhase { TurnStart, Draw, Rebuild, TimeReset, Main, Combat, End }
    public enum TestCardZone { Board, Hand, Discard, Contract, Deck, Player, OffField, Exile, Removed, Revealed }

    // Authoritative battle slice with an optional effect interpreter. Foresight is a separate future extension.
    public sealed partial class NetworkTestMatch
    {
        public sealed class Card
        {
            public Guid Id { get; }
            public string DefinitionId { get; }
            public int Owner { get; internal set; }
            public int OriginalOwner { get; private set; }
            public bool HiddenAttachment { get; internal set; }
            public int ZoneEnteredTurn { get; internal set; }
            public int BasePower { get; }
            public int Power { get; internal set; }
            public bool IsContract { get; }
            public bool IsDecision { get; }
            public int BaseTime { get; }
            public int Time { get; internal set; }
            public bool IsToken { get; internal set; }
            internal int FieldGeneration;
            public int SuppressedUntilTurn { get; internal set; } = -1;
            internal int LastEffectRebuiltTurn = -1;
            internal int LastRebuiltTurn = -1, ProtectedUntilTurn = -1, ProtectedAgainstPlayer = -1;
            internal readonly List<ContinuousContribution> ContinuousContributions = new List<ContinuousContribution>();
            internal readonly EffectVariableStore Variables = new EffectVariableStore();
            internal readonly List<StatModifier> Modifiers = new List<StatModifier>();
            public int NodeId { get; internal set; }
            public bool Tapped { get; internal set; }
            public int StackOrder { get; internal set; }
            public bool Covered { get; internal set; }
            public Guid AttachedTo { get; internal set; }
            public int AttackRange { get; internal set; } = 1;
            public TestCardZone Zone { get; internal set; }
            public Card(Guid id, string definitionId, int owner, int nodeId, int power = 0,
                bool contract = false, TestCardZone zone = TestCardZone.Board, bool decision = false, int time = 0)
            { Id = id; DefinitionId = definitionId; OriginalOwner = Owner = owner; NodeId = nodeId; BasePower = Power = power; IsContract = contract; Zone = zone; IsDecision = decision; BaseTime = Time = time >= 0 ? time : throw new ArgumentOutOfRangeException(nameof(time)); }
            internal Card Copy() => new Card(Id, DefinitionId, Owner, NodeId, Power, IsContract, Zone, IsDecision, Time)
                { OriginalOwner = OriginalOwner, HiddenAttachment = HiddenAttachment, ZoneEnteredTurn = ZoneEnteredTurn, Tapped = Tapped, StackOrder = StackOrder, Covered = Covered, AttachedTo = AttachedTo, AttackRange = AttackRange, IsToken = IsToken, SuppressedUntilTurn = SuppressedUntilTurn, LastRebuiltTurn = LastRebuiltTurn, LastEffectRebuiltTurn = LastEffectRebuiltTurn, ProtectedUntilTurn = ProtectedUntilTurn, ProtectedAgainstPlayer = ProtectedAgainstPlayer };
        }
        public sealed class View
        {
            public Guid MatchId;
            public int Revision, ActivePlayer, Turn;
            public TestTurnPhase Phase;
            public bool ContractMoved;
            public Card[] Board, OwnHand, PublicPiles;
            public VariableSnapshot[] Variables;
            public int OpponentHandCount;
            public int[] HandCounts;
            public int[] DeckCounts;
            public int[] CostPointers, DamagePointers;
            public ClockKind[] ClockKinds;
            public EffectChoice Choice;
            public bool[] PlayerFlipped;
            public int Winner;
            public string ResultReason;
            public double RemainingTurnSeconds;
            public bool TimerRunning;
            public int ResponsePlayer;
            public double RemainingResponseSeconds;
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
        private string lastAction = "Battle test ready.";
        private MatchDrawRules drawRules;
        private readonly PlayerClock[] clocks = { new PlayerClock(), new PlayerClock() };
        private readonly bool[] playerFlipped = new bool[2];
        private readonly bool[] emptyHandled = new bool[2];
        private Random random = new Random();
        private int decisionsUsed;
        public int Winner { get; private set; } = -1;
        public string ResultReason { get; private set; }
        public double RemainingTurnSeconds { get; private set; } = 120;
        public bool TimerRunning => !OpeningPending && Winner < 0 && responseAttack == null && effectChoice == null;
        private sealed class ResponseAttack
        {
            public Guid Attacker, Target;
            public int TargetNode, PriorityPlayer, Passes;
            public bool PlayerTarget;
            public readonly double[] Seconds = new double[2];
        }
        private ResponseAttack responseAttack;
        public int ResponsePlayer => responseAttack?.PriorityPlayer ?? -1;
        public double RemainingResponseSeconds => responseAttack == null ? 0 : responseAttack.Seconds[responseAttack.PriorityPlayer];
        private int TurnTimeSeconds => drawRules?.TurnTimeSeconds ?? 120;
        private int ActionTimeRefundSeconds => drawRules?.ActionTimeRefundSeconds ?? 5;
        // Called only with elapsed monotonic host time; clients cannot submit elapsed time.
        public bool AdvanceTime(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (effectChoice != null && Winner < 0)
            {
                effectChoice.Seconds = Math.Max(0, effectChoice.Seconds - seconds);
                if (effectChoice.Seconds > 0) return false;
                if (effectChoice.IsPayment) { CancelPayment(); DrainEffects(); }
                else if (effectChoice.IsForesightOffer) ResolveForesightOffer(false);
                else if (effectChoice.TriggerOptions.Length > 0) ResolveTriggerOrder(Guid.Empty);
                else if (effectChoice.NameOptions.Length > 0) ResolveDeclaration(0);
                else if (effectChoice.IsDeckView) ResolveDeckViewTimeout();
                else if (effectChoice.ZoneCandidates.Length > 0) ResolveZoneChoice(effectChoice.ZoneCandidates[0]);
                else ResolveChoice(effectChoice.Optional ? Guid.Empty : effectChoice.Candidates[0]);
                Revision++; return true;
            }
            if (responseAttack != null && Winner < 0)
            {
                int player = responseAttack.PriorityPlayer;
                responseAttack.Seconds[player] = Math.Max(0, responseAttack.Seconds[player] - seconds);
                if (responseAttack.Seconds[player] > 0) return false;
                PassResponse(true); Revision++; return true;
            }
            if (!TimerRunning) return false;
            RemainingTurnSeconds = Math.Max(0, RemainingTurnSeconds - seconds);
            if (RemainingTurnSeconds > 0) return false;
            int expiredPlayer = ActivePlayer;
            AddLog(expiredPlayer, "回合倒计时结束，自动结束回合。");
            BeginNextTurn();
            lastAction = $"P{expiredPlayer + 1} 回合倒计时结束，自动结束回合。";
            Revision++;
            return true;
        }
        private void RefundActionTime()
        {
            if (!OpeningPending && Winner < 0) RemainingTurnSeconds = Math.Min(TurnTimeSeconds, RemainingTurnSeconds + ActionTimeRefundSeconds);
        }
        private void BeginNextTurn()
        {
            RunSchedules("turnEnd", ActivePlayer);
            Publish(new Events.PhaseEndedEvent(ActivePlayer, Turn, Phase));
            Publish(new Events.TurnEndedEvent(ActivePlayer, Turn, Phase));
            eventContinuations.Enqueue(BeginNextTurnCore); DrainEffects();
        }
        private void BeginNextTurnCore()
        {
            foreach (var unit in cards) unit.Modifiers.RemoveAll(m => m.ExpiresAfterTurn <= Turn);
            activationCounts.Clear();
            turnVariables.ClearAll();
            occupationNode = -1; occupationCard = Guid.Empty;
            ActivePlayer = 1 - ActivePlayer; Turn++; Phase = TestTurnPhase.TurnStart;
            ContractMoved = false; decisionsUsed = 0; RemainingTurnSeconds = TurnTimeSeconds;
            Publish(new Events.TurnStartedEvent(ActivePlayer, Turn, Phase));
            Publish(new Events.PhaseStartedEvent(ActivePlayer, Turn, Phase));
            RefreshStats();
        }
        private void CheckResult(int player)
        {
            if (Winner < 0 && clocks[player].Lost)
            { Winner = 1 - player; ResultReason = $"P{player + 1} 败北：{clocks[player].LossReason}。"; AddLog(player, ResultReason); occupationNode = -1; responseAttack = null; }
        }
        // Server-side effect resolver seam. No client command accepts a damage amount.
        public void ResolvePlayerDamage(int player, int amount, bool moveCost = false)
        {
            if (player < 0 || player > 1) throw new ArgumentOutOfRangeException(nameof(player));
            if (Winner >= 0) return;
            ApplyDamage(player, amount, moveCost, "effect"); DrainEffects();
        }
        public bool OpeningPending { get; private set; }

        // Input is already validated by the room against its selected external mode.
        public static NetworkTestMatch FromDecks(BattleBoard board, IEnumerable<int> nodes, int hostStart, int guestStart,
            Card[][] decks, MatchDrawRules rules, Random random)
        {
            if (decks == null || decks.Length != 2 || rules == null || random == null) throw new ArgumentException("Invalid match setup.");
            var match = new NetworkTestMatch(board, nodes, hostStart, guestStart, "fixture", "fixture");
            match.cards.Clear(); match.drawRules = rules; match.random = random;
            match.RemainingTurnSeconds = rules.TurnTimeSeconds;
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
                    match.cards.Add(new Card(Guid.NewGuid(), card.DefinitionId, player, -1, card.Power, false, TestCardZone.Deck, card.IsDecision, card.Time));
            }
            match.OpeningPending = true;
            match.lastAction = "Decks ready; dealing opening hands...";
            return match;
        }
        public void DealOpeningHands(bool awaitConfirmation = false)
        {
            if (!OpeningPending || openingDealt) return;
            DealSetupCards(0, drawRules.OpeningHand); DealSetupCards(1, drawRules.OpeningHand);
            openingDealt = true; Revision++;
            if (!awaitConfirmation)
            {
                openingHandDecided[0] = openingHandDecided[1] = true;
                CompleteOpeningHands();
            }
            lastAction = "Opening hands dealt: " + drawRules.OpeningHand + " each.";
        }
        private int Draw(int player, int count)
        {
            int drawn = 0;
            while (drawn < count && Winner < 0)
            {
                RecycleEmptyDeck(player);
                if (Winner >= 0) break;
                var card = cards.FirstOrDefault(c => c.Owner == player && c.Zone == TestCardZone.Deck);
                if (card == null) break;
                card.Zone = TestCardZone.Hand; drawn++;
                PublishMove(card, TestCardZone.Deck, "draw");
                if (effects != null) Publish(new Events.DrawnEvent(EventCard(card, false), TestCardZone.Deck, TestCardZone.Hand, "draw", EventCard(eventCause)));
                RecycleEmptyDeck(player); // This rule interrupts even a multi-card draw.
            }
            return drawn;
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
            if (Winner >= 0) return Fail("对战已结束。", out error);
            if (OpeningPending) return Fail("Wait for opening hands.", out error);
            RefreshStats();
            if (effectChoice != null)
            {
                if (effectChoice.IsPayment)
                {
                    if (player != effectChoice.Player) return Fail("请等待对方支付费用。", out error);
                    if (effectChoice.IsNumber)
                    {
                        if (kind != TestCommandKind.ChooseNumber || targetNode < -1 || targetNode != -1 && (targetNode < effectChoice.NumberMinimum || targetNode > effectChoice.NumberMaximum)) return Fail("请选择合法费用数值。", out error);
                        ResolvePaymentNumber(targetNode);
                    }
                    else
                    {
                        if (kind != TestCommandKind.ChooseEffect || cardId != Guid.Empty && !effectChoice.Candidates.Contains(cardId)) return Fail("请选择合法费用卡片。", out error);
                        ResolvePaymentCard(cardId);
                    }
                    DrainEffects(); Revision++; return true;
                }
                if (effectChoice.IsForesightOffer)
                {
                    if (player != effectChoice.Player || kind != TestCommandKind.ChooseForesight || targetNode < 0 || targetNode > 1) return Fail("请选择进行或放弃未来视。", out error);
                    ResolveForesightOffer(targetNode == 1); Revision++; return true;
                }
                if (effectChoice.TriggerOptions.Length > 0)
                {
                    if (player != effectChoice.Player || kind != TestCommandKind.OrderTrigger || (cardId != Guid.Empty && !effectChoice.TriggerOptions.Any(t => t.Id == cardId)))
                        return Fail("请选择你控制的待处理自动效果。", out error);
                    ResolveTriggerOrder(cardId); Revision++; return true;
                }
                if (effectChoice.NameOptions.Length > 0)
                {
                    if (player != effectChoice.Player || kind != TestCommandKind.DeclareCardName || targetNode < 0 || targetNode >= effectChoice.NameOptions.Length)
                        return Fail("请选择有效卡名并完成宣言。", out error);
                    ResolveDeclaration(targetNode); Revision++; return true;
                }
                if (effectChoice.IsDeckView)
                {
                    if (player != effectChoice.Player || kind != TestCommandKind.ResolveDeckView || !ValidDeckViewChoice(cardId, targetNode))
                        return Fail("请选择本次查看的卡片及有效的放回位置。", out error);
                    ResolveDeckView(cardId, targetNode); Revision++; return true;
                }
                if (effectChoice.ZoneCandidates.Length > 0)
                {
                    if (effectChoice.IsBoardPlacement)
                    {
                        if (player != effectChoice.Player || kind != TestCommandKind.ChooseEffectZone || !effectChoice.ZoneCandidates.Contains(targetNode) || !EmptySummonNodes(player).Contains(targetNode)) return Fail("请选择合法空圆阵。", out error);
                        ResolveSummonPlacement(targetNode); Revision++; return true;
                    }
                    if (player != effectChoice.Player || kind != TestCommandKind.ChooseEffectZone || !effectChoice.ZoneCandidates.Contains(targetNode) || !AvailableOffFieldZones(PlacementOwner(executions.Peek())).Contains(targetNode))
                        return Fail("请选择有效的共享场外区。", out error);
                    ResolveZoneChoice(targetNode); Revision++; return true;
                }
                if (player != effectChoice.Player || kind != TestCommandKind.ChooseEffect) return Fail("请先完成当前效果选择。", out error);
                if (cardId == Guid.Empty ? !effectChoice.Optional : !effectChoice.Candidates.Contains(cardId)) return Fail("无效的效果目标。", out error);
                ResolveChoice(cardId); Revision++; return true;
            }
            if (kind == TestCommandKind.ActivateEffect) return Activate(player, cardId, out error);
            if (responseAttack != null) return Respond(player, kind, cardId, out error);
            if (player != ActivePlayer) return Fail("Not your turn.", out error);
            if (occupationNode >= 0)
            {
                if (kind != TestCommandKind.Occupy && kind != TestCommandKind.Stay)
                    return Fail("Choose Occupy or Stay before another action.", out error);
                var attacker = cards.Single(c => c.Id == occupationCard);
                if (kind == TestCommandKind.Occupy)
                    foreach (var member in cards.Where(c => c.Zone == TestCardZone.Board && c.NodeId == attacker.NodeId).ToArray()) member.NodeId = occupationNode;
                lastAction = kind == TestCommandKind.Occupy ? "Attacker occupied the defeated unit's node." : "Attacker stayed in place.";
                RefreshPlayerAttachments();
                occupationNode = -1; occupationCard = Guid.Empty;
                RefundActionTime();
                Revision++; return true;
            }
            if (kind == TestCommandKind.NextPhase)
            {
                if (Phase == TestTurnPhase.End) return Fail("Use End Turn in the End phase.", out error);
                AdvancePhaseWithEvents(player);
                Revision++; return true;
            }
            if (kind == TestCommandKind.EndTurn)
            {
                if (Phase != TestTurnPhase.End) return Fail("Advance to End phase first.", out error);
                BeginNextTurn();
                lastAction = "Turn passed to P" + (ActivePlayer + 1); Revision++; return true;
            }
            if (kind == TestCommandKind.Overclock)
            {
                if (!ValidateOverclock(player, cardId, targetNode, out var entry, out error)) return false;
                var source = cards.Single(c => c.Id == entry.SourceCardId);
                var target = cards.Single(c => c.Id == entry.TargetCardId);
                int cost = source.Time - target.Time;
                if (!PayTime(player, cost, source)) return Fail("费用时间不足，无法超频。", out error);
                foreach (var member in cards.Where(c => c.Zone == TestCardZone.Board && c.NodeId == targetNode)) { member.Covered = true; member.Tapped = true; }
                ResetFieldInstance(source);
                source.Zone = TestCardZone.Board; source.NodeId = targetNode;
                source.StackOrder = target.StackOrder + 1; source.Covered = false; source.Tapped = false;
                lastAction = $"{source.DefinitionId} 超频登场，支付时间差 {cost}。";
                QueueEffect(source, EffectEvent.Summoned, "overclock"); DrainEffects();
                RefundActionTime();
                Revision++; return true;
            }
            var card = cards.FirstOrDefault(c => c.Id == cardId);
            if (card == null || card.Owner != player) return Fail("You do not control this card.", out error);
            if (kind == TestCommandKind.Summon)
            {
                if (Phase != TestTurnPhase.Main) return Fail("Summon only in Main phase.", out error);
                if (card.Zone != TestCardZone.Hand || card.IsContract || card.IsDecision) return Fail("Select a normal unit in your hand.", out error);
                if (!ValidEmptyNode(player, targetNode, out error)) return false;
                if (!PayTime(player, card.IsDecision ? DecisionCost(card) : card.Time, card)) return Fail("费用时间不足，无法登场。", out error);
                ResetFieldInstance(card);
                card.Zone = TestCardZone.Board; card.NodeId = targetNode; card.Tapped = false;
                QueueEffect(card, EffectEvent.Summoned, "paidHand");
                lastAction = $"{card.DefinitionId} 登场，支付 {card.Time} 时间。";
            }
            else if (kind == TestCommandKind.PlayDecision)
            {
                if (Phase != TestTurnPhase.Main || card.Zone != TestCardZone.Hand || !card.IsDecision)
                    return Fail("仅可在自己的主要阶段使用手牌中的决策卡。", out error);
                if (!PreparePlay(card, out error)) return false;
                if (decisionsUsed > 0) return Fail("自己的回合只能使用一次决策卡。", out error);
                if (effects != null && effects.Rules(card.DefinitionId).PlayCosts.Length > 0)
                    return BeginPayment(card, preparedPlay, effects.Rules(card.DefinitionId).PlayCosts, DecisionCost(card), EffectEvent.Played, null, out error);
                if (!PayTime(player, card.IsDecision ? DecisionCost(card) : card.Time, card)) return Fail("费用时间不足，无法使用决策卡。", out error);
                decisionsUsed++; card.Zone = TestCardZone.Discard;
                QueueEffect(card, EffectEvent.Played, "decision"); DrainEffects();
                if (drawRules != null && effectChoice == null) RecycleEmptyDeck(player);
                lastAction = $"使用 {card.DefinitionId}，支付 {card.Time} 时间并结算效果。";
            }
            else if (kind == TestCommandKind.AttackPlayer)
            {
                int defender = board.PlayerAt(targetNode);
                if (Phase != TestTurnPhase.Combat || card.Zone != TestCardZone.Board || card.Covered || card.Tapped)
                    return Fail("仅可在战斗阶段用竖置时魔攻击。", out error);
                if (defender < 0 || defender == player || !board.InRange(card.NodeId, targetNode, card.AttackRange))
                    return Fail("请选择攻击范围内的对方玩家圆阵。", out error);
                if (!PlayerIsExposed(defender))
                    return Fail("玩家正在契约时魔下方，或玩家／防御圆阵仍有时魔，不能直接攻击玩家。", out error);
                QueueAttack(card, null, targetNode, defender);
                lastAction = $"{card.DefinitionId} 攻击玩家，造成 1 点伤害。";
            }
            else if (kind == TestCommandKind.MoveContract)
            {
                if (Phase != TestTurnPhase.Main) return Fail("Move only in Main phase.", out error);
                if (card.Zone != TestCardZone.Board || card.Covered || !card.IsContract) return Fail("Only an uncovered contract on the board can move.", out error);
                if (ContractMoved) return Fail("Contract already moved this turn.", out error);
                if (!board.AreAdjacent(card.NodeId, targetNode)) return Fail("Target must be adjacent.", out error);
                if (!ValidEmptyNode(player, targetNode, out error)) return false;
                foreach (var member in cards.Where(c => c.Zone == TestCardZone.Board && c.NodeId == card.NodeId).ToArray()) member.NodeId = targetNode;
                ContractMoved = true;
                AddLog(player, CardLabel(card) + " 移动到圆阵 " + targetNode);
                lastAction = "Contract moved.";
            }
            else if (kind == TestCommandKind.Attack)
            {
                if (Phase != TestTurnPhase.Combat) return Fail("Attack only in Combat phase.", out error);
                if (card.Zone != TestCardZone.Board || card.Covered) return Fail("Attacker must be the top card on the board.", out error);
                if (card.Tapped) return Fail("Tapped units cannot attack.", out error);
                var target = cards.FirstOrDefault(c => c.Zone == TestCardZone.Board && c.NodeId == targetNode && !c.Covered);
                if (target == null || target.Owner == player) return Fail("Select an enemy unit.", out error);
                if (effects != null && EffectsActive(card) && target.Time > effects.Rules(card.DefinitionId).AttackTimeLimit) return Fail("卡片效果禁止攻击该时间的目标。", out error);
                if (!board.InRange(card.NodeId, targetNode, card.AttackRange)) return Fail("目标不在攻击距离内。", out error);
                QueueAttack(card, target, targetNode, target.Owner);
            }
            else return Fail("Unsupported command.", out error);
            DrainEffects(); RefreshStats();
            RefundActionTime();
            Revision++; return true;
        }
        private bool ResolveUnitAttack(Card card, Card target, int targetNode, out string error, Action completed = null)
        {
                error = null;
                int player = card.Owner;
                // Reuse the existing tested no-effects combat slice for comparison/contract rules.
                var combat = new BattleState(board, player, BattlePhase.Combat, new[] {
                    new BattleUnit(1, card.DefinitionId, card.Owner, card.NodeId, card.Power, card.IsContract, attackRange: int.MaxValue),
                    new BattleUnit(2, target.DefinitionId, target.Owner, target.NodeId, target.Power, target.IsContract) });
                if (combat.DeclareAttack(player, 1, 2) != AttackError.None)
                    return Fail("Attack rejected by battle rules.", out error);
                combat.PassResponse(target.Owner); combat.PassResponse(player);
                combat.ResolveWithoutCardEffects();
                lastAction = "Attack: attacker tapped; target survived.";
                if (combat.TargetDestroyed)
                {
                    var priorCause = eventCause; eventCause = card;
                    try { RequestDestruction(target, "battle", () => {
                        lastAction = "战斗结算完成。";
                        if (Winner < 0 && target.Zone != TestCardZone.Board && card.Zone == TestCardZone.Board && combat.AwaitingOccupation && !cards.Any(c => c.Zone == TestCardZone.Board && c.NodeId == targetNode))
                        { occupationNode = targetNode; occupationCard = card.Id; }
                        completed?.Invoke();
                    }); } finally { eventCause = priorCause; }
                }
                else completed?.Invoke();
                return true;
        }
        private void StartResponse(Card attacker, Card target, int targetNode, int defender)
        {
            attacker.Tapped = true;
            responseAttack = new ResponseAttack { Attacker = attacker.Id, Target = target?.Id ?? Guid.Empty,
                TargetNode = targetNode, PlayerTarget = target == null, PriorityPlayer = defender };
            responseAttack.Seconds[0] = responseAttack.Seconds[1] = drawRules.ResponseTimeSeconds;
            lastAction = $"攻击宣言：等待 P{defender + 1} 使用决策卡或放弃响应。";
        }
        private bool Respond(int player, TestCommandKind kind, Guid cardId, out string error)
        {
            error = null;
            if (player != responseAttack.PriorityPlayer) return Fail("请等待你的响应机会。", out error);
            if (kind == TestCommandKind.PassResponse) { PassResponse(false); Revision++; return true; }
            if (kind != TestCommandKind.PlayDecision) return Fail("响应期间只能使用决策卡或放弃响应。", out error);
            var card = cards.FirstOrDefault(c => c.Id == cardId && c.Owner == player && c.Zone == TestCardZone.Hand && c.IsDecision);
            if (card == null) return Fail("请选择自己的手牌决策卡。", out error);
            if (!PreparePlay(card, out error)) return false;
            if (player == ActivePlayer && decisionsUsed > 0) return Fail("自己的回合只能使用一次决策卡。", out error);
            if (effects != null && effects.Rules(card.DefinitionId).PlayCosts.Length > 0)
                return BeginPayment(card, preparedPlay, effects.Rules(card.DefinitionId).PlayCosts, DecisionCost(card), EffectEvent.Played, null, out error);
            if (!PayTime(player, card.IsDecision ? DecisionCost(card) : card.Time, card)) return Fail("费用时间不足，无法使用决策卡。", out error);
            if (player == ActivePlayer) decisionsUsed++;
            card.Zone = TestCardZone.Discard;
            responseAttack.Seconds[player] = Math.Min(drawRules.ResponseTimeSeconds, responseAttack.Seconds[player] + ActionTimeRefundSeconds);
            responseAttack.Passes = 0; responseAttack.PriorityPlayer = 1 - player;
            QueueEffect(card, EffectEvent.Played, "decision"); DrainEffects();
            if (drawRules != null && effectChoice == null) RecycleEmptyDeck(player);
            lastAction = $"P{player + 1} 使用决策卡，支付 {card.Time} 时间并结算效果，交给另一方响应。";
            Revision++; return true;
        }
        private void PassResponse(bool timedOut)
        {
            var pending = responseAttack;
            int player = pending.PriorityPlayer;
            AddLog(player, timedOut ? "响应超时，自动放弃响应。" : "放弃响应。");
            pending.Passes++;
            if (pending.Passes < 2)
            {
                pending.PriorityPlayer = 1 - player;
                lastAction = $"P{player + 1} {(timedOut ? "响应超时，自动" : "")}放弃响应，等待另一方。";
                return;
            }
            responseAttack = null;
            var attacker = cards.FirstOrDefault(c => c.Id == pending.Attacker && c.Zone == TestCardZone.Board && !c.Covered);
            if (attacker == null || declaredCombat != null && attacker.FieldGeneration != declaredCombat.AttackerGeneration)
            { lastAction = "攻击者已离场，结束战斗。"; if (declaredCombat != null) EndCombat(declaredCombat, true); DrainEffects(); return; }
            var target = pending.PlayerTarget ? null : cards.FirstOrDefault(c => c.Id == pending.Target && c.Zone == TestCardZone.Board && !c.Covered);
            if (!pending.PlayerTarget && (target == null || declaredCombat != null && target.FieldGeneration != declaredCombat.TargetGeneration))
            { lastAction = "攻击目标已离场，结束战斗。"; if (declaredCombat != null) EndCombat(declaredCombat, true); DrainEffects(); return; }
            BeginForesightCombat(attacker, target, pending.TargetNode, 1 - attacker.Owner);
            DrainEffects();
        }

        private bool ValidEmptyNode(int player, int node, out string error)
        {
            error = null;
            if (!nodes.Contains(node)) return Fail("Unknown board node.", out error);
            if (board.IsOpposingProtectedNode(node, player)) return Fail("Cannot use enemy player/defense node.", out error);
            if (cards.Any(c => c.Zone == TestCardZone.Board && c.NodeId == node)) return Fail("Node occupied. Use the reserved Overclock action for friendly units.", out error);
            return true;
        }
        // Validation is shared with callers and never mutates cards or pays time.
        public bool ValidateOverclock(int player, Guid handCardId, int targetNode, out OverclockRequest request, out string error)
        {
            request = null; error = null;
            if (Winner >= 0 || OpeningPending || responseAttack != null || player != ActivePlayer || Phase != TestTurnPhase.Main || occupationNode >= 0)
                return Fail("Overclock only in your Main phase with no pending battle.", out error);
            var source = cards.FirstOrDefault(c => c.Id == handCardId && c.Owner == player && c.Zone == TestCardZone.Hand && !c.IsContract && !c.IsDecision);
            var target = cards.FirstOrDefault(c => c.NodeId == targetNode && c.Owner == player && c.Zone == TestCardZone.Board && !c.Covered);
            if (source == null || target == null) return Fail("Overclock needs a normal unit in hand and a friendly unit on the board.", out error);
            if (!nodes.Contains(targetNode) || board.IsOpposingProtectedNode(targetNode, player)) return Fail("此圆阵不能超频。", out error);
            if (source.Time <= target.Time) return Fail("超频时，新时魔的时间必须大于当前最上方时魔。", out error);
            if (source.Time - target.Time > clocks[player].RemainingTime) return Fail("费用时间不足，无法超频。", out error);
            request = new OverclockRequest(MatchId, Revision, player, source.Id, target.Id, targetNode);
            return true;
        }
        public View ForPlayer(int player)
        {
            if (player < 0 || player > 1) throw new ArgumentOutOfRangeException(nameof(player));
            return BuildView(player);
        }
        // A separate audience, never a player view with the hand removed afterwards.
        public View ForObserver() => BuildView(-1);
        private View BuildView(int player)
        {
            RefreshStats();
            return new View { Variables = player < 0 ? Array.Empty<VariableSnapshot>() : VariableSnapshots(player), ClockKinds = clocks.Select(c => c.Kind).ToArray(), Choice = ChoiceFor(player), MatchId = MatchId, Revision = Revision, ActivePlayer = ActivePlayer, Phase = Phase,
                Turn = Turn, ContractMoved = ContractMoved,
                RemainingTurnSeconds = RemainingTurnSeconds, TimerRunning = TimerRunning,
                ResponsePlayer = ResponsePlayer, RemainingResponseSeconds = effectChoice?.Seconds ?? RemainingResponseSeconds,
                CostPointers = clocks.Select(c => c.CostPointer).ToArray(), DamagePointers = clocks.Select(c => c.DamagePointer).ToArray(),
                PlayerFlipped = (bool[])playerFlipped.Clone(), Winner = Winner, ResultReason = ResultReason, Board = cards.Where(c => c.Zone == TestCardZone.Board).Select(c => VisibleCopy(c, player)).ToArray(),
                OwnHand = cards.Where(c => c.Zone == TestCardZone.Hand && c.Owner == player).Select(c => c.Copy()).ToArray(),
                PublicPiles = cards.Where(c => c.Zone == TestCardZone.Discard || c.Zone == TestCardZone.Contract || c.Zone == TestCardZone.OffField || c.Zone == TestCardZone.Exile).Select(c => VisibleCopy(c, player)).ToArray(),
                HandCounts = new[] { cards.Count(c => c.Zone == TestCardZone.Hand && c.Owner == 0), cards.Count(c => c.Zone == TestCardZone.Hand && c.Owner == 1) },
                OpponentHandCount = cards.Count(c => c.Zone == TestCardZone.Hand && c.Owner == (player < 0 ? 1 : 1 - player)),
                DeckCounts = new[] { cards.Count(c => c.Owner == 0 && c.Zone == TestCardZone.Deck), cards.Count(c => c.Owner == 1 && c.Zone == TestCardZone.Deck) }, OccupationNode = occupationNode,
                LastAction = Winner >= 0 ? ResultReason : player < 0 ? publicActionLog.LastOrDefault() ?? "等待公开操作" : lastAction };
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



