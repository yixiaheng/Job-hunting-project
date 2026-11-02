using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Characters;
using Game.Inventory;
using Game.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>原生资源搭建和自动验收入口；只处理 Temp 下显式创建的命令文件。</summary>
[InitializeOnLoad]
public static class CharacterProgressionSetup
{
    public const string RequestPath = "Temp/CharacterProgressionChecks/request.txt";
    public const string ResultPath = "Temp/CharacterProgressionChecks/result.txt";
    private const string Root = "Assets/Config/Character";
    private const string Prefabs = "Assets/UI/Character";
    static CharacterProgressionSetup()
    {
        EditorApplication.update += Poll;
    }

    private static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(RequestPath))
            return;
        string command = File.ReadAllText(RequestPath).Trim();
        File.Delete(RequestPath);
        try
        {
            if (command == "build")
                Build();
            else if (command == "test")
                CharacterProgressionVerification.Run();
            else if (command == "play")
            {
                CharacterProgressionPlayVerification.Begin();
                return;
            }
            else
                throw new ArgumentException("Unknown character task: " + command);
            File.WriteAllText(ResultPath, "SUCCESS " + command + " " + DateTime.Now.ToString("O"));
        }
        catch (Exception exception)
        {
            File.WriteAllText(ResultPath, exception.ToString());
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Characters/Build Progression Assets")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("请先退出运行模式。");
        EnsureFolder(Root);
        EnsureFolder(Prefabs);
        EnsureFolder("Assets/Config/UI/Window");
        EnsureFolder("Assets/Config/UI/Popup");
        var runtime = Asset<PlayerRuntimeConfig>("Assets/Resources/Bootstrap/PlayerRuntimeConfig.asset");
        runtime.units = AssetDatabase.LoadAssetAtPath<UnitCatalog>("Assets/Config/Battle/Units/UnitCatalog.asset");
        runtime.items = AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Config/Inventory/ItemCatalog.asset");
        runtime.uiFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Fonts/InventoryChinese SDF.asset");
        if (runtime.units == null || runtime.items == null || runtime.uiFont == null)
            throw new InvalidOperationException("角色/物品目录或中文字体丢失。");
        runtime.startingCharacters = runtime.units.Units.Where(u => u != null).Select(u => u.id).ToList();
        ConfigureCharacters(runtime);
        ConfigureEquipment(runtime);
        MigrateSceneInventory(runtime);
        runtime.items.BuildIndex();
        EditorUtility.SetDirty(runtime.items);
        EditorUtility.SetDirty(runtime);
        BuildPanels();
        BuildScene();
        InstallEntry(UIId.ExplorationHUD);
        InstallEntry(UIId.BattleParty);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (!SceneCatalogEditorUtility.ValidateRuntimeCatalog(true))
            throw new InvalidOperationException("场景登记验证失败。");
        Debug.Log("角色养成资源、叠加场景和 UIManager 配置已生成。");
    }

    private static void ConfigureCharacters(PlayerRuntimeConfig runtime)
    {
        var buffs = new Dictionary<int, BuffConfig>();
        string[] names =
        {
            "攻击强化",
            "防御削弱",
            "速度强化",
            "迟缓",
            "壁垒",
            "虚弱"
        };
        StatType[] stats =
        {
            StatType.Attack,
            StatType.Defence,
            StatType.Speed,
            StatType.Speed,
            StatType.Defence,
            StatType.Attack
        };
        float[] values =
        {
            .2f,
            -.2f,
            .15f,
            -.15f,
            .2f,
            -.2f
        };
        for (int i = 0; i < 6; i++)
        {
            var b = Asset<BuffConfig>(Root + "/Buff_" + (201 + i) + ".asset");
            b.id = 201 + i;
            b.Name = names[i];
            b.stat = stats[i];
            b.value = values[i];
            b.maxStack = 1;
            b.durationTimes = 2;
            b.isDebuff = values[i] < 0;
            buffs.Add(b.id, b);
            EditorUtility.SetDirty(b);
        }

        Color[] colors =
        {
            new Color(.9f, .37f, .25f),
            new Color(.34f, .8f, .55f),
            new Color(.7f, .48f, 1),
            new Color(.35f, .75f, 1),
            new Color(.55f, .35f, .75f),
            new Color(1, .8f, .3f)
        };
        int index = 0;
        foreach (var unit in runtime.units.Units.Where(u => u != null))
        {
            var growth = Asset<CharacterGrowthConfig>(Root + "/Growth_" + unit.id + ".asset");
            unit.growth = growth;
            growth.shardItem = "shard_" + unit.id;
            growth.talent = (CharacterTalent)(index + 1);
            growth.effectGrowth.Clear();
            growth.primaryStat = index == 0 ? StatType.Defence : StatType.Attack;
            growth.talentValue = index == 0 ? .3f : index == 3 ? .2f : .1f;
            growth.talentSecondary = .1f;
            growth.talentEnergy = index == 2 ? 10 : 5;
            var shard = Item(runtime.items, Root + "/" + growth.shardItem + ".asset", growth.shardItem, unit.displayName + "碎片");
            shard.description = "用于" + unit.displayName + "升阶；重复获得该角色转换为10枚碎片。";
            unit.icon = Icon(unit.id, colors[index % colors.Length]);
            foreach (var skill in unit.skills.Where(s => s != null))
            {
                var keys = new HashSet<string>();
                foreach (var effect in skill.effectConfigs.Where(e => e != null))
                {
                    string key = unit.id + "." + skill.kind + "." + effect.effectType;
                    int suffix = 2;
                    while (!keys.Add(key))
                        key = unit.id + "." + skill.kind + "." + effect.effectType + "." + suffix++;
                    effect.key = key;
                    effect.growsWithSkill = effect.effectType == EffectType.Damage || effect.effectType == EffectType.Heal || effect.effectType == EffectType.AddShield || effect.effectType == EffectType.ApplyBuff;
                    effect.buffValueMultiplier = 1;
                    if (effect.growsWithSkill)
                        growth.effectGrowth.Add(new SkillEffectGrowth { effectKey = key, perSkillLevel = growth.skillGrowth, requiredRank = skill.kind == SkillKind.Basic ? 5 : skill.kind == SkillKind.Skill ? 2 : 4, rankBonus = skill.kind == SkillKind.Basic ? growth.rankBasic : skill.kind == SkillKind.Skill ? growth.rankSkill : growth.rankUltimate });
                    if (effect.effectType == EffectType.ApplyBuff && buffs.TryGetValue(effect.buffId, out var buff))
                        effect.buff = buff;
                    if (effect.effectType == EffectType.AdvanceAction && unit.path == PathType.Hunt)
                    {
                        effect.overrideTarget = true;
                        effect.targetSide = TargetSide.OurSide;
                        effect.target = TargetRule.Self;
                    }
                    else if (effect.effectType == EffectType.AddShield && skill.targetSide == TargetSide.OtherSide)
                    {
                        effect.overrideTarget = true;
                        effect.targetSide = TargetSide.OurSide;
                        effect.target = TargetRule.AoE;
                    }
                    else
                        effect.overrideTarget = false;
                }

                EditorUtility.SetDirty(skill);
            }

            EditorUtility.SetDirty(unit);
            EditorUtility.SetDirty(growth);
            EditorUtility.SetDirty(shard);
            index++;
        }

        var manual = Item(runtime.items, Root + "/skill_manual.asset", "skill_manual", "技能研习手册");
        manual.description = "三项主动技能分别升级使用的材料。";
        EditorUtility.SetDirty(manual);
    }

    private static void ConfigureEquipment(PlayerRuntimeConfig runtime)
    {
        string[] cones =
        {
            "cone_guard",
            "cone_abundance",
            "cone_hunt",
            "cone_dawn",
            "cone_memory",
            "cone_wind"
        };
        string[] titles =
        {
            "守望之约",
            "春风的回信",
            "逐雷之矢",
            "黎明之前",
            "远星回响",
            "逐风旅人"
        };
        for (int i = 0; i < cones.Length; i++)
        {
            var cone = runtime.items.items.OfType<EquipmentConfig>().FirstOrDefault(c => c.id == cones[i]);
            if (cone == null)
                cone = Asset<EquipmentConfig>(Root + "/" + cones[i] + ".asset");
            cone.id = cones[i];
            cone.displayName = titles[i];
            cone.path = (PathType)i;
            cone.slot = EquipmentSlot.LightCone;
            cone.equipmentKind = EquipmentKind.LightCone;
            cone.category = ItemCategory.Equipment;
            cone.rarity = ItemRarity.Epic;
            cone.maxLevel = 80;
            cone.levelCaps = new[]
            {
                20,
                40,
                60,
                80
            };
            cone.ascensionCosts = new[]
            {
                2,
                4,
                8
            };
            cone.maxSuperimposition = 5;
            cone.passiveStat = i == 0 ? StatType.Defence : StatType.Attack;
            cone.passiveBase = .08f;
            cone.passivePerRank = .02f;
            cone.description = "仅同命途可穿戴。被动：" + cone.passiveStat + "提高8%，每次合并额外提高2%。";
            if (cone.baseStats.health == 0)
            {
                cone.baseStats = new EquipmentStats
                {
                    health = 120,
                    attack = 50,
                    defence = 30
                };
                cone.statsPerLevel = new EquipmentStats
                {
                    health = 8,
                    attack = 3,
                    defence = 2
                };
            }

            if (!runtime.items.items.Contains(cone))
                runtime.items.items.Add(cone);
            EditorUtility.SetDirty(cone);
        }

        for (int setIndex = 0; setIndex < 4; setIndex++)
        {
            bool planar = setIndex >= 2;
            var set = Asset<RelicSetConfig>(Root + "/Set_" + setIndex + ".asset");
            set.id = "set_" + setIndex;
            set.displayName = new[]
            {
                "磐岩守卫",
                "流星旅人",
                "生命之环",
                "远星信标"
            }[setIndex];
            set.planar = planar;
            set.twoPieceStat = setIndex == 0 ? StatType.Defence : setIndex == 2 ? StatType.Health : StatType.Attack;
            set.twoPieceBonus = setIndex == 0 ? .15f : .12f;
            set.fourPieceDamageBonus = planar ? 0 : setIndex == 0 ? .1f : .15f;
            set.fourPieceOpeningShield = setIndex == 0 ? .3f : 0;
            EditorUtility.SetDirty(set);
            int first = planar ? 5 : 1, last = planar ? 6 : 4;
            for (int slot = first; slot <= last; slot++)
            {
                var relic = Asset<EquipmentConfig>(Root + "/Relic_" + setIndex + "_" + slot + ".asset");
                relic.id = "relic_" + setIndex + "_" + slot;
                relic.displayName = set.displayName + new[]
                {
                    "",
                    "之冠",
                    "之手",
                    "之衣",
                    "之履",
                    "位面球",
                    "连结绳"
                }[slot];
                relic.equipmentKind = EquipmentKind.Instrument;
                relic.slot = (EquipmentSlot)slot;
                relic.relicSet = set;
                relic.maxLevel = 15;
                relic.category = ItemCategory.Equipment;
                relic.rarity = ItemRarity.Epic;
                relic.passiveBase = 0;
                relic.passivePerRank = 0;
                relic.baseStats = slot == 1 ? new EquipmentStats
                {
                    health = 120
                }

                : slot == 2 ? new EquipmentStats
                {
                    attack = 25
                }

                : slot == 4 ? new EquipmentStats
                {
                    speed = 2
                }

                : default;
                relic.statsPerLevel = slot == 1 ? new EquipmentStats
                {
                    health = 12
                }

                : slot == 2 ? new EquipmentStats
                {
                    attack = 3
                }

                : slot == 4 ? new EquipmentStats
                {
                    speed = 1
                }

                : default;
                relic.percentStat = set.twoPieceStat;
                relic.basePercent = slot == 3 || slot >= 5 ? .08f : 0;
                relic.percentPerLevel = slot == 3 || slot >= 5 ? .012f : 0;
                relic.description = "固定主属性，强化上限+15。" + set.displayName + "：两件提高" + set.twoPieceStat + set.twoPieceBonus.ToString("P0") + (planar ? "。" : "；四件增加伤害" + set.fourPieceDamageBonus.ToString("P0") + "。") + (set.fourPieceOpeningShield > 0 ? "战斗开始获得防御30%的护盾。" : "");
                if (!runtime.items.items.Contains(relic))
                    runtime.items.items.Add(relic);
                EditorUtility.SetDirty(relic);
            }
        }
    }

    private static void MigrateSceneInventory(PlayerRuntimeConfig runtime)
    {
        var source = SceneManager.GetActiveScene();
        var sample = SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
        bool opened = !sample.IsValid();
        if (opened)
            sample = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Additive);
        try
        {
            var manager = sample.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<InventoryManager>(true)).FirstOrDefault();
            if (runtime.startingItems.Count == 0 && manager != null)
            {
                var data = new SerializedObject(manager);
                var list = data.FindProperty("startingItems");
                for (int i = 0; i < list.arraySize; i++)
                {
                    var value = list.GetArrayElementAtIndex(i);
                    runtime.startingItems.Add(new ItemAmount(value.FindPropertyRelative("configId").stringValue, value.FindPropertyRelative("quantity").intValue));
                }

                runtime.saveSlot = data.FindProperty("saveSlot").stringValue;
            }

            // 只为全新进度发放开发材料；已有存档在运行时不重复领取。
            foreach (var item in runtime.items.items.Where(c => c != null))
                if (!runtime.startingItems.Any(a => a.configId == item.id))
                    runtime.startingItems.Add(new ItemAmount(item.id, item.IsEquipment ? 1 : item.id.StartsWith("shard_") ? 30 : 40));
            if (manager != null)
            {
                Object.DestroyImmediate(manager);
                EditorSceneManager.MarkSceneDirty(sample);
                EditorSceneManager.SaveScene(sample);
            }
        }
        finally
        {
            if (opened)
                EditorSceneManager.CloseScene(sample, true);
            if (source.IsValid())
                SceneManager.SetActiveScene(source);
        }
    }

    private static void BuildPanels()
    {
        var root = new GameObject("CharacterPanel", typeof(RectTransform), typeof(CanvasGroup));
        var rect = (RectTransform)root.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var panel = root.AddComponent<CharacterPanel>();
        panel.BuildLayout();
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/CharacterPanel.prefab");
        Object.DestroyImmediate(root);
        Register(UIId.CharacterDetail, prefab.GetComponent<UIPanel>(), UILayer.Window);
        foreach (UIId id in new[]
        {
            UIId.CharacterMaterials,
            UIId.CharacterEquipment,
            UIId.CharacterConfirm
        }

        )
        {
            root = new GameObject(id.ToString(), typeof(RectTransform), typeof(CanvasGroup));
            rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var popup = root.AddComponent<CharacterPopupPanel>();
            popup.BuildLayout();
            prefab = PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/" + id + ".prefab");
            Object.DestroyImmediate(root);
            Register(id, prefab.GetComponent<UIPanel>(), UILayer.Popup);
        }
    }

    private static void Register(UIId id, UIPanel prefab, UILayer layer)
    {
        var config = Asset<UIConfig>("Assets/Config/UI/" + layer + "/" + id + "UIConfig.asset");
        config.id = id;
        config.prefab = prefab;
        config.layer = layer;
        config.cacheMode = UICacheMode.DestroyOnClose;
        config.lifetime = UILifetime.Scene;
        config.instanceMode = UIInstanceMode.Single;
        config.handlesBack = true;
        config.showCursor = true;
        config.blockMovement = config.blockCamera = true;
        config.modal = layer == UILayer.Popup;
        config.blurBackground = false;
        EditorUtility.SetDirty(config);
        var registry = AssetDatabase.LoadAssetAtPath<UIRegistry>("Assets/Config/UI/UIRegistry.asset");
        var data = new SerializedObject(registry);
        var configs = data.FindProperty("configs");
        for (int i = 0; i < configs.arraySize; i++)
            if (configs.GetArrayElementAtIndex(i).objectReferenceValue == config)
                return;
        configs.InsertArrayElementAtIndex(configs.arraySize);
        configs.GetArrayElementAtIndex(configs.arraySize - 1).objectReferenceValue = config;
        data.ApplyModifiedPropertiesWithoutUndo();
        registry.Rebuild();
    }

    private static void InstallEntry(UIId id)
    {
        var registry = AssetDatabase.LoadAssetAtPath<UIRegistry>("Assets/Config/UI/UIRegistry.asset");
        registry.Rebuild();
        if (!registry.TryGet(id, out var config) || config.prefab == null)
            return;
        string path = AssetDatabase.GetAssetPath(config.prefab);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.transform.Find("CharacterEntry") != null)
                return;
            var holder = CharacterUIWidgets.Rect("CharacterEntry", root.transform, new Vector2(.79f, .02f), new Vector2(.96f, .08f));
            var button = CharacterUIWidgets.Button(holder, "角色培养");
            var entry = button.gameObject.AddComponent<CharacterEntryButton>();
            UnityEventTools.AddPersistentListener(button.onClick, entry.Open);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void BuildScene()
    {
        const string path = "Assets/Scenes/CharacterScene.unity";
        var original = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var root = new GameObject("CharacterPresentation");
            root.transform.position = new Vector3(10000, 0, 0);
            var backgroundObject = new GameObject("CharacterBackground", typeof(Camera));
            backgroundObject.transform.SetParent(root.transform, false);
            var backgroundCamera = backgroundObject.GetComponent<Camera>();
            backgroundCamera.cullingMask = 0;
            backgroundCamera.depth = -100;
            backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
            backgroundCamera.backgroundColor = new Color(.035f, .05f, .085f);
            var controller = root.AddComponent<CharacterSceneController>();
            var anchor = new GameObject("ModelAnchor").transform;
            anchor.SetParent(root.transform, false);
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("modelAnchor").objectReferenceValue = anchor;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var cameraObject = new GameObject("CharacterCamera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.cullingMask = 1 << 8;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.035f, .05f, .085f);
            camera.depth = -99;
            camera.transform.localPosition = new Vector3(0, 1.7f, -7f);
            camera.transform.LookAt(anchor.position + Vector3.up * 1.7f);
            camera.rect = new Rect(.23f, 0, .39f, 1);
            camera.fieldOfView = 38;
            var lightObject = new GameObject("CharacterKeyLight", typeof(Light));
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(40, -35, 0);
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2;
            light.cullingMask = 1 << 8;
            EditorSceneManager.SaveScene(scene, path);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (original.IsValid())
                SceneManager.SetActiveScene(original);
        }

        var layers = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        layers.FindProperty("layers").GetArrayElementAtIndex(8).stringValue = "CharacterPreview";
        layers.ApplyModifiedPropertiesWithoutUndo();
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == path))
            scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        var catalog = AssetDatabase.LoadAssetAtPath<SceneCatalog>(SceneCatalogEditorUtility.AssetPath);
        var data = new SerializedObject(catalog);
        var entries = data.FindProperty("entries");
        int index = -1;
        for (int i = 0; i < entries.arraySize; i++)
            if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("id").intValue == (int)SceneId.Character)
                index = i;
        if (index < 0)
        {
            index = entries.arraySize;
            entries.InsertArrayElementAtIndex(index);
        }

        var item = entries.GetArrayElementAtIndex(index);
        item.FindPropertyRelative("id").intValue = (int)SceneId.Character;
        item.FindPropertyRelative("scene").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        item.FindPropertyRelative("scenePath").stringValue = path;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static ItemConfig Item(ItemCatalog catalog, string path, string id, string title)
    {
        var item = Asset<ItemConfig>(path);
        item.id = id;
        item.displayName = title;
        item.category = ItemCategory.Material;
        item.rarity = ItemRarity.Rare;
        if (!catalog.items.Contains(item))
            catalog.items.Add(item);
        return item;
    }

    private static Sprite Icon(string id, Color color)
    {
        string path = Prefabs + "/" + id + ".png";
        if (!File.Exists(path))
        {
            var texture = new Texture2D(96, 96);
            var pixels = new Color[96 * 96];
            for (int y = 0; y < 96; y++)
                for (int x = 0; x < 96; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(48, 48));
                    pixels[y * 96 + x] = d < 40 ? color : new Color(.08f, .1f, .15f);
                }

            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
        }

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static T Asset<T>(string path)
        where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;
        EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
