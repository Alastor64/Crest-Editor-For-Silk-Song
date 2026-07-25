using System.Linq;
using UnityEngine;

namespace SilksongHelper;

public sealed class CharmEditor : MonoBehaviour
{
    private bool _visible;
    private bool _placed;
    private Rect _window = new Rect(40, 40, 880, 640);
    private Vector2 _contentScroll, _slotScroll;
    private readonly Vector2[] _partScroll = new Vector2[6];
    private Vector2 _previewScroll, _savedScroll;
    private CustomCharm _work = NewCharm();
    private string _nameBuf = "新建纹章";

    private const float Edge = 8f;
    private const float TitleH = 28f;
    private const float MinW = 640f, MinH = 480f;

    private enum ResizeEdge { None, N, S, E, W, NE, NW, SE, SW }
    private ResizeEdge _resizeEdge = ResizeEdge.None;
    private Vector2 _resizeAnchor;
    private Rect _resizeStartRect;

    private bool _isDragging;
    private Vector2 _dragStartMouse;
    private Vector2 _dragStartPos;

    private float _savedTimeScale = 1f;
    private bool _savedCursorVisible = true;
    private CursorLockMode _savedCursorLock;
    private bool _didPause;
    private object? _eventSystem;

    private GUIStyle? _bold, _small, _red;
    private GUIStyle Bold => _bold ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 18 };
    private GUIStyle Small => _small ??= new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
    private GUIStyle Red => _red ??= new GUIStyle(GUI.skin.label)
    {
        fontStyle = FontStyle.Bold,
        fontSize = 18,
        normal = { textColor = Color.red },
    };

    private static CustomCharm NewCharm() => new CustomCharm { Name = "新建纹章" };

    private float _lastToggle = -1f;

    private void OnGUI()
    {
        var e = Event.current;
        if (e != null && e.type == EventType.KeyDown && e.keyCode == Plugin.ToggleKey.Value)
        {
            float now = Time.realtimeSinceStartup;
            if (now - _lastToggle > 0.25f)
            {
                _visible = !_visible;
                _lastToggle = now;
                if (_visible) OpenEditor();
                else CloseEditor();
            }
        }
        if (!_visible)
            return;

        var oldDepth = GUI.depth;
        GUI.depth = -100;

        CrestCatalog.EnsureLoaded();
        if (e != null) HandleWindowInteraction(e);
        DrawCustomWindow();

        BlockGameInput(e);

        GUI.depth = oldDepth;
    }

    private void OpenEditor()
    {
        if (!_placed)
        {
            float w = Screen.width * 0.5f;
            float h = Screen.height * 0.5f;
            _window = new Rect(Screen.width * 0.25f, Screen.height * 0.15f, w, h);
            _placed = true;
        }
        CrestCatalog.EnsureLoaded();

        _didPause = Time.timeScale > 0f;
        if (_didPause) _savedTimeScale = Time.timeScale;
        _savedCursorVisible = Cursor.visible;
        _savedCursorLock = Cursor.lockState;

        Time.timeScale = 0f;

        var esType = System.Type.GetType("UnityEngine.EventSystems.EventSystem, UnityEngine.UI", throwOnError: false);
        if (esType != null)
        {
            _eventSystem = FindObjectOfType(esType);
            if (_eventSystem != null)
            {
                var prop = esType.GetProperty("enabled");
                if (prop != null) prop.SetValue(_eventSystem, false);
            }
        }
    }

    private void CloseEditor()
    {
        if (_didPause)
        {
            Time.timeScale = _savedTimeScale;
            _didPause = false;
        }
        Cursor.visible = _savedCursorVisible;
        Cursor.lockState = _savedCursorLock;

        if (_eventSystem != null)
        {
            var esType = System.Type.GetType("UnityEngine.EventSystems.EventSystem, UnityEngine.UI", throwOnError: false);
            if (esType != null)
            {
                var prop = esType.GetProperty("enabled");
                if (prop != null) prop.SetValue(_eventSystem, true);
            }
            _eventSystem = null;
        }
    }

    private void BlockGameInput(Event? e)
    {
        if (e == null) return;
        switch (e.type)
        {
            case EventType.Repaint:
            case EventType.Layout:
            case EventType.Ignore:
            case EventType.Used:
                return;
        }
        if (e.type == EventType.KeyDown || e.type == EventType.KeyUp)
        {
            if (e.keyCode == Plugin.ToggleKey.Value)
                return;
        }
        e.Use();
    }

    private void Update()
    {
        if (!_visible) return;

        if (_didPause && Time.timeScale > 0f)
            Time.timeScale = 0f;

        if (_eventSystem != null)
        {
            var esType = System.Type.GetType("UnityEngine.EventSystems.EventSystem, UnityEngine.UI", throwOnError: false);
            if (esType != null)
            {
                var prop = esType.GetProperty("enabled");
                if (prop != null)
                {
                    if ((bool)(prop.GetValue(_eventSystem) ?? false))
                        prop.SetValue(_eventSystem, false);
                }
            }
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void HandleWindowInteraction(Event e)
    {
        if (_resizeEdge != ResizeEdge.None)
        {
            if (e.type == EventType.MouseDrag)
            {
                ApplyResize(e.mousePosition);
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                _resizeEdge = ResizeEdge.None;
                e.Use();
            }
            return;
        }

        if (_isDragging)
        {
            if (e.type == EventType.MouseDrag)
            {
                _window.x = _dragStartPos.x + (e.mousePosition.x - _dragStartMouse.x);
                _window.y = _dragStartPos.y + (e.mousePosition.y - _dragStartMouse.y);
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                _isDragging = false;
                e.Use();
            }
            return;
        }

        if (e.type != EventType.MouseDown || e.button != 0)
            return;

        var local = e.mousePosition - _window.position;
        var titleRect = new Rect(0, Edge, _window.width, TitleH - Edge);
        if (titleRect.Contains(local))
        {
            _isDragging = true;
            _dragStartMouse = e.mousePosition;
            _dragStartPos = new Vector2(_window.x, _window.y);
            e.Use();
            return;
        }

        var edge = HitResize(local, _window.width, _window.height);
        if (edge != ResizeEdge.None)
        {
            _resizeEdge = edge;
            _resizeAnchor = e.mousePosition;
            _resizeStartRect = new Rect(_window);
            e.Use();
        }
    }

    private void ApplyResize(Vector2 screenMouse)
    {
        float dx = screenMouse.x - _resizeAnchor.x;
        float dy = screenMouse.y - _resizeAnchor.y;
        var r = new Rect(_resizeStartRect);
        var edge = _resizeEdge;

        bool left = edge is ResizeEdge.W or ResizeEdge.NW or ResizeEdge.SW;
        bool top = edge is ResizeEdge.N or ResizeEdge.NW or ResizeEdge.NE;
        bool right = edge is ResizeEdge.E or ResizeEdge.NE or ResizeEdge.SE;
        bool bottom = edge is ResizeEdge.S or ResizeEdge.SW or ResizeEdge.SE;

        if (right) r.width = _resizeStartRect.width + dx;
        if (bottom) r.height = _resizeStartRect.height + dy;
        if (left)
        {
            r.x = _resizeStartRect.x + dx;
            r.width = _resizeStartRect.width - dx;
        }
        if (top)
        {
            r.y = _resizeStartRect.y + dy;
            r.height = _resizeStartRect.height - dy;
        }

        if (r.width < MinW)
        {
            if (left) r.x = _resizeStartRect.x + (_resizeStartRect.width - MinW);
            r.width = MinW;
        }
        if (r.height < MinH)
        {
            if (top) r.y = _resizeStartRect.y + (_resizeStartRect.height - MinH);
            r.height = MinH;
        }

        _window = r;
    }

    private static ResizeEdge HitResize(Vector2 m, float w, float h)
    {
        if (m.x <= Edge && m.y <= Edge) return ResizeEdge.NW;
        if (m.x >= w - Edge && m.y <= Edge) return ResizeEdge.NE;
        if (m.x <= Edge && m.y >= h - Edge) return ResizeEdge.SW;
        if (m.x >= w - Edge && m.y >= h - Edge) return ResizeEdge.SE;
        if (m.y <= Edge) return ResizeEdge.N;
        if (m.y >= h - Edge) return ResizeEdge.S;
        if (m.x <= Edge) return ResizeEdge.W;
        if (m.x >= w - Edge) return ResizeEdge.E;
        return ResizeEdge.None;
    }

    private void DrawCustomWindow()
    {
        GUI.Box(_window, "", GUI.skin.window);

        var contentRect = new Rect(_window.x, _window.y + TitleH,
            _window.width, _window.height - TitleH);
        GUILayout.BeginArea(contentRect);
        try
        {
            using (var s = new GUILayout.ScrollViewScope(_contentScroll,
                GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true)))
            {
                _contentScroll = s.scrollPosition;
                DrawTopBar();
                DrawOptionRow(CharmPart.Slot, ref _slotScroll,
                    () => _work.SlotCrestId, v => _work.SlotCrestId = v);

                int si = 0;
                foreach (var part in CharmPartNames.NonSlotParts)
                {
                    var idx = si;
                    DrawOptionRow(part, ref _partScroll[idx],
                        () => _work.PartCrestIds.TryGetValue(part.ToString(), out var v) ? v : null,
                        v =>
                        {
                            if (v == null) _work.PartCrestIds.Remove(part.ToString());
                            else _work.PartCrestIds[part.ToString()] = v;
                        });
                    si++;
                }

                DrawPreview();
                GUILayout.Space(4);
                DrawSavedList();
            }
        }
        finally
        {
            GUILayout.EndArea();
        }

        GUI.Label(new Rect(_window.x + 12, _window.y + 4, _window.width - 20, 22),
            $"丝之歌助手 — 自定义纹章编辑器  ({_work.SlotCount}槽)", Bold);

        if (Event.current.type == EventType.Repaint)
            DrawResizeVisuals();
    }

    private void DrawResizeVisuals()
    {
        var w = _window.width;
        var h = _window.height;
        var x = _window.x;
        var y = _window.y;
        var dim = new Color(1f, 1f, 1f, 0.55f);
        var old = GUI.color;
        GUI.color = dim;
        GUI.Label(new Rect(x + w - 14, y + h - 14, 14, 14), "◢", Small);
        GUI.Label(new Rect(x + 2, y + 2, 10, 10), "◣", Small);
        GUI.Label(new Rect(x + w - 12, y + 2, 10, 10), "◢", Small);
        GUI.Label(new Rect(x + 2, y + h - 12, 10, 10), "◤", Small);
        GUI.color = old;
    }

    private void DrawTopBar()
    {
        using (new GUILayout.HorizontalScope("box"))
        {
            GUILayout.Label("名称", Bold, GUILayout.Width(48));
            _nameBuf = GUILayout.TextField(_nameBuf, 24, GUILayout.Width(160));
            GUILayout.Label(_work.IsComplete ? "组合完整" : "组合未完成", _work.IsComplete ? Bold : Red, GUILayout.Width(96));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("新建", GUILayout.Width(60)))
            {
                _work = NewCharm();
                _nameBuf = _work.Name;
            }
            if (GUILayout.Button("保存", GUILayout.Width(60)))
            {
                _work.Name = _nameBuf;
                Plugin.SaveData.Upsert(_work);
                Plugin.SaveData.Save();
                CustomCrestRegistry.MarkDirty();
                Plugin.Applier.ReapplyNow(_work);
            }
        }
    }

    private void DrawOptionRow(CharmPart part, ref Vector2 scroll,
        System.Func<string?> get, System.Action<string?> set)
    {
        using (new GUILayout.HorizontalScope("box"))
        {
            GUILayout.Label(CharmPartNames.Display(part), Bold, GUILayout.Width(110));
            using (var s = new GUILayout.ScrollViewScope(scroll, false, false, GUILayout.Height(150)))
            {
                scroll = s.scrollPosition;
                var current = get();
                using (new GUILayout.HorizontalScope())
                {
                    foreach (var opt in CrestCatalog.Options(part))
                    {
                        bool sel = opt.CrestId == current;
                        var old = GUI.color;
                        GUI.color = sel ? new Color(0.65f, 1f, 0.65f) : Color.white;
                        using (new GUILayout.VerticalScope("box", GUILayout.Width(72)))
                        {
                            if (GUILayout.Button(sel ? "已选" : "选择", GUILayout.Width(56)))
                                set(sel ? null : opt.CrestId);
                            GUILayout.Label(opt.Preview.CurrentFrame, GUILayout.Width(56), GUILayout.Height(56));
                            GUILayout.Label(opt.CrestName, Small, GUILayout.Width(56));
                        }
                        GUI.color = old;
                    }
                }
            }
        }
    }

    private void DrawPreview()
    {
        using (new GUILayout.HorizontalScope("box"))
        {
            GUILayout.Label("预览", Bold, GUILayout.Width(110));
            using (var s = new GUILayout.ScrollViewScope(_previewScroll, GUILayout.Height(80)))
            {
                _previewScroll = s.scrollPosition;
                foreach (var part in CharmPartNames.NonSlotParts)
                {
                    if (!_work.PartCrestIds.TryGetValue(part.ToString(), out var cid))
                        continue;
                    var crest = CrestCatalog.ById(cid);
                    if (crest == null) continue;
                    using (new GUILayout.VerticalScope("box", GUILayout.Width(64)))
                    {
                        GUILayout.Label(crest.Preview.CurrentFrame, GUILayout.Width(56), GUILayout.Height(56));
                        GUILayout.Label(crest.Name, Small, GUILayout.Width(56));
                    }
                }
            }
        }
    }

    private void DrawSavedList()
    {
        GUILayout.Label("已保存的纹章", Bold);
        using (var s = new GUILayout.ScrollViewScope(_savedScroll, GUILayout.Height(110), GUILayout.ExpandWidth(true)))
        {
            _savedScroll = s.scrollPosition;
            if (Plugin.SaveData.Charms.Count == 0)
            {
                GUILayout.Label("（暂无 — 保存一个纹章后会显示在此处。）", Small);
                return;
            }
            foreach (var c in Plugin.SaveData.Charms.ToList())
            {
                using (new GUILayout.HorizontalScope("box"))
                {
                    GUILayout.Label(c.Name, Bold, GUILayout.Width(160));
                    GUILayout.Label($"{c.SlotCount}槽", Small, GUILayout.Width(60));
                    GUILayout.Label(c.IsComplete ? "完整" : "未完成", Small, GUILayout.Width(60));
                    bool active = Plugin.Applier.ActiveCharmId == c.Id;
                    GUILayout.Label(active ? "已装备" : "—", active ? Bold : Small, GUILayout.Width(60));
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("加载", GUILayout.Width(60)))
                    {
                        _work = c.Clone();
                        _nameBuf = _work.Name;
                    }
                    if (GUILayout.Button("删除", GUILayout.Width(60)))
                    {
                        Plugin.SaveData.Delete(c.Id);
                        Plugin.SaveData.Save();
                        CustomCrestRegistry.MarkDirty();
                    }
                }
            }
        }
    }
}
