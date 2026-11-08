using UnityEngine;

namespace Game.Characters
{
    [CreateAssetMenu(menuName = "Characters/Relic Set", fileName = "RelicSet")]
    public sealed class RelicSetConfig : ScriptableObject
    {
        public string id;
        public string displayName;
        public bool planar;
        public StatType twoPieceStat = StatType.Attack;
        public float twoPieceBonus = 0.1f;
        public float fourPieceDamageBonus;
        public float fourPieceOpeningShield;
    }
}
