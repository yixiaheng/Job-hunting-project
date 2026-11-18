namespace Game.Characters
{
    /// <summary>进入养成场景的只读参数，不保存导航器运行状态。</summary>
    public sealed class CharacterSceneContext
    {
        public string UnitId { get; }
        public CharacterTab Tab { get; }
        public string EquipmentId { get; }

        public CharacterSceneContext(string unitId = null, CharacterTab tab = CharacterTab.Attributes, string equipmentId = null)
        {
            UnitId = unitId;
            Tab = tab;
            EquipmentId = equipmentId;
        }
    }
}
