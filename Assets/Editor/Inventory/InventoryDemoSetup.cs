using System;
using System.Collections.Generic;
using Game.Inventory;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class InventoryDemoSetup
{
    private const string ConfigPath = "Assets/Config/Inventory";
    private const string PrefabPath = "Assets/UI/Prefab/Inventory/InventoryItem.prefab";
    private static readonly Color PanelColor = new Color(0.055f, 0.075f, 0.115f, 0.94f);
    private static readonly Color CardColor = new Color(0.13f, 0.17f, 0.23f);
    private static readonly Color Gold = new Color(0.92f, 0.79f, 0.55f);
    private static TMP_FontAsset font;

    [MenuItem("Tools/Inventory/Setup Demo")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请在非运行状态搭建背包。");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/SampleScene.unity")
            throw new InvalidOperationException("请先打开 SampleScene，再搭建背包。");
        font = CreateFont();
        EnsureFolder(ConfigPath + "/Materials");
        EnsureFolder(ConfigPath + "/Foods");
        EnsureFolder(ConfigPath + "/LightCones");
        EnsureFolder("Assets/UI/Sprite/Inventory");
        EnsureFolder("Assets/UI/Prefab/Inventory");
        ItemCatalog catalog = CreateCatalog();
        InventoryItemView itemPrefab = CreateItemPrefab();
        InventoryManager manager = Object.FindFirstObjectByType<InventoryManager>(FindObjectsInactive.Include);
        if (manager == null) manager = new GameObject("InventoryManager").AddComponent<InventoryManager>();
        var managerData = new SerializedObject(manager);
        if (managerData.FindProperty("catalog").objectReferenceValue == null)
        {
            managerData.FindProperty("catalog").objectReferenceValue = catalog;
            var starting = managerData.FindProperty("startingItems");
            starting.arraySize = catalog.items.Count;
            for (int index = 0; index < catalog.items.Count; index++)
            {
                var element = starting.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("configId").stringValue = catalog.items[index].id;
                element.FindPropertyRelative("quantity").intValue = catalog.items[index].IsEquipment ? 1
                    : catalog.items[index].category == ItemCategory.Material ? 20 : 5;
            }
            managerData.ApplyModifiedPropertiesWithoutUndo();
        }
        BuildCanvas(manager, itemPrefab);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("背包搭建完成：12 份物品配置，运行后按 B 打开，Esc 关闭。");
    }

    [MenuItem("Tools/Inventory/Refresh Chinese Font")]
    public static void RefreshFont()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请在非运行状态配置字体。");
        font = CreateFont();
        var controller = Object.FindFirstObjectByType<InventoryUIController>(FindObjectsInactive.Include);
        if (controller != null)
        {
            foreach (TMP_Text label in controller.GetComponentsInChildren<TMP_Text>(true)) label.font = font;
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            EditorSceneManager.SaveScene(controller.gameObject.scene);
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            GameObject prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                foreach (TMP_Text label in prefab.GetComponentsInChildren<TMP_Text>(true)) label.font = font;
                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }
        AssetDatabase.SaveAssets();
    }

    private static TMP_FontAsset CreateFont()
    {
        EnsureFolder("Assets/UI/Fonts");
        const string path = "Assets/UI/Fonts/InventoryChinese SDF.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existing != null) return existing;
        var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/smiley-sans-v2.0.1/SmileySans-Oblique.otf");
        if (source == null) throw new InvalidOperationException("背包需要项目中的中文字体源文件。");
        var created = TMP_FontAsset.CreateFontAsset(source, 64, 6, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
            2048, 2048, AtlasPopulationMode.Dynamic, true);
        created.name = "InventoryChinese SDF";
        AssetDatabase.CreateAsset(created, path);
        AssetDatabase.AddObjectToAsset(created.material, created);
        foreach (Texture2D atlas in created.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, created);
        var characters = new System.Text.StringBuilder();
        foreach (string file in System.IO.Directory.GetFiles("Assets/Scripts/GamePlay/Inventory", "*.cs", System.IO.SearchOption.AllDirectories))
            characters.Append(System.IO.File.ReadAllText(file));
        characters.Append(System.IO.File.ReadAllText("Assets/Scripts/UI/Inventory/Inventory UI Controller.cs"));
        characters.Append(System.IO.File.ReadAllText("Assets/Editor/Inventory/InventoryDemoSetup.cs"));
        created.TryAddCharacters(characters.ToString());
        EditorUtility.SetDirty(created);
        AssetDatabase.SaveAssets();
        return created;
    }

    private static ItemCatalog CreateCatalog()
    {
        var items = new List<ItemConfig>
        {
            CreateItem("Materials", "character_exp", "旅途经验书", ItemCategory.Material, ItemRarity.Rare, ItemUseKind.CharacterExperience,
                "记录旅途见闻的经验书。\n用于提升角色等级。", 1000f, 0f, true, 0),
            CreateItem("Materials", "equipment_exp", "光锥强化片", ItemCategory.Material, ItemRarity.Rare, ItemUseKind.EquipmentExperience,
                "封存记忆的强化材料。\n用于提升光锥等级。", 500f, 0f, true, 1),
            CreateItem("Materials", "character_ascension", "星核碎晶", ItemCategory.Material, ItemRarity.Epic, ItemUseKind.CharacterAscension,
                "凝结星辰能量的晶体。\n用于角色突破，具体消耗由养成系统决定。", 0f, 0f, true, 2),
            CreateItem("Materials", "equipment_ascension", "记忆棱镜", ItemCategory.Material, ItemRarity.Epic, ItemUseKind.EquipmentAscension,
                "保存完整记忆的棱镜。\n用于光锥突破。", 0f, 0f, true, 3),
            CreateItem("Foods", "food_revive", "应急复苏剂", ItemCategory.Food, ItemRarity.Rare, ItemUseKind.Revive,
                "复苏一名倒下的角色，并恢复其最大生命值的 20%。\n需接入角色状态系统后使用。", 0f, 0.2f, true, 4),
            CreateItem("Foods", "food_technique", "秘技补给罐", ItemCategory.Food, ItemRarity.Common, ItemUseKind.TechniquePoints,
                "恢复队伍 2 点共享秘技点。\n需接入秘技点系统后使用。", 2f, 0f, false, 5),
            CreateItem("Foods", "food_heal", "便携营养餐", ItemCategory.Food, ItemRarity.Common, ItemUseKind.RestoreHealth,
                "为一名存活角色恢复最大生命值的 30%。\n需接入角色状态系统后使用。", 0f, 0.3f, true, 6),
            CreateItem("Foods", "food_attack", "战前能量饮", ItemCategory.Food, ItemRarity.Rare, ItemUseKind.AttackBoost,
                "下一场战斗中获得 15% 攻击力加成。\n需接入战前增益系统后使用。", 0f, 0.15f, false, 7),
            CreateEquipment("cone_dawn", "黎明之前", ItemRarity.Legendary, new EquipmentStats { health = 120, attack = 60, defence = 30 }, 8),
            CreateEquipment("cone_guard", "守望之约", ItemRarity.Epic, new EquipmentStats { health = 150, attack = 30, defence = 60 }, 9),
            CreateEquipment("cone_wind", "逐风旅人", ItemRarity.Epic, new EquipmentStats { health = 100, attack = 45, defence = 25, speed = 3 }, 10),
            CreateEquipment("cone_memory", "远星回响", ItemRarity.Rare, new EquipmentStats { health = 90, attack = 40, defence = 30, damageBonus = 0.03f }, 11)
        };
        string path = ConfigPath + "/ItemCatalog.asset";
        ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(path);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<ItemCatalog>();
            AssetDatabase.CreateAsset(catalog, path);
        }
        foreach (ItemConfig item in items) if (!catalog.items.Contains(item)) catalog.items.Add(item);
        catalog.BuildIndex();
        EditorUtility.SetDirty(catalog);
        return catalog;
    }

    private static ItemConfig CreateItem(string folder, string id, string title, ItemCategory category, ItemRarity rarity,
        ItemUseKind use, string description, float value, float ratio, bool target, int iconIndex)
    {
        string path = ConfigPath + "/" + folder + "/" + id + ".asset";
        var config = AssetDatabase.LoadAssetAtPath<ItemConfig>(path);
        if (config != null) return config;
        config = ScriptableObject.CreateInstance<ItemConfig>();
        config.id = id;
        config.displayName = title;
        config.category = category;
        config.rarity = rarity;
        config.useKind = use;
        config.description = description;
        config.effectValue = value;
        config.effectRatio = ratio;
        config.requiresTarget = target;
        config.icon = CreateIcon(id, iconIndex, rarity);
        AssetDatabase.CreateAsset(config, path);
        return config;
    }

    private static EquipmentConfig CreateEquipment(string id, string title, ItemRarity rarity, EquipmentStats stats, int iconIndex)
    {
        string path = ConfigPath + "/LightCones/" + id + ".asset";
        var config = AssetDatabase.LoadAssetAtPath<EquipmentConfig>(path);
        if (config != null) return config;
        config = ScriptableObject.CreateInstance<EquipmentConfig>();
        config.id = id;
        config.displayName = title;
        config.category = ItemCategory.Equipment;
        config.equipmentKind = EquipmentKind.LightCone;
        config.rarity = rarity;
        config.description = "独立光锥装备，每一份拥有独立等级、经验、突破与锁定状态。\n当前展示基础属性；穿戴与被动效果由角色装备系统接入。";
        config.baseStats = stats;
        config.statsPerLevel = new EquipmentStats { health = 8, attack = 3, defence = 2 };
        config.icon = CreateIcon(id, iconIndex, rarity);
        AssetDatabase.CreateAsset(config, path);
        return config;
    }

    private static Sprite CreateIcon(string id, int variant, ItemRarity rarity)
    {
        string path = "Assets/UI/Sprite/Inventory/" + id + ".png";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;
        var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        Color tint = InventoryItemView.GetRarityColor(rarity);
        var pixels = new Color[128 * 128];
        for (int row = 0; row < 128; row++)
        {
            for (int column = 0; column < 128; column++)
            {
                float horizontal = (column - 63.5f) / 64f;
                float vertical = (row - 63.5f) / 64f;
                float radius = Mathf.Sqrt(horizontal * horizontal + vertical * vertical);
                Color color = radius < 0.9f ? new Color(tint.r * 0.17f, tint.g * 0.17f, tint.b * 0.17f, 1f) : Color.clear;
                bool body = variant < 2 ? Mathf.Abs(horizontal) < 0.46f && Mathf.Abs(vertical) < 0.63f
                    : variant < 4 ? Mathf.Abs(horizontal) + Mathf.Abs(vertical * 0.75f) < 0.65f
                    : variant < 8 ? Mathf.Abs(horizontal) < 0.44f && Mathf.Abs(vertical) < 0.52f
                    : Mathf.Abs(horizontal) < 0.5f && Mathf.Abs(vertical) < 0.73f;
                if (body) color = tint * Mathf.Lerp(0.55f, 1f, (vertical + 1f) * 0.5f);
                bool mark = variant < 2 ? horizontal > -0.24f && horizontal < 0.3f && Mathf.Abs(vertical % 0.22f) < 0.022f
                    : variant < 4 ? Mathf.Abs(horizontal + vertical * 0.3f) < 0.025f
                    : variant < 8 ? Mathf.Abs(horizontal) < 0.065f && Mathf.Abs(vertical) < 0.3f
                        || Mathf.Abs(vertical) < 0.065f && Mathf.Abs(horizontal) < 0.3f
                    : Mathf.Abs(horizontal) + Mathf.Abs(vertical - 0.15f) < 0.25f
                        || Mathf.Abs(vertical + 0.45f) < 0.025f && Mathf.Abs(horizontal) < 0.3f;
                if (body && mark) color = new Color(0.95f, 0.96f, 1f);
                if (color.a > 0f) color.a = 1f;
                pixels[row * 128 + column] = color;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static InventoryItemView CreateItemPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null) return existing.GetComponent<InventoryItemView>();
        GameObject root = Rect("InventoryItem", null);
        ((RectTransform)root.transform).sizeDelta = new Vector2(168f, 193f);
        Image background = root.AddComponent<Image>();
        background.color = CardColor;
        Button button = root.AddComponent<Button>();
        button.targetGraphic = background;
        var selected = ImageRect("Selection", root.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Color(1f, 0.85f, 0.5f, 0.12f));
        var outline = selected.gameObject.AddComponent<Outline>();
        outline.effectColor = Gold;
        outline.effectDistance = new Vector2(2f, 2f);
        Image icon = ImageRect("Icon", root.transform, new Vector2(0.12f, 0.32f), new Vector2(0.88f, 0.95f), Color.white);
        icon.preserveAspect = true;
        Image bar = ImageRect("Rarity", root.transform, new Vector2(0f, 0f), new Vector2(1f, 0.024f), Gold);
        TMP_Text title = Label("Name", root.transform, new Vector2(0.04f, 0.15f), new Vector2(0.96f, 0.3f), "物品名称", 22f);
        title.alignment = TextAlignmentOptions.Center;
        TMP_Text amount = Label("Amount", root.transform, new Vector2(0.08f, 0.035f), new Vector2(0.55f, 0.14f), "20", 20f);
        TMP_Text locked = Label("Locked", root.transform, new Vector2(0.5f, 0.035f), new Vector2(0.96f, 0.14f), "", 18f);
        locked.alignment = TextAlignmentOptions.Right;
        var view = root.AddComponent<InventoryItemView>();
        var data = new SerializedObject(view);
        Assign(data, "button", button);
        Assign(data, "icon", icon);
        Assign(data, "rarityBar", bar);
        Assign(data, "selection", selected.gameObject);
        Assign(data, "nameLabel", title);
        Assign(data, "amountLabel", amount);
        Assign(data, "lockLabel", locked);
        data.ApplyModifiedPropertiesWithoutUndo();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<InventoryItemView>();
    }

    private static void BuildCanvas(InventoryManager manager, InventoryItemView prefab)
    {
        InventoryUIController controller = Object.FindFirstObjectByType<InventoryUIController>(FindObjectsInactive.Include);
        if (controller == null) throw new InvalidOperationException("SampleScene 缺少现有的 BackpackUICanvas 控制器。");
        Transform canvasRoot = controller.transform;
        if (canvasRoot.Find("InventoryRoot") != null) return;
        Canvas canvas = canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        GameObject root = Rect("InventoryRoot", canvasRoot);
        Transform blur = canvasRoot.Find("Panel");
        if (blur != null)
        {
            blur.SetParent(root.transform, false);
            Stretch((RectTransform)blur, Vector2.zero, Vector2.one);
            blur.gameObject.SetActive(true);
        }
        else
        {
            var background = Rect("Panel", root.transform);
            background.AddComponent<RawImage>();
            background.AddComponent<UIBlurMask>();
        }
        var shade = ImageRect("Dim", root.transform, Vector2.zero, Vector2.one, new Color(0.015f, 0.025f, 0.04f, 0.45f));
        shade.raycastTarget = true;
        var body = ImageRect("Content", root.transform, new Vector2(0.045f, 0.07f), new Vector2(0.955f, 0.93f), PanelColor);
        Transform content = body.transform;
        Label("Title", content, new Vector2(0.025f, 0.89f), new Vector2(0.3f, 0.985f), "背包 / INVENTORY", 38f).color = Gold;
        Label("Subtitle", content, new Vector2(0.32f, 0.91f), new Vector2(0.79f, 0.96f), "旅途收藏  ·  材料 / 消耗品 / 独立装备", 22f).color = new Color(0.6f, 0.67f, 0.76f);
        var close = MakeButton("Close", content, new Vector2(0.9f, 0.91f), new Vector2(0.976f, 0.973f), "关闭");
        ImageRect("Divider", content, new Vector2(0.025f, 0.884f), new Vector2(0.975f, 0.886f), Gold);
        TMP_Dropdown category = Dropdown("Category", content, new Vector2(0.025f, 0.8f), new Vector2(0.2f, 0.86f),
            new[] { "全部物品", "养成材料", "食物", "光锥", "仪器（预留）" });
        TMP_Dropdown rarity = Dropdown("Rarity", content, new Vector2(0.215f, 0.8f), new Vector2(0.38f, 0.86f),
            new[] { "全部品质", "普通", "稀有", "史诗", "传说" });
        TMP_Dropdown sort = Dropdown("Sort", content, new Vector2(0.395f, 0.8f), new Vector2(0.555f, 0.86f),
            new[] { "品质排序", "名称排序", "数量排序" });
        GameObject searchObject = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        searchObject.name = "Search";
        searchObject.transform.SetParent(content, false);
        Stretch((RectTransform)searchObject.transform, new Vector2(0.57f, 0.8f), new Vector2(0.975f, 0.86f));
        TMP_InputField search = searchObject.GetComponent<TMP_InputField>();
        search.characterLimit = 40;
        searchObject.GetComponent<Image>().color = CardColor;
        ((TMP_Text)search.placeholder).text = "搜索物品名称…";
        Transform grid = BuildScroll(content);
        var detail = ImageRect("Detail", content, new Vector2(0.71f, 0.12f), new Vector2(0.975f, 0.77f), new Color(0.1f, 0.13f, 0.19f));
        Transform detailRoot = detail.transform;
        Image detailIcon = ImageRect("Icon", detailRoot, new Vector2(0.25f, 0.66f), new Vector2(0.75f, 0.97f), Color.white);
        detailIcon.preserveAspect = true;
        TMP_Text detailTitle = Label("Title", detailRoot, new Vector2(0.06f, 0.57f), new Vector2(0.94f, 0.66f), "选择物品", 30f);
        detailTitle.alignment = TextAlignmentOptions.Center;
        TMP_Text description = Label("Description", detailRoot, new Vector2(0.08f, 0.33f), new Vector2(0.92f, 0.56f), "", 22f);
        description.alignment = TextAlignmentOptions.TopLeft;
        TMP_Text stats = Label("Stats", detailRoot, new Vector2(0.08f, 0.13f), new Vector2(0.92f, 0.33f), "", 21f);
        stats.alignment = TextAlignmentOptions.TopLeft;
        Button decrease = MakeButton("Decrease", detailRoot, new Vector2(0.25f, 0.055f), new Vector2(0.37f, 0.12f), "−");
        TMP_Text quantity = Label("Quantity", detailRoot, new Vector2(0.4f, 0.055f), new Vector2(0.6f, 0.12f), "1", 24f);
        quantity.alignment = TextAlignmentOptions.Center;
        Button increase = MakeButton("Increase", detailRoot, new Vector2(0.63f, 0.055f), new Vector2(0.75f, 0.12f), "+");
        Button use = MakeButton("Use", content, new Vector2(0.71f, 0.045f), new Vector2(0.975f, 0.105f), "使用物品");
        Button progression = MakeButton("Progression", detailRoot, new Vector2(0.08f, 0.055f), new Vector2(0.92f, 0.12f), "前往养成");
        Button equip = MakeButton("Equip", content, new Vector2(0.71f, 0.045f), new Vector2(0.836f, 0.105f), "装备");
        Button locked = MakeButton("Lock", content, new Vector2(0.849f, 0.045f), new Vector2(0.975f, 0.105f), "锁定装备");
        TMP_Text count = Label("Count", content, new Vector2(0.025f, 0.11f), new Vector2(0.3f, 0.15f), "", 22f);
        TMP_Text status = Label("Status", content, new Vector2(0.025f, 0.03f), new Vector2(0.53f, 0.095f), "按 B 打开背包，Esc 关闭", 21f);
        status.color = new Color(0.69f, 0.76f, 0.85f);
        Button save = MakeButton("Save", content, new Vector2(0.55f, 0.045f), new Vector2(0.68f, 0.105f), "保存背包");
        var data = new SerializedObject(controller);
        Assign(data, "backPanel", root);
        Assign(data, "inventory", manager);
        Assign(data, "itemRoot", grid);
        Assign(data, "itemPrefab", prefab);
        Assign(data, "categoryDropdown", category);
        Assign(data, "rarityDropdown", rarity);
        Assign(data, "sortDropdown", sort);
        Assign(data, "searchInput", search);
        Assign(data, "detailIcon", detailIcon);
        Assign(data, "detailTitle", detailTitle);
        Assign(data, "detailDescription", description);
        Assign(data, "detailStats", stats);
        Assign(data, "quantityLabel", quantity);
        Assign(data, "countLabel", count);
        Assign(data, "statusLabel", status);
        Assign(data, "useButton", use);
        Assign(data, "lockButton", locked);
        Assign(data, "equipButton", equip);
        Assign(data, "progressionButton", progression);
        Assign(data, "decreaseButton", decrease);
        Assign(data, "increaseButton", increase);
        Assign(data, "closeButton", close);
        Assign(data, "saveButton", save);
        data.ApplyModifiedPropertiesWithoutUndo();
        foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
        {
            label.font = font;
            label.color = label.color == Color.black ? Color.white : label.color;
            label.fontSize = Mathf.Min(label.fontSize, 38f);
            label.raycastTarget = false;
        }
        foreach (TMP_Text label in searchObject.GetComponentsInChildren<TMP_Text>(true)) label.fontSize = 23f;
        if (Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        root.SetActive(false);
        EditorUtility.SetDirty(controller);
    }

    private static Transform BuildScroll(Transform parent)
    {
        var scrollObject = Rect("ItemScroll", parent);
        Stretch((RectTransform)scrollObject.transform, new Vector2(0.025f, 0.17f), new Vector2(0.685f, 0.77f));
        Image viewport = ImageRect("Viewport", scrollObject.transform, Vector2.zero, Vector2.one, new Color(0.07f, 0.09f, 0.14f));
        viewport.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();
        var gridObject = Rect("Items", viewport.transform);
        var rect = (RectTransform)gridObject.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = Vector2.zero;
        var grid = gridObject.AddComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(12, 12, 12, 12);
        grid.spacing = new Vector2(14f, 14f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;
        grid.cellSize = new Vector2(168f, 193f);
        gridObject.AddComponent<InventoryGridLayout>();
        gridObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
        scroll.viewport = (RectTransform)viewport.transform;
        scroll.content = rect;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 32f;
        return gridObject.transform;
    }

    private static TMP_Dropdown Dropdown(string name, Transform parent, Vector2 min, Vector2 max, string[] options)
    {
        var root = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
        root.name = name;
        root.transform.SetParent(parent, false);
        Stretch((RectTransform)root.transform, min, max);
        root.GetComponent<Image>().color = CardColor;
        TMP_Dropdown dropdown = root.GetComponent<TMP_Dropdown>();
        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string>(options));
        foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
        {
            label.font = font;
            label.fontSize = 22f;
            label.color = Color.white;
        }
        foreach (Image image in dropdown.template.GetComponentsInChildren<Image>(true))
            if (image.gameObject.name == "Template" || image.gameObject.name == "Item Background") image.color = CardColor;
        return dropdown;
    }

    private static Button MakeButton(string name, Transform parent, Vector2 min, Vector2 max, string title)
    {
        Image image = ImageRect(name, parent, min, max, CardColor);
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        TMP_Text label = Label("Label", image.transform, new Vector2(0.03f, 0f), new Vector2(0.97f, 1f), title, 22f);
        label.alignment = TextAlignmentOptions.Center;
        label.color = Gold;
        return button;
    }

    private static TMP_Text Label(string name, Transform parent, Vector2 min, Vector2 max, string value, float size)
    {
        GameObject root = Rect(name, parent);
        Stretch((RectTransform)root.transform, min, max);
        var text = root.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.color = Color.white;
        text.text = value;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableAutoSizing = true;
        text.fontSizeMin = size * 0.78f;
        text.fontSizeMax = size;
        return text;
    }

    private static Image ImageRect(string name, Transform parent, Vector2 min, Vector2 max, Color color)
    {
        GameObject root = Rect(name, parent);
        Stretch((RectTransform)root.transform, min, max);
        Image image = root.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static GameObject Rect(string name, Transform parent)
    {
        var root = new GameObject(name, typeof(RectTransform));
        root.layer = LayerMask.NameToLayer("UI");
        if (parent != null) root.transform.SetParent(parent, false);
        Stretch((RectTransform)root.transform, Vector2.zero, Vector2.one);
        return root;
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void Assign(SerializedObject data, string property, Object value)
    {
        data.FindProperty(property).objectReferenceValue = value;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/'));
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }
}
