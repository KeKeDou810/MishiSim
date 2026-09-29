using FishNet.Broadcast;

namespace Mishi.Networking
{
    public struct NetworkHello : IBroadcast { public int Protocol; public string ContentHash; public string ModeId; public string[] Deck; public bool Spectator; public string Name, RoomName; public string Game, Version, RoomInstance, Proof, ResumeToken; }
    public struct NetworkRoomChallengeRequest : IBroadcast { public string Game, Version, RoomInstance; public int Protocol; }
    public struct NetworkRoomChallenge : IBroadcast { public string Game, Version, RoomInstance, Salt, Nonce; public int Protocol; }
    public struct NetworkRoomLeave : IBroadcast { }
    public struct NetworkRoomCommand : IBroadcast { public int Kind; public bool Ready; public string[] Deck; }
    public struct NetworkRoomState : IBroadcast { public string RoomName, ModeName; public string[] Names; public bool[] Ready; public int Seat, Spectators; public bool IsHost, Started; public NetworkRoomRules Rules; public string ResumeToken, RoomInstance; public bool Reconnecting; public float ReconnectSeconds; }
    public struct NetworkRoomRules : IBroadcast { public int OpeningHand, DrawPerTurn, TurnTimeSeconds, ActionTimeRefundSeconds, ResponseTimeSeconds; }
    public struct NetworkChatRequest : IBroadcast { public string Text; }
    public struct NetworkChatLine : IBroadcast { public string Text; }
    public struct NetworkOpeningCommand : IBroadcast { public string SessionId; public int Epoch, Value; }
    public struct NetworkOpeningState : IBroadcast
    {
        public string SessionId;
        public int Stage, Epoch, Round, You, Winner, FirstPlayer;
        public float Seconds;
        public int[] Gestures;
        public bool[] Selected;
        public bool CanRedraw, HandDecided;
        public NetworkCardInfo[] OwnHand, Revealed;
    }
    public struct NetworkCommand : IBroadcast
    {
        public string MatchId, CardId;
        public long Sequence;
        public int Revision, Kind, TargetNode;
        // Player identity is resolved from the connection, never sent by the client.
    }
    public struct NetworkCardInfo
    {
        public bool HiddenChoice;
        public bool NameEffectsBlocked;
        public int PlayCostAdjustment;
        public int ZoneEnteredTurn;
        public string InstanceId, DefinitionId, AttachedTo;
        public int Owner, NodeId, Zone, StackOrder, Power, Time;
        public int AttackRange;
        public int ActivationUses, NamedActivationUses, PlayUses, NamedPlayUses;
        public bool Tapped, FaceDown, Covered;
        public bool EffectsSuppressed;
    }
    public struct NetworkTriggerInfo
    {
        public string Id, DefinitionId, Label;
        public int Owner;
    }
    public struct NetworkSnapshot : IBroadcast
    {
        public string MatchId;
        public bool Spectator;
        public int[] HandCounts;
        public int You, Revision, ActivePlayer, Turn, Phase, OccupationNode, DecisionsUsed;
        public string LastAction;
        public string[] ActionLog;
        public NetworkVariableInfo[] Variables;
        public bool ContractMoved;
        public NetworkCardInfo[] Board, OwnHand, PublicPiles;
        public int OpponentHandCount;
        public int[] DeckCounts;
        public int[] CostPointers, DamagePointers, ClockKinds;
        public int ChoicePlayer;
        public string ChoicePrompt;
        public string[] ChoiceCards, ChoiceNames;
        public NetworkTriggerInfo[] ChoiceTriggers;
        public int[] ChoiceZones;
        public bool ChoiceOptional, ChoiceIsDeckView;
        public bool ChoiceIsForesightOffer, ChoicePublicView;
        public bool ChoiceIsBoardPlacement, ChoiceIsNumber, ChoiceIsPayment;
        public int ChoiceNumberMinimum, ChoiceNumberMaximum;
        public int[] DeckPositions;
        public NetworkCardInfo[] ViewedCards;
        public int Winner;
        public string ResultReason;
        public NetworkCardInfo[] PlayerCards;
        public float RemainingTurnSeconds;
        public bool TimerRunning;
        public int ResponsePlayer;
        public float RemainingResponseSeconds;
    }
    // Separate clock updates must not rebuild card views, cancel drags or invalidate commands.
    public struct NetworkTurnTimer : IBroadcast
    {
        public double ElapsedSeconds;
        public string MatchId;
        public int Turn, Revision;
        public long Sequence;
        public float RemainingSeconds;
        public bool Running;
        public int ResponsePlayer;
        public float RemainingResponseSeconds;
    }
    public struct NetworkCardPresentation : IBroadcast
    {
        public string MatchId, DefinitionId;
        public long Sequence;
        public int Owner, Kind;
        public int DiceSides, DiceResult;
    }
    public struct NetworkNotice : IBroadcast { public string Text; public bool Fatal; }
    // One sanitized stream serves both spectators and local recording files.
    public struct NetworkPublicFrame : IBroadcast
    {
        public string[] LogDelta;
        public long Sequence;
        public double ElapsedSeconds;
        public NetworkSnapshot State;
        public NetworkCardPresentation[] Presentations;
        public bool HasAction;
        public int Actor, CommandKind;
        public long CommandSequence;
    }
}

