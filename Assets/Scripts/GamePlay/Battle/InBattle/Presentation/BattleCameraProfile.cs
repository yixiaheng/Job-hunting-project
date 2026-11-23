using UnityEngine;

namespace Battle.Presentation
{
    [CreateAssetMenu(menuName = "Battle/Presentation/Camera Profile")]
    public sealed class BattleCameraProfile : ScriptableObject
    {
        [SerializeField] private BattleShotSettings[] shots = System.Array.Empty<BattleShotSettings>();
        public BattleShotSettings[] Shots => shots;
        public BattleShotSettings Find(BattleShotMode mode) => System.Array.Find(shots, s => s != null && s.enabled && s.mode == mode);
        public void InitializeShots()
        {
            shots = new BattleShotSettings[System.Enum.GetValues(typeof(BattleShotMode)).Length];
            for (int i = 0; i < shots.Length; i++)
            {
                var mode = (BattleShotMode)i;
                bool group = mode is BattleShotMode.EnemyGroup or BattleShotMode.EnemySpread or BattleShotMode.Boss;
                shots[i] = new BattleShotSettings { mode = mode, fitTargets = group, hideOtherAllies = mode is BattleShotMode.EnemySingle or BattleShotMode.EnemySpread or BattleShotMode.EnemyGroup or BattleShotMode.Boss,
                    targetWeight = group ? .25f : .55f };
                shots[i].lockActorViewport = mode is BattleShotMode.EnemySingle or BattleShotMode.EnemySpread or BattleShotMode.EnemyGroup or BattleShotMode.Boss;
                if (mode is BattleShotMode.EnemyGroup or BattleShotMode.EnemySpread)
                { shots[i].position = new Vector3(.65f,2.4f,-.2f); shots[i].fov = 55; shots[i].targetWeight = 0; }
                if (mode is BattleShotMode.AllySupport or BattleShotMode.Self)
                { shots[i].position = new Vector3(0, 1.7f, 5); shots[i].targetWeight = 0; shots[i].fitTargets = true; }
                if (mode is BattleShotMode.Overview or BattleShotMode.EnemyAction)
                { shots[i].position = new Vector3(4, 4, -9); shots[i].targetWeight = 0; shots[i].fitTargets = true; }
            }
        }
        // Serialized migration snapshot only. Runtime framing uses shots exclusively.
        [Tooltip("Offset relative to the battlefield ally-to-enemy direction, not animated model facing.")]
        [HideInInspector] public Vector3 shoulderOffset = new(1.8f, 1.7f, -3.5f);
        [HideInInspector] public Vector3 aimOffset = new(0f, 1.1f, 0f);
        [HideInInspector] public bool useManualRotation;
        [HideInInspector] public Vector3 rotationEuler = new(8f, 0f, 0f);
        [HideInInspector] [Range(0f, 1f)] public float enemyCenterWeight = 0.65f;
        [HideInInspector] public Vector3 selectedPositionOffset;
        [HideInInspector] public Vector3 selectedRotationOffset;
        [HideInInspector] public float selectedFovOffset;
        [HideInInspector] public Vector3 selectedAimOffset = new(0f, 1.1f, 0f);
        [HideInInspector] [Min(0f)] public float selectionBlendSeconds = 0.2f;
        [HideInInspector] [Range(15f, 90f)] public float fieldOfView = 48f;
        [HideInInspector] [Min(0f)] public float blendSeconds = 0.35f;
        [HideInInspector] [Range(0f, 1f)] public float selectionWeight = 0.12f;
        [HideInInspector] [Min(1f)] public float groupPadding = 1.3f;
        [Min(0f)] public float fallbackWindup = 0.3f;
        [Min(0f)] public float fallbackRecovery = 0.25f;
        [Min(0f)] public float deathSeconds = 0.65f;
        [Min(0f)] public float entranceSeconds = 0.8f;
    }
}
