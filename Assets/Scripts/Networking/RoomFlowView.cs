using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Mishi.Networking
{
    public sealed class RoomFlowView : MonoBehaviour
    {
        public GameObject home, connection, lobby, failure, chatPanel;
        public TMP_InputField address, port, roomName, playerName, chatInput;
        public TMP_InputField roomPassword;
        public TMP_Dropdown mode, deck, recordings;
        public TMP_Text members, failureReason, chatLines;
        public TMP_Text chatNotice;
        private int unreadChat;
        public ScrollRect chatScroll;
        public Button begin, deckBuild, replay, create, join, back, ready, spectate, playSeat, startMatch, leave, closeFailure;
        public TMP_InputField openingHand, drawPerTurn, turnTime, actionRefund, responseTime;
        public Button applyRules;
        public event Action<NetworkRoomRules> RulesSubmitted;
        public event Action<bool> ConnectRequested;
        public event Action ReadyRequested, SpectateRequested, PlayRequested, StartRequested, LeaveRequested;
        public event Action<string> DeckSelected, ChatSubmitted;
        private bool selecting, gameStarted;
        private bool refocusChat;
        private bool chatDirty;
        public bool InLobby => lobby.activeSelf;
        private void Awake()
        {
            address.characterLimit = 255; port.characterLimit = 5;
            roomPassword.characterLimit = 64; roomPassword.contentType = TMP_InputField.ContentType.Password;
            playerName.characterLimit = RoomText.NameLimit; roomName.characterLimit = RoomText.RoomLimit;
            chatInput.characterLimit = RoomText.ChatLimit;
            playerName.richText = roomName.richText = chatInput.richText = false;
            members.richText = failureReason.richText = chatLines.richText = false;
            create.onClick.AddListener(() => ConnectRequested?.Invoke(true)); join.onClick.AddListener(() => ConnectRequested?.Invoke(false));
            ready.onClick.AddListener(() => ReadyRequested?.Invoke()); spectate.onClick.AddListener(() => SpectateRequested?.Invoke());
            playSeat.onClick.AddListener(() => PlayRequested?.Invoke()); startMatch.onClick.AddListener(() => StartRequested?.Invoke());
            leave.onClick.AddListener(() => LeaveRequested?.Invoke()); closeFailure.onClick.AddListener(() => failure.SetActive(false));
            deck.onValueChanged.AddListener(i => { if (!selecting && i >= 0 && i < deck.options.Count) DeckSelected?.Invoke(deck.options[i].text); });
            failure.SetActive(false); chatPanel.SetActive(false); HidePages();
            chatInput.onSubmit.AddListener(text => {
                if (string.IsNullOrWhiteSpace(text)) { refocusChat = true; return; }
                ChatSubmitted?.Invoke(text); chatInput.text = ""; refocusChat = true;
            });
            foreach (var input in new[] { openingHand, drawPerTurn, turnTime, actionRefund, responseTime })
            { input.characterLimit = 4; input.contentType = TMP_InputField.ContentType.IntegerNumber; }
            applyRules.onClick.AddListener(() => {
                try { RulesSubmitted?.Invoke(new NetworkRoomRules { OpeningHand = int.Parse(openingHand.text), DrawPerTurn = int.Parse(drawPerTurn.text),
                    TurnTimeSeconds = int.Parse(turnTime.text), ActionTimeRefundSeconds = int.Parse(actionRefund.text), ResponseTimeSeconds = int.Parse(responseTime.text) }); }
                catch (Exception e) { ShowFailure("设置需要填写整数：" + e.Message); }
            });
            foreach (var button in GetComponentsInChildren<Button>(true))
                button.onClick.AddListener(() => { var events = EventSystem.current; if (events != null) events.SetSelectedGameObject(null); });
        }
        private void Start() { if (SceneManager.GetActiveScene().name == MainMenuController.MenuScene) MainMenuController.Bind(this); }
        public void HidePages() { home.SetActive(false); connection.SetActive(false); lobby.SetActive(false); chatPanel.SetActive(false); GetComponent<SteamLobbyView>()?.Hide(); }
        public void ShowHome() { HidePages(); home.SetActive(true); }
        public void ShowConnection() { HidePages(); connection.SetActive(true); }
        public void ShowLobby()
        {
            HidePages(); lobby.SetActive(true); members.text = "正在连接房间…";
            ready.interactable = spectate.interactable = playSeat.interactable = deck.interactable = applyRules.interactable = false;
            startMatch.gameObject.SetActive(false);
            foreach (var input in new[] { openingHand, drawPerTurn, turnTime, actionRefund, responseTime }) input.interactable = false;
        }
        public void ShowFailure(string reason) { failureReason.text = reason; failure.SetActive(true); }
        public void SetDecks(string[] names)
        {
            selecting = true; deck.ClearOptions(); deck.AddOptions(new List<string>(names)); deck.SetValueWithoutNotify(0); selecting = false;
        }
        public void Apply(NetworkRoomState state)
        {
            lobby.SetActive(!state.Started);
            members.text = state.RoomName + " · " + state.ModeName + "\n\n" +
                "P1  " + state.Names[0] + "  " + (state.Ready[0] ? "已准备" : "未准备") + "\n\n" +
                "P2  " + state.Names[1] + "  " + (state.Ready[1] ? "已准备" : "未准备") + "\n\n观战人数：" + state.Spectators;
            bool prepared = state.Seat >= 0 && state.Ready[state.Seat];
            ready.interactable = state.Seat >= 0; ready.GetComponentInChildren<TMP_Text>().text = prepared ? "取消准备" : "准备";
            deck.interactable = state.Seat >= 0 && !prepared;
            spectate.interactable = state.Seat >= 0; playSeat.interactable = state.Seat < 0;
            startMatch.gameObject.SetActive(state.IsHost); startMatch.interactable = state.Ready[0] && state.Ready[1];
            if (state.Started && !gameStarted) chatPanel.SetActive(false);
            gameStarted = state.Started;
            var inputs = new[] { openingHand, drawPerTurn, turnTime, actionRefund, responseTime };
            var values = new[] { state.Rules.OpeningHand, state.Rules.DrawPerTurn, state.Rules.TurnTimeSeconds, state.Rules.ActionTimeRefundSeconds, state.Rules.ResponseTimeSeconds };
            for (int i = 0; i < inputs.Length; i++) { inputs[i].interactable = state.IsHost && !state.Started; inputs[i].SetTextWithoutNotify(values[i].ToString()); }
            applyRules.interactable = state.IsHost && !state.Started;
            if (state.Reconnecting)
            {
                members.text += $"\n\n等待重连 · 剩余 {Mathf.CeilToInt(state.ReconnectSeconds)} 秒";
                ready.interactable = spectate.interactable = playSeat.interactable = deck.interactable = startMatch.interactable = applyRules.interactable = false;
            }
        }
        public void SetChat(string text)
        {
            if (chatLines.text == text) return;
            chatLines.text = text; chatDirty = true;
            if (InLobby && !chatPanel.activeSelf) unreadChat++;
            RefreshChatNotice();
        }
        private void RefreshChatNotice()
        {
            chatNotice.text = unreadChat > 0 ? $"新聊天消息（{unreadChat}） · Enter 查看" : "Enter 打开聊天 · Enter 发送 · 点击窗外收起";
            chatNotice.color = unreadChat > 0 ? new Color(.898f, .753f, .482f) : new Color(.67f, .698f, .749f);
        }
        private void CloseChat()
        {
            refocusChat = false; chatInput.DeactivateInputField(); chatPanel.SetActive(false);
            var events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject == chatInput.gameObject) events.SetSelectedGameObject(null);
        }
        private void LateUpdate()
        {
            if (chatDirty && chatPanel.activeSelf)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(chatScroll.content);
                chatScroll.StopMovement(); chatScroll.verticalNormalizedPosition = 0; chatDirty = false;
            }
            if (!refocusChat) return;
            refocusChat = false;
            if (chatPanel.activeSelf) { chatInput.Select(); chatInput.ActivateInputField(); }
        }
        private void Update()
        {
            if (InLobby && chatPanel.activeSelf && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
                !RectTransformUtility.RectangleContainsScreenPoint((RectTransform)chatPanel.transform, Mouse.current.position.ReadValue(), null)) CloseChat();
            if (!InLobby || Keyboard.current == null || failure.activeSelf) return;
            if (Keyboard.current.escapeKey.wasPressedThisFrame) CloseChat();
            if (!Keyboard.current.enterKey.wasPressedThisFrame && !Keyboard.current.numpadEnterKey.wasPressedThisFrame) return;
            if (chatPanel.activeSelf && chatInput.isFocused)
            {
                // TMP's onSubmit handles the message; avoid sending twice.
            }
            else
            {
                if (ChatInputFocus.BlocksShortcut(chatInput)) return;
                chatPanel.SetActive(true); unreadChat = 0; RefreshChatNotice(); chatDirty = true; chatInput.Select(); chatInput.ActivateInputField();
            }
        }
    }
}
