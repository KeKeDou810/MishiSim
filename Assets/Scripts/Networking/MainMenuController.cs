using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mishi.Networking
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Mishi.Networking", "Assembly-CSharp", "MvpMenu")]
    public sealed class MainMenuController : MonoBehaviour
    {
        public const string MenuScene = "MainMenu", BattleScene = "CardTestGym", DeckScene = "DeckBuilder";
        private static MainMenuController instance;
        private static bool launchPending, launchHost;
        private static string replayPath, launchAddress;
        private static ushort launchPort;
        public static string PlayerName { get; private set; } = "玩家";
        public static string RoomName { get; private set; } = "迷时房间";
        public static string RoomPassword { get; private set; } = "";
        public static bool UseSteam { get; private set; }
        private CardDatabaseService service;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; launchPending = false; replayPath = null; UseSteam = false; RoomPassword = ""; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureMenu() { if (instance == null) new GameObject("Menu Navigation").AddComponent<MainMenuController>(); }
        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this; DontDestroyOnLoad(gameObject); Application.runInBackground = true;
        }
        public static void Bind(RoomFlowView view)
        {
            EnsureMenu(); instance.BindView(view);
        }
        private void BindView(RoomFlowView view)
        {
            view.ShowHome();
            try
            {
                service = CardDatabaseService.Instance; service.EnsureLoaded();
                view.address.text = PlayerPrefs.GetString("room.address", "127.0.0.1"); view.port.text = PlayerPrefs.GetString("room.port", "7770");
                view.playerName.text = PlayerPrefs.GetString("room.player", PlayerName); view.roomName.text = PlayerPrefs.GetString("room.name", RoomName);
                view.playerName.onEndEdit.AddListener(SavePlayerName);
                view.mode.ClearOptions(); view.mode.AddOptions(service.Modes.Select(m => m.Name).ToList());
                view.mode.SetValueWithoutNotify(service.Modes.ToList().FindIndex(m => m.Id == service.ActiveMode.Id));
                view.mode.onValueChanged.AddListener(i => { try { service.SelectMode(service.Modes[i].Id); } catch (Exception e) { view.ShowFailure(e.Message); } });
                var files = BattleRecording.SavedFiles(service.ContentRoot);
                view.recordings.ClearOptions(); view.recordings.AddOptions(files.Select(Path.GetFileNameWithoutExtension).ToList());
                view.recordings.gameObject.SetActive(false);
                view.replay.GetComponentInChildren<TMPro.TMP_Text>().text = "录像回放";
                view.replay.onClick.AddListener(() => {
                    if (files.Length == 0) { view.ShowFailure("暂无已保存录像。"); return; }
                    if (!view.recordings.gameObject.activeSelf)
                    {
                        view.recordings.gameObject.SetActive(true);
                        view.replay.GetComponentInChildren<TMPro.TMP_Text>().text = "播放选中的录像";
                        view.recordings.Show(); return;
                    }
                    replayPath = files[view.recordings.value]; launchPending = false; SceneManager.LoadScene(BattleScene);
                });
            }
            catch (Exception e) { view.ShowFailure(e.Message); }
            var hall = view.GetComponent<SteamLobbyView>();
            hall.Bind(view, service, (host, name, player, password) => {
                UseSteam = true; RoomPassword = password; RoomName = name; PlayerName = player;
                launchAddress = SteamRoomService.Instance.HostId.ToString(); launchPort = RoomIdentity.SteamPort;
                launchHost = host; launchPending = true; replayPath = null;
                PlayerPrefs.SetString("room.player", player); PlayerPrefs.SetString("room.name", name); PlayerPrefs.Save();
                SceneManager.LoadScene(BattleScene);
            });
            view.begin.onClick.AddListener(hall.Open); hall.DirectConnect.onClick.AddListener(view.ShowConnection); view.back.onClick.AddListener(view.ShowHome);
            view.deckBuild.onClick.AddListener(() => SceneManager.LoadScene(DeckScene));
            view.ConnectRequested += host => {
                try
                {
                    PlayerName = RoomText.Validate(view.playerName.text, RoomText.NameLimit, "玩家名");
                    RoomName = RoomText.Validate(view.roomName.text, RoomText.RoomLimit, "房间名");
                    UseSteam = false; RoomPassword = RoomIdentity.Password(view.roomPassword.text);
                    if (!ushort.TryParse(view.port.text, out launchPort) || launchPort == 0) throw new ArgumentException("端口必须为 1～65535。");
                    launchAddress = view.address.text.Trim(); if (string.IsNullOrEmpty(launchAddress)) throw new ArgumentException("请填写主机 IP 或域名。");
                    service.EnsureLoaded(); launchHost = host; launchPending = true; replayPath = null;
                    PlayerPrefs.SetString("room.address", launchAddress); PlayerPrefs.SetString("room.port", view.port.text);
                    PlayerPrefs.SetString("room.player", PlayerName); PlayerPrefs.SetString("room.name", RoomName);
                    PlayerPrefs.Save();
                    SceneManager.LoadScene(BattleScene);
                }
                catch (Exception e) { launchPending = false; view.ShowFailure(e.Message); }
            };
        }
        public static bool TakeLaunch(out bool host, out string address, out ushort port, out bool spectator)
        { host = launchHost; address = launchAddress; port = launchPort; spectator = false; bool take = launchPending; launchPending = false; return take; }
        private static void SavePlayerName(string text)
        {
            try { PlayerName = RoomText.Validate(text, RoomText.NameLimit, "玩家名"); }
            catch (ArgumentException) { return; } // Keep the last valid local name while editing.
            PlayerPrefs.SetString("room.player", PlayerName); PlayerPrefs.Save();
        }
        public static bool TakeReplay(out string path) { path = replayPath; replayPath = null; return !string.IsNullOrEmpty(path); }
        private void OnGUI()
        {
            if (SceneManager.GetActiveScene().name != DeckScene) return;
            using var theme = AtomOneTheme.Use();
            if (GUI.Button(new Rect(Screen.width - 160, 8, 150, 32), "返回主界面")) SceneManager.LoadScene(MenuScene);
        }
        private void OnDestroy() { if (instance == this) instance = null; }
    }
}
