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
    private const int ResizeControlId = 0x5C1A;

    private enum ResizeEdge { None, N, S, E, W, NE, NW, SE, SW }
    private ResizeEdge _resizeEdge = ResizeEdge.None;
    private Vector2 _resizeAnchorScreen;
    private Rect _resizeStartRect;

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
                if (_visible)
                {
                    if (!_placed)
                    {
                        float w = Screen.width * 0.5f;
                        float h = Screen.height * 0.5f;
                        _window = new Rect(Screen.width * 0.25f, Screen.height * 0.15f, w, h);
                        _placed = true;
                    }
                    CrestCatalog.EnsureLoaded();
                }
            }
        }
        if (!_visible)
            return;

        HandleResize();
        _window = GUI.Window(GetInstanceID(), _window, DrawWindow, "");
    }

    private void HandleResize()
    {
        var e = Event.current;
        if (e == null) return;

        var hot = GUIUtility.hotControl;

        if (_resizeEdge == ResizeEdge.None)
        {
            if (e.type != EventType.MouseDown || e.button != 0)
                return;
            var local = e.mousePosition - _window.position;
            var edge = HitResize(local, _window.width, _window.height);
            if (edge == ResizeEdge.None)
                return;
            _resizeEdge = edge;
            _resizeAnchorScreen = e.mousePosition;
            _resizeStartRect = new Rect(_window);
            GUIUtility.hotControl = ResizeControlId;
            e.Use();
            return;
        }

        if (hot != ResizeControlId)
            return;

        if (e.type == EventType.MouseDrag)
        {
            ApplyResize(e.mousePosition);
            e.Use();
        }
        else if (e.type == EventType.MouseUp)
        {
            _resizeEdge = ResizeEdge.None;
            GUIUtility.hotControl = 0;
            e.Use();
        }
    }

    private void ApplyResize(Vector2 screenMouse)
    {
        float dx = screenMouse.x - _resizeAnchorScreen.x;
        float dy = screenMouse.y - _resizeAnchorScreen.y;
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

    private void DrawWindow(int id)
    {
        CrestCatalog.EnsureLoaded();

        GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(TitleH));
        GUI.Label(new Rect(12, 4, _window.width - 20, 22),
            $"丝之歌助手 — 自定义纹章编辑器  ({_work.SlotCount}槽)", Bold);

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

        DrawResizeVisuals();
        GUI.DragWindow(new Rect(0, Edge, _window.width, TitleH - Edge));
    }

    private void DrawResizeVisuals()
    {
        if (Event.current.type != EventType.Repaint)
            return;
        var w = _window.width;
        var h = _window.height;
        var dim = new Color(1f, 1f, 1f, 0.55f);
        var old = GUI.color;
        GUI.color = dim;
        GUI.Label(new Rect(w - 14, h - 14, 14, 14), "◢", Small);
        GUI.Label(new Rect(2, 2, 10, 10), "◣", Small);
        GUI.Label(new Rect(w - 12, 2, 10, 10), "◢", Small);
        GUI.Label(new Rect(2, h - 12, 10, 10), "◤", Small);
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
            using (var s = new GUILayout.ScrollViewScope(scroll, GUILayout.Height(110)))
            {
                scroll = s.scrollPosition;
                var current = get();
                foreach (var opt in CrestCatalog.Options(part))
                {
                    bool sel = opt.CrestId == current;
                    var old = GUI.color;
                    GUI.color = sel ? new Color(0.65f, 1f, 0.65f) : Color.white;
                    using (new GUILayout.VerticalScope("box", GUILayout.Width(72)))
                    {
                        GUILayout.Label(opt.Preview.CurrentFrame, GUILayout.Width(56), GUILayout.Height(56));
                        GUILayout.Label(opt.CrestName, Small, GUILayout.Width(56));
                        if (GUILayout.Button(sel ? "已选" : "选择", GUILayout.Width(56)))
                            set(sel ? null : opt.CrestId);
                    }
                    GUI.color = old;
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
