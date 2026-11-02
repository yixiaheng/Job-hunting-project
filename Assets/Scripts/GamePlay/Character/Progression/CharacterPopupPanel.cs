using System;
using System.Linq;
using System.Text;
using Game.Inventory;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Characters
{
    public sealed class CharacterPopupContext : IUIContext
    {
        public CharacterPanel Parent { get; }
        public string UnitId { get; }
        public EquipmentSlot Slot { get; }
        public string MergeTarget { get; }
        public CharacterRequest Request { get; }
        public CharacterPreview Preview { get; }

        public CharacterPopupContext(CharacterPanel parent, string id, EquipmentSlot slot, string mergeTarget = null)
        {
            Parent = parent;
            UnitId = id;
            Slot = slot;
            MergeTarget = mergeTarget;
        }

        public CharacterPopupContext(CharacterPanel parent, CharacterRequest request)
        {
            Parent = parent;
            Request = request.Copy();
            UnitId = request.unitId;
        }

        public CharacterPopupContext(CharacterPanel parent, CharacterPreview preview)
        {
            Parent = parent;
            Preview = preview;
        }
    }

    public sealed class CharacterPopupPanel : UIPanel
    {
        [SerializeField]
        private RectTransform content;
        [SerializeField]
        private Button cancel;
        private CharacterPopupContext data;
        private CharacterProgressionService service;
        public void BuildLayout()
        {
            CharacterUIWidgets.Clear(transform);
            CharacterUIWidgets.Image("Mask", transform, Vector2.zero, Vector2.one, new Color(0, 0, 0, .72f));
            content = CharacterUIWidgets.Scroll(transform, new Vector2(.28f, .19f), new Vector2(.85f, .86f));
            var bottom = CharacterUIWidgets.Rect("Cancel", transform, new Vector2(.28f, .10f), new Vector2(.85f, .17f));
            cancel = CharacterUIWidgets.Button(bottom, "取消 / 返回");
        }

        protected override void OnInitialize()
        {
            if (content == null)
                BuildLayout();
            cancel.onClick.AddListener(() => Owner.Close(Handle));
        }

        protected override void OnOpen(IUIContext context)
        {
            data = context as CharacterPopupContext;
            service = PlayerRuntime.Instance.Characters;
            Render();
        }

        protected override void OnContextChanged(IUIContext context) => OnOpen(context);
        private void Render()
        {
            CharacterUIWidgets.Clear(content);
            if (data == null)
                return;
            if (data.Preview != null)
                RenderConfirmation();
            else if (data.Request != null)
                RenderMaterials();
            else
                RenderEquipment();
        }

        private void RenderConfirmation()
        {
            var preview = data.Preview;
            if (!preview.Success)
            {
                Label(preview.Message);
                return;
            }

            var text = new StringBuilder(preview.Description);
            text.Append("\n\n培养前\n").Append(preview.Before.Explain()).Append("\n\n培养后\n").Append(preview.After.Explain());
            if (preview.Costs.Count > 0)
                text.Append("\n\n消耗：");
            foreach (var cost in preview.Costs)
            {
                service.Inventory.TryGetConfig(cost.configId, out var item);
                text.Append("\n").Append(item != null ? item.displayName : cost.configId).Append(" × ").Append(cost.quantity);
            }

            if (preview.WastedExperience > 0)
                text.Append("\n达到突破上限，最后一份材料溢出经验损失：").Append(preview.WastedExperience);
            Label(text.ToString());
            var status = Label("确认前不会扣除材料。装备转移会同时更新双方角色。");
            CharacterUIWidgets.Button(content, "确认", () =>
            {
                if (!service.TryCommit(preview, out string reason))
                {
                    status.text = reason;
                    return;
                }

                data.Parent?.Refresh();
                Owner.Close(Handle);
            });
        }

        private void RenderMaterials()
        {
            Label("选择经验材料与数量，下一步预览实际消耗");
            var kind = data.Request.operation == CharacterOperation.Level ? ItemUseKind.CharacterExperience : ItemUseKind.EquipmentExperience;
            bool found = false;
            foreach (var stack in service.Inventory.CreateSnapshot().stacks)
            {
                if (!service.Inventory.TryGetConfig(stack.configId, out var item) || item.useKind != kind)
                    continue;
                found = true;
                Label(item.displayName + "    拥有 " + stack.quantity + "    单份经验 " + item.effectValue);
                foreach (int count in new[]
                {
                    1,
                    5,
                    10,
                    stack.quantity
                }.Distinct().Where(n => n > 0 && n <= stack.quantity))
                {
                    var request = data.Request.Copy();
                    request.materialId = item.id;
                    request.quantity = count;
                    CharacterUIWidgets.Button(content, "使用 " + count + " 份 → 预览", () =>
                    {
                        Owner.Close(Handle);
                        data.Parent.Confirm(request);
                    });
                }
            }

            if (!found)
                Label("没有可用的经验材料。");
        }

        private void RenderEquipment()
        {
            bool merging = !string.IsNullOrEmpty(data.MergeTarget);
            Label(merging ? "选择要消耗的同款光锥（其等级、突破和经验不会转移）" : "选择装备 → 预览属性与转移影响");
            service.Inventory.TryGetEquipment(data.MergeTarget, out var target);
            bool found = false;
            foreach (var instance in service.Inventory.CreateSnapshot().equipment)
            {
                service.Inventory.TryGetConfig(instance.configId, out var item);
                var config = item as EquipmentConfig;
                if (config == null)
                    continue;
                if (merging)
                {
                    if (target == null || instance.instanceId == target.instanceId || instance.configId != target.configId || instance.isLocked || service.IsEquipped(instance.instanceId))
                        continue;
                }
                else if (!CharacterProgressionService.Matches(service.Config(data.UnitId), config, data.Slot))
                    continue;
                found = true;
                string owner = service.GetOwnerName(instance.instanceId);
                string label = data.Parent.EquipmentTitle(instance) + "\n拥有者：" + (owner ?? "未穿戴") + (instance.isLocked ? "    已锁定" : "") + "\n实例 " + instance.instanceId;
                var request = new CharacterRequest
                {
                    unitId = data.UnitId,
                    slot = data.Slot,
                    equipmentId = merging ? data.MergeTarget : instance.instanceId,
                    operation = merging ? CharacterOperation.Merge : CharacterOperation.Equip,
                    materialEquipmentId = merging ? instance.instanceId : null,
                    allowTransfer = true
                };
                var button = CharacterUIWidgets.Button(content, label, () =>
                {
                    Owner.Close(Handle);
                    data.Parent.Confirm(request);
                });
                button.GetComponent<LayoutElement>().preferredHeight = 210;
            }

            if (!found)
                Label("没有符合条件的装备。");
        }

        private TMP_Text Label(string text) => CharacterUIWidgets.Label(content, text);
    }
}
