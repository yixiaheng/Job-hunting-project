using System.Collections.Generic;
using UnityEngine;

public interface IBattleActorLookup
{
    bool TryGetActor(BattleUnit unit, out BattleUnitActor actor);
}

/// <summary>拥有逻辑单位与场景模型的映射及模型生命周期。</summary>
public sealed class BattleActorRegistry : IBattleActorLookup
{
    private readonly Dictionary<BattleUnit, BattleUnitActor> actors = new();
    private readonly MouseRayCasting rayCasting;

    public int Count => actors.Count;

    public BattleActorRegistry(MouseRayCasting rayCasting = null)
    {
        this.rayCasting = rayCasting;
    }

    public bool TrySpawn(UnitConfig config, BattleUnit unit, Transform anchor,
        out BattleUnitActor actor, out string error)
    {
        actor = null;
        error = null;
        if (config == null || unit == null || config.prefab == null || anchor == null)
        {
            error = $"{unit?.faction} 站位 {unit?.pos} 缺少模型配置或场景站位。";
            return false;
        }
        if (actors.ContainsKey(unit))
        {
            error = $"单位 {unit.displayName} 已经绑定场景模型。";
            return false;
        }

        GameObject instance = Object.Instantiate(config.prefab);
        instance.transform.position = anchor.position;
        actor = instance.GetComponent<BattleUnitActor>();
        if (actor == null)
            actor = instance.AddComponent<BattleUnitActor>();
        actor.Bind(unit);
        if (instance.GetComponentInChildren<Collider>() == null)
            instance.AddComponent<CapsuleCollider>();
        actors.Add(unit, actor);
        return true;
    }

    public bool TryGetActor(BattleUnit unit, out BattleUnitActor actor)
    {
        if (unit != null && actors.TryGetValue(unit, out actor) && actor != null)
            return true;
        actor = null;
        return false;
    }

    public void Remove(BattleUnit unit)
    {
        if (unit == null || !actors.TryGetValue(unit, out BattleUnitActor actor))
            return;
        actors.Remove(unit);
        DestroyActor(actor);
    }

    public void Clear()
    {
        rayCasting?.ClearSelection();
        foreach (BattleUnitActor actor in actors.Values)
            DestroyActor(actor);
        actors.Clear();
    }

    private static void DestroyActor(BattleUnitActor actor)
    {
        if (actor == null)
            return;
        actor.Bind(null);
        if (Application.isPlaying)
            Object.Destroy(actor.gameObject);
        else
            Object.DestroyImmediate(actor.gameObject);
    }
}
