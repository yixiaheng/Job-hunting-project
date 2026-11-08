using System;
using System.Linq;
using Game.UI;
using Game.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Characters
{
    public sealed class CharacterPanel : UIPanel
    {
        [SerializeField]
        private RectTransform rosterRoot;
        [SerializeField]
        private RectTransform detailRoot;
        [SerializeField]
        private RectTransform tabRoot;
        [SerializeField]
        private Button back;
        [SerializeField]
        private TMP_Text title;
        [SerializeField]
        private CharacterModelDrag drag;
        private CharacterProgressionService service;
        private CharacterSceneController controller;
        private string unitId;
        private CharacterTab tab;
        private string focusedEquipment;
        private IDisposable subscription;
        public string SelectedUnitId => unitId;
        public CharacterTab SelectedTab => tab;

        public void BuildLayout()
        {
            CharacterUIWidgets.Clear(transform);
            var header = CharacterUIWidgets.Image("Header", transform, new Vector2(.02f, .9f), new Vector2(.98f, .98f), CharacterUIWidgets.Background);
            title = CharacterUIWidgets.Label(header.transform, "角色培养", 46);
            var t = (RectTransform)title.transform;
            t.anchorMax = new Vector2(.78f, 1);
            back = CharacterUIWidgets.Button(header.transform, "返回");
            var br = (RectTransform)back.transform;
            br.anchorMin = new Vector2(.83f, 0);
            br.anchorMax = Vector2.one;
            rosterRoot = CharacterUIWidgets.Scroll(transform, new Vector2(.02f, .04f), new Vector2(.22f, .87f));
            detailRoot = CharacterUIWidgets.Scroll(transform, new Vector2(.62f, .04f), new Vector2(.98f, .79f));
            tabRoot = CharacterUIWidgets.Rect("Tabs", transform, new Vector2(.62f, .81f), new Vector2(.98f, .88f));
            var tabs = tabRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabs.spacing = 10;
            tabs.childForceExpandWidth = true;
            tabs.childControlWidth = tabs.childControlHeight = tabs.childForceExpandHeight = true;
            var dragArea = CharacterUIWidgets.Image("ModelDrag", transform, new Vector2(.24f, .10f), new Vector2(.60f, .86f), Color.clear);
            drag = dragArea.gameObject.AddComponent<CharacterModelDrag>();
            var hint = CharacterUIWidgets.Rect("Hint", transform, new Vector2(.25f, .03f), new Vector2(.59f, .09f));
            CharacterUIWidgets.Label(hint, "拖动展示区域旋转角色", 28).alignment = TextAlignmentOptions.Center;
        }

        protected override void OnInitialize()
        {
            if (rosterRoot == null)
                BuildLayout();
            back.onClick.AddListener(() => CharacterSceneNavigator.Instance?.Return());
        }

        protected override void OnOpen(IUIContext context)
        {
            service = PlayerRuntime.Instance.Characters;
            var data = context as CharacterUIContext;
            controller = data?.Controller;
            tab = data?.Tab ?? CharacterTab.Attributes;
            unitId = service.Owns(data?.UnitId) ? data.UnitId : service.OwnedCharacters.FirstOrDefault()?.id;
            focusedEquipment = data?.EquipmentId;
            drag.Controller = controller;
            subscription?.Dispose();
            subscription = Game.GameEvents.Character.Subscribe<CharacterChangedEvent>(_ => Refresh());
            Refresh();
        }

        protected override void OnClosed()
        {
            subscription?.Dispose();
            subscription = null;
        }

        protected override void OnContextChanged(IUIContext context)
        {
            OnClosed();
            OnOpen(context);
        }

        public override bool HandleBack()
        {
            CharacterSceneNavigator.Instance?.Return();
            return true;
        }

        public void Refresh()
        {
            CharacterUIWidgets.Clear(rosterRoot);
            CharacterUIWidgets.Clear(detailRoot);
            CharacterUIWidgets.Clear(tabRoot);
            foreach (var config in service.OwnedCharacters)
            {
                string id = config.id;
                var p = service.GetCharacter(id);
                var button = CharacterUIWidgets.Button(rosterRoot, config.displayName + "  Lv." + p.level, () => Select(id));
                if (id == unitId)
                    button.image.color = new Color(.42f, .35f, .18f);
            }

            if (unitId == null)
            {
                CharacterUIWidgets.Label(detailRoot, "尚未拥有角色");
                return;
            }

            var unit = service.Config(unitId);
            var progress = service.GetCharacter(unitId);
            title.text = unit.displayName + "    " + unit.path + "    Lv." + progress.level + "    " + progress.rank + "阶";
            controller?.Show(unit);
            string[] names =
            {
                "属性",
                "光锥",
                "遗器",
                "技能",
                "升阶"
            };
            for (int i = 0; i < names.Length; i++)
            {
                var target = (CharacterTab)i;
                var b = CharacterUIWidgets.Button(tabRoot, names[i], () =>
                {
                    tab = target;
                    Refresh();
                });
                if (tab == target)
                    b.image.color = new Color(.42f, .35f, .18f);
            }

            switch (tab)
            {
                case CharacterTab.Attributes:
                    Text(service.Stats(unitId).Explain());
                    Text("经验 " + progress.experience + " / " + unit.growth.NextExperience(progress.level) + "    等级上限 " + unit.growth.levelCaps[progress.ascension]);
                    Action("升级", () => Materials(new CharacterRequest { operation = CharacterOperation.Level, unitId = unitId }));
                    Action("突破", () => Confirm(new CharacterRequest { operation = CharacterOperation.Ascend, unitId = unitId }));
                    Text(CharacterStatCalculator.DescribeSources(unit, progress, service.Inventory));
                    break;
                case CharacterTab.LightCone:
                    EquipmentSlotUI(progress, EquipmentSlot.LightCone);
                    break;
                case CharacterTab.Relics:
                    for (int i = 1; i < 7; i++)
                        EquipmentSlotUI(progress, (EquipmentSlot)i);
                    break;
                case CharacterTab.Skills:
                    foreach (SkillKind kind in Enum.GetValues(typeof(SkillKind)))
                    {
                        var skill = unit.skills.Find(s => s != null && s.kind == kind);
                        Text((skill != null ? skill.Name : kind.ToString()) + "    Lv." + progress.SkillLevel(kind) + " / " + unit.growth.skillCaps[progress.ascension]);
                        if (skill != null)
                            Text(CharacterSkillCalculator.Describe(unit.growth, progress, skill));
                        var selected = kind;
                        Action("升级技能", () => Confirm(new CharacterRequest { operation = CharacterOperation.Skill, unitId = unitId, skill = selected }));
                    }

                    Text((progress.level >= unit.growth.talentUnlockLevel ? "天赋已解锁：" : "天赋将在60级解锁：") + TalentDescription(unit.growth.talent));
                    break;
                case CharacterTab.Rank:
                    Text("当前 " + progress.rank + " / 5 阶\n碎片：" + service.Inventory.GetQuantity(unit.growth.shardItem));
                    Text("一阶：主要属性 +" + unit.growth.rankPrimaryFirst.ToString("P0") + "\n二阶：战技强化\n三阶：生命 +" + unit.growth.rankHealth.ToString("P0") + "\n四阶：终结技强化\n五阶：主要属性与普攻强化");
                    Action("预览升阶", () => Confirm(new CharacterRequest { operation = CharacterOperation.Rank, unitId = unitId }));
                    break;
            }
        }

        private void Select(string id)
        {
            Owner.Close(UIId.CharacterMaterials);
            Owner.Close(UIId.CharacterEquipment);
            Owner.Close(UIId.CharacterConfirm);
            unitId = id;
            focusedEquipment = null;
            Refresh();
        }

        private void EquipmentSlotUI(CharacterProgress p, EquipmentSlot slot)
        {
            string id = p.equipment[(int)slot];
            bool exists = service.Inventory.TryGetEquipment(id, out var item);
            string[] names =
            {
                "光锥",
                "头部",
                "手部",
                "躯干",
                "脚部",
                "位面球",
                "连结绳"
            };
            Text(names[(int)slot] + "：" + (exists ? EquipmentTitle(item) : "未穿戴"));
            Action("选择 / 更换", () => Owner.Open(UIId.CharacterEquipment, new CharacterPopupContext(this, unitId, slot), Handle));
            if (!exists && !string.IsNullOrEmpty(focusedEquipment) && service.Inventory.TryGetEquipment(focusedEquipment, out var focused))
            {
                var config = (EquipmentConfig)GetItem(focused.configId);
                if (CharacterProgressionService.Matches(service.Config(unitId), config, slot))
                    Action("穿戴背包中选中的「" + config.displayName + "」", () => Confirm(new CharacterRequest { operation = CharacterOperation.Equip, unitId = unitId, slot = slot, equipmentId = focused.instanceId, allowTransfer = true }));
            }

            if (!exists)
                return;
            Action("卸下", () => Confirm(new CharacterRequest { operation = CharacterOperation.Unequip, unitId = unitId, slot = slot }));
            Action(slot == EquipmentSlot.LightCone ? "升级光锥" : "强化遗器", () => Materials(new CharacterRequest { operation = CharacterOperation.EquipmentLevel, unitId = unitId, equipmentId = id }));
            if (slot != EquipmentSlot.LightCone)
                return;
            Action("光锥突破", () => Confirm(new CharacterRequest { operation = CharacterOperation.EquipmentAscend, unitId = unitId, equipmentId = id }));
            Action("合并同款光锥", () => Owner.Open(UIId.CharacterEquipment, new CharacterPopupContext(this, unitId, slot, id), Handle));
        }

        private ItemConfig GetItem(string id)
        {
            service.Inventory.TryGetConfig(id, out var item);
            return item;
        }

        internal string EquipmentTitle(EquipmentInstance instance)
        {
            var config = (EquipmentConfig)GetItem(instance.configId);
            var stats = config.GetStats(instance.level);
            string value = config.equipmentKind == EquipmentKind.LightCone ? "Lv." + instance.level + " 突破" + instance.ascension + " 合并" + instance.superimposition + "/5" : "+" + instance.level;
            string percent = config.basePercent + config.percentPerLevel * instance.level > 0 ? "\n" + config.percentStat + "+" + (config.basePercent + config.percentPerLevel * instance.level).ToString("P1") : "";
            string passive = config.equipmentKind == EquipmentKind.LightCone ? "\n命途被动：" + config.passiveStat + "+" + (config.passiveBase + config.passivePerRank * (instance.superimposition - 1)).ToString("P1") : "";
            return config.displayName + "  " + value + "\n生命+" + stats.health + " 攻击+" + stats.attack + " 防御+" + stats.defence + " 速度+" + stats.speed + percent + passive;
        }

        internal void Confirm(CharacterRequest request)
        {
            var preview = service.Preview(request);
            Owner.Open(UIId.CharacterConfirm, new CharacterPopupContext(this, preview), Handle);
        }

        private void Materials(CharacterRequest request) => Owner.Open(UIId.CharacterMaterials, new CharacterPopupContext(this, request), Handle);
        private void Text(string value) => CharacterUIWidgets.Label(detailRoot, value);
        private void Action(string label, UnityEngine.Events.UnityAction callback) => CharacterUIWidgets.Button(detailRoot, label, callback);
        public static string TalentDescription(CharacterTalent talent) => talent switch
        {
            CharacterTalent.Aegis => "回合开始，为生命比例最低的存活队友提供自身防御30%的护盾。",
            CharacterTalent.Verdant => "实际治疗的队友防御提高10%，持续其后两次行动，重复刷新。",
            CharacterTalent.VoltHunt => "主动技能击败敌人后恢复10能量，行动提前10%，每次行动一次。",
            CharacterTalent.FrostNova => "主动攻击命中两名以上敌人后，对命中的存活敌人追加20%攻击伤害。",
            CharacterTalent.UmbralHex => "主动技能成功施加负面效果后恢复5能量，每次行动一次。",
            CharacterTalent.AstralChord => "成功对友方使用战技或终结技，其他存活队友恢复5能量。",
            _ => "无"
        };
    }
}
