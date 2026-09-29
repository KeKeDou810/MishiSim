using UnityEngine;
using UnityEngine.UI;

namespace Mishi.Networking
{
    // IMGUI does not consume EventSystem physics raycasts. A transparent UI surface does.
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Mishi.Networking", "Assembly-CSharp", "MvpGuiBlocker")]
    public sealed class GuiPointerBlocker : MonoBehaviour
    {
        private RectTransform surface;
        public static GuiPointerBlocker Create(Transform parent)
        {
            var root = new GameObject("Menu pointer blocker", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32760;
            var image = new GameObject("Blocked area", typeof(RectTransform), typeof(Image));
            image.transform.SetParent(root.transform, false);
            image.GetComponent<Image>().color = Color.clear;
            var result = root.AddComponent<GuiPointerBlocker>();
            result.surface = image.GetComponent<RectTransform>();
            result.surface.anchorMin = result.surface.anchorMax = result.surface.pivot = new Vector2(0, 1);
            return result;
        }
        public void SetArea(Rect rect)
        {
            surface.anchoredPosition = new Vector2(rect.x, -rect.y);
            surface.sizeDelta = rect.size;
        }
    }
}
