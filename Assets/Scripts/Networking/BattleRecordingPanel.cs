using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mishi.Networking
{
    // All controls live in BattleInteractionUI.prefab; no runtime UI construction.
    public sealed class BattleRecordingPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_InputField recordingName;
        [SerializeField] private TMP_Text description;
        [SerializeField] private Button save, discard;
        public event Action<string> SaveRequested;
        public event Action DiscardRequested;
        public bool IsOpen => panel != null && panel.activeSelf;
        private void Awake()
        {
            save.onClick.AddListener(() => SaveRequested?.Invoke(recordingName.text));
            discard.onClick.AddListener(() => DiscardRequested?.Invoke());
            panel.SetActive(false);
        }
        public void Show(string reason)
        {
            panel.SetActive(true); panel.transform.SetAsLastSibling();
            recordingName.SetTextWithoutNotify("对局-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            description.text = reason + "\n只保存本次收到的公开场面与有效操作。";
            recordingName.Select(); recordingName.ActivateInputField();
        }
        public void ShowError(string error) { description.text = error; }
        public void Hide() { panel.SetActive(false); }
    }
}
