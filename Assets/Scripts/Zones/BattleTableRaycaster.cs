using System.Collections.Generic;
using UnityEngine.EventSystems;

// Keep UI raycast priority, but decorative/table colliders must not consume board input.
public sealed class BattleTableRaycaster : PhysicsRaycaster
{
    private readonly List<RaycastResult> hits = new List<RaycastResult>();
    public override void Raycast(PointerEventData eventData, List<RaycastResult> results)
    {
        hits.Clear();
        base.Raycast(eventData, hits);
        foreach (var hit in hits)
        {
            if (hit.gameObject.GetComponentInParent<BattleCardPointer>() != null ||
                hit.gameObject.GetComponentInParent<CardPlacementZoneView>() != null)
                results.Add(hit);
        }
    }
}
