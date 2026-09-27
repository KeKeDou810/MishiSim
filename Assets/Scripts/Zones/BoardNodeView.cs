using System;
using UnityEngine;

public enum BoardNodeKind { Normal, Center, Defense, Player }
public sealed class BoardNodeView : CardPlacementZoneView
{
    [SerializeField] private int nodeId;
    [SerializeField] private BoardNodeKind nodeKind;
    [SerializeField] private BoardNodeView[] neighbours = Array.Empty<BoardNodeView>();
    public override CardZoneKind Kind => CardZoneKind.Board;
    public int NodeId => nodeId;
    public BoardNodeKind NodeKind => nodeKind;
    public System.Collections.Generic.IReadOnlyList<BoardNodeView> Neighbours => Array.AsReadOnly(neighbours);
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        foreach (var node in neighbours) if (node != null) Gizmos.DrawLine(CardAnchor.position, node.CardAnchor.position);
    }
}
