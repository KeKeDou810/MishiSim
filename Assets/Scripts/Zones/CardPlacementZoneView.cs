using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public enum CardZoneKind { Board, Deck, Discard, Contract, OffField }
public enum CardZoneLayout { Stack, Row, Slots }

// Presentation only: placement APIs must be called AFTER rule/host approval.
public abstract class CardPlacementZoneView : MonoBehaviour, IPointerEnterHandler,
    IPointerExitHandler, IPointerClickHandler, IDropHandler
{
    [SerializeField] private string zoneId;
    [SerializeField] private int ownerId = -1;
    [SerializeField] private Transform cardAnchor;
    [SerializeField] private CardZoneLayout layout;
    [SerializeField] private Vector3 stackOffset = new Vector3(0, 0, -.003f);
    [SerializeField] private Vector3 rowOffset = new Vector3(.7f, 0, 0);
    [SerializeField] private Transform[] slots = Array.Empty<Transform>();
    [SerializeField, Min(0)] private int visualCapacity;
    [SerializeField] private bool faceDown;
    [SerializeField] private GameObject hoverHighlight;
    private readonly List<BattleCard> cards = new List<BattleCard>();
    public abstract CardZoneKind Kind { get; }
    public string ZoneId => zoneId;
    public int OwnerId => ownerId;
    public Transform CardAnchor => cardAnchor != null ? cardAnchor : transform;
    public IReadOnlyList<BattleCard> Cards => cards.AsReadOnly();
    public bool IsHovered { get; private set; }
    public event Action<CardPlacementZoneView, bool> HoverChanged;
    public event Action<CardPlacementZoneView, PointerEventData.InputButton> Clicked;
    public event Action<CardPlacementZoneView, BattleCard> PlacementRequested;

    protected virtual void Awake()
    {
        // PhysicsRaycaster needs a collider on the receiving zone, not just on the card.
        // Existing authored colliders keep their shape/settings; supply one only if missing.
        if (GetComponent<Collider>() != null) return;
        var mesh = GetComponent<MeshFilter>();
        var bounds = mesh != null && mesh.sharedMesh != null
            ? mesh.sharedMesh.bounds : new Bounds(Vector3.zero, new Vector3(1, 1, .01f));
        var hitArea = gameObject.AddComponent<BoxCollider>();
        hitArea.center = bounds.center;
        hitArea.size = new Vector3(Mathf.Max(bounds.size.x, .01f),
            Mathf.Max(bounds.size.y, .01f), Mathf.Max(bounds.size.z, .01f));
    }

    protected virtual void OnEnable()
    {
        if (string.IsNullOrWhiteSpace(zoneId))
        { Debug.LogError("区域需要唯一 ZoneId。", this); return; }
        ZoneRegistry.Register(this);
    }
    protected virtual void OnDisable()
    {
        ZoneRegistry.Unregister(this);
        SetHover(false);
    }
    public void OnPointerEnter(PointerEventData e) { SetHover(true); }
    public void OnPointerExit(PointerEventData e) { SetHover(false); }
    private void SetHover(bool value)
    {
        IsHovered = value;
        if (hoverHighlight != null) hoverHighlight.SetActive(value);
        HoverChanged?.Invoke(this, value);
    }
    public void OnPointerClick(PointerEventData e)
    {
        if (!e.dragging) Clicked?.Invoke(this, e.button);
    }
    public void OnDrop(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left || e.pointerDrag == null) return;
        var pointer = e.pointerDrag.GetComponent<BattleCardPointer>();
        if (pointer != null) pointer.SubmitDrop(this);
    }
    public void RequestPlacement(BattleCard card)
    {
        if (card == null || !card.IsInitialized) return;
        PlacementRequested?.Invoke(this, card);
    }
    public bool PlaceApproved(BattleCard card)
    {
        if (card == null || !card.IsInitialized) return false;
        cards.RemoveAll(c => c == null);
        if (cards.Contains(card)) { RefreshPlacement(); return true; }
        if (visualCapacity > 0 && cards.Count >= visualCapacity) return false;
        if (layout == CardZoneLayout.Slots && (cards.Count >= slots.Length || slots[cards.Count] == null)) return false;
        if (card.CurrentZone != null) card.CurrentZone.RemoveView(card);
        cards.Add(card);
        card.CurrentZone = this;
        RefreshPlacement();
        return true;
    }
    public void RemoveView(BattleCard card)
    {
        if (!cards.Remove(card)) return;
        if (card != null && card.CurrentZone == this) card.CurrentZone = null;
        RefreshPlacement();
    }
    public Pose GetPlacementPose(int index)
    {
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        Transform anchor = CardAnchor;
        Vector3 offset = layout == CardZoneLayout.Row ? rowOffset * index : stackOffset * index;
        if (layout == CardZoneLayout.Slots)
        {
            if (index >= slots.Length || slots[index] == null) throw new InvalidOperationException("区域槽位未配置。");
            anchor = slots[index]; offset = Vector3.zero;
        }
        // Ignore anchor scale: changing a zone mesh's size must not scale cards or spacing.
        return new Pose(anchor.position + anchor.rotation * offset,
            anchor.rotation * (faceDown ? Quaternion.Euler(0, 180, 0) : Quaternion.identity));
    }
    public void RefreshPlacement()
    {
        cards.RemoveAll(c => c == null);
        for (int i = 0; i < cards.Count; i++)
        {
            Pose pose = GetPlacementPose(i);
            cards[i].transform.SetPositionAndRotation(pose.position, pose.rotation * cards[i].PlacementRotationOffset);
        }
    }
}
