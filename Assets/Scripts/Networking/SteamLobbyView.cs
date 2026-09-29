using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mishi.Networking
{
    public sealed class SteamLobbyView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text status;
        [SerializeField] private Button[] rows;
        [SerializeField] private Button refresh, previous, next, create, join, back;
        [SerializeField] private TMP_InputField playerName, roomName, password;
        [SerializeField] private TMP_Dropdown mode;
        public Button DirectConnect;
        private SteamRoomService steam;
        private CardDatabaseService database;
        private RoomFlowView flow;
        private SteamRoomService.Room[] rooms = Array.Empty<SteamRoomService.Room>();
        private int page, selected = -1;
        private string joiningPassword;
        private Action<bool, string, string, string> launch;
        private void Awake()
        {
            panel.SetActive(false);
            playerName.characterLimit = RoomText.NameLimit; roomName.characterLimit = RoomText.RoomLimit;
            password.characterLimit = 64; password.contentType = TMP_InputField.ContentType.Password;
            for (int i = 0; i < rows.Length; i++)
            {
                int slot = i; rows[i].onClick.AddListener(() => { selected = page * rows.Length + slot; Render(); });
                rows[i].GetComponentInChildren<TMP_Text>().richText = false;
            }
            refresh.onClick.AddListener(Refresh); previous.onClick.AddListener(() => { page--; Render(); }); next.onClick.AddListener(() => { page++; Render(); });
            create.onClick.AddListener(() => Request(true)); join.onClick.AddListener(() => Request(false));
            back.onClick.AddListener(() => { steam?.CancelRequest(); Hide(); flow.ShowHome(); });
            playerName.onEndEdit.AddListener(text => { try { PlayerPrefs.SetString("room.player", RoomText.Validate(text, RoomText.NameLimit, "玩家名")); PlayerPrefs.Save(); } catch (ArgumentException) { } });
        }
        public void Bind(RoomFlowView roomFlow, CardDatabaseService service, Action<bool, string, string, string> start)
        {
            flow = roomFlow; database = service; launch = start;
            steam = SteamRoomService.Instance; steam.Listed += OnList; steam.Failed += OnFailure; steam.Ready += OnReady;
            mode.ClearOptions(); mode.AddOptions(database.Modes.Select(m => m.Name).ToList());
            mode.SetValueWithoutNotify(Math.Max(0, database.Modes.ToList().FindIndex(m => m.Id == database.ActiveMode.Id)));
        }
        public void Open()
        {
            flow.HidePages(); panel.SetActive(true);
            playerName.text = PlayerPrefs.GetString("room.player", "玩家"); roomName.text = PlayerPrefs.GetString("room.name", "迷时房间");
            password.text = ""; Refresh();
        }
        private void Refresh()
        {
            try { status.text = "正在获取同版本迷时房间…"; steam.Refresh(); }
            catch (Exception e) { OnFailure(e.Message); }
        }
        private void OnList(SteamRoomService.Room[] result) { rooms = result; page = 0; selected = -1; Render(); }
        private void Render()
        {
            int pages = Math.Max(1, (rooms.Length + rows.Length - 1) / rows.Length); page = Mathf.Clamp(page, 0, pages - 1);
            for (int i = 0; i < rows.Length; i++)
            {
                int index = page * rows.Length + i; rows[i].gameObject.SetActive(index < rooms.Length);
                if (index >= rooms.Length) continue;
                rows[i].GetComponentInChildren<TMP_Text>().text = (index == selected ? "▶ " : "") + rooms[index].Label;
            }
            previous.interactable = page > 0; next.interactable = page + 1 < pages;
            status.text = $"迷时 {Application.version} · {rooms.Length} 个房间 · {page + 1}/{pages}" + (rooms.Length == 0 ? "\n暂无房间，可以创建一个。" : "\n选择房间；有密码时填写右侧密码后加入。");
        }
        private void Request(bool host)
        {
            try
            {
                RoomText.Validate(playerName.text, RoomText.NameLimit, "玩家名"); joiningPassword = RoomIdentity.Password(password.text);
                if (host) { database.SelectMode(database.Modes[mode.value].Id); steam.Create(RoomText.Validate(roomName.text, RoomText.RoomLimit, "房间名"), database.ActiveMode.Id, joiningPassword.Length > 0); }
                else
                {
                    if (selected < 0 || selected >= rooms.Length) throw new ArgumentException("请先选择房间。");
                    database.SelectMode(rooms[selected].Mode); steam.Join(rooms[selected].Id);
                }
                status.text = host ? "正在创建房间…" : "正在加入房间…";
            }
            catch (Exception e) { OnFailure(e.Message); }
        }
        private void OnReady(bool host, string name, string modeId)
        {
            try { database.SelectMode(modeId); launch(host, name, RoomText.Validate(playerName.text, RoomText.NameLimit, "玩家名"), joiningPassword); }
            catch (Exception e) { steam.Leave(); OnFailure(e.Message); }
        }
        private void OnFailure(string error) { status.text = error; flow.ShowFailure(error); }
        private void Update()
        {
            if (!panel.activeSelf) return;
            bool ready = steam != null && !steam.Busy;
            refresh.interactable = create.interactable = ready; join.interactable = ready && selected >= 0;
        }
        public void Hide() { panel.SetActive(false); password.text = ""; }
        private void OnDestroy() { if (steam != null) { steam.Listed -= OnList; steam.Failed -= OnFailure; steam.Ready -= OnReady; } }
    }
}
