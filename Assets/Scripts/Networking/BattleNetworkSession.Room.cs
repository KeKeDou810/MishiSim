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
        private RoomFlowView roomView;
        private NetworkRoomState roomState;
        private readonly Dictionary<NetworkConnection, string> roomNames = new Dictionary<NetworkConnection, string>();
        private readonly Dictionary<NetworkConnection, double> chatSentAt = new Dictionary<NetworkConnection, double>();
        private readonly List<string> roomChat = new List<string>();
        private readonly bool[] roomReady = new bool[2];
        private string[] roomDeck;
        private NetworkRoomRules roomRules;
        private void BindRoom()
        {
            BindOpening();
            roomView = FindFirstObjectByType<RoomFlowView>();
            if (roomView == null) throw new InvalidOperationException("缺少房间界面 prefab。");
            roomView.HidePages();
            roomView.ReadyRequested += () => SendRoomCommand(0, roomState.Seat >= 0 && !roomState.Ready[roomState.Seat]);
            roomView.SpectateRequested += () => SendRoomCommand(1);
            roomView.PlayRequested += () => SendRoomCommand(2);
            roomView.StartRequested += () => SendRoomCommand(3);
            roomView.LeaveRequested += LeaveBattle;
            roomView.DeckSelected += SelectRoomDeck;
            roomView.ChatSubmitted += SendRoomChat;
            roomView.RulesSubmitted += rules => { if (sessionOpen && network != null) network.ClientManager.Broadcast(rules); };
            var log = FindFirstObjectByType<BattleLogWindow>();
            if (log != null) { log.ChatSubmitted += SendRoomChat; log.UnreadChanged += clockHud.SetChatUnread; }
        }
        private void RegisterRoomNetwork()
        {
            RegisterAccess();
            roomState = new NetworkRoomState { Seat = -1, Ready = new bool[2] };
            var mode = database.ActiveMode;
            roomRules = new NetworkRoomRules { OpeningHand = mode.OpeningHand, DrawPerTurn = mode.DrawPerTurn, TurnTimeSeconds = mode.TurnTimeSeconds,
                ActionTimeRefundSeconds = mode.ActionTimeRefundSeconds, ResponseTimeSeconds = mode.ResponseTimeSeconds };
            roomDeck = null;
            roomView.SetDecks(new[] { "请选择卡组" }.Concat(database.SavedDeckNames()).ToArray());
            network.ServerManager.RegisterBroadcast<NetworkRoomCommand>(OnRoomCommand);
            network.ServerManager.RegisterBroadcast<NetworkChatRequest>(OnRoomChat);
            network.ServerManager.RegisterBroadcast<NetworkRoomRules>(OnRoomRules);
            network.ClientManager.RegisterBroadcast<NetworkRoomState>(OnRoomState);
            network.ClientManager.RegisterBroadcast<NetworkChatLine>(OnChatLine);
            network.ServerManager.RegisterBroadcast<NetworkOpeningCommand>(OnOpeningCommand);
            network.ClientManager.RegisterBroadcast<NetworkOpeningState>(OnOpeningState);
        }
        private void SelectRoomDeck(string name)
        {
            try
            {
                if (name == "请选择卡组") roomDeck = null;
                else
                {
                    var data = DeckStorage.Read(database.DeckDirectory, name);
                    if (data.ModeId != database.ActiveMode.Id) throw new InvalidOperationException("卡组游戏模式与当前房间不一致。");
                    var ids = data.Cards.SelectMany(e => Enumerable.Repeat(e.Key, e.Value)).ToArray();
                    database.ValidateDeck(ids); roomDeck = ids;
                }
            }
            catch (Exception e) { roomDeck = null; roomView.ShowFailure(e.Message); }
        }
        private void SendRoomCommand(int kind, bool ready = false)
        {
            if (!sessionOpen || network == null || snapshot.HasValue) return;
            if (kind == 0 && ready && roomDeck == null)
            { roomView.ShowFailure("请先在房间内选择卡组，再准备。"); return; }
            network.ClientManager.Broadcast(new NetworkRoomCommand { Kind = kind, Ready = ready, Deck = ready ? roomDeck : null });
        }
        private void AcceptRoomMember(NetworkConnection connection, NetworkHello message)
        {
            if (stopping || !sessionOpen || roomNames.ContainsKey(connection)) return;
            try
            {
                VerifyAccess(connection, message);
                if (message.Protocol != Protocol || message.ContentHash != hash || message.ModeId != database.ActiveMode.Id)
                    throw new InvalidOperationException("版本、卡库、地图或游戏模式不一致，请使用相同版本与 Content。");
                string name = RoomText.Validate(message.Name, RoomText.NameLimit, "玩家名");
                if (RoomText.Validate(message.RoomName, RoomText.RoomLimit, "房间名") != MainMenuController.RoomName)
                    throw new InvalidOperationException("房间名不匹配。");
                if (RestoreSeat(connection, message)) { memberIdentities[connection] = ConnectionIdentity(connection); return; }
                int seat = Enumerable.Range(0, 2).Where(i => !seats.Values.Contains(i) && !reservations.ContainsKey(i)).DefaultIfEmpty(-1).First();
                if (message.Spectator || match != null || OpeningActive || seat < 0) audience.AddSpectator(connection);
                else audience.AddPlayer(connection, seat);
                roomNames.Add(connection, name); resumeTokens[connection] = RoomIdentity.RandomToken(); memberIdentities[connection] = ConnectionIdentity(connection); BroadcastRoom();
                foreach (string line in roomChat) network.ServerManager.Broadcast(connection, new NetworkChatLine { Text = line });
                if (match != null) network.ServerManager.Broadcast(connection, CreatePublicFrame(publicSequence, Array.Empty<NetworkCardPresentation>()));
                if (openingDuel != null) SendOpening(connection);
            }
            catch (Exception e) { RejectConnection(connection, e.Message); }
        }
        private void OnRoomCommand(NetworkConnection connection, NetworkRoomCommand request, Channel channel)
        {
            if (stopping || match != null || OpeningActive || RoomPaused || !roomNames.ContainsKey(connection)) return;
            try
            {
                bool player = seats.TryGetValue(connection, out int seat);
                if (request.Kind == 1 && player)
                {
                    audience.Remove(connection); audience.AddSpectator(connection); roomReady[seat] = false;
                    submittedDecks[seat] = null; playerCards.RemoveAll(c => c.Owner == seat);
                }
                else if (request.Kind == 2 && !player)
                {
                    int free = Enumerable.Range(0, 2).Where(i => !seats.Values.Contains(i)).DefaultIfEmpty(-1).First();
                    if (free < 0) throw new InvalidOperationException("玩家席位已满，可继续观战。");
                    audience.Remove(connection); audience.AddPlayer(connection, free);
                }
                else if (request.Kind == 0 && player)
                {
                    if (request.Ready) PrepareRoomDeck(seat, request);
                    roomReady[seat] = request.Ready;
                }
                else if (request.Kind == 3)
                {
                    if (!connection.IsLocalClient) throw new InvalidOperationException("只有房主可以开始游戏。");
                    if (seats.Count != 2 || !roomReady.All(value => value)) throw new InvalidOperationException("需要两位玩家都准备后才能开始。");
                    StartOpening();
                }
                BroadcastRoom();
            }
            catch (Exception e) { network.ServerManager.Broadcast(connection, new NetworkNotice { Text = e.Message }); }
        }
        private void PrepareRoomDeck(int seat, NetworkRoomCommand request)
        {
                var deck = database.ValidateDeck(request.Deck);
                int playerNode = nodes.Values.Single(n => n.NodeKind == BoardNodeKind.Player && n.OwnerId == seat).NodeId;
                // Lua playerCards order defines the starting face; version IDs do not.
                var pair = deck.PlayerCards.ToArray();
                if (pair.Length != 2 || pair.Any(id => string.IsNullOrEmpty(id) || !catalog.Cards.TryGetValue(id, out var def) || !def.IsPlayer))
                    throw new InvalidOperationException("契约需要配置两张有效的玩家卡。");
                submittedDecks[seat] = deck.Entries.SelectMany(e => Enumerable.Repeat(catalog.Cards[e.Key], e.Value))
                    .Select(c => new NetworkTestMatch.Card(Guid.Empty, c.Id, seat, -1, c.Power, c.IsContract, TestCardZone.Deck, c.IsDecision, c.Level)).ToArray();
                playerCards.RemoveAll(c => c.Owner == seat);
                for (int i = 0; i < 2; i++) playerCards.Add(new NetworkCardInfo { InstanceId = Guid.NewGuid().ToString("N"), DefinitionId = pair[i], Owner = seat,
                    NodeId = playerNode, FaceDown = i == 1, Zone = (int)TestCardZone.Player });
        }
        private void StartRoomMatch()
        {
                var created = NetworkTestMatch.FromDecks(board, nodes.Keys, hostStart.NodeId, guestStart.NodeId,
                    submittedDecks, new MatchDrawRules(roomRules.OpeningHand, roomRules.DrawPerTurn,
                        database.ActiveMode.FirstTurnSkip.Select(p => (TestTurnPhase)Enum.Parse(typeof(TestTurnPhase), p)),
                        roomRules.TurnTimeSeconds, roomRules.ActionTimeRefundSeconds, roomRules.ResponseTimeSeconds), new System.Random());
                created.AttachEffects(effectCatalog, catalog.Cards[submittedDecks[0].Single(c => c.IsContract).DefinitionId].Clock,
                    catalog.Cards[submittedDecks[1].Single(c => c.IsContract).DefinitionId].Clock, Enumerable.Range(0, sharedOffFieldZones.Length));
                foreach (int owner in new[] { 0, 1 })
                {
                    var pair = playerCards.Where(c => c.Owner == owner).ToArray();
                    created.AddPlayerCards(owner, pair[0].NodeId, pair.Select(c => new NetworkTestMatch.Card(Guid.Parse(c.InstanceId), c.DefinitionId, owner, c.NodeId, 0, false, TestCardZone.Player)).ToArray());
                }
                match = created;
                match.SetFirstPlayer(openingDuel.FirstPlayer);
                openingDeadline = Time.unscaledTime + .8f;
                matchStartedAt = Time.realtimeSinceStartupAsDouble;
                lastClockUpdate = Time.realtimeSinceStartupAsDouble;
                nextClockBroadcast = lastClockUpdate;
                BroadcastSnapshots();
        }
        private void ResetVacantSeats()
        {
            foreach (int seat in Enumerable.Range(0, 2).Where(i => !seats.Values.Contains(i) && !reservations.ContainsKey(i)))
            { roomReady[seat] = false; submittedDecks[seat] = null; playerCards.RemoveAll(c => c.Owner == seat); }
        }
        private void BroadcastRoom()
        {
            if (network == null) return;
            var names = new[] { "空位", "空位" };
            foreach (var seat in reservations) names[seat.Key] = seat.Value.Name + "（重连中）";
            foreach (var seat in seats) names[seat.Value] = roomNames[seat.Key];
            if (MainMenuController.UseSteam) SteamRoomService.Existing?.Publish(seats.Count + reservations.Count, observers.Count, match != null || OpeningActive);
            foreach (var connection in roomNames.Keys)
                network.ServerManager.Broadcast(connection, new NetworkRoomState { RoomName = MainMenuController.RoomName, ModeName = database.ActiveMode.Name,
                    Names = names, Ready = roomReady.ToArray(), Seat = seats.TryGetValue(connection, out int seat) ? seat : -1,
                    IsHost = connection.IsLocalClient, Spectators = observers.Count, Started = match != null || OpeningActive, Rules = roomRules,
                    RoomInstance = roomInstance, ResumeToken = resumeTokens.TryGetValue(connection, out string token) ? token : null,
                    Reconnecting = RoomPaused, ReconnectSeconds = RoomPaused ? (float)Math.Max(0, reservations.Values.Min(r => r.Until) - Time.realtimeSinceStartupAsDouble) : 0 });
        }
        private void OnRoomState(NetworkRoomState state, Channel channel)
        {
            roomState = state; spectator = state.Seat < 0; connectDeadline = 0;
            bool restored = reconnecting; reconnecting = false;
            if (restored && !network.ServerManager.Started) roomChat.Clear();
            clientResumeToken = state.Seat >= 0 ? state.ResumeToken : null;
            clockHud.SetConnectionStatus(state.Reconnecting ? $"等待重连 · {Math.Ceiling(state.ReconnectSeconds)} 秒" : null);
            if (state.Reconnecting) { status = "玩家掉线，等待重连（操作与计时暂停）"; CancelInteraction(); }
            else if (restored) status = "已重新连接，正在同步局面。";
            roomView.Apply(state); clockHud.SetNames(state.Names);
        }
        private void SendRoomChat(string text)
        {
            if (!sessionOpen || network == null || replay != null) return;
            try { network.ClientManager.Broadcast(new NetworkChatRequest { Text = RoomText.Validate(text, RoomText.ChatLimit, "聊天消息") }); }
            catch (ArgumentException e) { roomView.ShowFailure(e.Message); }
        }
        public static void ValidateRoomRules(NetworkRoomRules rules, int deckSize)
        {
            RoomRuleLimits.Validate(rules.OpeningHand, rules.DrawPerTurn, rules.TurnTimeSeconds, rules.ActionTimeRefundSeconds, rules.ResponseTimeSeconds, deckSize);
        }
        private void OnRoomRules(NetworkConnection connection, NetworkRoomRules rules, Channel channel)
        {
            if (!roomNames.ContainsKey(connection) || !connection.IsLocalClient || match != null || OpeningActive || RoomPaused) return;
            try
            {
                ValidateRoomRules(rules, database.ActiveMode.DeckSize); roomRules = rules;
                roomReady[0] = roomReady[1] = false; BroadcastRoom();
            }
            catch (ArgumentException e) { network.ServerManager.Broadcast(connection, new NetworkNotice { Text = e.Message }); }
        }
        private void OnRoomChat(NetworkConnection connection, NetworkChatRequest request, Channel channel)
        {
            if (!roomNames.TryGetValue(connection, out string name)) return;
            try
            {
                string text = RoomText.Validate(request.Text, RoomText.ChatLimit, "聊天消息");
                double now = Time.realtimeSinceStartupAsDouble;
                if (chatSentAt.TryGetValue(connection, out double before) && now - before < .6) return;
                chatSentAt[connection] = now;
                BroadcastPublic(new NetworkChatLine { Text = name + "：" + text });
            }
            catch (ArgumentException) { }
        }
        private void OnChatLine(NetworkChatLine line, Channel channel)
        {
            roomChat.Add(line.Text); if (roomChat.Count > 100) roomChat.RemoveAt(0);
            string text = string.Join("\n", roomChat);
            roomView.SetChat(text);
            var log = battleLog != null ? battleLog : FindFirstObjectByType<BattleLogWindow>();
            log?.SetChat(text, !roomView.InLobby);
        }
        private void ClearRoomNetwork()
        {
            ClearAccess();
            if (network != null && network.Initialized)
            {
                network.ServerManager.UnregisterBroadcast<NetworkRoomCommand>(OnRoomCommand);
                network.ServerManager.UnregisterBroadcast<NetworkChatRequest>(OnRoomChat);
                network.ServerManager.UnregisterBroadcast<NetworkRoomRules>(OnRoomRules);
                network.ClientManager.UnregisterBroadcast<NetworkRoomState>(OnRoomState);
                network.ClientManager.UnregisterBroadcast<NetworkChatLine>(OnChatLine);
                network.ServerManager.UnregisterBroadcast<NetworkOpeningCommand>(OnOpeningCommand);
                network.ClientManager.UnregisterBroadcast<NetworkOpeningState>(OnOpeningState);
            }
            roomNames.Clear(); chatSentAt.Clear(); roomReady[0] = roomReady[1] = false;
            ClearOpening();
        }
    }
}
