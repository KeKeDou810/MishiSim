using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

[RequireComponent(typeof(BattleCard))]
public sealed class BattleCardPointer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
    IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
    // The local PlayerView/controller sets these permissions, not card definitions.
    public bool CanPreview { get; set; }
    public bool CanDrag { get; set; }
    public event System.Action<BattleCard> Clicked;
    public event System.Action<BattleCard> DragDenied;
    public event System.Action<BattleCard> DragStarted;
    private static readonly HashSet<BattleCardPointer> handPointers = new HashSet<BattleCardPointer>();
    private Camera handCamera;
    private Rect handHitArea;
    private Vector2 handRestCenter, handRestSize;
    private float handRestAngle;
    private bool hasHandShape, handRaised;
    public bool IsHandManaged { get; private set; }
    public bool SnapHandLayout { get; set; }
    public event System.Action<float> HandScrolled;
    public bool CanPanHand { get; set; }
    public event System.Action HandPanStarted;
    public static bool IsHorizontalHandGesture(Vector2 delta) => Mathf.Abs(delta.x) > Mathf.Abs(delta.y) * 1.25f;
    public void SetHandArea(Camera camera, Rect area)
    { handCamera = camera; handHitArea = area; IsHandManaged = true; handPointers.Add(this); }
    public void SetHandShape(Vector2 center, Vector2 size, float angle)
    { handRestCenter = center; handRestSize = size; handRestAngle = angle; hasHandShape = true; }
    public void SetHandRaised(bool raised) { handRaised = raised; }
    public void ClearHandArea()
    { handPointers.Remove(this); IsHandManaged = false; handCamera = null; hasHandShape = handRaised = false; }
    public void OnScroll(PointerEventData e)
    {
        if (dragging || e.dragging) return;
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (IsHandManaged && keyboard != null && keyboard.shiftKey.isPressed)
            HandScrolled?.Invoke(e.scrollDelta.y);
        else if (CanPreview && card.Definition != null)
            CardPreviewScrollEvent.Trigger(e.scrollDelta.y);
        e.Use();
    }
    public static BattleCardPointer HandAt(Camera camera, Vector2 position)
    {
        BattleCardPointer best = null;
        float distance = float.MaxValue;
        foreach (var pointer in handPointers)
        {
            if (pointer == null || !pointer.isActiveAndEnabled || pointer.dragging || pointer.handCamera != camera || !pointer.ContainsRestingHand(position)) continue;
            float current = Mathf.Abs(position.x - pointer.handHitArea.center.x);
            if (current < distance) { best = pointer; distance = current; }
        }
        if (best != null) return best;
        // Only the already-hovered card can retain hover above the resting strip, and
        // only on its actually rendered surface. An invisible enlarged box cannot steal board clicks.
        foreach (var pointer in handPointers)
            if (pointer != null && pointer.isActiveAndEnabled && !pointer.dragging && pointer.handCamera == camera &&
                pointer.handRaised && pointer.ContainsRaisedHand(camera, position)) return pointer;
        return null;
    }
    private bool ContainsRestingHand(Vector2 position)
    {
        if (!handHitArea.Contains(position)) return false;
        if (!hasHandShape) return true;
        Vector2 delta = position - handRestCenter;
        float angle = handRestAngle * Mathf.Deg2Rad, c = Mathf.Cos(angle), s = Mathf.Sin(angle);
        return Mathf.Abs(delta.x * c + delta.y * s) <= handRestSize.x * .5f &&
               Mathf.Abs(-delta.x * s + delta.y * c) <= handRestSize.y * .5f;
    }
    private bool ContainsRaisedHand(Camera camera, Vector2 position)
    {
        var box = GetComponent<BoxCollider>();
        if (box == null || !box.enabled) return false;
        var ray = camera.ScreenPointToRay(position);
        var plane = new Plane(transform.forward, transform.TransformPoint(box.center));
        if (!plane.Raycast(ray, out float distance)) return false;
        var local = transform.InverseTransformPoint(ray.GetPoint(distance)) - box.center;
        return Mathf.Abs(local.x) <= box.size.x * .5f && Mathf.Abs(local.y) <= box.size.y * .5f;
    }
    private BattleCard card;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Plane dragPlane;
    private Vector3 pointerOffset;
    private Collider[] colliders;
    private bool[] enabledStates;
    private bool dragging;
    public bool IsDragging => dragging;
    public Vector3 DragDelta => dragging ? transform.position - startPosition : Vector3.zero;
    private Camera dragCamera;
    private readonly List<RaycastResult> dropHits = new List<RaycastResult>();
    private void Awake() { card = GetComponent<BattleCard>(); }
    public bool IsHovered { get; private set; }
    public void OnPointerEnter(PointerEventData e) { IsHovered = true; if (!e.dragging) Preview(); }
    public void OnPointerExit(PointerEventData e) { IsHovered = false; }
    public void OnPointerDown(PointerEventData e)
    {
        // Capture the press without firing click actions twice (press and release).
    }
    public void OnPointerClick(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left && !e.dragging && CanPreview)
        { Preview(); Clicked?.Invoke(card); }
    }
    private void Preview()
    {
        if (!dragging && CanPreview && card.Definition != null)
            CardPreviewRequestEvent.Trigger(card.Definition, CardDatabaseService.Instance.ContentRoot, card.EffectivePower, card.EffectiveTime);
    }
    public void OnBeginDrag(PointerEventData e)
    {
        if (!card.IsInitialized || e.button != PointerEventData.InputButton.Left) return;
        if (IsHandManaged && CanPanHand && IsHorizontalHandGesture(e.position - e.pressPosition))
        { HandPanStarted?.Invoke(); return; }
        if (!CanDrag) { DragDenied?.Invoke(card); return; }
        DragStarted?.Invoke(card);
        dragCamera = e.pressEventCamera != null ? e.pressEventCamera : Camera.main;
        if (dragCamera == null) return;
        startPosition = transform.position; startRotation = transform.rotation;
        dragPlane = new Plane(Vector3.up, startPosition);
        Ray ray = dragCamera.ScreenPointToRay(e.pressPosition);
        if (!dragPlane.Raycast(ray, out float distance)) return;
        pointerOffset = startPosition - ray.GetPoint(distance);
        colliders = GetComponentsInChildren<Collider>();
        enabledStates = new bool[colliders.Length];
        for (int i = 0; i < colliders.Length; i++) { enabledStates[i] = colliders[i].enabled; colliders[i].enabled = false; }
        dragging = true;
        IsHovered = false;
        card.StopMovement();
        card.MoveTo(transform.position, transform.rotation, immediate: true);
    }
    public void OnDrag(PointerEventData e)
    {
        if (!dragging || dragCamera == null) return;
        Ray ray = dragCamera.ScreenPointToRay(e.position);
        if (dragPlane.Raycast(ray, out float distance)) transform.position = ray.GetPoint(distance) + pointerOffset + Vector3.up * .12f;
    }
    public void SubmitDrop(CardPlacementZoneView target)
    {
        if (!dragging) return;
        Restore(); // Restore BEFORE notifying rules; approved placement must not be undone.
        target.RequestPlacement(card);
    }
    public void OnEndDrag(PointerEventData e)
    {
        // OnDrop normally handles this. If it did not, resolve the release target while our
        // colliders are still disabled. Never skip a UI hit to drop through a menu/preview.
        if (dragging && EventSystem.current != null)
        {
            dropHits.Clear();
            EventSystem.current.RaycastAll(e, dropHits);
            if (dropHits.Count > 0)
            {
                var hit = dropHits[0].gameObject;
                var zone = hit.GetComponentInParent<CardPlacementZoneView>();
                if (zone == null)
                {
                    var targetCard = hit.GetComponentInParent<BattleCard>();
                    if (targetCard != null) zone = targetCard.CurrentZone;
                }
                if (zone != null) SubmitDrop(zone);
            }
        }
        Restore();
    }
    public void CancelDrag() { Restore(); }
    private void OnDisable() { IsHovered = false; Restore(); ClearHandArea(); }
    private void Restore()
    {
        if (!dragging) return;
        dragging = false;
        card.MoveTo(startPosition, startRotation);
        for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = enabledStates[i];
    }
}
