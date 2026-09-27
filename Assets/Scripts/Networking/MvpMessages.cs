using FishNet.Broadcast;

namespace Mishi.Networking
{
    public struct MvpHello : IBroadcast { public int Protocol; public string ContentHash; public string ModeId; public string[] Deck; }
    public struct MvpCommand : IBroadcast
    {
        public string MatchId, CardId;
        public long Sequence;
        public int Revision, Kind, TargetNode;
        // Player identity is resolved from the connection, never sent by the client.
    }
    public struct MvpCardInfo
    {
        public string InstanceId, DefinitionId;
        public int Owner, NodeId, Zone;
        public bool Tapped;
    }
    public struct MvpSnapshot : IBroadcast
    {
        public string MatchId;
        public int You, Revision, ActivePlayer, Turn, Phase, OccupationNode;
        public string LastAction;
        public bool ContractMoved;
        public MvpCardInfo[] Board, OwnHand, PublicPiles;
        public int OpponentHandCount;
        public int[] DeckCounts;
    }
    public struct MvpNotice : IBroadcast { public string Text; public bool Fatal; }
}

