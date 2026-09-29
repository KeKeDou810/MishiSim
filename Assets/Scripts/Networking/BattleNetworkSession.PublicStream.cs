using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Connection;
using FishNet.Transporting;
using Mishi.Battle;
using UnityEngine;

namespace Mishi.Networking
{
    public sealed partial class BattleNetworkSession
    {
        private IReadOnlyCollection<NetworkConnection> observers => audience.Spectators;
        private bool spectator;
        private long publicSequence, receivedPublicSequence;
        private long sentPublicLogSequence;
        private readonly List<string> receivedPublicLog = new List<string>();
        private readonly HashSet<NetworkConnection> publicRecipients = new HashSet<NetworkConnection>();
        private double matchStartedAt;
        private double PublicElapsed => Math.Max(0, Time.realtimeSinceStartupAsDouble - matchStartedAt);
        private void BroadcastPublic<T>(T message) where T : struct, FishNet.Broadcast.IBroadcast
        {
            publicRecipients.Clear(); publicRecipients.UnionWith(seats.Keys); publicRecipients.UnionWith(observers);
            // FishNet serializes once, then distributes the same payload to the room audience.
            network.ServerManager.Broadcast(publicRecipients, message);
        }
        private string ComputeSessionHash(string modeId = null)
        {
            var map = string.Join(";", nodes.Values.OrderBy(n => n.NodeId).Select(n =>
                $"{n.NodeId}:{n.ZoneId}:{n.NodeKind}:{n.OwnerId}:" +
                string.Join(",", nodes.Keys.Where(id => board.AreAdjacent(n.NodeId, id)).OrderBy(id => id))));
            map += "|offField:" + string.Join(";", sharedOffFieldZones.Select(z => z.ZoneId));
            map += $"|{Protocol}|{hostStart.NodeId}|{guestStart.NodeId}|{modeId ?? database.ActiveMode.Id}";
            return NetworkContentHash.Compute(database.ContentRoot, map);
        }
        private static NetworkCardInfo HideCardIdentity(NetworkCardInfo card) => new NetworkCardInfo {
            InstanceId = card.InstanceId, Owner = card.Owner, NodeId = card.NodeId, Zone = card.Zone,
            StackOrder = card.StackOrder, Covered = card.Covered, Tapped = card.Tapped, FaceDown = true, AttachedTo = card.AttachedTo
        };
        private NetworkCardPresentation EncodePresentation(CardPresentation item) => new NetworkCardPresentation {
            MatchId = match.MatchId.ToString("N"), Sequence = item.Sequence, DefinitionId = item.DefinitionId, Owner = item.Owner,
            Kind = (int)item.Kind, DiceSides = item.DiceSides, DiceResult = item.DiceResult
        };
        private NetworkPublicFrame CreatePublicFrame(long id, NetworkCardPresentation[] presentations)
        {
            var state = BuildSnapshot(-1);
            // History is streamed as deltas. A late join baseline contains no earlier actions.
            state.ActionLog = Array.Empty<string>();
            state.LastAction = state.Winner >= 0 ? state.ResultReason : "已同步当前公开场面";
            BattleRecording.ValidatePublic(state);
            return new NetworkPublicFrame { Sequence = id, ElapsedSeconds = PublicElapsed, State = state, Presentations = presentations, LogDelta = Array.Empty<string>(), Actor = -1 };
        }
        private static long PublicLogSequence(string line) => long.TryParse(line.Split('.')[0], out var id) ? id : 0;
        private void OnPublicFrame(NetworkPublicFrame frame, Channel channel)
        {
            if (!sessionOpen || stopping || frame.Sequence <= receivedPublicSequence) return;
            try { BattleRecording.ValidatePublic(frame.State); }
            catch (Exception e) { status = e.Message; stopNextUpdate = true; return; }
            receivedPublicSequence = frame.Sequence;
            receivedPublicLog.AddRange(frame.LogDelta ?? Array.Empty<string>());
            if (receivedPublicLog.Count > 160) receivedPublicLog.RemoveRange(0, receivedPublicLog.Count - 160);
            frame.State.ActionLog = receivedPublicLog.ToArray();
            if (frame.State.Winner < 0 && receivedPublicLog.Count > 0) frame.State.LastAction = receivedPublicLog[receivedPublicLog.Count - 1];
            if (spectator)
            {
                OnSnapshot(frame.State, channel);
                foreach (var presentation in frame.Presentations ?? Array.Empty<NetworkCardPresentation>()) OnCardPresentation(presentation, channel);
            }
            RecordFrame(frame);
        }
    }
}
