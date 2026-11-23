using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using Game;
using Battle.Events;

//鼠标战斗检测：用于选择对象
public class MouseRayCasting : MonoBehaviour
{
    public BattleUnit selectedTarget{get; private set;}
    public GameObject makerPrefab;
    Transform marker;
    private readonly List<GameObject> rangeMarkers = new();
    private Material rangeMaterial;

    void Update()
    {
        if (BattleManager.Instance != null && !BattleManager.Instance.CanAcceptCommands) return;
        var scenes = GameSceneManager.Instance;
        if (scenes != null && (scenes.IsLoading || scenes.IsSceneSuspended(gameObject.scene))) return;
        if (Mouse.current == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        if (IsPointerOverUI()) return;

        Camera cam = BattleManager.Instance != null ? BattleManager.Instance.OutputCamera : Camera.main;
        if (cam == null) return;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        var actor = hit.collider.GetComponentInParent<BattleUnitActor>();
        if (actor == null || actor.Unit == null || !actor.Unit.IsActive)
            return;

        BattleManager.Instance?.SelectTarget(actor.Unit);

    }

    public void DisplaySelection(BattleUnit target, IReadOnlyList<BattleUnit> affected)
    {
        SetSelectedTarget(target);
        foreach (var item in rangeMarkers) if (item != null) Destroy(item);
        rangeMarkers.Clear();
        if (affected != null) foreach (var unit in affected)
        {
            if (unit == null || !BattleManager.Instance.ActorLookup.TryGetActor(unit, out var recipient)) continue;
            if (rangeMaterial == null) rangeMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            var go = new GameObject("Affected target preview"); go.transform.SetParent(recipient.PresentationRoot,false);
            var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = rangeMaterial; line.useWorldSpace = false;
            line.loop = true; line.positionCount = 40; line.widthMultiplier = unit == target ? .065f : .035f;
            line.startColor = line.endColor = unit == target ? Color.yellow : new Color(1,.5f,.15f);
            for (int i=0;i<40;i++) { float angle=i*Mathf.PI*2/40; line.SetPosition(i,new Vector3(Mathf.Cos(angle)*.65f,.04f,Mathf.Sin(angle)*.65f)); }
            rangeMarkers.Add(go);
        }
        if (target != null && BattleManager.Instance.ActorLookup.TryGetActor(target, out var actor)) ShowMarker(actor.transform);
        else if (marker != null) marker.gameObject.SetActive(false);
    }

    public void ClearSelection()
    {
        if (marker != null)
            Destroy(marker.gameObject);

        marker = null;
        foreach(var item in rangeMarkers) if(item!=null) Destroy(item);
        rangeMarkers.Clear();
        SetSelectedTarget(null);
    }

    void ShowMarker(Transform parent)
    {
        if(marker == null)
        {
            GameObject go = makerPrefab != null 
                    ? Instantiate(makerPrefab) 
                    : GameObject.CreatePrimitive(PrimitiveType.Quad);

            if(makerPrefab == null)
                Destroy(go.GetComponent<Collider>());
            
            go.AddComponent<FaceCamera>();
            marker = go.transform;
        }

        marker.SetParent(parent, false);
        marker.localPosition = new Vector3(0f, 2f, 0f); // 头上，自己调高度
        marker.localRotation = Quaternion.identity;
        marker.gameObject.SetActive(true);


    }

    private void OnDestroy() { if(rangeMaterial!=null) Destroy(rangeMaterial); }
    bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;

        var data = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, results);
        return results.Count > 0;
    }

    private void SetSelectedTarget(BattleUnit target)
    {
        if(selectedTarget == target)
            return;
        BattleUnit previousTarget = selectedTarget;

        Guid battleId = target != null ? target.BattleId : previousTarget.BattleId;

        selectedTarget = target;


        GameEvents.Battle.Publish( new TargetSelectionChangedEvent
        (
            battleId,
            previousTarget,
            selectedTarget
        ));
    }

}
