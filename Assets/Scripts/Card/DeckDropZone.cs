using UnityEngine;
using UnityEngine.EventSystems;

public sealed class DeckDropZone : MonoBehaviour, IDropHandler
{
    [SerializeField] private CardBrowser browser;
    public void OnDrop(PointerEventData e)
    {
        if (browser == null || e.button != PointerEventData.InputButton.Left || e.pointerDrag == null) return;
        var item = e.pointerDrag.GetComponent<CardListItem>();
        if (item != null && !item.InDeck && item.Owner == browser) browser.Add(item.Card.Id);
    }
}
