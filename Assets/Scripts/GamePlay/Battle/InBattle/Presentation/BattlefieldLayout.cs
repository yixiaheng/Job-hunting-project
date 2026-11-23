using System;
using System.Collections.Generic;
using UnityEngine;

namespace Battle.Presentation
{
    [Serializable]
    public sealed class BattleSlotPose
    {
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("position"), InspectorName("position")] private Vector3 positionValue;
        public Vector3 position { get => positionValue; set => positionValue = value; }
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("euler"), InspectorName("euler")] private Vector3 eulerValue;
        public Vector3 euler { get => eulerValue; set => eulerValue = value; }
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("readyOffset"), InspectorName("readyOffset")] private Vector3 readyOffsetValue = new(0, 0, .6f);
        public Vector3 readyOffset { get => readyOffsetValue; set => readyOffsetValue = value; }
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("readyEuler"), InspectorName("readyEuler")] private Vector3 readyEulerValue;
        public Vector3 readyEuler { get => readyEulerValue; set => readyEulerValue = value; }
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("cameraOffset"), InspectorName("cameraOffset")] private Vector3 cameraOffsetValue;
        public Vector3 cameraOffset { get => cameraOffsetValue; set => cameraOffsetValue = value; }
        public BattleSlotPose Copy() => (BattleSlotPose)MemberwiseClone();
    }

    [Serializable]
    public sealed class EnemyFormationPreset
    {
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("id"), InspectorName("id")] private string idValue;
        public string id { get => idValue; set => idValue = value; }
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("slots"), InspectorName("slots")] private BattleSlotPose[] slotsValue = Array.Empty<BattleSlotPose>();
        public BattleSlotPose[] slots { get => slotsValue; set => slotsValue = value; }
    }

    [CreateAssetMenu(menuName = "Battle/Presentation/Battlefield Layout")]
    public sealed class BattlefieldLayout : ScriptableObject
    {
        [SerializeField] private BattleSlotPose[] allies = Array.Empty<BattleSlotPose>();
        [SerializeField] private List<EnemyFormationPreset> enemyFormations = new();
        [SerializeField, Min(.1f)] private float generatedEnemySpacing = 2.2f;
        [SerializeField] private float enemyDepth = 4f;
        [SerializeField, Min(0)] private float readySeconds = .2f;
        public BattleSlotPose[] Allies => allies;
        public IReadOnlyList<EnemyFormationPreset> EnemyFormations => enemyFormations;
        public float ReadySeconds => readySeconds;

        public BattleSlotPose GetSlot(Faction side, int slot, int capacity, string preset = null)
        {
            if (side == Faction.OurSide && slot >= 0 && slot < allies.Length && allies[slot] != null)
                return allies[slot];
            if (side == Faction.Enemy)
            {
                EnemyFormationPreset found = !string.IsNullOrEmpty(preset)
                    ? enemyFormations.Find(p => p.id == preset) : enemyFormations.Find(p => p.id == "Count" + capacity);
                if (found != null && slot >= 0 && slot < found.slots.Length && found.slots[slot] != null)
                    return found.slots[slot];
            }
            return new BattleSlotPose
            {
                position = new Vector3((slot - (capacity - 1) * .5f) * generatedEnemySpacing, 0,
                    side == Faction.Enemy ? enemyDepth : -4f),
                euler = side == Faction.Enemy ? new Vector3(0, 180, 0) : Vector3.zero,
                readyOffset = side == Faction.Enemy ? Vector3.zero : new Vector3(0, 0, .6f)
            };
        }

        public void InitializeDefaults()
        {
            allies = new BattleSlotPose[4];
            for (int i = 0; i < 4; i++) allies[i] = new BattleSlotPose { position = new Vector3((i - 1.5f) * 1.8f, 0, -4f) };
            enemyFormations.Clear();
            for (int count = 1; count <= 5; count++)
            {
                var formation = new EnemyFormationPreset { id = "Count" + count, slots = new BattleSlotPose[count] };
                for (int i = 0; i < count; i++) formation.slots[i] = new BattleSlotPose
                { position = new Vector3((i - (count - 1) * .5f) * generatedEnemySpacing, 0, enemyDepth),
                    euler = new Vector3(0, 180, 0), readyOffset = Vector3.zero };
                enemyFormations.Add(formation);
            }
            enemyFormations.Add(new EnemyFormationPreset { id = "Boss", slots = new[] {
                new BattleSlotPose { position = new Vector3(0, 0, enemyDepth + 2), euler = new Vector3(0,180,0), readyOffset = Vector3.zero } } });
        }
    }
}
