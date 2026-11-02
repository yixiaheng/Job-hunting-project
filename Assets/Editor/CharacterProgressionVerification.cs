using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Battle.Logic;
using Game.Characters;
using Game.Inventory;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CharacterProgressionVerification
{
    private sealed class MemoryStore : IPlayerProfileStore
    {
        public bool Fail;
        public PlayerProfileData Saved;
        public bool TrySave(PlayerProfileData profile, out string reason)
        {
            reason = Fail ? "simulated disk failure" : null;
            if (Fail)
                return false;
            Saved = JsonUtility.FromJson<PlayerProfileData>(JsonUtility.ToJson(profile));
            return true;
        }
    }

    private static int count;
    private static readonly List<string> results = new();
    private static void Check(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException("FAILED: " + name);
        count++;
        results.Add("PASS " + name);
    }

    public static void Run()
    {
        count = 0;
        results.Clear();
        var runtime = AssetDatabase.LoadAssetAtPath<PlayerRuntimeConfig>("Assets/Resources/Bootstrap/PlayerRuntimeConfig.asset");
        Check(runtime != null && runtime.units.Units.Count == 6, "six owned character configurations");
        var inventory = new InventoryService(runtime.items, false);
        foreach (var item in runtime.items.items.Where(i => i != null))
            Check(inventory.TryAdd(item.id, item.IsEquipment ? 4 : 100000, out _), "seed " + item.id);
        var roster = new CharacterRosterData();
        foreach (var unit in runtime.units.Units)
            roster.characters.Add(new CharacterProgress { unitId = unit.id });
        var store = new MemoryStore();
        var service = new CharacterProgressionService(runtime.units, runtime.items, inventory, roster, store);
        var first = runtime.units.Units[0];
        string id = first.id;
        CharacterRequest Request(CharacterOperation op) => new CharacterRequest
        {
            operation = op,
            unitId = id
        };
        void Commit(CharacterRequest request, string name)
        {
            var p = service.Preview(request);
            Check(p.Success, name + " preview: " + p.Message);
            Check(service.TryCommit(p, out string reason), name + " commit: " + reason);
        }

        bool notificationRejected = false;
        Action attemptDuringNotification = () =>
        {
            var nested = service.Preview(Request(CharacterOperation.Acquire));
            notificationRejected = !nested.Success && !service.TryCommit(nested, out _) && store.Saved == null;
        };
        inventory.Changed += attemptDuringNotification;
        inventory.TryAdd(first.growth.shardItem, 1, out _);
        inventory.Changed -= attemptDuringNotification;
        Check(notificationRejected, "inventory notification rejects cultivation before saving");
        var readOnlyProperty = typeof(InventoryService).GetProperty(nameof(InventoryService.IsReadOnly));
        readOnlyProperty.SetValue(inventory, true);
        Check(!service.Preview(Request(CharacterOperation.Acquire)).Success && store.Saved == null, "read-only inventory prevents profile writes");
        readOnlyProperty.SetValue(inventory, false);
        var level = Request(CharacterOperation.Level);
        level.quantity = int.MaxValue;
        string before = JsonUtility.ToJson(inventory.CreateSnapshot());
        var preview = service.Preview(level);
        Check(preview.Success && service.GetCharacter(id).level == 1 && before == JsonUtility.ToJson(inventory.CreateSnapshot()), "preview never mutates roster or inventory");
        store.Fail = true;
        Check(!service.TryCommit(preview, out _) && service.GetCharacter(id).level == 1 && before == JsonUtility.ToJson(inventory.CreateSnapshot()), "failed save consumes nothing");
        store.Fail = false;
        Check(service.TryCommit(preview, out _), "atomic successful commit");
        Check(service.GetCharacter(id).level == 20 && service.GetCharacter(id).experience == 0, "level 20 boundary");
        Check(preview.WastedExperience >= 0 && preview.Costs[0].quantity < 100000, "automatically reduce excess books");
        Check(!service.TryCommit(preview, out _), "stale preview rejected");
        for (int stage = 0; stage < 5; stage++)
        {
            int cap = first.growth.skillCaps[stage];
            foreach (SkillKind kind in Enum.GetValues(typeof(SkillKind)))
            {
                while (service.GetCharacter(id).SkillLevel(kind) < cap)
                {
                    var request = Request(CharacterOperation.Skill);
                    request.skill = kind;
                    Commit(request, "skill " + kind + " stage " + stage);
                }

                var requestCap = Request(CharacterOperation.Skill);
                requestCap.skill = kind;
                Check(!service.Preview(requestCap).Success, "skill stage cap " + stage + " " + kind);
            }

            if (stage == 4)
                break;
            Commit(Request(CharacterOperation.Ascend), "character ascension " + stage);
            Commit(level, "level next cap " + stage);
            Check(service.GetCharacter(id).level == first.growth.levelCaps[stage + 1], "level cap " + first.growth.levelCaps[stage + 1]);
        }

        Check(!service.Preview(level).Success && service.GetCharacter(id).level == 100, "max 100 blocks leveling");
        for (int i = 0; i < 5; i++)
            Commit(Request(CharacterOperation.Rank), "rank " + (i + 1));
        Check(!service.Preview(Request(CharacterOperation.Rank)).Success, "rank cap 5");
        var fresh = service.GetCharacter(id);
        fresh.level = 59;
        fresh.ascension = 2;
        var locked = new BattleCharacterSnapshot(first, fresh, inventory).CreateUnit(0, 0);
        fresh.level = 60;
        var unlocked = new BattleCharacterSnapshot(first, fresh, inventory).CreateUnit(0, 0);
        Check(locked.talent == CharacterTalent.None && unlocked.talent == first.growth.talent, "talent unlock at 60 exactly");
        Release(locked);
        Release(unlocked);
        var cone = inventory.CreateSnapshot().equipment.First(e => ((EquipmentConfig)runtime.items.items.First(i => i.id == e.configId)).equipmentKind == EquipmentKind.LightCone && ((EquipmentConfig)runtime.items.items.First(i => i.id == e.configId)).path == first.path);
        var equip = Request(CharacterOperation.Equip);
        equip.equipmentId = cone.instanceId;
        equip.slot = EquipmentSlot.LightCone;
        Commit(equip, "equip matching cone");
        var wrong = inventory.CreateSnapshot().equipment.First(e => ((EquipmentConfig)runtime.items.items.First(i => i.id == e.configId)).equipmentKind == EquipmentKind.LightCone && ((EquipmentConfig)runtime.items.items.First(i => i.id == e.configId)).path != first.path);
        var bad = equip.Copy();
        bad.equipmentId = wrong.instanceId;
        Check(!service.Preview(bad).Success, "path restriction");
        var coneLevel = Request(CharacterOperation.EquipmentLevel);
        coneLevel.equipmentId = cone.instanceId;
        coneLevel.quantity = int.MaxValue;
        for (int stage = 0; stage < 4; stage++)
        {
            Commit(coneLevel, "cone level " + stage);
            if (stage < 3)
            {
                var r = Request(CharacterOperation.EquipmentAscend);
                r.equipmentId = cone.instanceId;
                Commit(r, "cone ascend " + stage);
            }
        }

        inventory.TryGetEquipment(cone.instanceId, out var target);
        Check(target.level == 80, "cone maximum 80");
        var donors = inventory.CreateSnapshot().equipment.Where(e => e.configId == cone.configId && e.instanceId != cone.instanceId).ToArray();
        foreach (var donor in donors)
        {
            var r = Request(CharacterOperation.Merge);
            r.equipmentId = cone.instanceId;
            r.materialEquipmentId = donor.instanceId;
            Commit(r, "merge donor");
        }

        inventory.TryGetEquipment(cone.instanceId, out target);
        Check(target.level == 80 && target.ascension == 3 && target.superimposition == 4, "merge preserves target progress");
        inventory.TryAdd(cone.configId, 2, out _);
        var more = inventory.CreateSnapshot().equipment.Where(e => e.configId == cone.configId && e.instanceId != cone.instanceId).ToArray();
        var merge = Request(CharacterOperation.Merge);
        merge.equipmentId = cone.instanceId;
        merge.materialEquipmentId = more[0].instanceId;
        inventory.TrySetLocked(more[0].instanceId, true, out _);
        Check(!service.Preview(merge).Success, "locked donor rejected");
        inventory.TrySetLocked(more[0].instanceId, false, out _);
        Commit(merge, "merge reaches five");
        merge.materialEquipmentId = more[1].instanceId;
        Check(!service.Preview(merge).Success, "merge overcap rejected");
        var relics = inventory.CreateSnapshot().equipment.Where(e => e.configId.StartsWith("relic_0_")).GroupBy(e => e.configId).Select(g => g.First()).ToArray();
        foreach (var relic in relics)
        {
            var config = (EquipmentConfig)runtime.items.items.First(i => i.id == relic.configId);
            var r = Request(CharacterOperation.Equip);
            r.slot = config.slot;
            r.equipmentId = relic.instanceId;
            Commit(r, "equip relic " + config.slot);
        }

        var stats = service.Stats(id);
        Check(stats.damageBonus >= .1f, "four-piece damage bonus active");
        var relicLevel = Request(CharacterOperation.EquipmentLevel);
        relicLevel.equipmentId = relics[0].instanceId;
        relicLevel.quantity = int.MaxValue;
        Commit(relicLevel, "relic enhance");
        inventory.TryGetEquipment(relics[0].instanceId, out var enhanced);
        Check(enhanced.level == 15 && !service.Preview(relicLevel).Success, "relic max +15");
        string other = runtime.units.Units[1].id;
        var transfer = new CharacterRequest
        {
            unitId = other,
            operation = CharacterOperation.Equip,
            slot = EquipmentSlot.Head,
            equipmentId = relics[0].instanceId
        };
        Check(!service.Preview(transfer).Success, "transfer requires confirmation");
        transfer.allowTransfer = true;
        Commit(transfer, "confirmed equipment transfer");
        Check(service.GetCharacter(id).equipment[1] == null && service.GetCharacter(other).equipment[1] == relics[0].instanceId, "unique owner after transfer");
        Check(!inventory.TryRemoveEquipment(relics[0].instanceId, out _), "equipped item cannot be deleted");
        var snapshot = new BattleCharacterSnapshot(first, service.GetCharacter(id), inventory);
        var captured = snapshot.CreateUnit(0, 0);
        int capturedAttack = captured.attack;
        Commit(Request(CharacterOperation.Unequip), "unequip cone");
        var retry = snapshot.CreateUnit(0, 0);
        Check(retry.attack == capturedAttack && retry.skills[0] != captured.skills[0], "snapshot immutable and runtime skills independent");
        Release(captured);
        Release(retry);
        var duplicate = Request(CharacterOperation.Acquire);
        int shards = inventory.GetQuantity(first.growth.shardItem);
        Commit(duplicate, "duplicate character");
        Check(inventory.GetQuantity(first.growth.shardItem) == shards + 10, "duplicate converts to ten shards even at max rank");
        CombatChecks(runtime);
        SaveAndMigrationChecks(runtime);
        Check(SceneCatalogEditorUtility.ValidateRuntimeCatalog(), "scene catalog valid");
        File.WriteAllLines("Temp/CharacterProgressionChecks/editmode.txt", results.Concat(new[] { "TOTAL " + count + " assertions passed" }));
        Debug.Log("Character progression verification: " + count + " assertions passed.");
    }

    private static void CombatChecks(PlayerRuntimeConfig runtime)
    {
        BattleUnit U(Faction side, int pos) => new BattleUnit
        {
            faction = side,
            pos = pos,
            maxHealth = 1000,
            currentHealth = 1000,
            attack = 100,
            defence = 20,
            speed = 100,
            tieBreaker = pos
        };
        foreach (var config in runtime.units.Units)
        {
            var caster = U(Faction.OurSide, 0);
            caster.talent = config.growth.talent;
            caster.talentValue = config.growth.talentValue;
            caster.talentEnergy = config.growth.talentEnergy;
            caster.talentSecondary = config.growth.talentSecondary;
            caster.talentDuration = 2;
            caster.speed = 1000;
            var ally = U(Faction.OurSide, 1);
            ally.currentHealth = 500;
            var e1 = U(Faction.Enemy, 0);
            var e2 = U(Faction.Enemy, 1);
            if (caster.talent == CharacterTalent.VoltHunt)
                e1.currentHealth = 1;
            var units = new[]
            {
                caster,
                ally,
                e1,
                e2
            };
            var flow = new BattleFlow(units);
            flow.Ready();
            var skill = config.skills.First(s => s.kind == SkillKind.Skill);
            Check(flow.Execute(new BattleAction { skill = skill, mainTarget = config.path == PathType.Abundance ? ally : e1 }), "talent action " + config.id);
            switch (caster.talent)
            {
                case CharacterTalent.Aegis:
                    Check(ally.shield > 0, "Aegis lowest health shield");
                    break;
                case CharacterTalent.Verdant:
                    Check(ally.GetStat(StatType.Defence) > ally.defence && ally.buffs.Last().remainingTurns == 2, "Verdant real heal defense for two actions");
                    break;
                case CharacterTalent.VoltHunt:
                    Check(caster.currentEnergy >= caster.talentEnergy && caster.nextActionTime < flowTime(caster), "Volt kill energy and self advance");
                    break;
                case CharacterTalent.FrostNova:
                    Check(e1.currentHealth < 930 && e2.currentHealth < 930, "Frost multi-target follow-up");
                    Check(flow.LastResolution.FollowUps.Count == 2 && flow.LastResolution.FollowUps.All(hit => hit.Origin == EffectOrigin.Talent && hit.EffectKey == "Talent.FrostNova.FollowUp"), "Frost follow-up retains talent source without recursion");
                    break;
                case CharacterTalent.UmbralHex:
                    Check(caster.currentEnergy >= caster.talentEnergy && e1.buffs.Count > 0, "Umbral successful debuff energy");
                    break;
                case CharacterTalent.AstralChord:
                    Check(ally.currentEnergy == 5, "Harmony other allies energy");
                    Check(caster.GetStat(StatType.Speed) > caster.speed && caster.nextActionTime < flowTime(caster), "self speed buff affects the next action interval");
                    break;
            }

            foreach (var u in units)
                foreach (var b in u.buffs)
                    if (b.ownsConfig)
                        Object.DestroyImmediate(b.config);
        }

        var guard = runtime.units.Units.First(u => u.path == PathType.Preservation);
        var a = U(Faction.OurSide, 0);
        a.speed = 1000;
        a.currentEnergy = 100;
        var partner = U(Faction.OurSide, 1);
        var enemy = U(Faction.Enemy, 0);
        var battle = new BattleFlow(new[] { a, partner, enemy });
        battle.Ready();
        Check(battle.Execute(new BattleAction { skill = guard.skills.First(s => s.kind == SkillKind.Ultimate), mainTarget = enemy }), "mixed targets execute");
        Check(partner.shield > 0 && enemy.shield == 0 && enemy.currentHealth < 1000, "damage enemies and protect allies in same skill");
        a.currentEnergy = 98;
        new EnergyService().Execute(a, new EffectConfig { value = 999 });
        Check(a.currentEnergy == 100, "energy clamp");
    }

    private static double flowTime(BattleUnit caster) => 2 * ActionQueue.BaseActionValue / caster.speed;
    private static void SaveAndMigrationChecks(PlayerRuntimeConfig runtime)
    {
        var legacy = new InventoryData();
        legacy.stacks.Add(new ItemStack { configId = "character_exp", quantity = 2 });
        legacy.equipment.Add(new EquipmentInstance { instanceId = "legacy-equipment-stable-id", configId = "cone_guard", level = 45, superimposition = 0 });
        var imported = PlayerProfileMigration.ImportLegacy(legacy, runtime);
        Check(imported.inventory.equipment[0].instanceId == "legacy-equipment-stable-id" && imported.inventory.equipment[0].ascension == 2 && imported.inventory.equipment[0].superimposition == 1, "legacy equipment IDs and missing fields migrated");
        Check(imported.roster.characters.Count == 6 && imported.inventory.stacks.Count == 1 && imported.inventory.stacks[0].quantity == 2, "legacy migration preserves quantities without starter rewards");
        PlayerProfileMigration.AddInitialCharacters(imported, runtime);
        Check(imported.roster.characters.Count == 6 && legacy.equipment[0].superimposition == 0, "legacy migration is idempotent and leaves input untouched");
        var empty = new InventoryService(runtime.items, false);
        var fail = new CharacterProgressionService(runtime.units, runtime.items, empty, imported.roster, new MemoryStore());
        Check(!fail.Preview(new CharacterRequest { unitId = imported.roster.characters[0].unitId, operation = CharacterOperation.Level }).Success, "insufficient materials has no changes");
        var storage = typeof(TapTap21.SaveSystem.dyh.SaveSystem).Assembly.GetType("TapTap21.SaveSystem.dyh.SaveStorage");
        var withRoot = storage.GetMethod("WithRoot", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).MakeGenericMethod(typeof(bool));
        string root = Path.GetFullPath("Temp/CharacterProgressionChecks/Storage_" + Guid.NewGuid().ToString("N"));
        var action = new Func<bool>(() =>
        {
            var store = new PlayerProfileStore("slot_character_test");
            Check(store.TrySave(imported, out _), "native profile serialization");
            string directory = Path.Combine(root, "Saves", "slot_character_test");
            File.WriteAllText(Path.Combine(directory, "unrelated_module.json"), "preserved");
            imported.roster.characters[0].level = 2;
            imported.inventory.stacks[0].quantity = 1;
            Check(store.TrySave(imported, out _), "native profile replacement and backup");
            Check(File.ReadAllText(Path.Combine(directory, "unrelated_module.json")) == "preserved", "unrelated module file retained");
            File.WriteAllText(Path.Combine(directory, "player_profile.json"), "corrupted-primary");
            var recovered = store.Load();
            Check(recovered.Success && recovered.UsedBackup && recovered.Data.roster.characters[0].level == 1 && recovered.Data.inventory.stacks[0].quantity == 2, "roster and inventory recovered from same backup");
            Check(File.ReadAllText(Path.Combine(directory, "player_profile.json")) == "corrupted-primary", "corrupt primary is preserved on load");
            File.WriteAllText(Path.Combine(directory, "player_profile.json.bak"), "corrupted-backup");
            Check(!store.Load().Success, "both corrupt files reject load");
            return true;
        });
        withRoot.Invoke(null, new object[] { root, action });
    }

    private static void Release(BattleUnit unit)
    {
        foreach (var skill in unit.skills)
            Object.DestroyImmediate(skill);
    }
}
