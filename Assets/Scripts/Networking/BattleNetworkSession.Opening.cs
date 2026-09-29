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
        private OpeningDuel openingDuel;
        private OpeningMatchView openingView;
        private NetworkOpeningState openingState;
        private string openingSessionId;
        private double openingUpdatedAt, openingBroadcastAt, handChoiceDeadline, revealUntil;
        private bool openingHandsShown;
        private readonly List<NetworkCardInfo> openingRevealed = new List<NetworkCardInfo>();
        private bool OpeningActive => openingDuel != null && openingDuel.Stage != OpeningStage.Complete;

        private void BindOpening()
        {
            openingView = FindFirstObjectByType<OpeningMatchView>();
            if (openingView == null) throw new InvalidOperationException("场景缺少 OpeningMatchUI prefab。");
            openingView.GetComponent<OpeningGestureTable>().Configure(hostStart.CardAnchor, guestStart.CardAnchor);
            openingView.Chosen += value => {
                if (sessionOpen && network != null)
                    network.ClientManager.Broadcast(new NetworkOpeningCommand { SessionId = openingState.SessionId, Epoch = openingState.Epoch, Value = value });
            };
        }
        private void StartOpening()
        {
            openingSessionId = Guid.NewGuid().ToString("N");
            openingDuel = new OpeningDuel(roomRules.ResponseTimeSeconds, new System.Random());
            openingHandsShown = false; openingRevealed.Clear(); revealUntil = 0;
            openingUpdatedAt = Time.realtimeSinceStartupAsDouble; openingBroadcastAt = 0;
            BroadcastOpening();
        }
        private void OnOpeningCommand(NetworkConnection connection, NetworkOpeningCommand command, Channel channel)
        {
            if (stopping || RoomPaused || !OpeningActive || !seats.TryGetValue(connection, out int player)) return;
            AdvanceOpening(); // Expiry wins over a late packet.
            if (!OpeningActive || command.SessionId != openingSessionId || command.Epoch != openingDuel.Epoch) { SendOpening(connection); return; }
            if (openingDuel.Stage == OpeningStage.Hand)
            {
                if (openingHandsShown && command.Value >= 0 && command.Value <= 1 && match.DecideOpeningHand(player, command.Value == 1, out var revealed))
                {
                    if (revealed.Length > 0)
                    {
                        openingRevealed.AddRange(revealed.Select(ToInfo));
                        revealUntil = Time.realtimeSinceStartupAsDouble + 5;
                    }
                    BroadcastSnapshots();
                }
            }
            else openingDuel.Choose(player, command.Epoch, command.Value);
            BeginOpeningHandIfReady();
            BroadcastOpening();
        }
        private void BeginOpeningHandIfReady()
        {
            if (openingDuel.Stage != OpeningStage.Hand || match != null) return;
            try { StartRoomMatch(); BroadcastRoom(); }
            catch (Exception error)
            {
                match = null; ClearOpening(); roomReady[0] = roomReady[1] = false;
                BroadcastOpening(); BroadcastRoom();
                BroadcastPublic(new NetworkNotice { Text = "无法开始对局：" + error.Message });
                Debug.LogException(error, this);
            }
        }
        private void AdvanceOpening()
        {
            if (!OpeningActive || stopping) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (RoomPaused)
            {
                double pause = Math.Max(0, now - openingUpdatedAt); openingUpdatedAt = now;
                openingDeadline += (float)pause; handChoiceDeadline += pause; revealUntil += pause;
                return;
            }
            bool changed = openingDuel.Advance(Math.Max(0, now - openingUpdatedAt)); openingUpdatedAt = now;
            BeginOpeningHandIfReady();
            if (openingDuel == null) return;
            if (openingDuel.Stage == OpeningStage.Hand && match != null)
            {
                if (!openingHandsShown && Time.unscaledTime >= openingDeadline)
                {
                    match.DealOpeningHands(true); openingHandsShown = true;
                    handChoiceDeadline = now + roomRules.ResponseTimeSeconds;
                    // Ineligible hands need no decision and are never publicly revealed.
                    for (int p = 0; p < 2; p++) if (!match.CanRedrawOpeningHand(p)) match.DecideOpeningHand(p, false, out _);
                    BroadcastSnapshots(); changed = true;
                }
                if (openingHandsShown)
                {
                    if (now >= handChoiceDeadline && !match.OpeningHandsDecided)
                    {
                        for (int p = 0; p < 2; p++) match.DecideOpeningHand(p, false, out _);
                        changed = true;
                    }
                    if (match.OpeningHandsDecided && now >= revealUntil)
                    {
                        openingDuel.Complete(); match.CompleteOpeningHands();
                        lastClockUpdate = now; BroadcastSnapshots(); changed = true;
                    }
                }
            }
            if (changed || now >= openingBroadcastAt) { openingBroadcastAt = now + .25; BroadcastOpening(); }
        }
        private void SendOpening(NetworkConnection connection)
        {
            if (network == null) return;
            int viewer = seats.TryGetValue(connection, out int seat) ? seat : -1;
            var state = new NetworkOpeningState { SessionId = openingSessionId, You = viewer, Stage = (int)(openingDuel?.Stage ?? OpeningStage.None),
                Epoch = openingDuel?.Epoch ?? 0, Round = openingDuel?.Round ?? 0, Winner = openingDuel?.Winner ?? -1,
                FirstPlayer = openingDuel?.FirstPlayer ?? -1, Gestures = new[] { -1, -1 }, Selected = new bool[2],
                OwnHand = Array.Empty<NetworkCardInfo>(), Revealed = Array.Empty<NetworkCardInfo>() };
            if (openingDuel != null)
            {
                state.Seconds = (float)openingDuel.Seconds;
                for (int p = 0; p < 2; p++) { state.Gestures[p] = openingDuel.VisibleGesture(p, viewer); state.Selected[p] = openingDuel.HasChosen(p); }
                if (openingDuel.Stage == OpeningStage.Hand && openingHandsShown)
                {
                    state.Seconds = (float)Math.Max(0, (match.OpeningHandsDecided ? revealUntil : handChoiceDeadline) - Time.realtimeSinceStartupAsDouble);
                    state.HandDecided = viewer < 0 || match.OpeningHandDecided(viewer);
                    state.CanRedraw = viewer >= 0 && match.CanRedrawOpeningHand(viewer);
                    if (viewer >= 0) state.OwnHand = match.ForPlayer(viewer).OwnHand.Select(ToInfo).ToArray();
                    state.Revealed = openingRevealed.ToArray();
                }
            }
            network.ServerManager.Broadcast(connection, state);
        }
        private void BroadcastOpening() { foreach (var connection in roomNames.Keys) SendOpening(connection); }
        private void OnOpeningState(NetworkOpeningState state, Channel channel)
        {
            if (!sessionOpen || stopping) return;
            openingState = state;
            perspective?.SetCentered(true); perspective?.SetPlayer(Math.Max(0, state.You));
            openingView.Apply(state, catalog, roomState.Names);
        }
        private void ClearOpening()
        {
            openingDuel = null; openingHandsShown = false; openingRevealed.Clear(); openingSessionId = null;
            if (openingView != null) openingView.Hide();
        }
    }
}
