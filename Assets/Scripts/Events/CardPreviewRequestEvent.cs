using MoreMountains.Tools;
using UnityEngine;

public struct CardPreviewRequestEvent
{
    public CardDefinition Card;
    public string ContentRoot;
    public int? EffectivePower, EffectiveTime;

    public CardPreviewRequestEvent(CardDefinition card, string contentRoot)
    {
        Card = card;
        ContentRoot = contentRoot;
        EffectivePower = null; EffectiveTime = null;
    }

    public static CardPreviewRequestEvent e;
    public static void Trigger(CardDefinition card, string contentRoot, int? effectivePower = null, int? effectiveTime = null)
    {
        e.Card = card;
        e.ContentRoot = contentRoot;
        e.EffectivePower = effectivePower; e.EffectiveTime = effectiveTime;
        MMEventManager.TriggerEvent(e);
    }
}

// Mouse wheel over a visible battle card controls the floating effect description.
public struct CardPreviewScrollEvent
{
    public float Delta;
    public static void Trigger(float delta)
    { MMEventManager.TriggerEvent(new CardPreviewScrollEvent { Delta = delta }); }
}
