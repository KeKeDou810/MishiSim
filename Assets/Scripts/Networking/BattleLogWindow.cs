using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Mishi.Networking
{
    internal static class ChatInputFocus
    {
        public static bool BlocksShortcut(GameObject selected, TMP_InputField chatInput)
        {
            // Unity objects can retain a managed reference after their native object is destroyed.
            if (selected == null || !selected.activeInHierarchy) return false;
            var input = selected.GetComponent<TMP_InputField>();
            return input != null && input != chatInput && input.isActiveAndEnabled && input.IsInteractable();
        }

        public static bool BlocksShortcut(TMP_InputField chatInput)
        {
            var events = UnityEngine.EventSystems.EventSystem.current;
            return events != null && BlocksShortcut(events.currentSelectedGameObject, chatInput);
        }
    }

    // Separate authored bottom-left window; no card selection is needed to read the log.
    public sealed class BattleLogWindow : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text entries;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private Button close;
        [SerializeField] private Button logTab, chatTab;
        [SerializeField] private TMP_InputField chatInput;
        public event System.Action<string> ChatSubmitted;
        public event System.Action<int> UnreadChanged;
        private int unread;
        private void SetUnread(int count)
        {
            unread = count;
            chatTab.GetComponentInChildren<TMP_Text>().text = unread > 0 ? $"聊天({unread})" : "聊天";
            UnreadChanged?.Invoke(unread);
        }
        private bool chatSelected;
        private string chat = "暂无聊天消息。";
        private bool refocusChat;
        public bool ChatFocused => IsOpen && chatSelected && chatInput.isFocused;
        private string matchId, content = "暂无操作记录。";
        private float position = 1;
        public bool IsOpen => panel.activeSelf;
        public RectTransform Panel => (RectTransform)panel.transform;
        private void Awake()
        {
            close.onClick.AddListener(Hide);
            logTab.onClick.AddListener(() => SelectTab(false)); chatTab.onClick.AddListener(() => SelectTab(true));
            chatInput.characterLimit = RoomText.ChatLimit; chatInput.richText = false;
            chatInput.gameObject.SetActive(false);
            chatInput.onSubmit.AddListener(text => {
                if (string.IsNullOrWhiteSpace(text)) { refocusChat = true; return; }
                ChatSubmitted?.Invoke(text); chatInput.text = ""; refocusChat = true;
            });
            entries.richText = false;
            entries.text = content;
            panel.SetActive(false);
        }
        public void SetLog(string match, string[] log)
        {
            bool changedMatch = matchId != match;
            if (changedMatch) { matchId = match; position = 1; }
            else if (IsOpen) position = scroll.verticalNormalizedPosition;
            string next = log == null || log.Length == 0 ? "暂无操作记录。" : string.Join("\n\n", log.Reverse());
            if (!changedMatch && content == next) return;
            content = next;
            entries.richText = false; entries.text = chatSelected ? chat : content;
            if (IsOpen) RefreshLayout();
        }
        public void Toggle()
        {
            if (unread > 0) { panel.SetActive(true); SelectTab(true); return; }
            if (IsOpen) { Hide(); return; }
            panel.SetActive(true);
            if (chatSelected) SetUnread(0);
            RefreshLayout();
        }
        public void SetChat(string text, bool notify = true)
        {
            if (chat == text) return;
            chat = text;
            if (notify && !(IsOpen && chatSelected)) SetUnread(unread + 1);
            if (chatSelected) { entries.text = chat; position = 0; if (IsOpen) RefreshLayout(); }
        }
        private void SelectTab(bool showChat)
        {
            chatSelected = showChat; entries.text = showChat ? chat : content;
            if (showChat) SetUnread(0);
            chatInput.gameObject.SetActive(showChat); position = showChat ? 0 : 1;
            RefreshLayout();
        }
        public void Hide()
        {
            if (IsOpen) position = scroll.verticalNormalizedPosition;
            panel.SetActive(false);
            refocusChat = false; chatInput.DeactivateInputField();
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (events != null && events.currentSelectedGameObject == chatInput.gameObject)
                events.SetSelectedGameObject(null);
        }
        public void Clear()
        {
            Hide(); matchId = null; position = 1; content = "暂无操作记录。";
            entries.text = content;
            SetUnread(0);
        }
        private void RefreshLayout()
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            scroll.StopMovement(); scroll.verticalNormalizedPosition = position;
        }
        private void Update()
        {
            if (IsOpen && chatSelected && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
                !RectTransformUtility.RectangleContainsScreenPoint(Panel, Mouse.current.position.ReadValue(), null)) Hide();
            if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Hide();
            var keyboard = Keyboard.current;
            if (keyboard == null || (!keyboard.enterKey.wasPressedThisFrame && !keyboard.numpadEnterKey.wasPressedThisFrame)) return;
            var room = FindFirstObjectByType<RoomFlowView>();
            if (room != null && (room.InLobby || room.failure.activeSelf)) return;
            if (ChatFocused)
            {
                // TMP's onSubmit handles this Enter press.
            }
            else
            {
                if (ChatInputFocus.BlocksShortcut(chatInput)) return;
                panel.SetActive(true); SelectTab(true); chatInput.Select(); chatInput.ActivateInputField();
            }
        }
        private void LateUpdate()
        {
            if (!refocusChat) return;
            refocusChat = false;
            if (IsOpen && chatSelected) { chatInput.Select(); chatInput.ActivateInputField(); }
        }
    }
}
