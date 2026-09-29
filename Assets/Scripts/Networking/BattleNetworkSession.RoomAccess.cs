using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Connection;
using FishNet.Transporting;
using UnityEngine;

namespace Mishi.Networking
{
    public sealed partial class BattleNetworkSession
    {
        private sealed class Challenge { public string Nonce; public double Until; }
        private sealed class Reservation { public string Token, Name, Identity; public double Until; }
        private readonly Dictionary<NetworkConnection, Challenge> challenges = new Dictionary<NetworkConnection, Challenge>();
        private readonly Dictionary<NetworkConnection, string> resumeTokens = new Dictionary<NetworkConnection, string>();
        private readonly Dictionary<int, Reservation> reservations = new Dictionary<int, Reservation>();
        private readonly HashSet<NetworkConnection> intentionalLeaves = new HashSet<NetworkConnection>();
        private string roomInstance, passwordSalt, clientResumeToken, reconnectAddress;
        private byte[] passwordKey;
        private bool reconnecting;
        private double reconnectUntil, retryAt, reservationPublishedAt;
        private bool RoomPaused => reservations.Count > 0;
        private void RegisterAccess()
        {
            roomInstance = MainMenuController.UseSteam ? SteamRoomService.Instance.RoomInstance : Guid.NewGuid().ToString("N");
            passwordSalt = RoomIdentity.RandomToken(); passwordKey = RoomIdentity.Key(MainMenuController.RoomPassword, passwordSalt);
            network.ServerManager.RegisterBroadcast<NetworkRoomChallengeRequest>(OnChallengeRequest);
            network.ServerManager.RegisterBroadcast<NetworkRoomLeave>(OnRoomLeave);
            network.ClientManager.RegisterBroadcast<NetworkRoomChallenge>(OnChallenge);
        }
        private void RequestChallenge() => network.ClientManager.Broadcast(new NetworkRoomChallengeRequest {
            Game = RoomIdentity.Game, Protocol = Protocol, Version = Application.version,
            RoomInstance = MainMenuController.UseSteam ? SteamRoomService.Instance.RoomInstance : roomState.RoomInstance });
        private string ConnectionIdentity(NetworkConnection connection) => MainMenuController.UseSteam
            ? connection.IsLocalClient ? Steamworks.SteamUser.GetSteamID().m_SteamID.ToString() : network.TransportManager.Transport.GetConnectionAddress(connection.ClientId)
            : "direct";
        private void OnChallengeRequest(NetworkConnection connection, NetworkRoomChallengeRequest request, Channel channel)
        {
            if (roomNames.ContainsKey(connection) || challenges.ContainsKey(connection)) return;
            if (request.Game != RoomIdentity.Game || request.Protocol != Protocol || request.Version != Application.version ||
                !string.IsNullOrEmpty(request.RoomInstance) && request.RoomInstance != roomInstance)
            { RejectConnection(connection, "不是同版本的迷时模拟器房间，或房间已重新创建。"); return; }
            if (MainMenuController.UseSteam && (!ulong.TryParse(ConnectionIdentity(connection), out ulong steamId) || !SteamRoomService.Instance.Contains(steamId)))
            { RejectConnection(connection, "请先从迷时大厅加入此房间。"); return; }
            var challenge = new Challenge { Nonce = RoomIdentity.RandomToken(), Until = Time.realtimeSinceStartupAsDouble + 15 };
            challenges[connection] = challenge;
            network.ServerManager.Broadcast(connection, new NetworkRoomChallenge { Game = RoomIdentity.Game, Protocol = Protocol, Version = Application.version,
                RoomInstance = roomInstance, Salt = passwordSalt, Nonce = challenge.Nonce });
        }
        private void OnChallenge(NetworkRoomChallenge challenge, Channel channel)
        {
            try
            {
                if (!sessionOpen || challenge.Game != RoomIdentity.Game || challenge.Protocol != Protocol || challenge.Version != Application.version ||
                    !Guid.TryParseExact(challenge.RoomInstance, "N", out _) || challenge.Nonce?.Length != 44 || challenge.Salt?.Length != 44 ||
                    MainMenuController.UseSteam && challenge.RoomInstance != SteamRoomService.Instance.RoomInstance ||
                    !string.IsNullOrEmpty(roomState.RoomInstance) && challenge.RoomInstance != roomState.RoomInstance)
                    throw new InvalidOperationException("房间身份校验失败。");
                byte[] key = RoomIdentity.Key(MainMenuController.RoomPassword, challenge.Salt);
                string proof = RoomIdentity.Proof(key, challenge.RoomInstance, challenge.Nonce, Application.version, MainMenuController.PlayerName, clientResumeToken);
                Array.Clear(key, 0, key.Length);
                network.ClientManager.Broadcast(new NetworkHello { Game = RoomIdentity.Game, Version = Application.version, RoomInstance = challenge.RoomInstance, Proof = proof,
                    ResumeToken = clientResumeToken, Protocol = Protocol, ContentHash = hash, ModeId = database.ActiveMode.Id,
                    Spectator = spectator, Name = MainMenuController.PlayerName, RoomName = MainMenuController.RoomName });
            }
            catch (Exception e) { status = e.Message; stopNextUpdate = true; }
        }
        private void VerifyAccess(NetworkConnection connection, NetworkHello hello)
        {
            if (!challenges.TryGetValue(connection, out var challenge)) throw new InvalidOperationException("缺少入房校验，请重新连接。");
            challenges.Remove(connection);
            if (Time.realtimeSinceStartupAsDouble > challenge.Until || hello.Game != RoomIdentity.Game || hello.Version != Application.version || hello.RoomInstance != roomInstance ||
                !RoomIdentity.Equal(RoomIdentity.Proof(passwordKey, roomInstance, challenge.Nonce, Application.version, hello.Name, hello.ResumeToken), hello.Proof))
                throw new InvalidOperationException("房间密码错误或入房校验已失效。");
        }
        private bool RestoreSeat(NetworkConnection connection, NetworkHello hello)
        {
            if (string.IsNullOrEmpty(hello.ResumeToken)) return false;
            // A reconnect can beat the server transport's timeout for the old socket.
            foreach (var old in seats.Keys.ToArray())
            {
                if (old == connection || !resumeTokens.TryGetValue(old, out string token) || !RoomIdentity.Equal(token, hello.ResumeToken)) continue;
                if (!memberIdentities.TryGetValue(old, out string identity) || identity != ConnectionIdentity(connection))
                    throw new InvalidOperationException("重连身份不匹配。");
                ReserveDisconnected(old); old.Disconnect(false); break;
            }
            foreach (var item in reservations.ToArray())
            {
                if (!RoomIdentity.Equal(item.Value.Token, hello.ResumeToken)) continue;
                if (Time.realtimeSinceStartupAsDouble > item.Value.Until || item.Value.Identity != ConnectionIdentity(connection))
                    throw new InvalidOperationException("重连身份不匹配或保留时间已结束。");
                reservations.Remove(item.Key); audience.AddPlayer(connection, item.Key);
                roomNames[connection] = item.Value.Name; resumeTokens[connection] = item.Value.Token;
                BroadcastRoom(); if (match != null)
                {
                    SendSnapshot(connection, item.Key);
                    network.ServerManager.Broadcast(connection, CreatePublicFrame(publicSequence, Array.Empty<NetworkCardPresentation>()));
                }
                if (openingDuel != null) SendOpening(connection);
                if (!connection.IsLocalClient) foreach (string line in roomChat) network.ServerManager.Broadcast(connection, new NetworkChatLine { Text = line });
                lastClockUpdate = openingUpdatedAt = Time.realtimeSinceStartupAsDouble;
                return true;
            }
            throw new InvalidOperationException("重连席位已失效，请重新加入房间。");
        }
        private bool ReserveDisconnected(NetworkConnection connection)
        {
            challenges.Remove(connection);
            if (!seats.TryGetValue(connection, out int seat) || intentionalLeaves.Remove(connection) || !resumeTokens.TryGetValue(connection, out string token))
            { resumeTokens.Remove(connection); memberIdentities.Remove(connection); return false; }
            reservations[seat] = new Reservation { Token = token, Name = roomNames[connection], Identity = memberIdentities.TryGetValue(connection, out string identity) ? identity : "direct",
                Until = Time.realtimeSinceStartupAsDouble + RoomIdentity.ReconnectSeconds };
            resumeTokens.Remove(connection); audience.Remove(connection); roomNames.Remove(connection); chatSentAt.Remove(connection);
            memberIdentities.Remove(connection); BroadcastRoom(); return true;
        }
        private readonly Dictionary<NetworkConnection, string> memberIdentities = new Dictionary<NetworkConnection, string>();
        private void OnRoomLeave(NetworkConnection connection, NetworkRoomLeave request, Channel channel)
        { if (roomNames.ContainsKey(connection)) intentionalLeaves.Add(connection); }
        private void BeginReconnect()
        {
            if (reconnecting) return;
            reconnecting = true; reconnectUntil = Time.realtimeSinceStartupAsDouble + RoomIdentity.ReconnectSeconds;
            retryAt = 0; pending = false; status = "连接中断，正在自动重连…"; CancelInteraction();
            clockHud.SetConnectionStatus("连接中断，正在重连…");
        }
        private void UpdateAccess()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            foreach (var item in challenges.Where(p => p.Value.Until < now).ToArray())
            { challenges.Remove(item.Key); RejectConnection(item.Key, "入房校验超时。"); }
            if (reservations.Any(p => p.Value.Until < now))
            {
                if (match != null)
                {
                    BroadcastPublic(new NetworkNotice { Text = "玩家未在 90 秒内重连，对局结束。", Fatal = true });
                    status = "重连等待超时，对局结束。"; stopNextUpdate = true; reservations.Clear();
                }
                else
                {
                    foreach (int seat in reservations.Where(p => p.Value.Until < now).Select(p => p.Key).ToArray()) reservations.Remove(seat);
                    ClearOpening(); roomReady[0] = roomReady[1] = false; ResetVacantSeats(); BroadcastOpening(); BroadcastRoom();
                }
            }
            if (RoomPaused && now - reservationPublishedAt >= 1) { reservationPublishedAt = now; BroadcastRoom(); }
            if (!reconnecting || stopping || network == null) return;
            if (now >= reconnectUntil) { status = "自动重连超时，请返回大厅重新加入。"; stopNextUpdate = true; return; }
            status = $"连接中断，正在自动重连…剩余 {Math.Ceiling(reconnectUntil - now)} 秒";
            clockHud.SetConnectionStatus($"正在重连 · {Math.Ceiling(reconnectUntil - now)} 秒");
            if (now < retryAt || network.ClientManager.Started || network.TransportManager.Transport.GetConnectionState(false) != LocalConnectionState.Stopped) return;
            retryAt = now + 3;
            network.ClientManager.StartConnection(reconnectAddress, port);
        }
        private void ClearAccess()
        {
            if (network != null && network.Initialized)
            {
                network.ServerManager.UnregisterBroadcast<NetworkRoomChallengeRequest>(OnChallengeRequest);
                network.ServerManager.UnregisterBroadcast<NetworkRoomLeave>(OnRoomLeave);
                network.ClientManager.UnregisterBroadcast<NetworkRoomChallenge>(OnChallenge);
            }
            challenges.Clear(); reservations.Clear(); resumeTokens.Clear(); memberIdentities.Clear(); intentionalLeaves.Clear();
            reconnecting = false; clientResumeToken = null;
            if (passwordKey != null) Array.Clear(passwordKey, 0, passwordKey.Length); passwordKey = null;
        }
    }
}
