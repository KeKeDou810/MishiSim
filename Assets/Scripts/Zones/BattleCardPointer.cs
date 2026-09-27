using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

[RequireComponent(typeof(BattleCard))]
public sealed class BattleCardPointer : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler,
    IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // The local PlayerView/controller sets these permissions, not card definitions.
    public bool CanPreview { get; set; }
    public bool CanDrag { get; set; }
    public event System.Action<BattleCard> Clicked;
    public event System.Action<BattleCard> DragDenied;
    private BattleCard card;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Plane dragPlane;
    private Vector3 pointerOffset;
    private Collider[] colliders;
    private bool[] enabledStates;
    private bool dragging;
    public bool IsDragging => dragging;
    private Camera dragCamera;
    private readonly List<RaycastResult> dropHits = new List<RaycastResult>();
    private void Awake() { card = GetComponent<BattleCard>(); }
    public void OnPointerEnter(PointerEventData e) { Preview(); }
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
        if (CanPreview && card.Definition != null)
            CardPreviewRequestEvent.Trigger(card.Definition, CardDatabaseService.Instance.ContentRoot);
    }
    public void OnBeginDrag(PointerEventData e)
    {
        if (!card.IsInitialized || e.button != PointerEventData.InputButton.Left) return;
        if (!CanDrag) { DragDenied?.Invoke(card); return; }
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
    private void OnDisable() { Restore(); }
    private void Restore()
    {
        if (!dragging) return;
        dragging = false;
        transform.SetPositionAndRotation(startPosition, startRotation);
        for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = enabledStates[i];
    }
}
