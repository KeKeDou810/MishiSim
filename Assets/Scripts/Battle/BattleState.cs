using System;
using System.Collections.Generic;

namespace Mishi.Battle
{
    public enum BattlePhase { Main, Combat, End }
    public enum UnitZone { Board, Discard, Contract }
    public enum AttackError { None, WrongPhase, AttackAlreadyPending, PendingEffects, WrongPlayer, MissingUnit, NotOnBoard, NotControlled, FriendlyTarget, Exhausted, OutOfRange }

    // An instance owns mutable match state; a card definition remains shared metadata.
    public sealed class BattleUnit
    {
        public int InstanceId { get; }
        public string DefinitionId { get; }
        public int ControllerId { get; }
        public int NodeId { get; internal set; }
        public int Power { get; }
        public bool IsContract { get; }
        public int Level { get; }
        public int AttackRange { get; }
        public bool IsExhausted { get; internal set; }
        public UnitZone Zone { get; internal set; }

        public BattleUnit(int instanceId, string definitionId, int controllerId, int nodeId,
            int power, bool isContract = false, bool isExhausted = false, UnitZone zone = UnitZone.Board, int level = 0, int attackRange = 1)
        {
            if (string.IsNullOrWhiteSpace(definitionId)) throw new ArgumentException("Missing definition ID.");
            InstanceId = instanceId; DefinitionId = definitionId; ControllerId = controllerId;
            NodeId = nodeId; Power = power; IsContract = isContract; IsExhausted = isExhausted; Zone = zone;
            Level = level;
            AttackRange = attackRange;
        }
    }

    // Connections are explicit map data. Node numbers do not imply adjacency.
    public sealed class BattleBoard
    {
        private readonly HashSet<(int, int)> edges = new HashSet<(int, int)>();
        private readonly Dictionary<int, int> protectedOwners = new Dictionary<int, int>();
        private readonly Dictionary<int, int> playerOwners = new Dictionary<int, int>();
        public void SetPlayerNode(int nodeId, int ownerId)
        { SetPlayerOrDefenseNode(nodeId, ownerId); playerOwners[nodeId] = ownerId; }
        public int PlayerAt(int nodeId) => playerOwners.TryGetValue(nodeId, out int owner) ? owner : -1;
        public bool IsProtectedBy(int nodeId, int player) => protectedOwners.TryGetValue(nodeId, out int owner) && owner == player;
        public void SetPlayerOrDefenseNode(int nodeId, int ownerId) => protectedOwners[nodeId] = ownerId;
        public bool IsOpposingProtectedNode(int nodeId, int playerId) =>
            protectedOwners.TryGetValue(nodeId, out int ownerId) && ownerId != playerId;
        public void Connect(int a, int b)
        {
            if (a == b) throw new ArgumentException("A node cannot connect to itself.");
            edges.Add((a, b)); edges.Add((b, a));
        }
        public bool AreAdjacent(int a, int b) => edges.Contains((a, b));
        public bool InRange(int from, int to, int range)
        {
            if (from == to || range < 1) return false;
            var seen = new HashSet<int> { from }; var queue = new Queue<(int node, int distance)>(); queue.Enqueue((from, 0));
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.distance >= range) continue;
                foreach (var edge in edges)
                {
                    if (edge.Item1 != current.node) continue;
                    if (edge.Item2 == to) return true;
                    if (seen.Add(edge.Item2)) queue.Enqueue((edge.Item2, current.distance + 1));
                }
            }
            return false;
        }
    }

    public sealed class PendingAttack
    {
        public int AttackerId { get; }
        public int TargetId { get; }
        public int RespondingPlayerId { get; }
        public int ConsecutivePasses { get; internal set; }
        public int PriorityPlayerId { get; internal set; }
        internal PendingAttack(BattleUnit attacker, BattleUnit target)
        {
            AttackerId = attacker.InstanceId; TargetId = target.InstanceId;
            RespondingPlayerId = target.ControllerId;
            PriorityPlayerId = target.ControllerId;
        }
    }

    public sealed class BattleState
    {
        private readonly Dictionary<int, BattleUnit> units = new Dictionary<int, BattleUnit>();
        private readonly BattleBoard board;
        public int ActivePlayerId { get; }
        public BattlePhase Phase { get; }
        public PendingAttack PendingAttack { get; private set; }
        public bool AwaitingOccupation { get; private set; }
        public bool TargetDestroyed { get; private set; }
        // These requests are consumed by the future draw/player-card systems.
        public int? PendingContractDrawPlayerId { get; private set; }
        public bool PendingLevelZeroPlayerFlip { get; private set; }

        public BattleState(BattleBoard board, int activePlayerId, BattlePhase phase, IEnumerable<BattleUnit> initialUnits)
        {
            this.board = board ?? throw new ArgumentNullException(nameof(board));
            ActivePlayerId = activePlayerId; Phase = phase;
            var occupied = new HashSet<int>();
            foreach (BattleUnit unit in initialUnits)
            {
                if (unit.Zone == UnitZone.Board && !occupied.Add(unit.NodeId))
                    throw new ArgumentException("Two units occupy the same node.");
                // Own a copy so fixtures or another match cannot mutate this state.
                units.Add(unit.InstanceId, new BattleUnit(unit.InstanceId, unit.DefinitionId,
                    unit.ControllerId, unit.NodeId, unit.Power, unit.IsContract, unit.IsExhausted, unit.Zone, unit.Level, unit.AttackRange));
            }
        }
        public BattleUnit GetUnit(int id) => units[id];

        public bool PassResponse(int playerId)
        {
            if (PendingAttack == null || AwaitingOccupation || PendingAttack.ConsecutivePasses >= 2 ||
                PendingAttack.PriorityPlayerId != playerId) return false;
            PendingAttack.ConsecutivePasses++;
            PendingAttack.PriorityPlayerId = playerId == ActivePlayerId
                ? PendingAttack.RespondingPlayerId : ActivePlayerId;
            return true;
        }

        // Test slice only: call for battles with NO decision, foresight or triggered effects.
        // Full effect resolution must be implemented before using this in live gameplay.
        public bool ResolveWithoutCardEffects()
        {
            if (PendingAttack == null || AwaitingOccupation || PendingAttack.ConsecutivePasses != 2) return false;
            BattleUnit attacker = units[PendingAttack.AttackerId];
            BattleUnit target = units[PendingAttack.TargetId];
            TargetDestroyed = attacker.IsContract || attacker.Power >= target.Power;
            PendingContractDrawPlayerId = null;
            PendingLevelZeroPlayerFlip = false;
            if (!TargetDestroyed) { PendingAttack = null; return true; }
            target.Zone = target.IsContract ? UnitZone.Contract : UnitZone.Discard;
            if (target.IsContract)
            {
                PendingContractDrawPlayerId = target.ControllerId;
                PendingLevelZeroPlayerFlip = target.Level == 0;
            }
            // Opposing player/defense nodes cannot be occupied (rulebook Q&A).
            AwaitingOccupation = !board.IsOpposingProtectedNode(target.NodeId, ActivePlayerId);
            if (!AwaitingOccupation) PendingAttack = null;
            return true;
        }

        public bool ChooseOccupation(int playerId, bool occupy)
        {
            if (!AwaitingOccupation || PendingAttack == null || playerId != ActivePlayerId) return false;
            if (occupy) units[PendingAttack.AttackerId].NodeId = units[PendingAttack.TargetId].NodeId;
            AwaitingOccupation = false;
            PendingAttack = null;
            return true;
        }

        // Unit-to-unit declaration only. Resolution must wait for decisions and foresight.
        public AttackError DeclareAttack(int playerId, int attackerId, int targetId)
        {
            if (Phase != BattlePhase.Combat) return AttackError.WrongPhase;
            if (PendingAttack != null) return AttackError.AttackAlreadyPending;
            if (PendingContractDrawPlayerId != null) return AttackError.PendingEffects;
            if (playerId != ActivePlayerId) return AttackError.WrongPlayer;
            if (!units.TryGetValue(attackerId, out BattleUnit attacker) || !units.TryGetValue(targetId, out BattleUnit target))
                return AttackError.MissingUnit;
            if (attacker.Zone != UnitZone.Board || target.Zone != UnitZone.Board) return AttackError.NotOnBoard;
            if (attacker.ControllerId != playerId) return AttackError.NotControlled;
            if (target.ControllerId == playerId) return AttackError.FriendlyTarget;
            if (attacker.IsExhausted) return AttackError.Exhausted;
            if (!board.InRange(attacker.NodeId, target.NodeId, attacker.AttackRange)) return AttackError.OutOfRange;

            attacker.IsExhausted = true;
            PendingAttack = new PendingAttack(attacker, target);
            return AttackError.None;
        }
    }
}
