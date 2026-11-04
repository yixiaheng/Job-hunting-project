using System;
using System.IO;
using System.Linq;
using Game.Characters;
using Game.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class CharacterProgressionPlayVerification
{
    private const string Active = "CharacterProgressionChecks.play";
    private static int step, scenario, levelBefore;
    private static double at;
    private static Scene source;
    private static UIPanel originalPanel;
    private static UIHandle originalHandle;
    private static Vector3 position;
    private static PlayerInputHandler player;
    private static float timeScale;
    private static string rosterBefore, inventoryBefore, unitId;
    private static CharacterStats expected;
    [Serializable]
    private sealed class Setup
    {
        public SceneSetup[] scenes;
    }

    static CharacterProgressionPlayVerification()
    {
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += Changed;
    }

    public static void Begin()
    {
        if (EditorSceneManager.GetSceneManagerSetup().Any(s => SceneManager.GetSceneByPath(s.path).isDirty))
            throw new InvalidOperationException("请先保存当前场景。");
        SessionState.SetString("CharacterChecks.setup", JsonUtility.ToJson(new Setup { scenes = EditorSceneManager.GetSceneManagerSetup() }));
        var config = Resources.Load<PlayerRuntimeConfig>("Bootstrap/PlayerRuntimeConfig");
        SessionState.SetString("CharacterChecks.originalSlot", config.saveSlot);
        config.saveSlot = "slot_character_verification_" + Guid.NewGuid().ToString("N");
        SessionState.SetString("CharacterChecks.testSlot", config.saveSlot);
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssetIfDirty(config);
        SessionState.SetBool(Active, true);
        File.WriteAllText("Temp/CharacterProgressionChecks/playmode.txt", "");
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        EditorApplication.isPlaying = true;
    }

    private static void Changed(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
        {
            step = scenario = 0;
            at = EditorApplication.timeSinceStartup;
        }

        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Active, false))
            return;
        SessionState.SetBool(Active, false);
        var config = Resources.Load<PlayerRuntimeConfig>("Bootstrap/PlayerRuntimeConfig");
        config.saveSlot = SessionState.GetString("CharacterChecks.originalSlot", "slot_0");
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssetIfDirty(config);
        string slot = SessionState.GetString("CharacterChecks.testSlot", "");
        if (slot.StartsWith("slot_character_verification_", StringComparison.Ordinal))
            TapTap21.SaveSystem.dyh.SaveSystem.DeleteSlot(slot);
        var setup = JsonUtility.FromJson<Setup>(SessionState.GetString("CharacterChecks.setup", ""));
        if (setup?.scenes != null)
            EditorSceneManager.RestoreSceneManagerSetup(setup.scenes);
    }

    private static void Check(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException(name);
        File.AppendAllText("Temp/CharacterProgressionChecks/playmode.txt", "PASS " + name + Environment.NewLine);
    }

    private static void Next(int value)
    {
        step = value;
        at = EditorApplication.timeSinceStartup;
    }

    private static CharacterPanel Main => UnityEngine.Object.FindFirstObjectByType<CharacterPanel>();

    private static CharacterPopupPanel Popup(UIId id) => UnityEngine.Object.FindObjectsByType<CharacterPopupPanel>(FindObjectsSortMode.None).FirstOrDefault(p => p.Config.id == id && p.State == UIState.Open);
    private static void Click(Component root, string label, bool contains = false)
    {
        var button = root.GetComponentsInChildren<Button>().FirstOrDefault(b =>
        {
            string text = b.GetComponentInChildren<TMP_Text>()?.text;
            return contains ? text?.Contains(label) == true : text == label;
        });
        Check(button != null && button.interactable, "button wired: " + label);
        button.onClick.Invoke();
    }

    private static void Update()
    {
        if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying || EditorApplication.isPaused)
            return;
        try
        {
            if (EditorApplication.timeSinceStartup - at > 45)
                throw new TimeoutException("Play verification step " + step);
            if (Time.frameCount < 5 || PlayerRuntime.Instance == null || UIManager.Instance == null || EditorApplication.timeSinceStartup - at < .3)
                return;
            if (!PlayerRuntime.Instance.IsReady)
                throw new Exception(PlayerRuntime.Instance.LastMessage);
            var service = PlayerRuntime.Instance.Characters;
            var ui = UIManager.Instance;
            if (step == 0)
            {
                if (scenario == 1 && SceneManager.GetActiveScene().path != "Assets/Scenes/PartyScreen.unity")
                {
                    Check(GameSceneManager.Instance.TryLoad(SceneId.Party, out _), "load party");
                    Next(1);
                    return;
                }

                Enter();
                return;
            }

            if (step == 1)
            {
                if (EditorApplication.timeSinceStartup - at > 1)
                    Enter();
                return;
            }

            if (step == 2)
            {
                if (Main == null || Main.State != UIState.Open || EditorApplication.timeSinceStartup - at < 1)
                    return;
                Check(Time.timeScale == 0, "time paused " + scenario);
                Check(source.isLoaded && source.GetRootGameObjects().All(g => g.name != "Player" || g.activeSelf), "source roots retained " + scenario);
                Check(originalPanel != null && originalPanel.IsOpen && originalPanel.IsPresentationSuspended && originalPanel.transform.parent.GetComponent<CanvasGroup>().alpha == 0, "source UI retained hidden " + scenario);
                Check(!originalPanel.GetComponent<CanvasGroup>().blocksRaycasts, "source clicks blocked " + scenario);
                Check(source.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>()).All(c => !c.enabled), "source cameras disabled " + scenario);
                Check(source.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AudioListener>()).All(c => !c.enabled), "source listeners disabled " + scenario);
                Check(Main.SelectedUnitId != null && UnityEngine.Object.FindFirstObjectByType<CharacterSceneController>().DisplayedUnitId == Main.SelectedUnitId, "model bound " + scenario);
                if (scenario == 0)
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Temp/CharacterProgressionChecks/character.png"));
                Next(4);
                return;
            }

            if (step == 4)
            {
                if (scenario != 0)
                {
                    ui.Back();
                    Next(3);
                    return;
                }

                rosterBefore = JsonUtility.ToJson(service.CreateSnapshot());
                inventoryBefore = JsonUtility.ToJson(service.Inventory.CreateSnapshot());
                Click(Main, "升级");
                Next(10);
                return;
            }

            if (step == 10)
            {
                if (Popup(UIId.CharacterMaterials) == null)
                    return;
                ui.Back();
                Next(11);
                return;
            }

            if (step == 11)
            {
                if (ui.IsOpen(UIId.CharacterMaterials))
                    return;
                Check(Main != null && CharacterSceneNavigator.IsActive, "back closes popup before main");
                Check(rosterBefore == JsonUtility.ToJson(service.CreateSnapshot()) && inventoryBefore == JsonUtility.ToJson(service.Inventory.CreateSnapshot()), "material cancel changes nothing");
                Click(Main, "升级");
                Next(12);
                return;
            }

            if (step == 12)
            {
                var popup = Popup(UIId.CharacterMaterials);
                if (popup == null)
                    return;
                Click(popup, "使用 1 份", true);
                Next(13);
                return;
            }

            if (step == 13)
            {
                if (Popup(UIId.CharacterConfirm) == null)
                    return;
                Check(rosterBefore == JsonUtility.ToJson(service.CreateSnapshot()), "preview does not change level");
                ScreenCapture.CaptureScreenshot(Path.GetFullPath("Temp/CharacterProgressionChecks/confirmation.png"));
                Next(14);
                return;
            }

            if (step == 14)
            {
                ui.Back();
                Next(15);
                return;
            }

            if (step == 15)
            {
                Check(inventoryBefore == JsonUtility.ToJson(service.Inventory.CreateSnapshot()), "confirmation cancel consumes nothing");
                unitId = Main.SelectedUnitId;
                levelBefore = service.GetCharacter(unitId).level;
                var preview = service.Preview(new CharacterRequest { unitId = unitId, operation = CharacterOperation.Level, quantity = 1 });
                ui.Open(UIId.CharacterConfirm, new CharacterPopupContext(Main, preview), Main.Handle);
                Next(16);
                return;
            }

            if (step == 16)
            {
                var popup = Popup(UIId.CharacterConfirm);
                if (popup == null)
                    return;
                Click(popup, "确认");
                Next(17);
                return;
            }

            if (step == 17)
            {
                Check(service.GetCharacter(unitId).level > levelBefore, "real confirmation levels character");
                Click(Main, "技能");
                var preview = service.Preview(new CharacterRequest { unitId = unitId, operation = CharacterOperation.Skill, skill = SkillKind.Basic });
                ui.Open(UIId.CharacterConfirm, new CharacterPopupContext(Main, preview), Main.Handle);
                Next(18);
                return;
            }

            if (step == 18)
            {
                var popup = Popup(UIId.CharacterConfirm);
                if (popup == null)
                    return;
                Click(popup, "确认");
                Next(19);
                return;
            }

            if (step == 19)
            {
                Check(service.GetCharacter(unitId).basicLevel == 2, "real confirmation upgrades basic skill");
                expected = service.Stats(unitId);
                var disk = new PlayerProfileStore(PlayerRuntime.Instance.Config.saveSlot).Load();
                Check(disk.Success && disk.Data.roster.characters.First(c => c.unitId == unitId).basicLevel == 2, "native save reload retains cultivation");
                var renderer = UnityEngine.Object.FindFirstObjectByType<CharacterSceneController>().GetComponentInChildren<Renderer>();
                var rotation = renderer.transform.rotation;
                Main.GetComponentInChildren<CharacterModelDrag>().OnDrag(new PointerEventData(EventSystem.current) { delta = new Vector2(60, 0) });
                Check(rotation != renderer.transform.rotation, "drag rotation at zero time scale");
                ui.Back();
                Next(3);
                return;
            }

            if (step == 3)
            {
                if (CharacterSceneNavigator.IsActive || EditorApplication.timeSinceStartup - at < .8)
                    return;
                Check(SceneManager.GetActiveScene() == source && Time.timeScale == timeScale, "source scene and time restored " + scenario);
                Check(originalPanel != null && originalPanel.IsOpen && !originalPanel.IsPresentationSuspended && originalPanel.GetComponent<CanvasGroup>().alpha > 0, "same UI instance restored " + scenario);
                if (player != null)
                    Check(player.transform.position == position, "source position restored " + scenario);
                if (++scenario < 3)
                {
                    Next(0);
                    return;
                }

                Check(BattlePartyManager.Instance.AddOrReplace(service.Config(unitId), 0), "cultivated character enters party");
                UnityEngine.Object.FindFirstObjectByType<PartySceneController>().StartBattle();
                Next(20);
                return;
            }

            if (step == 20)
            {
                if (BattleManager.Instance == null || BattleManager.Instance.battleFlow == null)
                    return;
                var ally = UnityEngine.Object.FindObjectsByType<BattleUnitActor>(FindObjectsSortMode.None).Select(a => a.Unit).FirstOrDefault(u => u != null && u.faction == Faction.OurSide && u.displayName == service.Config(unitId).displayName);
                Check(ally != null && ally.maxHealth == expected.health && ally.attack == expected.attack && ally.defence == expected.defence, "cultivation stats enter actual BattleManager");
                Check(ally.skills.First(s => s.kind == SkillKind.Basic).effectConfigs[0].value > service.Config(unitId).skills.First(s => s.kind == SkillKind.Basic).effectConfigs[0].value, "cultivated skill enters actual battle");
                Check(ui.IsOpen(UIId.BattleCommands) && ui.IsOpen(UIId.BattleEnemiesStatus), "battle commands and enemy UI retained");
                Check(!CharacterSceneNavigator.TryEnter(), "battle cultivation blocked");
                Check(BattleManager.Instance.TryCreateLaunchData(out var retry, out _), "original retry snapshot available");
                var retried = retry.CreateAlly(0, 0);
                Check(retried.attack == expected.attack, "retry preserves snapshot");
                foreach (var skill in retried.skills)
                    UnityEngine.Object.Destroy(skill);
                Check(GameSceneManager.Instance.TryLoad(SceneId.Character, out _), "direct character scene load");
                Next(21);
                return;
            }

            if (step == 21)
            {
                if (Main == null || !CharacterSceneNavigator.IsActive)
                    return;
                ui.Back();
                Next(22);
                return;
            }

            if (step == 22)
            {
                if (CharacterSceneNavigator.IsActive || SceneManager.GetActiveScene().path != "Assets/Scenes/SampleScene.unity")
                    return;
                Check(Time.timeScale == 1, "direct character scene returns to Sample");
                File.WriteAllText(CharacterProgressionSetup.ResultPath, "SUCCESS play: three entries, popup cancellation, real cultivation/save, battle snapshot and direct scene return");
                EditorApplication.isPlaying = false;
            }
        }
        catch (Exception exception)
        {
            File.WriteAllText(CharacterProgressionSetup.ResultPath, exception.ToString());
            Debug.LogException(exception);
            EditorApplication.isPlaying = false;
        }
    }

    private static void Enter()
    {
        var ui = UIManager.Instance;
        var id = scenario == 0 ? UIId.ExplorationHUD : scenario == 1 ? UIId.BattleParty : UIId.Backpack;
        if (scenario == 2)
            originalHandle = ui.Open(id);
        else
        {
            var panel = UnityEngine.Object.FindObjectsByType<UIPanel>(FindObjectsSortMode.None).FirstOrDefault(p => p.Config != null && p.Config.id == id);
            if (panel == null)
                return;
            originalHandle = panel.Handle;
        }

        if (!ui.TryGetPanel(originalHandle, out originalPanel))
            return;
        source = SceneManager.GetActiveScene();
        timeScale = Time.timeScale;
        player = UnityEngine.Object.FindFirstObjectByType<PlayerInputHandler>();
        if (player != null)
            position = player.transform.position;
        if (scenario == 2)
            Check(Game.Inventory.InventoryManager.Instance.RequestProgression("character_exp"), "inventory progression entry connected");
        else if (scenario == 1)
        {
            Check(originalPanel.GetComponentInChildren<CharacterEntryButton>(true) == null, "party window has no main HUD entry");
            Check(CharacterSceneNavigator.TryEnter(), "navigation preserves an existing party window");
        }
        else
        {
            var entry = originalPanel.GetComponentInChildren<CharacterEntryButton>();
            Check(entry != null, "native entry exists " + scenario);
            entry.GetComponent<Button>().onClick.Invoke();
            Check(CharacterSceneNavigator.IsActive, "native entry button works " + scenario);
        }

        Check(!CharacterSceneNavigator.TryEnter(), "duplicate entry refused " + scenario);
        Next(2);
    }
}
