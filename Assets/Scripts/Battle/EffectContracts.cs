using System;
using System.Collections.Generic;

namespace Mishi.Battle
{
    public enum ClockKind { Black, White }
    public enum EffectEvent { Summoned, Destroyed, Played, Activated, Triggered }
    public enum EffectOp { Choose, Draw, Discard, Destroy, Modify, Move, Ready, Damage, Heal, Cost, Spawn, Scry, ReturnToDeck, DeclareCardName, RememberCards, SetValue, AddValue, ClearValue }
    public sealed class EffectInstruction
    {
        // Legacy enum remains source-compatible; new operations use EffectId without extending it.
        public EffectOp Op;
        public string EffectId;
        internal string ScriptDefinitionId;
        public string OperationId => string.IsNullOrWhiteSpace(EffectId) ? Op.ToString() : EffectId;
        public string Target = "self", Side = "own", Type, Race, Name, DefinitionId, Stat = "power", Duration = "turn", Mode = "add", Prompt;
        public TestCardZone Zone = TestCardZone.Board;
        public int Amount, Minimum = 0, Maximum = int.MaxValue;
        public bool Optional;
        public bool Append;
        public bool? OncePerTurn, OncePerNamePerTurn;
        public bool NotRebuiltThisTurn;
        public bool EnteredThisTurn;
        public int MaximumCost = 11, MinimumDamage = 1;
        public bool MoveCost = false;
        public string StoreAs, FromSet, Except;
        public string Key, Visibility = "private";
        public VariableScope Scope;
        public EffectValue Value;
        public VariableReference ValueReference, AmountReference;
        public EffectInstruction Copy() => (EffectInstruction)MemberwiseClone();
        public string NameMatch = "exact", Callback;
        public string Placement = "any";
        public int PlacementNode = -1;
        public string[] Types = Array.Empty<string>();
        public int MinimumCount = 1, MaximumCount = 1;
        public string From = "top", Position = "top", AfterCallback;
        public bool Reveal;
        public string Timing = "turnEnd";
        public int ScryIndex;
        public IReadOnlyList<EffectInstruction> After = Array.Empty<EffectInstruction>();
        public ActivationCost[] Costs = Array.Empty<ActivationCost>();
    }
    public sealed class CardEffectRules
    {
        public string Name, Faction, Race, Type, Sign;
        public int Power, Time, AttackTimeLimit = int.MaxValue;
        public int ForesightCount;
        public ForesightMark ForesightMark;
        public bool HasPlay, HasActivate, OwnTurnOnly, IsToken;
        public TestCardZone ActivateZone = TestCardZone.Board;
        public int ActivateCost;
        public ActivationCost[] ActivationCosts = Array.Empty<ActivationCost>();
        public ActivationCost[] PlayCosts = Array.Empty<ActivationCost>();
        public bool OncePerTurn;
        public bool OncePerNamePerTurn;
        public string AuraRace;
        public int AuraPower;
        public TestCardZone AuraZone = TestCardZone.OffField;
        public bool AuraOwnTurn = true;
    }
    public sealed class EffectContext
    {
        public string DefinitionId;
        public string InstanceId;
        public int Time, Power, Node;
        public int Owner, ActivePlayer, Turn, Cost, Damage, HandCount;
        public int EmptyBoardCount, ContractZoneCount;
        public ClockKind Clock, OpponentClock;
        public int OpponentCost, OpponentDamage, OpponentHandCount;
        public ScryCardInfo[] PublicCards = Array.Empty<ScryCardInfo>();
        public EffectEvent Event;
        public Events.BattleEvent TriggerEvent;
        public TestCardZone SourceZone;
        public string Reason, ContractName;
        public Dictionary<string, int> OwnFieldNameCounts;
        public ScryCardInfo[] ScryCards = Array.Empty<ScryCardInfo>();
        public Dictionary<VariableScope, Dictionary<string, EffectValue>> Variables = VariableContexts.Empty();
        public Dictionary<string, string> Results = new Dictionary<string, string>();
        public Dictionary<string, ScryCardInfo[]> Sets = new Dictionary<string, ScryCardInfo[]>();
    }
    // Implementations cannot mutate the match. They return bounded instructions to the authoritative interpreter.
    public interface ICardEffectProvider
    {
        CardEffectRules Rules(string definitionId);
        bool CanPlay(EffectContext context);
        bool CanActivate(EffectContext context);
        IReadOnlyList<EffectInstruction> Build(EffectContext context);
    }
    public sealed class ScryCardInfo
    {
        public string DefinitionId, Name, Type, Race, Sign, InstanceId;
        public TestCardZone Zone;
        public int Node, ZoneEnteredTurn, LastRebuiltTurn;
        public int LastEffectRebuiltTurn = -1;
        public int Owner, Power, Time;
    }
    public interface ICardNameProvider
    {
        IReadOnlyList<string> CardNames { get; }
    }
    public interface IScryEffectProvider
    {
        IReadOnlyList<EffectInstruction> BuildScry(EffectContext context, string callback);
    }
    public sealed class StatModifier
    {
        public string Stat;
        public int Amount, ExpiresAfterTurn;
        public Guid Source;
        public bool SetValue, SourceBound, BattleBound;
        public int SourceGeneration;
        public TestCardZone SourceZone;
    }
    public sealed class EffectChoice
    {
        public int Player;
        public string Prompt;
        public Guid[] Candidates = Array.Empty<Guid>();
        public int[] ZoneCandidates = Array.Empty<int>();
        public bool Optional;
        public bool IsDeckView;
        public bool IsForesightOffer, PublicView;
        public bool IsBoardPlacement, IsNumber, IsPayment;
        public int NumberMinimum, NumberMaximum;
        public TriggerOption[] TriggerOptions = Array.Empty<TriggerOption>();
        public string[] NameOptions = Array.Empty<string>();
        public int[] DeckPositions = Array.Empty<int>();
        public NetworkTestMatch.Card[] ViewedCards = Array.Empty<NetworkTestMatch.Card>();
        public double Seconds;
    }
}
