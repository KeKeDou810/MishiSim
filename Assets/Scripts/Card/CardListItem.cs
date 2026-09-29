using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class CardListItem : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler,
    IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image artwork;
    public CardDefinition Card { get; private set; }
    public bool InDeck { get; private set; }
    public CardBrowser Owner { get; private set; }
    private GameObject ghost;
    private GameObject hoverBackground;

    public void Bind(CardDefinition card, bool inDeck, int count, CardBrowser owner)
    {
        Card = card; InDeck = inDeck; Owner = owner;
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        label.raycastTarget = false;
        label.text = inDeck ? card.Name : $"{card.Name}\n<size=75%>{card.Id}\n{card.Faction}</size>";
        if (card.IsPlayer) label.text += "\n<size=75%>契约附带</size>";
        if (artwork == null) artwork = transform.Find("Image").GetComponent<Image>();
        artwork.gameObject.SetActive(true);
        artwork.raycastTarget = false;
        artwork.sprite = owner.GetArtwork(card);
        artwork.preserveAspect = true;
        artwork.color = artwork.sprite == null ? AtomOneTheme.Raised : Color.white;
        var artRect = artwork.rectTransform;
        var textRect = label.rectTransform;
        if (inDeck)
        {
            Stretch(artRect, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2));
            if (hoverBackground == null)
            {
                hoverBackground = new GameObject("Hover name background", typeof(RectTransform), typeof(Image));
                hoverBackground.transform.SetParent(transform, false);
            }
            var bg = hoverBackground.GetComponent<Image>();
            bg.color = AtomOneTheme.Surface; bg.raycastTarget = false;
            Stretch((RectTransform)hoverBackground.transform, Vector2.zero, new Vector2(1, .48f), Vector2.zero, Vector2.zero);
            Stretch(textRect, Vector2.zero, new Vector2(1, .48f), new Vector2(4, 3), new Vector2(-4, -3));
            label.color = AtomOneTheme.Text; label.fontSize = 16;
            label.enableAutoSizing = true; label.fontSizeMin = 11; label.fontSizeMax = 16;
            label.alignment = TextAlignmentOptions.Center;
            label.transform.SetAsLastSibling();
            SetHover(false);
        }
        else
        {
            // Library layout and typography are authored in CardLibraryItem.prefab.
            label.gameObject.SetActive(true);
        }
    }
    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min; rect.anchorMax = max; rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
    }
    private void SetHover(bool show)
    {
        if (!InDeck) return;
        if (hoverBackground != null) hoverBackground.SetActive(show);
        if (label != null) label.gameObject.SetActive(show);
    }
    public void OnPointerEnter(PointerEventData e)
    {
        if (Owner == null || Card == null) return;
        SetHover(true); Owner.Select(Card);
    }
    public void OnPointerExit(PointerEventData e) { SetHover(false); }
    public void OnPointerClick(PointerEventData e)
    {
        if (e.dragging || Owner == null) return;
        if (e.button == PointerEventData.InputButton.Left) Owner.Select(Card);
        else if (e.button == PointerEventData.InputButton.Right)
        {
            if (InDeck) Owner.Remove(Card.Id); else Owner.Add(Card.Id);
        }
    }
    public void OnBeginDrag(PointerEventData e)
    {
        if (InDeck || Card.IsPlayer || e.button != PointerEventData.InputButton.Left) return;
        Canvas canvas = GetComponentInParent<Canvas>().rootCanvas;
        ghost = new GameObject("Card drag", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        ghost.transform.SetParent(canvas.transform, false);
        ghost.GetComponent<CanvasGroup>().blocksRaycasts = false;
        var image = ghost.GetComponent<Image>();
        image.sprite = artwork.sprite; image.preserveAspect = true; image.raycastTarget = false;
        image.color = new Color(1, 1, 1, .85f);
        ghost.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 140);
        OnDrag(e);
    }
    public void OnDrag(PointerEventData e)
    {
        if (ghost == null) return;
        Canvas canvas = ghost.GetComponentInParent<Canvas>();
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)canvas.transform, e.position, camera, out Vector3 point))
            ghost.transform.position = point;
    }
    public void OnEndDrag(PointerEventData e) { ClearDrag(); }
    private void OnDisable() { ClearDrag(); SetHover(false); }
    private void ClearDrag() { if (ghost != null) Destroy(ghost); ghost = null; }
}
