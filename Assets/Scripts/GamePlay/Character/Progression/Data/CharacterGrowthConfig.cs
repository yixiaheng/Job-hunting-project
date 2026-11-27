using UnityEngine;

namespace Game.Characters
{
    [CreateAssetMenu(menuName = "Characters/Growth", fileName = "CharacterGrowth")]
    public sealed class CharacterGrowthConfig : ScriptableObject
    {
        public int[] levelCaps =
        {
            20,
            40,
            60,
            90,
            100
        };
        public int[] skillCaps =
        {
            2,
            3,
            4,
            6,
            7
        };
        public int[] ascensionCosts =
        {
            2,
            4,
            8,
            12
        };
        public int[] shardCosts =
        {
            10,
            20,
            40,
            60,
            100
        };
        public float levelGrowth = 0.025f;
        public float ascensionGrowth = 0.05f;
        [Header("伤害属性成长（按百分点累加，不使用生命/攻防的成长倍率）")]
        [SerializeField, InspectorName("每级增加"), Tooltip("从2级起每级增加；0.01代表每级增加1个百分点。未配置为0，不成长。")]
        private Battle.Logic.BattleOffensiveStats offensivePerLevel;
        [SerializeField, InspectorName("每次突破增加"), Tooltip("按已完成突破次数累加；0.05代表每次增加5个百分点。")]
        private Battle.Logic.BattleOffensiveStats offensivePerAscension;
        public Battle.Logic.BattleOffensiveStats OffensivePerLevel => offensivePerLevel;
        public Battle.Logic.BattleOffensiveStats OffensivePerAscension => offensivePerAscension;

        public Battle.Logic.BattleOffensiveStats CalculateOffensiveStats(
            Battle.Logic.BattleOffensiveStats baseStats, int level, int ascension)
        {
            if (level < 1) throw new System.ArgumentOutOfRangeException(nameof(level));
            if (ascension < 0) throw new System.ArgumentOutOfRangeException(nameof(ascension));
            return baseStats.AddGrowth(offensivePerLevel, level - 1)
                .AddGrowth(offensivePerAscension, ascension);
        }

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
