using System;
using System.Collections.Generic;
using Game.Inventory;

namespace Game.Characters
{
    public enum EquipmentSlot
    {
        LightCone,
        Head,
        Hands,
        Body,
        Feet,
        Sphere,
        Rope
    }

    public enum CharacterTalent
    {
        None,
        Aegis,
        Verdant,
        VoltHunt,
        FrostNova,
        UmbralHex,
        AstralChord
    }

    public enum CharacterOperation
    {
        Level,
        Ascend,
        Skill,
        Rank,
        Equip,
        Unequip,
        EquipmentLevel,
        EquipmentAscend,
        Merge,
        Acquire
    }

    public enum CharacterTab
    {
        Attributes,
        LightCone,
        Relics,
        Skills,
        Rank
    }

    [Serializable]
    public sealed class CharacterProgress
    {
        public string unitId;
        public int level = 1;
        public int experience;
        public int ascension;
        public int basicLevel = 1;
        public int skillLevel = 1;
        public int ultimateLevel = 1;
        public int rank;
        public string[] equipment = new string[7];
        public int SkillLevel(SkillKind kind) => kind == SkillKind.Basic ? basicLevel : kind == SkillKind.Skill ? skillLevel : ultimateLevel;
        public CharacterProgress Copy() => new CharacterProgress
        {
            unitId = unitId,
            level = level,
            experience = experience,
            ascension = ascension,
            basicLevel = basicLevel,
            skillLevel = skillLevel,
            ultimateLevel = ultimateLevel,
            rank = rank,
            equipment = equipment == null ? new string[7] : (string[])equipment.Clone()
        };
    }

    [Serializable]
    public sealed class CharacterRosterData
    {
        public long revision;
        public List<CharacterProgress> characters = new List<CharacterProgress>();
        public CharacterRosterData Copy()
        {
            var copy = new CharacterRosterData
            {
                revision = revision
            };
            foreach (CharacterProgress character in characters)
                copy.characters.Add(character.Copy());
            return copy;
        }
    }

    [Serializable]
    public struct CharacterStats
    {
        public int health, attack, defence, speed;
        public float damageBonus;
        public string Explain() => $"生命 {health}    攻击 {attack}\n防御 {defence}    速度 {speed}\n伤害加成 {damageBonus:P0}";
    }

    public sealed class CharacterRequest
    {
        public CharacterOperation operation;
        public string unitId;
        public SkillKind skill;
        public EquipmentSlot slot;
        public string equipmentId;
        public string materialEquipmentId;
        public string materialId;
        public int quantity = 1;
        public bool allowTransfer;
        public CharacterRequest Copy() => (CharacterRequest)MemberwiseClone();
    }

    public sealed class CharacterPreview
    {
        public bool Success { get; internal set; }
        public string Message { get; internal set; }
        public string Description { get; internal set; }
        public int WastedExperience { get; internal set; }
        public CharacterStats Before { get; internal set; }
        public CharacterStats After { get; internal set; }
        public IReadOnlyList<ItemAmount> Costs => costs.AsReadOnly();

        internal readonly List<ItemAmount> costs = new List<ItemAmount>();
        internal CharacterRequest request;
        internal CharacterRosterData roster;
        internal InventoryData inventory;
        internal long revision;
        internal long inventoryRevision;
        internal readonly List<string> affected = new List<string>();
    }
}
