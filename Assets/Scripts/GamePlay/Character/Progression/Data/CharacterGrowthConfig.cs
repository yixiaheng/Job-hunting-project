using UnityEngine;

namespace Game.Characters
{
    [CreateAssetMenu(menuName = "Characters/Growth", fileName = "CharacterGrowth")]
    public sealed class CharacterGrowthConfig : ScriptableObject
    {
        [SerializeField, InspectorName("养成配置ID"), Tooltip("Excel稳定关联标识；旧资源未填写时使用资源GUID。修改ID不是资源重命名。")]
        private string configId;
        public string ConfigId => configId;
        // 等级上限来自全局成长表，角色不能单独覆盖。
        public int[] levelCaps => CharacterGrowthTable.Shared.GetLevelCaps();
        public int[] skillCaps = { 2, 3, 3, 4, 4, 6, 7 };
        public int[] ascensionCosts = { 2, 2, 4, 4, 8, 12 };
        public int[] shardCosts =
        {
            10,
            20,
            40,
            60,
            100
        };
        public int experienceBase = 100;
        public int experiencePerLevel = 20;
        public int talentUnlockLevel = 60;
        public int duplicateShards = 10;
        public string characterExperience = "character_exp";
        public string characterAscension = "character_ascension";
        public string skillMaterial = "skill_manual";
        public int skillCostPerLevel = 2;
        public float skillGrowth = 0.1f;
        public System.Collections.Generic.List<SkillEffectGrowth> effectGrowth = new();
        public float rankPrimaryFirst = 0.05f;
        public float rankHealth = 0.1f;
        public float rankPrimaryLast = 0.1f;
        public float rankBasic = 0.2f;
        public float rankSkill = 0.1f;
        public float rankUltimate = 0.15f;
        public StatType primaryStat = StatType.Attack;
        public string shardItem;
        public CharacterTalent talent;
        public float talentValue = 0.1f;
        public float talentSecondary = 0.1f;
        public int talentEnergy = 5;
        public int talentDuration = 2;
        public int NextExperience(int level) => checked(experienceBase + experiencePerLevel * level);
    }

    [System.Serializable]
    public sealed class SkillEffectGrowth
    {
        public string effectKey;
        public float perSkillLevel = 0.1f;
        public int requiredRank;
        public float rankBonus;
    }
}
