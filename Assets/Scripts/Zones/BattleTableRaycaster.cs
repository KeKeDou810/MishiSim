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
        var hand = BattleCardPointer.HandAt(eventCamera, eventData.position);
        if (hand != null)
        {
            results.Add(new RaycastResult { gameObject = hand.gameObject, module = this,
                distance = UnityEngine.Vector3.Distance(eventCamera.transform.position, hand.transform.position),
                worldPosition = hand.transform.position, screenPosition = eventData.position, index = results.Count });
        }
        foreach (var hit in hits)
        {
            var pointer = hit.gameObject.GetComponentInParent<BattleCardPointer>();
            if (pointer != null && pointer.IsHandManaged) continue;
            if (pointer != null ||
                hit.gameObject.GetComponentInParent<CardPlacementZoneView>() != null)
                results.Add(hit);
        }
    }
}
