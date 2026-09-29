using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

namespace Mishi.Networking
{
    public sealed class SteamRoomService : MonoBehaviour
    {
        public sealed class Room
        {
            public ulong Id; public string Name, Mode, Instance;
            public bool Locked, Playing; public int Players, Spectators;
            public string Label => Name + (Locked ? " · 有密码" : " · 无密码") + $" · {Players}/2 · 观战 {Spectators}" + (Playing ? " · 对局中" : " · 准备中");
        }
        private static SteamRoomService instance;
        public static SteamRoomService Instance
        {
            get
            {
                if (instance == null) { var go = new GameObject("Steam Rooms"); instance = go.AddComponent<SteamRoomService>(); DontDestroyOnLoad(go); }
                return instance;
            }
        }
        public static SteamRoomService Existing => instance;
        public bool Initialized { get; private set; }
        public bool Busy { get; private set; }
        public ulong LobbyId { get; private set; }
        public ulong HostId { get; private set; }
        public string RoomInstance { get; private set; }
        public bool IsHost => Initialized && HostId == SteamUser.GetSteamID().m_SteamID;
        public event Action<Room[]> Listed;
        public event Action<string> Failed;
        public event Action<bool, string, string> Ready;
        private CallResult<LobbyMatchList_t> listCall;
        private CallResult<LobbyCreated_t> createCall;
        private CallResult<LobbyEnter_t> enterCall;
        private double deadline;
        private int requestGeneration;
        private readonly List<IDisposable> pendingCalls = new List<IDisposable>();
        private string creatingName, creatingMode;
        private bool creatingLocked;
        private ulong joining;
        private const string GameKey = "mishi.game", VersionKey = "mishi.version", ProtocolKey = "mishi.protocol";
        public void EnsureInitialized()
        {
            if (Initialized) return;
            if (!SteamAPI.Init()) throw new InvalidOperationException("Steam 初始化失败。请启动并登录 Steam，再重启模拟器。");
            Initialized = true;
            if (SteamUtils.GetAppID().m_AppId != RoomIdentity.AppId)
            { SteamAPI.Shutdown(); Initialized = false; throw new InvalidOperationException("当前测试使用 Spacewar App ID 480，请检查 steam_appid.txt。"); }
            SteamNetworkingUtils.InitRelayNetworkAccess();
        }
        private CallResult<T> Track<T>(CallResult<T>.APIDispatchDelegate callback)
        {
            CallResult<T> call = null;
            call = CallResult<T>.Create((result, error) => {
                try { callback(result, error); }
                finally { pendingCalls.Remove(call); call.Dispose(); }
            });
            pendingCalls.Add(call); return call;
        }
        private void Begin()
        {
            EnsureInitialized();
            if (!SteamUser.BLoggedOn()) throw new InvalidOperationException("Steam 当前未在线登录。");
            if (Busy) throw new InvalidOperationException("正在处理上一次大厅请求，请稍候。");
            requestGeneration++; Busy = true; deadline = Time.realtimeSinceStartupAsDouble + 20;
        }
        public void Refresh()
        {
            Begin();
            SteamMatchmaking.AddRequestLobbyListStringFilter(GameKey, RoomIdentity.Game, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(VersionKey, Application.version, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(ProtocolKey, RoomIdentity.Protocol.ToString(), ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter("mishi.ready", "1", ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(100);
            int generation = requestGeneration;
            listCall = Track<LobbyMatchList_t>((result, error) => { if (generation == requestGeneration) OnList(result, error); });
            listCall.Set(SteamMatchmaking.RequestLobbyList());
        }
        private string Data(ulong id, string key) => SteamMatchmaking.GetLobbyData(new CSteamID(id), key);
        private bool Compatible(ulong id) => RoomIdentity.Compatible(Data(id, GameKey), Data(id, VersionKey), Data(id, ProtocolKey), Application.version);
        private void OnList(LobbyMatchList_t result, bool error)
        {
            if (!Busy) return;
            Busy = false;
            if (error) { Failed?.Invoke("获取 Steam 大厅列表失败。"); return; }
            var rooms = new List<Room>();
            for (int i = 0; i < result.m_nLobbiesMatching; i++)
            {
                ulong id = SteamMatchmaking.GetLobbyByIndex(i).m_SteamID;
                if (!Compatible(id) || Data(id, "mishi.ready") != "1") continue;
                int.TryParse(Data(id, "mishi.players"), out int players); int.TryParse(Data(id, "mishi.spectators"), out int spectators);
                rooms.Add(new Room { Id = id, Name = Data(id, "mishi.name"), Mode = Data(id, "mishi.mode"), Instance = Data(id, "mishi.instance"),
                    Locked = Data(id, "mishi.locked") == "1", Playing = Data(id, "mishi.playing") == "1", Players = players, Spectators = spectators });
            }
            Listed?.Invoke(rooms.ToArray());
        }
        public void Create(string name, string mode, bool locked)
        {
            Begin(); creatingName = name; creatingMode = mode; creatingLocked = locked;
            int generation = requestGeneration;
            createCall = Track<LobbyCreated_t>((result, error) => {
                if (generation == requestGeneration) OnCreated(result, error);
                else if (result.m_eResult == EResult.k_EResultOK) SteamMatchmaking.LeaveLobby(new CSteamID(result.m_ulSteamIDLobby));
            });
            createCall.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 250));
        }
        private void OnCreated(LobbyCreated_t result, bool error)
        {
            if (!Busy) { if (result.m_eResult == EResult.k_EResultOK) SteamMatchmaking.LeaveLobby(new CSteamID(result.m_ulSteamIDLobby)); return; }
            Busy = false;
            if (error || result.m_eResult != EResult.k_EResultOK) { Failed?.Invoke("创建 Steam 房间失败：" + result.m_eResult); return; }
            LobbyId = result.m_ulSteamIDLobby; HostId = SteamUser.GetSteamID().m_SteamID; RoomInstance = Guid.NewGuid().ToString("N");
            Set(GameKey, RoomIdentity.Game); Set(VersionKey, Application.version); Set(ProtocolKey, RoomIdentity.Protocol.ToString());
            Set("mishi.instance", RoomInstance); Set("mishi.name", creatingName); Set("mishi.mode", creatingMode);
            Set("mishi.host", HostId.ToString()); Set("mishi.locked", creatingLocked ? "1" : "0"); Set("mishi.ready", "0");
            Ready?.Invoke(true, creatingName, creatingMode);
        }
        public void Join(ulong id)
        {
            Begin(); joining = id; int generation = requestGeneration;
            enterCall = Track<LobbyEnter_t>((result, error) => {
                if (generation == requestGeneration) OnEntered(result, error);
                else if (result.m_ulSteamIDLobby != LobbyId) SteamMatchmaking.LeaveLobby(new CSteamID(result.m_ulSteamIDLobby));
            });
            enterCall.Set(SteamMatchmaking.JoinLobby(new CSteamID(id)));
        }
        private void OnEntered(LobbyEnter_t result, bool error)
        {
            if (!Busy) { if (result.m_ulSteamIDLobby != LobbyId) SteamMatchmaking.LeaveLobby(new CSteamID(result.m_ulSteamIDLobby)); return; }
            Busy = false;
            ulong id = result.m_ulSteamIDLobby;
            if (error || id != joining || result.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            { Failed?.Invoke("加入 Steam 房间失败：" + (EChatRoomEnterResponse)result.m_EChatRoomEnterResponse); return; }
            if (!Compatible(id) || Data(id, "mishi.ready") != "1" || !ulong.TryParse(Data(id, "mishi.host"), out ulong host) ||
                SteamMatchmaking.GetLobbyOwner(new CSteamID(id)).m_SteamID != host || !Guid.TryParseExact(Data(id, "mishi.instance"), "N", out _))
            { SteamMatchmaking.LeaveLobby(new CSteamID(id)); Failed?.Invoke("房间已关闭，或不是同版本的迷时模拟器房间。"); return; }
            LobbyId = id; HostId = host; RoomInstance = Data(id, "mishi.instance");
            Ready?.Invoke(false, Data(id, "mishi.name"), Data(id, "mishi.mode"));
        }
        private void Set(string key, string value) => SteamMatchmaking.SetLobbyData(new CSteamID(LobbyId), key, value);
        public void Publish(int players, int spectators, bool playing)
        {
            if (LobbyId == 0 || !IsHost) return;
            Set("mishi.players", players.ToString()); Set("mishi.spectators", spectators.ToString()); Set("mishi.playing", playing ? "1" : "0"); Set("mishi.ready", "1");
        }
        public bool Contains(ulong steamId)
        {
            if (!Initialized || LobbyId == 0) return false;
            for (int i = 0; i < SteamMatchmaking.GetNumLobbyMembers(new CSteamID(LobbyId)); i++)
                if (SteamMatchmaking.GetLobbyMemberByIndex(new CSteamID(LobbyId), i).m_SteamID == steamId) return true;
            return false;
        }
        public void CancelRequest() { Busy = false; requestGeneration++; }
        public void Leave()
        {
            CancelRequest();
            if (Initialized && LobbyId != 0)
            {
                if (IsHost) { Set("mishi.ready", "0"); SteamMatchmaking.SetLobbyJoinable(new CSteamID(LobbyId), false); }
                SteamMatchmaking.LeaveLobby(new CSteamID(LobbyId));
            }
            LobbyId = HostId = 0; RoomInstance = null;
        }
        private void Update()
        {
            if (!Initialized) return;
            SteamAPI.RunCallbacks();
            if (Busy && Time.realtimeSinceStartupAsDouble >= deadline) { CancelRequest(); Failed?.Invoke("Steam 大厅请求超时，请重试。"); }
        }
        private void OnDestroy()
        {
            Leave(); foreach (var call in pendingCalls) call.Dispose(); pendingCalls.Clear();
            if (Initialized) SteamAPI.Shutdown(); Initialized = false; if (instance == this) instance = null;
        }
    }
}
