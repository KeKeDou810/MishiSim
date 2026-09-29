using TMPro;
using UnityEngine;

namespace Mishi.Networking
{
    public sealed class OpeningGestureCard : MonoBehaviour
    {
        [SerializeField] private GameObject face;
        [SerializeField] private Renderer front;
        [SerializeField] private TMP_Text caption;
        public BattleCard Card { get; private set; }
        public BattleCardPointer Pointer { get; private set; }
        private void Awake() { Card = GetComponent<BattleCard>(); Pointer = GetComponent<BattleCardPointer>(); }
        public void Show(int gesture, bool revealed, bool selectable)
        {
            face.SetActive(revealed); front.enabled = false;
            if (revealed && gesture >= 0)
            {
                string[] names = { "石头", "剪刀", "布" };
                caption.text = names[gesture] + "\n\n<size=45%>胜 " + names[(gesture + 1) % 3] + "\n负 " + names[(gesture + 2) % 3] + "</size>";
            }
            Pointer.CanPreview = selectable; Pointer.CanDrag = false;
            GetComponent<BoxCollider>().enabled = selectable;
        }
    }
}
