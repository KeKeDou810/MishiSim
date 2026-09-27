using System.Collections.Generic;
using UnityEngine;

public sealed class DeckZoneView : CardPlacementZoneView
{
    public override CardZoneKind Kind => CardZoneKind.Deck;
    private readonly List<GameObject> backs = new List<GameObject>();
    // Anonymous proxies: no definition, instance identity, hover or drag is exposed.
    public void SetHiddenCount(int count, BattleCard prefab)
    {
        while (backs.Count > count)
        {
            int last = backs.Count - 1;
            Destroy(backs[last]); backs.RemoveAt(last);
        }
        while (backs.Count < count)
        {
            var card = Instantiate(prefab, transform);
            card.name = "Hidden deck card";
            foreach (var collider in card.GetComponentsInChildren<Collider>()) collider.enabled = false;
            var pointer = card.GetComponent<BattleCardPointer>();
            if (pointer != null) pointer.enabled = false;
            foreach (var visual in card.GetComponentsInChildren<CardVisual>()) visual.HideFace();
            card.enabled = false;
            backs.Add(card.gameObject);
        }
        for (int i = 0; i < backs.Count; i++)
        {
            Pose pose = GetPlacementPose(i);
            backs[i].transform.SetPositionAndRotation(pose.position, pose.rotation);
            // Zone scales control the board mesh, not card dimensions.
            backs[i].transform.localScale = new Vector3(1 / transform.lossyScale.x, 1 / transform.lossyScale.y, 1 / transform.lossyScale.z);
        }
    }
}
