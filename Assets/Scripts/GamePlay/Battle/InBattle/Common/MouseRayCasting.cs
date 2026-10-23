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

    void Update()
    {
        if (Mouse.current == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        if (IsPointerOverUI()) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        var actor = hit.collider.GetComponentInParent<BattleUnitActor>();
        if (actor == null || actor.Unit == null || !actor.Unit.IsActive)
            return;

        ShowMarker(actor.transform);
        //广播
        SetSelectedTarget(actor.Unit);

        Debug.Log($"选中 {selectedTarget.displayName} pos={selectedTarget.pos}");

    }

    public void ClearSelection()
    {
        if (marker != null)
            Destroy(marker.gameObject);

        marker = null;
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
