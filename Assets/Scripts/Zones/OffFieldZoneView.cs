using System;
using UnityEngine;

public sealed class OffFieldZoneView : CardPlacementZoneView
{
    [SerializeField] private BoardNodeView[] affectedNodes = Array.Empty<BoardNodeView>();
    public override CardZoneKind Kind => CardZoneKind.OffField;
    public System.Collections.Generic.IReadOnlyList<BoardNodeView> AffectedNodes => Array.AsReadOnly(affectedNodes);
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        foreach (var node in affectedNodes) if (node != null) Gizmos.DrawLine(CardAnchor.position, node.CardAnchor.position);
    }
}
