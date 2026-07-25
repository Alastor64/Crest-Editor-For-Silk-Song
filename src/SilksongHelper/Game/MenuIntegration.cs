using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using TMProOld;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SilksongHelper;

internal static class MenuIntegration
{
    private const string WorkshopTitle = "纹章工坊";
    private const float FallbackSubmitAnimationDelay = 0.35f;
    private const float MaximumSubmitAnimationDelay = 0.9f;

    private static CharmWorkshopUI? _workshopUI;
    private static bool _workshopOpenPending;

    private static CharmWorkshopUI? WorkshopUI
    {
        get
        {
            if (_workshopUI != null && _workshopUI.gameObject != null)
                return _workshopUI;
            try
            {
                _workshopUI = UnityEngine.Object.FindObjectOfType<CharmWorkshopUI>();
            }
            catch { }
            return _workshopUI;
        }
    }

    private static void QueueWorkshopOpen()
    {
        QueueWorkshopOpen(null);
    }

    private static void QueueWorkshopOpen(Animator? submitAnimator)
    {
        try
        {
            if (_workshopOpenPending || WorkshopUI is { IsVisible: true })
                return;

            var ui = WorkshopUI;
            if (ui == null)
            {
                Plugin.Log.LogWarning("[MenuIntegration] WorkshopUI 未找到!");
                return;
            }

            _workshopOpenPending = true;
            ui.StartCoroutine(OpenWorkshopAfterSubmitAnimation(ui, submitAnimator));
        }
        catch (Exception e)
        {
            _workshopOpenPending = false;
            Plugin.Log.LogError($"[MenuIntegration] QueueWorkshopOpen 异常: {e}");
        }
    }

    private static IEnumerator OpenWorkshopAfterSubmitAnimation(
        CharmWorkshopUI ui,
        Animator? submitAnimator)
    {
        if (submitAnimator == null || !submitAnimator.isActiveAndEnabled)
        {
            yield return new WaitForSecondsRealtime(FallbackSubmitAnimationDelay);
        }
        else
        {
            // The submit event is raised in the same frame as the Flash trigger.
            // Wait one frame for the Animator to enter that state, then keep the
            // original menu visible until the flash finishes.
            yield return null;

            var startedAt = Time.realtimeSinceStartup;
            var observedFlashState = false;
            while (Time.realtimeSinceStartup - startedAt < MaximumSubmitAnimationDelay)
            {
                var current = submitAnimator.GetCurrentAnimatorStateInfo(0);
                var next = submitAnimator.IsInTransition(0)
                    ? submitAnimator.GetNextAnimatorStateInfo(0)
                    : default;
                var isFlashing = IsFlashState(current) || IsFlashState(next);
                observedFlashState |= isFlashing;

                if (observedFlashState && !isFlashing)
                    break;
                if (!observedFlashState
                    && Time.realtimeSinceStartup - startedAt >= FallbackSubmitAnimationDelay)
                    break;

                yield return null;
            }
        }

        _workshopOpenPending = false;
        if (ui != null && ui.gameObject != null)
            ui.Show();
    }

    private static bool IsFlashState(AnimatorStateInfo state)
    {
        return state.shortNameHash == Animator.StringToHash("Flash")
            || state.IsName("Flash")
            || state.IsName("Base Layer.Flash");
    }

    [HarmonyPatch(typeof(UIManager), "ConfigureMenu")]
    internal static class ConfigureMenuPatch
    {
        internal static void Prefix(UIManager __instance)
        {
            try
            {
                var parent = __instance.mainMenuButtons?.optionsButton?.transform.parent
                    ?? __instance.mainMenuButtons?.quitButton?.transform.parent;
                if (parent == null) return;

                var existing = parent.Find("CrestWorkshopButton");
                if (existing != null)
                    UnityEngine.Object.Destroy(existing.gameObject);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(MainMenuOptions), "ConfigureNavigation")]
    internal static class MainMenuConfigureNavigationPatch
    {
        internal static void Prefix(MainMenuOptions __instance)
        {
            try
            {
                var existingBtn = __instance.optionsButton ?? __instance.quitButton;
                if (existingBtn == null) return;
                var parent = existingBtn.transform.parent;
                if (parent == null) return;

                if (parent.Find("CrestWorkshopButton") != null) return;

                var cloneGo = CloneTemplateButton("CrestWorkshopButton", parent, existingBtn.gameObject);
                cloneGo.transform.SetAsLastSibling();
                Plugin.Log.LogInfo("[MenuIntegration] 主菜单按钮已克隆");
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[MenuIntegration] ConfigureNav Prefix: {e}"); }
        }

        internal static void Postfix(MainMenuOptions __instance)
        {
            try
            {
                var existingBtn = __instance.optionsButton ?? __instance.quitButton;
                if (existingBtn == null) return;
                var parent = existingBtn.transform.parent;
                if (parent == null) return;

                var ourBtn = parent.Find("CrestWorkshopButton");
                if (ourBtn == null) return;

                var selectable = ourBtn.GetComponent<Selectable>();
                if (selectable != null) selectable.interactable = true;

                var canvasGroup = ourBtn.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                }

                WireWorkshopSubmitActions(ourBtn.gameObject);
                SetWorkshopButtonText(ourBtn.gameObject);

                RegisterButtonWithMenuButtonList(parent, ourBtn.gameObject);
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[MenuIntegration] ConfigureNav Postfix: {e}"); }
        }
    }

    [HarmonyPatch(typeof(UIManager), "GoToPauseMenu")]
    internal static class GoToPauseMenuPatch
    {
        internal static void Postfix(UIManager __instance)
        {
            try
            {
                __instance.StartCoroutine(DelayedPauseSetup(__instance));
            }
            catch { }
        }

        private static IEnumerator DelayedPauseSetup(UIManager ui)
        {
            for (int attempt = 0; attempt < 15; attempt++)
            {
                yield return null;
                try
                {
                    var anchor = FindPauseAnchorButton();
                    if (anchor == null) continue;

                    var parent = anchor.transform.parent;
                    if (parent == null) continue;

                    var existing = parent.Find("CrestWorkshopPauseBtn");
                    GameObject ourBtn;
                    if (existing != null)
                    {
                        ourBtn = existing.gameObject;
                    }
                    else
                    {
                        ourBtn = CloneTemplateButton("CrestWorkshopPauseBtn", parent, anchor.gameObject);
                        ourBtn.transform.SetAsLastSibling();
                    }

                    var selectable = ourBtn.GetComponent<Selectable>();
                    if (selectable != null) selectable.interactable = true;

                    var canvasGroup = ourBtn.GetComponent<CanvasGroup>();
                    if (canvasGroup != null)
                    {
                        canvasGroup.interactable = true;
                        canvasGroup.blocksRaycasts = true;
                    }

                    WireWorkshopSubmitActions(ourBtn);
                    SetWorkshopButtonText(ourBtn);

                    RegisterButtonWithMenuButtonList(parent, ourBtn);
                    Plugin.Log.LogInfo("[MenuIntegration] 暂停菜单按钮已创建并注册");
                    yield break;
                }
                catch (Exception e) { Plugin.Log.LogWarning($"[MenuIntegration] pause setup: {e}"); }
            }
        }

        private static GameObject? FindPauseAnchorButton()
        {
            var pauseBtns = Resources.FindObjectsOfTypeAll<PauseMenuButton>();
            if (pauseBtns != null && pauseBtns.Length > 0)
            {
                foreach (var pb in pauseBtns)
                    if (pb.gameObject.activeInHierarchy) return pb.gameObject;
                return pauseBtns[pauseBtns.Length - 1].gameObject;
            }

            var menuBtns = Resources.FindObjectsOfTypeAll<MenuButton>();
            if (menuBtns != null && menuBtns.Length > 0)
            {
                foreach (var mb in menuBtns)
                    if (mb.gameObject.activeInHierarchy) return mb.gameObject;
            }

            return null;
        }
    }

    [HarmonyPatch(typeof(PauseMenuButton), "OnSubmit")]
    internal static class PauseButtonOnSubmitPatch
    {
        internal static bool Prefix(PauseMenuButton __instance)
        {
            if (__instance.name == "CrestWorkshopPauseBtn")
            {
                PlayPauseButtonSubmitFeedback(__instance);
                QueueWorkshopOpen(__instance.flashEffect);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(PauseMenuButton), "OnCancel")]
    internal static class PauseButtonOnCancelPatch
    {
        internal static bool Prefix(PauseMenuButton __instance)
        {
            if (__instance.name == "CrestWorkshopPauseBtn")
                return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(UIManager), "UIClosePauseMenu")]
    internal static class ClosePauseMenuPatch
    {
        internal static void Prefix()
        {
            try { if (WorkshopUI is { IsVisible: true }) WorkshopUI.Hide(); } catch { }
        }
    }

    [HarmonyPatch(typeof(UIManager), "UIContinueGame", typeof(int))]
    internal static class ContinueGamePatch
    {
        internal static void Prefix()
        {
            try { WorkshopUI?.Hide(); } catch { }
        }
    }

    [HarmonyPatch(typeof(UIManager), "ReturnToMainMenu")]
    internal static class ReturnToMainMenuPatch
    {
        internal static void Prefix()
        {
            try { WorkshopUI?.Hide(); } catch { }
        }
    }

    private static GameObject CloneTemplateButton(string name, Transform parent, GameObject template)
    {
        var clone = UnityEngine.Object.Instantiate(template, parent);
        clone.name = name;

        foreach (var child in clone.GetComponentsInChildren<Transform>(true))
        {
            var comps = child.GetComponents<Component>();
            foreach (var c in comps)
            {
                if (c == null) continue;
                var typeName = c.GetType().Name;
                var ns = c.GetType().Namespace ?? "";

                bool remove = typeName == "PlayMakerFSM"
                    || typeName == "PlayMakerUGuiSceneProxy"
                    || typeName == "FsmTemplate"
                    || typeName == "AutoLocalizeTextUI"
                    || typeName.StartsWith("PlayMaker")
                    || ns.StartsWith("HutongGames");

                if (remove)
                {
                    Plugin.Log.LogDebug($"[MenuIntegration] 移除脚本: {typeName} from {child.name}");
                    if (c is Behaviour behaviour)
                        behaviour.enabled = false;
                    UnityEngine.Object.Destroy(c);
                }
            }
        }

        SetWorkshopButtonText(clone);
        WireWorkshopSubmitActions(clone);

        foreach (var t in clone.GetComponentsInChildren<TMP_Text>(true))
            t.raycastTarget = true;
        foreach (var t in clone.GetComponentsInChildren<Text>(true))
            t.raycastTarget = true;

        return clone;
    }

    private static void WireWorkshopSubmitActions(GameObject buttonGo)
    {
        var menuButton = buttonGo.GetComponent<MenuButton>();
        var pauseButton = buttonGo.GetComponent<PauseMenuButton>();
        var submitAnimator = menuButton != null
            ? menuButton.flashEffect
            : pauseButton != null
                ? pauseButton.flashEffect
                : null;

        // MenuButton has its own submit UnityEvent. Replacing the event object
        // also removes persistent callbacks copied from the original Options button.
        if (menuButton != null)
        {
            menuButton.OnSubmitPressed = new UnityEvent();
            menuButton.OnSubmitPressed.AddListener(
                () => QueueWorkshopOpen(submitAnimator));
        }

        // Some menu prefabs also carry a regular Button with a serialized onClick.
        // Remove the cloned Options callback rather than only its runtime listeners.
        foreach (var standardButton in buttonGo.GetComponentsInChildren<Button>(true))
        {
            standardButton.onClick = new Button.ButtonClickedEvent();
            standardButton.onClick.AddListener(
                () => QueueWorkshopOpen(submitAnimator));
        }

        // Keep selection/cancel EventTrigger entries used by MenuSelectable, but
        // remove direct submit/click callbacks inherited from the template.
        foreach (var eventTrigger in buttonGo.GetComponentsInChildren<EventTrigger>(true))
        {
            eventTrigger.triggers?.RemoveAll(entry =>
                entry.eventID == EventTriggerType.Submit
                || entry.eventID == EventTriggerType.PointerClick);
        }
    }

    private static void PlayPauseButtonSubmitFeedback(PauseMenuButton button)
    {
        try
        {
            if (button.flashEffect != null)
            {
                button.flashEffect.ResetTrigger("Flash");
                button.flashEffect.SetTrigger("Flash");
            }

            button.ForceDeselect();
            AccessTools.Method(typeof(MenuSelectable), "PlaySubmitSound")?.Invoke(button, null);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[MenuIntegration] 播放暂停菜单点击反馈失败: {e}");
        }
    }

    private static void SetWorkshopButtonText(GameObject buttonGo)
    {
        foreach (var t in buttonGo.GetComponentsInChildren<TMP_Text>(true))
            t.text = WorkshopTitle;
        foreach (var t in buttonGo.GetComponentsInChildren<Text>(true))
            t.text = WorkshopTitle;
    }

    private static void RegisterButtonWithMenuButtonList(Transform parent, GameObject buttonGo)
    {
        var menuButtonList = parent.GetComponent<MenuButtonList>();
        if (menuButtonList == null) return;

        var selectable = buttonGo.GetComponent<Selectable>();
        if (selectable == null) return;

        var listType = typeof(MenuButtonList);
        var entriesField = listType.GetField("entries",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (entriesField == null) return;

        var currentEntries = (Array?)entriesField.GetValue(menuButtonList);
        if (currentEntries == null) return;

        var entryType = listType.GetNestedType("Entry",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (entryType == null) return;

        var selectableField = entryType.GetField("selectable",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (selectableField == null) return;

        var forceEnableField = entryType.GetField("forceEnable",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        for (int i = 0; i < currentEntries.Length; i++)
        {
            var entry = currentEntries.GetValue(i);
            if (entry != null && selectableField.GetValue(entry) as Selectable == selectable)
                return;
        }

        var newEntry = Activator.CreateInstance(entryType);
        if (newEntry == null) return;
        selectableField.SetValue(newEntry, selectable);

        if (forceEnableField != null)
            forceEnableField.SetValue(newEntry, true);

        var newEntries = Array.CreateInstance(entryType, currentEntries.Length + 1);
        Array.Copy(currentEntries, newEntries, currentEntries.Length);
        newEntries.SetValue(newEntry, currentEntries.Length);

        entriesField.SetValue(menuButtonList, newEntries);

        // SetupActive rebuilds activeSelectables and the explicit wrap-around
        // up/down navigation for every registered entry.
        menuButtonList.SetupActive();

        Plugin.Log.LogInfo($"[MenuIntegration] 已注册到MenuButtonList, entries={newEntries.Length}");
    }
}
