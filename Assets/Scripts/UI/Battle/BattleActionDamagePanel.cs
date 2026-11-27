using Battle.Presentation;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    public sealed class BattleActionDamageUIContext : IUIContext
    {
        public BattleActionDamageDisplay Source { get; }
        public BattleActionDamageUIContext(BattleActionDamageDisplay source) => Source = source;
    }

    public sealed class BattleActionDamagePanel : UIPanel
    {
        [SerializeField] private GameObject content;
        [SerializeField] private TMP_Text damageText;
        private BattleActionDamageDisplay source;
        public long DisplayedDamage => source?.Total ?? 0;
        public bool HasVisibleDamage => content != null && content.activeSelf;

        protected override void OnOpen(IUIContext context) => Bind(context);
        protected override void OnContextChanged(IUIContext context) => Bind(context);
        protected override void OnClosing() => Unbind();
        private void OnDisable() => Unbind();
        private void OnEnable() { if (IsOpen) Bind(Context); }

        private void Bind(IUIContext context)
        {
            Unbind();
            source = (context as BattleActionDamageUIContext)?.Source;
            if (source != null) source.Changed += Refresh;
            Refresh();
        }
        private void Refresh()
        {
            bool visible = source != null && source.Visible;
            if (damageText != null) damageText.text = visible ? source.Total.ToString("N0", CultureInfo.InvariantCulture) : "";
            if (content != null) content.SetActive(visible);
        }
        private void Unbind()
        {
            if (source != null) source.Changed -= Refresh;
            source = null;
            Refresh();
        }
    }
}
