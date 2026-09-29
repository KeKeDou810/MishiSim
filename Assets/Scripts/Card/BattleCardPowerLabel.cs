using TMPro;
using UnityEngine;

public sealed class BattleCardPowerLabel : MonoBehaviour
{
    [SerializeField] private RectTransform panel;
    [SerializeField] private TMP_Text value;
    [SerializeField] private Vector3 localOffset = new Vector3(0, -.23f, -.025f);
    [SerializeField] private Color increased = new Color(.596f, .765f, .475f);
    [SerializeField] private Color decreased = new Color(.878f, .424f, .459f);
    private bool visible;
    private bool countMode;
    private Vector3 countPosition;
    public void ApplyCount(int count, bool show, Vector3 position)
    {
        countMode = true; countPosition = position; visible = show;
        panel.gameObject.SetActive(show);
        if (!show) return;
        value.SetText("{0}", count); value.color = Color.white;
    }
    private readonly Vector3[] corners = new Vector3[4];
    private void Awake() { if (panel != null) panel.gameObject.SetActive(false); }
    public void Apply(CardDefinition definition, int currentPower, bool onField)
    {
        countMode = false;
        visible = onField && definition != null && !definition.IsPlayer && !definition.IsDecision;
        panel.gameObject.SetActive(visible);
        if (!visible) return;
        value.text = currentPower.ToString();
        value.color = currentPower > definition.Power ? increased : currentPower < definition.Power ? decreased : Color.white;
    }
    private void LateUpdate()
    {
        if (!visible || Camera.main == null) return;
        PositionForCamera(Camera.main);
    }
    private void PositionForCamera(Camera camera)
    {
        if (countMode)
        {
            panel.position = countPosition + camera.transform.up * .48f - camera.transform.forward * .15f;
            panel.rotation = camera.transform.rotation;
            var parentScale = panel.parent.lossyScale;
            panel.localScale = new Vector3(.0025f / Mathf.Abs(parentScale.x), .0025f / Mathf.Abs(parentScale.y), .0025f / Mathf.Abs(parentScale.z));
            return;
        }
        panel.position = transform.TransformPoint(localOffset);
        panel.rotation = camera.transform.rotation;
        // A billboard tilted relative to the card can put its bottom half inside the card.
        // Lift the entire rectangle toward the camera, retaining its screen-space anchor.
        Vector3 normal = -transform.forward;
        Vector3 towardCamera = -camera.transform.forward;
        float facing = Vector3.Dot(normal, towardCamera);
        if (facing <= .01f) return;
        panel.GetWorldCorners(corners);
        float minimumDistance = float.PositiveInfinity;
        foreach (var corner in corners)
            minimumDistance = Mathf.Min(minimumDistance, Vector3.Dot(corner - transform.position, normal));
        float clearance = .03f * Mathf.Abs(transform.lossyScale.z);
        if (minimumDistance < clearance)
            panel.position += towardCamera * ((clearance - minimumDistance) / facing);
    }
}
