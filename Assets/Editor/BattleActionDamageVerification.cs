using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Battle.Logic;
using Battle.Presentation;
using Game.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class BattleActionDamageVerification
{
    [MenuItem("Tools/Battle/Verify Action Damage HUD")]
    public static void Run()
    {
        var checks = new List<string>(); string error = null; GameObject instance = null;
        void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); checks.Add(name); }
        try
        {
            var config = AssetDatabase.LoadAssetAtPath<UIConfig>(BattleActionDamageSetup.ConfigPath);
            Check(config != null && config.id == UIId.BattleActionDamage && config.layer == UILayer.HUD && config.space == UISpace.Screen && config.lifetime == UILifetime.Scene, "HUD config and scene lifetime are correct");
            var registry = AssetDatabase.LoadAssetAtPath<UIRegistry>("Assets/Config/UI/UIRegistry.asset"); registry.Rebuild();
            Check(registry.TryGet(UIId.BattleActionDamage, out var registered) && registered == config, "UIManager registry resolves new damage HUD");
            instance = UnityEngine.Object.Instantiate(config.prefab.gameObject);
            var panel = instance.GetComponent<BattleActionDamagePanel>();
            var source = new BattleActionDamageDisplay();
            void Invoke(string name, params object[] args) => typeof(UIPanel).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(panel, args);
            void Routine(string name, params object[] args)
            {
                var routine = (IEnumerator)typeof(UIPanel).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(panel, args);
                while (routine.MoveNext()) { }
            }
            Invoke("Initialize", null, default(UIHandle), config);
            Routine("OpenRoutine", new BattleActionDamageUIContext(source));
            Check(panel.State == UIState.Open && !panel.HasVisibleDamage, "real UIPanel open lifecycle starts hidden");
            source.Begin(new BattleActionRecord());
            Check(!panel.HasVisibleDamage, "action begins without premature number");
            source.Present(0, new BattleEffectRecord(BattleEffectRecordType.Heal, null, null, 100));
            source.Present(1, new BattleEffectRecord(BattleEffectRecordType.Shield, null, null, 200));
            source.Present(2, new BattleEffectRecord(BattleEffectRecordType.Damage, null, null, 0));
            Check(!panel.HasVisibleDamage, "healing shield gain and zero damage remain hidden");
            source.Present(3, new BattleEffectRecord(BattleEffectRecordType.Damage, null, null, 100));
            Check(panel.HasVisibleDamage && panel.DisplayedDamage == 100, "first hit reveals action total");
            source.Present(4, new BattleEffectRecord(BattleEffectRecordType.Damage, null, null, 50, shieldBefore: 60, shieldAfter: 10));
            Check(panel.DisplayedDamage == 200, "next hit adds health damage and shield absorption");
            source.Present(4, new BattleEffectRecord(BattleEffectRecordType.Damage, null, null, 50));
            Check(panel.DisplayedDamage == 200, "duplicate effect index cannot double count");
            source.Present(5, new BattleEffectRecord(BattleEffectRecordType.FollowUp, null, null, 25));
            Check(panel.DisplayedDamage == 225, "legacy follow-up record contributes damage");
            Check(instance.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget), "damage decoration never blocks pointer input");
            Check(instance.transform.Find("DamageContent").GetComponent<RectTransform>().anchorMax == Vector2.one, "damage content anchored top right");
            source.End();
            Check(!panel.HasVisibleDamage && panel.DisplayedDamage == 0, "action end immediately hides and resets total");
            source.Present(6, new BattleEffectRecord(BattleEffectRecordType.Damage, null, null, 100));
            Check(!source.Visible, "late hit outside an action is ignored");
            source.Begin(new BattleActionRecord());
            source.Present(0, new BattleEffectRecord(BattleEffectRecordType.Damage, null, null, 0, shieldBefore: 80, shieldAfter: 0));
            Check(panel.DisplayedDamage == 80 && panel.HasVisibleDamage, "shield-only hit displays damage and new action is independent");
            source.Present(1, new BattleEffectRecord(BattleEffectRecordType.Damage, null, null, int.MaxValue));
            source.Present(2, new BattleEffectRecord(BattleEffectRecordType.Damage, null, null, int.MaxValue));
            Check(panel.DisplayedDamage == 80L + 2L * int.MaxValue, "large multi-target totals do not overflow int");
            instance.SetActive(false);
            // Non-ExecuteAlways behaviours do not receive player lifecycle messages in Edit Mode.
            typeof(BattleActionDamagePanel).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(panel, null);
            source.Present(3, new BattleEffectRecord(BattleEffectRecordType.Damage, null, null, 20));
            Check(!panel.HasVisibleDamage, "disabled panel unsubscribes and hides");
            instance.SetActive(true);
            typeof(BattleActionDamagePanel).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(panel, null);
            Check(panel.HasVisibleDamage && panel.DisplayedDamage == source.Total, "re-enabled panel restores latest source state");
            var replacement = new BattleActionDamageDisplay();
            Invoke("UpdateContext", new BattleActionDamageUIContext(replacement));
            source.End();
            replacement.Begin(new BattleActionRecord()); replacement.Present(0, new BattleEffectRecord(BattleEffectRecordType.Damage, null, null, 33));
            source.Begin(new BattleActionRecord()); source.Present(0, new BattleEffectRecord(BattleEffectRecordType.Damage, null, null, 999));
            Check(panel.DisplayedDamage == 33, "context rebinding detaches previous battle source");
            Routine("CloseRoutine");
            replacement.Present(1, new BattleEffectRecord(BattleEffectRecordType.Damage, null, null, 100));
            Check(panel.State == UIState.Closed && !panel.HasVisibleDamage && panel.DisplayedDamage == 0, "UI close lifecycle unsubscribes without stale number");
        }
        catch (Exception e) { error = e.ToString(); throw; }
        finally
        {
            if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            Directory.CreateDirectory("Temp/BattleActionDamage");
            File.WriteAllText("Temp/BattleActionDamage/results.json", Newtonsoft.Json.JsonConvert.SerializeObject(new { passed = error == null, error, checks, count = checks.Count, completedAt = DateTimeOffset.Now }, Newtonsoft.Json.Formatting.Indented));
        }
    }
}
