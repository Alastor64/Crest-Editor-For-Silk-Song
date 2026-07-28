using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace SilksongHelper;

public sealed class CharmWorkshopUI : MonoBehaviour
{
    public bool IsVisible => _canvas != null && _canvas.gameObject.activeSelf;

    private Canvas? _canvas;
    private CanvasScaler? _scaler;
    private GraphicRaycaster? _raycaster;
    private GameObject? _root;
    private GameObject? _scrollContent;
    private ScrollRect? _scrollRect;

    private CustomCharm _work = new() { Name = "新建纹章" };
    private string _nameText = "新建纹章";
    private string _descriptionText = "";
    private string? _statusText;
    private InputField? _nameInput;
    private InputField? _descriptionInput;

    private Dictionary<CharmPart, (Text label, ScrollRect scroller, List<(Button btn, string crestId)> options)> _partRows = new();

    private Font? _gameFont;
    private float _prevTimeScale = 1f;
    private bool _didPause;
    private int _pauseCancelConsumedFrame = -1;

    private void Awake()
    {
        StartCoroutine(DeferredInit());
    }

    private System.Collections.IEnumerator DeferredInit()
    {
        yield return null;
        yield return null;
        BuildUI();
        Hide();
    }

    public void Show()
    {
        CrestCatalog.EnsureLoaded();
        if (_canvas == null) BuildUI();
        if (_canvas == null) return;

        _didPause = Time.timeScale > 0f;
        if (_didPause) _prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        RefreshAll();
        _canvas.gameObject.SetActive(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void Hide()
    {
        if (_didPause)
        {
            Time.timeScale = _prevTimeScale;
            _didPause = false;
        }
        if (_canvas != null) _canvas.gameObject.SetActive(false);
    }

    public void Toggle()
    {
        if (IsVisible) Hide();
        else Show();
    }

    internal bool TryConsumePauseMenuCancel()
    {
        if (IsVisible)
        {
            _pauseCancelConsumedFrame = Time.frameCount;
            Hide();
            return true;
        }

        // Script Update order is not guaranteed. If this component already
        // handled Escape this frame, do not let the same key press also close
        // the underlying pause menu.
        return _pauseCancelConsumedFrame == Time.frameCount;
    }

    private void Update()
    {
        if (!IsVisible) return;
        if (_didPause && Time.timeScale > 0f) Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            _pauseCancelConsumedFrame = Time.frameCount;
            Hide();
            return;
        }
    }

    private void BuildUI()
    {
        _gameFont = FindGameFont();
        var font = _gameFont ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        _root = new GameObject("CharmWorkshopRoot");
        _root.transform.SetParent(transform, false);
        _root.SetActive(false);

        _canvas = _root.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;

        _scaler = _root.AddComponent<CanvasScaler>();
        _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        _scaler.referenceResolution = new Vector2(1920, 1080);
        _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        _scaler.matchWidthOrHeight = 0.5f;

        _raycaster = _root.AddComponent<GraphicRaycaster>();

        var bgGo = new GameObject("Background", typeof(Image));
        bgGo.transform.SetParent(_root.transform, false);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        bgGo.GetComponent<Image>().color = new Color(0.07f, 0.07f, 0.09f, 1f);

        var titleGo = MakeText("title", _root.transform, "纹章工坊", font, 36, TextAnchor.MiddleCenter);
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0, 0.92f);
        titleRt.anchorMax = new Vector2(1, 0.98f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;

        var closeBtn = MakeButton("closeBtn", _root.transform, "关闭", font, 20);
        var closeRt = closeBtn.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(0.88f, 0.92f);
        closeRt.anchorMax = new Vector2(0.98f, 0.98f);
        closeRt.offsetMin = Vector2.zero;
        closeRt.offsetMax = Vector2.zero;
        closeBtn.onClick.AddListener(Hide);

        var scrollGo = new GameObject("ScrollRect", typeof(ScrollRect), typeof(Image), typeof(Mask));
        scrollGo.transform.SetParent(_root.transform, false);
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0.02f, 0.04f);
        scrollRt.anchorMax = new Vector2(0.98f, 0.90f);
        scrollRt.offsetMin = Vector2.zero;
        scrollRt.offsetMax = Vector2.zero;
        scrollGo.GetComponent<Image>().color = new Color(0, 0, 0, 0f);

        var viewport = new GameObject("Viewport", typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollGo.transform, false);
        var vpRt = viewport.GetComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.offsetMin = Vector2.zero;
        vpRt.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = new Color(0, 0, 0, 0f);

        _scrollContent = new GameObject("Content", typeof(ContentSizeFitter), typeof(VerticalLayoutGroup));
        _scrollContent.transform.SetParent(viewport.transform, false);
        var ctRt = _scrollContent.GetComponent<RectTransform>();
        ctRt.anchorMin = new Vector2(0, 1);
        ctRt.anchorMax = new Vector2(1, 1);
        ctRt.pivot = new Vector2(0.5f, 1);
        ctRt.offsetMin = Vector2.zero;
        ctRt.offsetMax = Vector2.zero;
        ctRt.sizeDelta = new Vector2(0, 800);

        var fitter = _scrollContent.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var layout = _scrollContent.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 16, 16);
        layout.spacing = 8;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        _scrollRect = scrollGo.GetComponent<ScrollRect>();
        _scrollRect.viewport = viewport.GetComponent<RectTransform>();
        _scrollRect.content = ctRt;
        _scrollRect.horizontal = false;
        _scrollRect.vertical = true;
        _scrollRect.scrollSensitivity = 20;

        BuildEditorRows(font);
    }

    private void BuildEditorRows(Font font)
    {
        if (_scrollContent == null) return;

        BuildNameBar(font);
        BuildSlotRow(font);

        for (int i = 0; i < CharmPartNames.NonSlotParts.Count; i++)
        {
            var part = CharmPartNames.NonSlotParts[i];
            BuildPartRow(part, font);
        }

        BuildSavedListRow(font);
    }

    private void BuildNameBar(Font font)
    {
        var bar = MakeRow("nameBar", _scrollContent!.transform, 150);
        var barLayout = bar.AddComponent<VerticalLayoutGroup>();
        barLayout.padding = new RectOffset(10, 10, 8, 8);
        barLayout.spacing = 6;
        barLayout.childControlWidth = true;
        barLayout.childControlHeight = true;
        barLayout.childForceExpandWidth = true;
        barLayout.childForceExpandHeight = false;
        bar.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.15f, 0.9f);

        var nameRow = MakeRow("nameRow", bar.transform, 52);
        var nameLayout = nameRow.AddComponent<HorizontalLayoutGroup>();
        nameLayout.spacing = 12;
        nameLayout.childControlWidth = true;
        nameLayout.childControlHeight = true;
        nameLayout.childForceExpandWidth = false;
        nameLayout.childForceExpandHeight = true;

        var nameLabel = MakeText("label", nameRow.transform, "名称：", font, 22, TextAnchor.MiddleLeft);
        var nameLabelLayout = nameLabel.gameObject.AddComponent<LayoutElement>();
        nameLabelLayout.preferredWidth = 80;
        nameLabelLayout.preferredHeight = 40;

        _nameInput = MakeInputField(
            "NameInput", nameRow.transform, font, 22, "输入纹章名称...", false);
        var nameInputLayout = _nameInput.gameObject.AddComponent<LayoutElement>();
        nameInputLayout.preferredWidth = 280;
        nameInputLayout.preferredHeight = 44;
        _nameInput.characterLimit = 24;
        _nameInput.text = _nameText;
        _nameInput.onValueChanged.AddListener(v =>
        {
            _nameText = v ?? "新建纹章";
            _work.Name = _nameText;
        });

        var statusTxt = MakeText("status", nameRow.transform, "组合未完成", font, 20, TextAnchor.MiddleLeft);
        statusTxt.name = "_statusText";
        statusTxt.color = Color.red;
        var statusLayout = statusTxt.gameObject.AddComponent<LayoutElement>();
        statusLayout.preferredWidth = 260;
        statusLayout.preferredHeight = 40;

        var spacer = new GameObject("spacer", typeof(RectTransform));
        spacer.transform.SetParent(nameRow.transform, false);
        spacer.AddComponent<LayoutElement>().flexibleWidth = 1;

        var saveBtn = MakeButton("saveBtn", nameRow.transform, "保存", font, 20);
        var saveLayout = saveBtn.gameObject.AddComponent<LayoutElement>();
        saveLayout.preferredWidth = 80;
        saveLayout.preferredHeight = 40;
        saveBtn.onClick.AddListener(() =>
        {
            PullEditorText();
            Plugin.SaveData.Upsert(_work);
            Plugin.SaveData.Save();
            CustomCrestRegistry.MarkDirty();
            _statusText = "已保存；重新装备后生效";
            UpdateStatusText();
        });

        var newBtn = MakeButton("newBtn", nameRow.transform, "新建", font, 20);
        var newLayout = newBtn.gameObject.AddComponent<LayoutElement>();
        newLayout.preferredWidth = 80;
        newLayout.preferredHeight = 40;
        newBtn.onClick.AddListener(() =>
        {
            _work = new CustomCharm { Name = "新建纹章" };
            _nameText = _work.Name;
            _descriptionText = _work.Description;
            _statusText = null;
            RefreshAll();
        });

        var descriptionRow = MakeRow("descriptionRow", bar.transform, 72);
        var descriptionLayout = descriptionRow.AddComponent<HorizontalLayoutGroup>();
        descriptionLayout.spacing = 12;
        descriptionLayout.childControlWidth = true;
        descriptionLayout.childControlHeight = true;
        descriptionLayout.childForceExpandWidth = false;
        descriptionLayout.childForceExpandHeight = true;

        var descriptionLabel = MakeText(
            "label", descriptionRow.transform, "描述：", font, 22, TextAnchor.UpperLeft);
        var descriptionLabelLayout = descriptionLabel.gameObject.AddComponent<LayoutElement>();
        descriptionLabelLayout.preferredWidth = 80;
        descriptionLabelLayout.preferredHeight = 64;

        _descriptionInput = MakeInputField(
            "DescriptionInput", descriptionRow.transform, font, 19, "输入纹章描述...", true);
        _descriptionInput.characterLimit = 180;
        _descriptionInput.text = _descriptionText;
        var descriptionLayoutElement = _descriptionInput.gameObject.AddComponent<LayoutElement>();
        descriptionLayoutElement.flexibleWidth = 1;
        descriptionLayoutElement.preferredHeight = 64;
        _descriptionInput.onValueChanged.AddListener(v =>
        {
            _descriptionText = v ?? "";
            _work.Description = _descriptionText;
        });
    }

    private static InputField MakeInputField(
        string name,
        Transform parent,
        Font font,
        int fontSize,
        string placeholderText,
        bool multiline)
    {
        var inputFieldGo = new GameObject(name, typeof(InputField), typeof(Image));
        inputFieldGo.transform.SetParent(parent, false);
        var inputField = inputFieldGo.GetComponent<InputField>();
        inputField.lineType = multiline
            ? InputField.LineType.MultiLineNewline
            : InputField.LineType.SingleLine;

        var textGo = new GameObject("Text", typeof(Text));
        textGo.transform.SetParent(inputFieldGo.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(8, 4);
        textRt.offsetMax = new Vector2(-8, -4);
        var textComp = textGo.GetComponent<Text>();
        textComp.font = font;
        textComp.fontSize = fontSize;
        textComp.color = Color.white;
        textComp.alignment = multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
        textComp.supportRichText = false;

        var placeholder = new GameObject("Placeholder", typeof(Text));
        placeholder.transform.SetParent(inputFieldGo.transform, false);
        var phRt = placeholder.GetComponent<RectTransform>();
        phRt.anchorMin = Vector2.zero;
        phRt.anchorMax = Vector2.one;
        phRt.offsetMin = new Vector2(8, 4);
        phRt.offsetMax = new Vector2(-8, -4);
        var phText = placeholder.GetComponent<Text>();
        phText.font = font;
        phText.fontSize = fontSize;
        phText.color = new Color(0.5f, 0.5f, 0.5f);
        phText.alignment = multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
        phText.text = placeholderText;
        phText.fontStyle = FontStyle.Italic;

        inputField.textComponent = textComp;
        inputField.placeholder = phText;
        inputField.image.color = new Color(0.2f, 0.2f, 0.25f);
        return inputField;
    }

    private void PullEditorText()
    {
        _nameText = _nameInput?.text ?? _nameText;
        _descriptionText = _descriptionInput?.text ?? _descriptionText;
        _work.Name = string.IsNullOrWhiteSpace(_nameText) ? "新建纹章" : _nameText;
        _work.Description = _descriptionText ?? "";
    }

    private void BuildSlotRow(Font font)
    {
        var row = MakePartRow("slotRow", _scrollContent!.transform, "插槽", font);
        var scroller = MakeHorizontalScroller(row.transform);
        var options = BuildCrestOptions(scroller.content.gameObject, CharmPart.Slot, font, crestId =>
        {
            _work.SlotCrestId = crestId;
            UpdateStatusText();
        });
        _partRows[CharmPart.Slot] = (row.transform.Find("label").GetComponent<Text>(), scroller, options);
    }

    private void BuildPartRow(CharmPart part, Font font)
    {
        var row = MakePartRow($"partRow_{part}", _scrollContent!.transform, CharmPartNames.Display(part), font);
        var scroller = MakeHorizontalScroller(row.transform);
        var options = BuildCrestOptions(scroller.content.gameObject, part, font, crestId =>
        {
            if (crestId == null) _work.PartCrestIds.Remove(part.ToString());
            else _work.PartCrestIds[part.ToString()] = crestId;
            UpdateStatusText();
        });
        _partRows[part] = (row.transform.Find("label").GetComponent<Text>(), scroller, options);
    }

    private void BuildSavedListRow(Font font)
    {
        var titleGo = MakeText("savedTitle", _scrollContent!.transform, "已保存的纹章", font, 24, TextAnchor.MiddleLeft);
        titleGo.GetComponent<LayoutElement>().minHeight = 36;

        var listGo = new GameObject("_savedList", typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        listGo.transform.SetParent(_scrollContent.transform, false);
        listGo.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.15f, 0.9f);
        var vlg = listGo.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.spacing = 4;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        listGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private GameObject MakePartRow(string name, Transform parent, string labelText, Font font)
    {
        var row = MakeRow(name, parent, 160);
        row.AddComponent<HorizontalLayoutGroup>().spacing = 6;
        row.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.13f, 0.9f);

        var label = MakeText("label", row.transform, labelText, font, 22, TextAnchor.MiddleLeft);
        label.gameObject.AddComponent<VerticalLayoutGroup>();
        var lt = label.GetComponent<RectTransform>();
        lt.sizeDelta = new Vector2(120, 150);

        return row;
    }

    private ScrollRect MakeHorizontalScroller(Transform parent)
    {
        var scrollGo = new GameObject("scroller", typeof(ScrollRect), typeof(Image), typeof(Mask));
        scrollGo.transform.SetParent(parent, false);
        scrollGo.AddComponent<LayoutElement>().flexibleWidth = 1;
        var srt = scrollGo.GetComponent<RectTransform>();
        srt.sizeDelta = new Vector2(0, 150);

        var img = scrollGo.GetComponent<Image>();
        img.color = new Color(0.08f, 0.08f, 0.1f, 0.5f);

        var viewport = new GameObject("Viewport", typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollGo.transform, false);
        var vpRt = viewport.GetComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.offsetMin = Vector2.zero;
        vpRt.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = new Color(0, 0, 0, 0f);

        var content = new GameObject("Content", typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var ctRt = content.GetComponent<RectTransform>();
        ctRt.anchorMin = new Vector2(0, 0);
        ctRt.anchorMax = new Vector2(0, 1);
        ctRt.pivot = new Vector2(0, 0.5f);
        ctRt.offsetMin = new Vector2(0, 4);
        ctRt.offsetMax = new Vector2(0, -4);
        ctRt.sizeDelta = new Vector2(200, 0);

        var hlg = content.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(4, 4, 0, 0);
        hlg.spacing = 6;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        content.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        var sr = scrollGo.GetComponent<ScrollRect>();
        sr.viewport = vpRt;
        sr.content = ctRt;
        sr.horizontal = true;
        sr.vertical = false;
        sr.scrollSensitivity = 30;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.inertia = true;
        sr.decelerationRate = 0.135f;

        return sr;
    }

    private List<(Button btn, string crestId)> BuildCrestOptions(GameObject container, CharmPart part, Font font, Action<string?> onSelect)
    {
        var list = new List<(Button, string)>();
        foreach (var opt in CrestCatalog.Options(part))
        {
            var crestId = opt.CrestId;
            var card = MakeCrestCard(crestId, opt.CrestName, opt.Preview.CurrentFrame, font, part, onSelect);
            card.transform.SetParent(container.transform, false);
            var btn = card.transform.Find("selectBtn")?.GetComponent<Button>();
            if (btn != null) list.Add((btn, crestId));
        }
        return list;
    }

    private GameObject MakeCrestCard(string crestId, string crestName, Texture2D preview, Font font, CharmPart part, Action<string?> onSelect)
    {
        var card = new GameObject("card_" + crestId, typeof(Image), typeof(VerticalLayoutGroup));
        card.AddComponent<LayoutElement>().preferredWidth = 80;
        var vlg = card.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(4, 4, 6, 6);
        vlg.spacing = 2;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        card.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.18f, 1f);

        var rawImgGo = new GameObject("previewImg", typeof(RawImage));
        rawImgGo.transform.SetParent(card.transform, false);
        rawImgGo.AddComponent<LayoutElement>().preferredHeight = 56;
        var rawImg = rawImgGo.GetComponent<RawImage>();
        rawImg.texture = preview;
        rawImg.GetComponent<RectTransform>().sizeDelta = new Vector2(56, 56);

        var nameTxt = MakeText("name", card.transform, crestName, font, 13, TextAnchor.MiddleCenter);
        nameTxt.GetComponent<RectTransform>().sizeDelta = new Vector2(70, 20);
        nameTxt.horizontalOverflow = HorizontalWrapMode.Wrap;

        var btn = MakeButton("selectBtn", card.transform, "选择", font, 14);
        btn.GetComponent<RectTransform>().sizeDelta = new Vector2(60, 28);
        var capturedId = crestId;
        btn.onClick.AddListener(() =>
        {
            bool isSelected = part == CharmPart.Slot
                ? CrestCatalog.AreEquivalent(part, _work.SlotCrestId, capturedId)
                : _work.PartCrestIds.TryGetValue(part.ToString(), out var cid)
                  && CrestCatalog.AreEquivalent(part, cid, capturedId);
            onSelect(isSelected ? null : capturedId);
            RefreshCardButton(btn, capturedId, part);
            RefreshAllCardButtons(part);
        });

        return card;
    }

    private void RefreshCardButton(Button btn, string crestId, CharmPart part)
    {
        var txt = btn.GetComponentInChildren<Text>();
        if (txt == null) return;
        bool isSelected = part == CharmPart.Slot
            ? CrestCatalog.AreEquivalent(part, _work.SlotCrestId, crestId)
            : _work.PartCrestIds.TryGetValue(part.ToString(), out var cid)
              && CrestCatalog.AreEquivalent(part, cid, crestId);
        txt.text = isSelected ? "已选" : "选择";
        var bgColor = isSelected ? new Color(0.25f, 0.55f, 0.25f) : new Color(0.2f, 0.2f, 0.25f);
        var btnImg = btn.GetComponent<Image>();
        if (btnImg != null) btnImg.color = bgColor;
    }

    private void RefreshAllCardButtons(CharmPart part)
    {
        if (!_partRows.TryGetValue(part, out var row)) return;
        foreach (var (btn, crestId) in row.options)
            RefreshCardButton(btn, crestId, part);
    }

    private void UpdateStatusText()
    {
        var statusGo = _scrollContent?.transform.Find("nameBar/nameRow/_statusText");
        var txt = statusGo?.GetComponent<Text>();
        if (txt == null) return;

        if (!string.IsNullOrEmpty(_statusText))
            txt.text = _statusText;
        else
            txt.text = _work.IsComplete ? "组合完整" : "组合未完成";
        txt.color = _work.IsComplete ? new Color(0.4f, 1f, 0.4f) : Color.red;
    }

    private void RefreshSavedList()
    {
        var listGo = _scrollContent?.transform.Find("_savedList");
        if (listGo == null) return;
        for (int i = listGo.childCount - 1; i >= 0; i--)
            Destroy(listGo.GetChild(i).gameObject);

        if (_gameFont == null) return;

        if (Plugin.SaveData.Charms.Count == 0)
        {
            var emptyTxt = MakeText("empty", listGo, "（暂无已保存的纹章）", _gameFont, 18, TextAnchor.MiddleLeft);
            emptyTxt.color = new Color(0.5f, 0.5f, 0.5f);
            return;
        }

        foreach (var charm in Plugin.SaveData.Charms)
        {
            var row = new GameObject("saved_" + charm.Id, typeof(HorizontalLayoutGroup), typeof(Image));
            row.transform.SetParent(listGo, false);
            row.AddComponent<LayoutElement>().minHeight = 44;
            row.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.18f, 1f);
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(8, 8, 4, 4);
            hlg.spacing = 10;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            var nameTxt = MakeText("name", row.transform, charm.Name, _gameFont, 20, TextAnchor.MiddleLeft);
            nameTxt.GetComponent<RectTransform>().sizeDelta = new Vector2(160, 36);

            var slotTxt = MakeText("slot", row.transform, $"{charm.SlotCount}槽", _gameFont, 16, TextAnchor.MiddleLeft);
            slotTxt.GetComponent<RectTransform>().sizeDelta = new Vector2(60, 36);

            var completeTxt = MakeText("complete", row.transform, charm.IsComplete ? "完整" : "未完成", _gameFont, 16, TextAnchor.MiddleLeft);
            completeTxt.GetComponent<RectTransform>().sizeDelta = new Vector2(80, 36);
            completeTxt.color = charm.IsComplete ? Color.green : Color.red;

            bool active = Plugin.Applier.ActiveCharmId == charm.Id;
            var equipTxt = MakeText("equip", row.transform, active ? "已装备" : "—", _gameFont, 16, TextAnchor.MiddleLeft);
            equipTxt.GetComponent<RectTransform>().sizeDelta = new Vector2(80, 36);

            var spacer = new GameObject("spacer", typeof(RectTransform));
            spacer.transform.SetParent(row.transform, false);
            spacer.AddComponent<LayoutElement>().flexibleWidth = 1;

            var loadBtn = MakeButton("loadBtn", row.transform, "加载", _gameFont, 16);
            loadBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(60, 32);
            loadBtn.onClick.AddListener(() =>
            {
                _work = charm.Clone();
                _nameText = _work.Name;
                _descriptionText = _work.Description;
                RefreshAll();
            });

            var delBtn = MakeButton("delBtn", row.transform, "删除", _gameFont, 16);
            delBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(60, 32);
            delBtn.onClick.AddListener(() =>
            {
                Plugin.SaveData.Delete(charm.Id);
                Plugin.SaveData.Save();
                CustomCrestRegistry.MarkDirty();
                RefreshSavedList();
            });
        }
    }

    private void RefreshAll()
    {
        if (_scrollContent == null) return;
        CrestCatalog.EnsureLoaded();
        _nameText = _work.Name ?? "新建纹章";
        _descriptionText = _work.Description ?? "";
        if (_nameInput != null && _nameInput.text != _nameText)
            _nameInput.text = _nameText;
        if (_descriptionInput != null && _descriptionInput.text != _descriptionText)
            _descriptionInput.text = _descriptionText;
        UpdateAllCardStates();
        UpdateStatusText();
        RefreshSavedList();
    }

    private void UpdateAllCardStates()
    {
        foreach (var kv in _partRows)
        {
            RefreshAllCardButtons(kv.Key);
        }
    }

    private static GameObject MakeRow(string name, Transform parent, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.AddComponent<LayoutElement>().preferredHeight = height;
        return go;
    }

    private static Text MakeText(string name, Transform parent, string text, Font font, int size, TextAnchor align)
    {
        var go = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);
        var txt = go.GetComponent<Text>();
        txt.font = font;
        txt.fontSize = size;
        txt.text = text;
        txt.color = Color.white;
        txt.alignment = align;
        txt.raycastTarget = false;
        return txt;
    }

    private static Button MakeButton(string name, Transform parent, string label, Font font, int size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f);

        var textGo = new GameObject("Text", typeof(Text));
        textGo.transform.SetParent(go.transform, false);
        var txt = textGo.GetComponent<Text>();
        txt.font = font;
        txt.fontSize = size;
        txt.text = label;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;
        var tRt = textGo.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        return go.GetComponent<Button>();
    }

    private static Font? FindGameFont()
    {
        try
        {
            var uiManagerType = Type.GetType("UIManager, Assembly-CSharp");
            if (uiManagerType == null) return null;
            var instanceProp = uiManagerType.GetProperty("instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            var ui = instanceProp?.GetValue(null);
            if (ui == null) return null;
            var field = uiManagerType.GetField("gameFont", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (field != null) return field.GetValue(ui) as Font;
            field = uiManagerType.GetField("font", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (field != null) return field.GetValue(ui) as Font;
            return null;
        }
        catch { return null; }
    }
}
