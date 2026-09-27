using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using NUnit.Framework;
using Object = UnityEngine.Object;

// Unity runtime geometry tests; run in EditMode Test Runner, not the standalone rule harness.
public sealed class BattleInteractionViewTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private EventSystem oldEvents;
    private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(name)).First(t => t != null);
    private GameObject Create(string name)
    { var go = new GameObject(name); objects.Add(go); return go; }
    [SetUp] public void Setup() { oldEvents = EventSystem.current; }
    [TearDown] public void Cleanup()
    {
        foreach (var go in objects) Object.DestroyImmediate(go);
        objects.Clear(); EventSystem.current = oldEvents;
    }
    [Test] public void EveryAuthoredBattleZoneHasAnEnabledCollider()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/Scenes/CardTestGym.unity");
        try
        {
            var zoneType = RuntimeType("CardPlacementZoneView");
            var zones = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren(zoneType, true)).ToArray();
            Assert.AreEqual(21, zones.Length);
            foreach (var zone in zones)
            {
                var collider = zone.GetComponent<Collider>();
                Assert.NotNull(collider, zone.name + " cannot receive a drop without a collider.");
                Assert.IsTrue(collider.enabled, zone.name);
            }
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }
    [Test] public void GuestCameraKeepsTheirStartOnSameScreenSideAsHost()
    {
        var camera = Create("camera").AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(new Vector3(-1.7f, 7.15f, -5.93f), Quaternion.Euler(52.838f, 0, 0));
        var host = new Vector3(0, .062f, -1.3f); var guest = new Vector3(0, .062f, 2.7f);
        Vector3 expected = camera.WorldToViewportPoint(host);
        var type = RuntimeType("Mishi.Networking.LocalBattlePerspective");
        var view = Activator.CreateInstance(type, camera, (host + guest) * .5f);
        type.GetMethod("SetPlayer").Invoke(view, new object[] { 1 });
        Assert.Less(Vector3.Distance(expected, camera.WorldToViewportPoint(guest)), .001f);
        var once = camera.transform.position;
        type.GetMethod("SetPlayer").Invoke(view, new object[] { 1 });
        Assert.Less(Vector3.Distance(once, camera.transform.position), .001f, "Snapshots must not accumulate rotations.");
        type.GetMethod("Restore").Invoke(view, null);
        Assert.Less(Vector3.Distance(expected, camera.WorldToViewportPoint(host)), .001f);
    }
    [Test] public void TableColliderDoesNotStealCardHitAndDragRestoresColliders()
    {
        var camera = Create("camera").AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(new Vector3(0, 5, 0), Quaternion.Euler(90, 0, 0));
        camera.pixelRect = new Rect(0, 0, 800, 600);
        var raycaster = (BaseRaycaster)camera.gameObject.AddComponent(RuntimeType("BattleTableRaycaster"));
        var scenery = Create("non-interactive collider above card");
        scenery.transform.position = new Vector3(0, 1, 0);
        scenery.AddComponent<BoxCollider>();
        var card = Create("card");
        var cardComponent = card.AddComponent(RuntimeType("BattleCard"));
        cardComponent.GetType().GetMethod("Initialize").Invoke(cardComponent, new object[] { Guid.NewGuid() });
        var collider = card.AddComponent<BoxCollider>(); collider.size = new Vector3(1, .01f, 1);
        var pointer = card.AddComponent(RuntimeType("BattleCardPointer"));
        // Awake does not necessarily run in EditMode for a regular MonoBehaviour.
        pointer.GetType().GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(pointer, null);
        pointer.GetType().GetProperty("CanDrag").SetValue(pointer, true);
        var events = Create("events").AddComponent<EventSystem>(); EventSystem.current = events;
        var input = new PointerEventData(events) { position = camera.WorldToScreenPoint(Vector3.zero), button = PointerEventData.InputButton.Left };
        input.pressPosition = input.position;
        Physics.SyncTransforms();
        var results = new List<RaycastResult>(); raycaster.Raycast(input, results);
        Assert.IsNotEmpty(results); Assert.AreEqual(card, results[0].gameObject);
        input.pointerPressRaycast = results[0];
        ((IBeginDragHandler)pointer).OnBeginDrag(input);
        Assert.IsFalse(collider.enabled);
        input.position = camera.WorldToScreenPoint(new Vector3(1, 0, 0));
        ((IDragHandler)pointer).OnDrag(input);
        Assert.Greater(card.transform.position.x, .9f);
        pointer.GetType().GetMethod("CancelDrag").Invoke(pointer, null);
        Assert.IsTrue(collider.enabled); Assert.AreEqual(Vector3.zero, card.transform.position);
    }
}
