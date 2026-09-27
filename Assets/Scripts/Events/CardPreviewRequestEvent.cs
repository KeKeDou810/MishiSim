using MoreMountains.Tools;
using UnityEngine;

public struct CardPreviewRequestEvent
{
    public CardDefinition Card;
    public string ContentRoot;

    public CardPreviewRequestEvent(CardDefinition card, string contentRoot)
    {
        Card = card;
        ContentRoot = contentRoot;
    }

    public static CardPreviewRequestEvent e;
    public static void Trigger(CardDefinition card, string contentRoot)
    {
        e.Card = card;
        e.ContentRoot = contentRoot;
        MMEventManager.TriggerEvent(e);
    }
}
