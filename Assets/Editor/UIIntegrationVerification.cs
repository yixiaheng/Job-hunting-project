using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Exercises real registered panels inside SceneFlowVerification's isolated Play Mode session.</summary>
public static class UIIntegrationVerification
{
    public static IEnumerator Run(Action<bool, string> check)
    {
        var ui = UIManager.Instance;
        var settingsButton = UnityEngine.Object.FindObjectsByType<UIOpenButton>(FindObjectsSortMode.None)
            .Single(button => button.transform.parent.name == "SettingButton");
        settingsButton.GetComponent<Button>().onClick.Invoke();
        check(ui.IsOpen(UIId.Settings), "UI: real HUD settings button opens Settings");
        var settings = Find(UIId.Settings);
        check(settings.GetComponent<UIFadeTransition>() != null, "UI: registered window uses framework fade");
        yield return WaitForOpen(settings);
        check(ui.Input.Current.BlockMovement && ui.Input.Current.ShowCursor, "UI: settings requests input through framework");
        check(!settingsButton.GetComponent<CanvasGroup>().blocksRaycasts, "UI: obscured HUD entry cannot receive clicks");

        // Open a Window after a Popup: Back must still choose the visually higher Popup.
        var popupHandle = ui.Open(UIId.CharacterConfirm);
        yield return WaitForOpen(Find(UIId.CharacterConfirm));
        ui.Open(UIId.Settings);
        check(!settings.GetComponent<CanvasGroup>().interactable, "UI: later Window remains covered by Popup");
        check(ui.Back(), "UI: Back accepts top Popup");
        yield return WaitForClosed(popupHandle);
        check(ui.IsOpen(UIId.Settings), "UI: Back leaves lower Window open");

        ui.Registry.TryGet(UIId.CharacterConfirm, out var popupConfig);
        bool oldHide = popupConfig.hideLowerWindows;
        try
        {
            popupConfig.hideLowerWindows = true;
            popupHandle = ui.Open(UIId.CharacterConfirm, parent: settings.Handle);
            yield return WaitForOpen(Find(UIId.CharacterConfirm));
            var presentation = settings.transform.parent.GetComponent<CanvasGroup>();
            check(presentation.alpha == 0 && !presentation.blocksRaycasts, "UI: hideLowerWindows hides and gates lower Window");
            using (ui.SuspendPresentation()) { }
            check(presentation.alpha == 0, "UI: resume does not reveal a still-covered Window");
            ui.Close(popupHandle);
            yield return WaitForClosed(popupHandle);
            check(presentation.alpha == 1 && presentation.blocksRaycasts, "UI: closing covering Popup reveals Window");
        }
        finally
        {
            popupConfig.hideLowerWindows = oldHide;
        }

        popupHandle = ui.Open(UIId.CharacterConfirm, parent: settings.Handle);
        yield return WaitForOpen(Find(UIId.CharacterConfirm));
        ui.Close(settings.Handle);
        yield return WaitForClosed(settings.Handle);
        check(!ui.TryGetPanel<UIPanel>(popupHandle, out _), "UI: parent close releases child after animation");
        check(!ui.Input.Current.BlockMovement, "UI: last Window close releases movement restriction");

        // An immediate reopen during a close must finish with the same cached instance visible.
        var handle = ui.Open(UIId.Settings);
        yield return WaitForOpen(settings);
        ui.Close(handle);
        var reopened = ui.Open(UIId.Settings);
        yield return WaitForOpen(settings);
        check(handle == reopened && settings.GetComponent<CanvasGroup>().alpha == 1,
            "UI: close/reopen reuses cached instance and completes animation");
        ui.Close(handle);
        yield return WaitForClosed(handle);

        float oldTime = Time.timeScale;
        try
        {
            Time.timeScale = 0f;
            handle = ui.Open(UIId.Settings);
            yield return WaitForOpen(settings);
            var overlayHandle = ui.Open(UIId.SceneTransition);
            var overlay = Find(UIId.SceneTransition) as SceneTransitionPanel;
            yield return WaitForOpen(overlay);
            using (ui.SuspendPresentation())
            {
                check(!overlay.IsPresentationSuspended, "UI: transition Overlay survives business presentation suspension");
                yield return overlay.FadeTo(1f, .05f);
                check(overlay.GetComponent<Image>().color.a == 1f, "UI: transition fade advances at timeScale zero");
                yield return overlay.FadeTo(0f, .05f);
                Canvas.ForceUpdateCanvases();
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current)
                {
                    position = new Vector2(Screen.width / 2f, Screen.height / 2f)
                }, hits);
                check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<SceneTransitionPanel>() == overlay,
                    "UI: transparent Overlay still receives the top pointer raycast");
                check(!ui.Back(), "UI: transition Overlay blocks Back to underlying panels");
            }
            ui.Close(overlayHandle);
            yield return WaitForClosed(overlayHandle);
            ui.Close(handle);
            yield return WaitForClosed(handle);
        }
        finally
        {
            Time.timeScale = oldTime;
        }
        check(!ui.Input.Current.BlockMovement, "UI: transition and window release their input requests");
        check(settingsButton.GetComponent<CanvasGroup>().blocksRaycasts, "UI: HUD entries return after all foreground UI closes");

        yield return VerifyCampaign(ui, check);

        handle = ui.Open(UIId.Settings);
        yield return WaitForOpen(settings);
        popupHandle = ui.Open(UIId.CharacterConfirm, parent: handle);
        yield return WaitForOpen(Find(UIId.CharacterConfirm));
        ui.Close(handle);
        yield return null;
        check(ui.Release(handle), "UI: immediate release interrupts a closing parent and its child");
        yield return new WaitForSecondsRealtime(.3f);
        check(!ui.TryGetPanel<UIPanel>(handle, out _) && !ui.TryGetPanel<UIPanel>(popupHandle, out _),
            "UI: interrupted animations cannot restore released panel records");
        check(!ui.Input.Current.BlockMovement, "UI: immediate release also releases input");

        // 一个非活动场景的归属标识：子面板必须跟随父面板，而非当前活动场景。
        const int sourceSceneHandle = -123456;
        handle = ui.Open(UIId.Settings, ownerSceneHandle: sourceSceneHandle);
        using (ui.SuspendScenePresentation(sourceSceneHandle))
        {
            popupHandle = ui.Open(UIId.CharacterConfirm, parent: handle);
            yield return WaitForOpen(Find(UIId.CharacterConfirm));
            check(Find(UIId.CharacterConfirm).IsPresentationSuspended,
                "UI: child inherits parent scene and its existing presentation suspension");
            ui.ReleaseSceneUI(sourceSceneHandle);
            check(!ui.TryGetPanel<UIPanel>(popupHandle, out _), "UI: scene release finds child by inherited ownership");
        }
        ui.Release(handle);
    }

    private static IEnumerator VerifyCampaign(UIManager ui, Action<bool, string> check)
    {
        UIHandle firstHandle = default;
        LuaUIPanel firstPanel = null;

        for (int index = 0; index < 3; index++)
        {
            UIHandle handle = ui.Open(UIId.Campaign);
            check(handle.IsValid, $"UI: Campaign open {index + 1} returns a valid handle");
            check(ui.TryGetPanel(handle, out LuaUIPanel panel),
                $"UI: Campaign open {index + 1} creates a LuaUIPanel");
            yield return WaitForOpen(panel);

            TMP_Text title = panel.transform.Find("Window/TitleText")?.GetComponent<TMP_Text>();
            TMP_Text status = panel.transform.Find("Window/StatusText")?.GetComponent<TMP_Text>();
            check(title != null && title.text == "活动", "UI: Campaign Lua refreshes title text");
            check(status != null && status.text == "七日签到即将接入", "UI: Campaign Lua refreshes status text");
            check(ui.Input.Current.BlockMovement && ui.Input.Current.BlockCamera,
                "UI: Campaign input is owned by UIManager");

            if (index == 0)
            {
                firstHandle = handle;
                firstPanel = panel;
            }
            else
            {
                check(handle == firstHandle && panel == firstPanel,
                    "UI: Campaign reuses its cached Lua panel instance");
            }

            Button close = panel.transform.Find("Window/CloseButton")?.GetComponent<Button>();
            check(close != null, "UI: Campaign exposes its close Button");
            close.onClick.Invoke();
            yield return WaitForClosed(handle);
            check(!ui.Input.Current.BlockMovement && !ui.Input.Current.BlockCamera,
                "UI: Campaign close restores gameplay input");
        }

        check(ui.Release(firstHandle), "UI: Campaign cached instance can be released");
        yield return null;

        ui.Registry.TryGet(UIId.Campaign, out UIConfig campaignConfig);
        LuaUIPanel template = campaignConfig.prefab as LuaUIPanel;
        var referencesField = typeof(LuaUIPanel).GetField(
            "references",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var targetField = typeof(LuaUIReference).GetField(
            "target",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var references = referencesField?.GetValue(template) as LuaUIReference[];
        check(references != null && references.Length > 0 && targetField != null,
            "UI: Campaign test can access its serialized Lua references");

        UnityEngine.Object originalTarget = targetField.GetValue(references[0]) as UnityEngine.Object;
        try
        {
            targetField.SetValue(references[0], null);
            UIHandle failedHandle = ui.Open(UIId.Campaign);
            check(!failedHandle.IsValid && !ui.IsOpen(UIId.Campaign),
                "UI: failed Lua initialization rolls back the Campaign instance");
        }
        finally
        {
            targetField.SetValue(references[0], originalTarget);
        }

        UIHandle recoveredHandle = ui.Open(UIId.Campaign);
        bool recovered = ui.TryGetPanel(recoveredHandle, out LuaUIPanel recoveredPanel);
        check(recoveredHandle.IsValid && recovered,
            "UI: Campaign can open again after initialization rollback");
        yield return WaitForOpen(recoveredPanel);
        ui.Close(recoveredHandle);
        yield return WaitForClosed(recoveredHandle);
        ui.Release(recoveredHandle);
    }

    public static IEnumerator VerifyPartyMask(Action<bool, string> check)
    {
        var ui = UIManager.Instance;
        var parent = Find(UIId.BattleParty);
        var handle = ui.Open(UIId.AlliesChoose,
            new AlliesChooseUIContext(BattlePartyManager.Instance, PartyMode.Replace, 0), parent.Handle);
        var panel = Find(UIId.AlliesChoose);
        yield return WaitForOpen(panel);
        var mask = panel.GetComponentInChildren<UIMaskClose>(true);
        check(mask != null, "UI: party background uses shared mask component");
        bool oldClose = panel.Config.closeWhenClickMask;
        try
        {
            panel.Config.closeWhenClickMask = false;
            Click(mask.gameObject);
            yield return null;
            check(panel.State == UIState.Open, "UI: mask click respects disabled closeWhenClickMask");
            panel.Config.closeWhenClickMask = true;
            Click(mask.gameObject);
            yield return WaitForClosed(handle);
            check(ui.IsOpen(UIId.BattleParty), "UI: mask closes only child Popup through UIManager");
        }
        finally
        {
            ui.Registry.TryGet(UIId.AlliesChoose, out var config);
            config.closeWhenClickMask = oldClose;
        }
    }

    private static void Click(GameObject target)
    {
        ExecuteEvents.Execute(target, new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left
        }, ExecuteEvents.pointerClickHandler);
    }

    private static UIPanel Find(UIId id)
    {
        return UnityEngine.Object.FindObjectsByType<UIPanel>(FindObjectsSortMode.None)
            .Single(panel => panel.Config != null && panel.Config.id == id);
    }

    private static IEnumerator WaitForOpen(UIPanel panel)
    {
        float deadline = Time.realtimeSinceStartup + 5f;
        while (panel.State != UIState.Open)
        {
            if (Time.realtimeSinceStartup > deadline)
                throw new InvalidOperationException("Timed out opening " + panel.name);
            yield return null;
        }
    }

    public static IEnumerator WaitForClosed(UIHandle handle)
    {
        float deadline = Time.realtimeSinceStartup + 5f;
        while (UIManager.Instance.TryGetPanel<UIPanel>(handle, out var panel) && panel.State != UIState.Closed)
        {
            if (Time.realtimeSinceStartup > deadline)
                throw new InvalidOperationException("Timed out closing " + panel.name);
            yield return null;
        }
    }
}
