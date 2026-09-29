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
    private bool hasPlayerCards;
    public void SetPlayerCardsPresent(bool value)
    {
        if (hasPlayerCards == value) return;
        hasPlayerCards = value; RefreshPlacement();
    }
    public override Pose GetPlacementPose(int index)
    {
        Pose pose = base.GetPlacementPose(index);
        pose.position += CardAnchor.rotation * new Vector3(0, 0, -.012f * index);
        // Leave room above the board for the two back-to-back player cards.
        if (nodeKind == BoardNodeKind.Player || hasPlayerCards) pose.position += CardAnchor.rotation * new Vector3(0, 0, -.04f);
        return pose;
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        foreach (var node in neighbours) if (node != null) Gizmos.DrawLine(CardAnchor.position, node.CardAnchor.position);
    }
}
