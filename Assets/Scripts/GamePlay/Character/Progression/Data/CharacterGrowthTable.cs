using System;
using UnityEngine;

namespace Game.Characters
{
    [Serializable]
    public struct CharacterGrowthStage
    {
        [SerializeField] private int startLevel;
        [SerializeField] private int maxLevel;
        [SerializeField] private float startMultiplier;
        [SerializeField] private float endMultiplier;
        public int StartLevel => startLevel;
        public int MaxLevel => maxLevel;
        public float StartMultiplier => startMultiplier;
        public float EndMultiplier => endMultiplier;
        public CharacterGrowthStage(int start, int max, float first, float last)
        { startLevel = start; maxLevel = max; startMultiplier = first; endMultiplier = last; }
    }

    /// <summary>所有角色共用。阶段内线性插值，突破只通过切换阶段体现，不再额外加成。</summary>
    [CreateAssetMenu(menuName = "Characters/Shared Growth Table")]
    public sealed class CharacterGrowthTable : ScriptableObject
    {
        public const string ResourcePath = "Bootstrap/CharacterGrowthTable";
        [SerializeField] private CharacterGrowthStage[] stages =
        {
            new(1,20,1f,1.95f), new(20,30,2.35f,2.85f), new(30,40,3.25f,3.75f),
            new(40,50,4.15f,4.65f), new(50,60,5.05f,5.55f),
            new(60,70,5.95f,6.45f), new(70,80,6.85f,7.35f)
        };
        public static CharacterGrowthTable Shared => Resources.Load<CharacterGrowthTable>(ResourcePath)
            ?? throw new InvalidOperationException("缺少共用成长表：Resources/" + ResourcePath);
        public int StageCount => stages.Length;
        public int MaxLevel => stages[stages.Length - 1].MaxLevel;
        public CharacterGrowthStage GetStage(int ascension)
        {
            if (ascension < 0 || ascension >= stages.Length) throw new ArgumentOutOfRangeException(nameof(ascension));
            return stages[ascension];
        }
        public bool IsValidProgress(int level, int ascension) => ascension >= 0 && ascension < stages.Length
            && level >= stages[ascension].StartLevel && level <= stages[ascension].MaxLevel;
        public float Evaluate(int level, int ascension)
        {
            Validate();
            if (!IsValidProgress(level, ascension))
                throw new ArgumentOutOfRangeException(nameof(level), $"等级{level}与突破阶段{ascension}不匹配。");
            var s = stages[ascension];
            float t = (level - s.StartLevel) / (float)(s.MaxLevel - s.StartLevel);
            return Mathf.Lerp(s.StartMultiplier, s.EndMultiplier, t);
        }
        public int[] GetLevelCaps()
        {
            var result = new int[stages.Length];
            for (int i = 0; i < stages.Length; i++) result[i] = stages[i].MaxLevel;
            return result;
        }
        public void Validate()
        {
            if (stages == null || stages.Length == 0) throw new InvalidOperationException("成长表不能为空。");
            for (int i = 0; i < stages.Length; i++)
            {
                var s = stages[i];
                if (s.StartLevel < 1 || s.MaxLevel <= s.StartLevel || !Finite(s.StartMultiplier) || !Finite(s.EndMultiplier)
                    || s.StartMultiplier <= 0 || s.EndMultiplier < s.StartMultiplier
                    || (i == 0 && (s.StartLevel != 1 || s.StartMultiplier != 1))
                    || (i > 0 && (s.StartLevel != stages[i-1].MaxLevel || s.StartMultiplier < stages[i-1].EndMultiplier)))
                    throw new InvalidOperationException("成长表阶段不连续或系数无效：" + i);
            }
        }
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
