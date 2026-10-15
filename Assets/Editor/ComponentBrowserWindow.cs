using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 컴포넌트 타입과 방별로 씬 오브젝트를 탐색하는 편집기 창.
/// </summary>
public sealed class ComponentBrowserWindow : EditorWindow
{
    readonly ComponentBrowserIndex _index = new();
    readonly ComponentBrowserInspectorPanel _inspector = new();
    readonly Dictionary<Type, List<ComponentBrowserIndex.Row>> _groups = new();
    readonly List<Type> _types = new();
    readonly string[] _scopes = { "열린 모든 씬", "현재 활성 씬", "선택한 오브젝트와 자식", "현재 Prefab Mode" };
    GameObject[] _selection = Array.Empty<GameObject>();
    Type _type;
    GameObject _target;
    Room _room;
    int _scope, _roomMode;
    string _typeSearch = "", _objectSearch = "";
    bool _includeInactive = true, _autoRefresh = true, _dirty = true;
    double _nextRefresh;
    Vector2 _typeScroll, _objectScroll, _inspectorScroll;

    [MenuItem("Tools/Debug/컴포넌트 탐색기")]
    public static void Open()
    {
        GetWindow<ComponentBrowserWindow>("컴포넌트 탐색기").minSize = new Vector2(950f, 450f);
    }

    void OnEnable()
    {
        _selection = Selection.gameObjects;
        EditorApplication.hierarchyChanged += RequestRefresh;
        EditorApplication.playModeStateChanged += OnPlayMode;
        Undo.undoRedoPerformed += RequestRefresh;
        Selection.selectionChanged += OnSelection;
        EditorApplication.update += Tick;
        RequestRefresh();
    }

    void OnDisable()
    {
        EditorApplication.hierarchyChanged -= RequestRefresh;
        EditorApplication.playModeStateChanged -= OnPlayMode;
        Undo.undoRedoPerformed -= RequestRefresh;
        Selection.selectionChanged -= OnSelection;
        EditorApplication.update -= Tick;
        _inspector.Dispose();
    }

    void OnSelection()
    {
        // 내부 탐색과 Hierarchy 선택이 조회 범위를 흔들지 않도록 범위는 명시적으로 캡처한다.
        if (_scope != 2) RequestRefresh();
    }

    void OnPlayMode(PlayModeStateChange state)
    {
        _target = null;
        _inspector.Dispose();
        RequestRefresh();
    }

    void RequestRefresh() { _dirty = true; Repaint(); }

    void Tick()
    {
        if (EditorApplication.timeSinceStartup < _nextRefresh) return;
        _nextRefresh = EditorApplication.timeSinceStartup + 1;
        if (_autoRefresh) _dirty = true;
        Repaint();
    }

    void OnGUI()
    {
        if (_dirty && Event.current.type == EventType.Layout)
        {
            _index.Refresh(_scope, _selection);
            _dirty = false;
        }
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            EditorGUI.BeginChangeCheck();
            _scope = EditorGUILayout.Popup(_scope, _scopes, GUILayout.Width(175));
            if (EditorGUI.EndChangeCheck())
            {
                _selection = Selection.gameObjects;
                RequestRefresh();
            }
            if (_scope == 2 && GUILayout.Button("현재 선택으로 범위 지정", EditorStyles.toolbarButton))
            { _selection = Selection.gameObjects; RequestRefresh(); }
            DrawRoomFilter();
            _includeInactive = GUILayout.Toggle(_includeInactive, "비활성 포함", EditorStyles.toolbarButton);
            _autoRefresh = GUILayout.Toggle(_autoRefresh, "자동 갱신 (1초)", EditorStyles.toolbarButton);
            if (GUILayout.Button("새로고침", EditorStyles.toolbarButton)) RequestRefresh();
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            _typeSearch = EditorGUILayout.TextField("컴포넌트 검색", _typeSearch);
            _objectSearch = EditorGUILayout.TextField("이름 / 경로 검색", _objectSearch);
        }
        RebuildGroups();
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(230)))
            {
                GUILayout.Label($"컴포넌트 타입 ({_types.Count})", EditorStyles.boldLabel);
                _typeScroll = EditorGUILayout.BeginScrollView(_typeScroll);
                foreach (Type type in _types)
                {
                    if (!Matches(type.FullName, _typeSearch)) continue;
                    if (GUILayout.Toggle(_type == type, new GUIContent($"{type.Name}  ({_groups[type].Count})", type.AssemblyQualifiedName), "Button"))
                        _type = type;
                }
                EditorGUILayout.EndScrollView();
            }
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Max(280, position.width * .33f))))
            {
                GUILayout.Label("오브젝트", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(_type == null || !_groups.ContainsKey(_type)))
                    {
                        if (GUILayout.Button(new GUIContent("전체 선택", "현재 컴포넌트와 필터에 해당하는 오브젝트를 Hierarchy에서 모두 선택합니다.")))
                            SelectAllObjects();
                    }
                    if (GUILayout.Button(new GUIContent("전체 해제", "Hierarchy 선택과 탐색기 상세 선택을 모두 해제합니다.")))
                    {
                        Selection.objects = Array.Empty<UnityEngine.Object>();
                        _target = null;
                        _inspector.Dispose();
                    }
                }
                _objectScroll = EditorGUILayout.BeginScrollView(_objectScroll);
                bool visibleTarget = false;
                if (_type != null && _groups.TryGetValue(_type, out var rows))
                    foreach (var row in rows)
                    {
                        if (row.Target == null) continue;
                        if (_target == row.Target) visibleTarget = true;
                        string roomName = row.AmbiguousRoom ? "방 영역 겹침" : row.Room != null ? row.Room.name : "방 미지정";
                        string text = row.Target.name + (row.Target.activeInHierarchy ? "" : " [비활성]") +
                            "\n" + row.Target.scene.name + " / " + roomName + "\n" + row.Path;
                        if (GUILayout.Toggle(_target == row.Target, text, "Button")) _target = row.Target;
                    }
                if (!visibleTarget && Event.current.type == EventType.Layout) _target = null;
                if (_type == null || !_groups.ContainsKey(_type))
                    EditorGUILayout.HelpBox("조회할 컴포넌트 타입을 선택하세요.", MessageType.Info);
                EditorGUILayout.EndScrollView();
            }
            using (new EditorGUILayout.VerticalScope())
            {
                GUILayout.Label("선택 대상 Inspector", EditorStyles.boldLabel);
                _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll);
                _inspector.SetTarget(_target, _type);
                _inspector.Draw();
                EditorGUILayout.EndScrollView();
            }
        }
        GUILayout.Label("방: 같은 씬의 현재 위치 → 부모 Room 보완. 겹친 영역은 방 미지정에 표시. Inspector 변경은 기본 Inspector와 동일하게 적용됩니다.", EditorStyles.miniLabel);
    }

    void DrawRoomFilter()
    {
        var labels = new List<string> { "전체 방", "방 미지정 / 영역 겹침" };
        int selected = _roomMode == 2 ? 0 : _roomMode;
        foreach (Room room in _index.Rooms)
        {
            labels.Add(ComponentBrowserIndex.Label(room));
            if (_roomMode == 2 && _room == room) selected = labels.Count - 1;
        }
        if (_roomMode == 2 && selected == 0) { _roomMode = 0; _room = null; }
        int next = EditorGUILayout.Popup(selected, labels.ToArray(), GUILayout.MinWidth(160));
        if (next == selected) return;
        _roomMode = next < 2 ? next : 2;
        _room = next < 2 ? null : _index.Rooms[next - 2];
    }

    /// <summary>
    /// 현재 필터 결과만 Hierarchy의 다중 선택으로 전달한다.
    /// </summary>
    void SelectAllObjects()
    {
        if (_type == null || !_groups.TryGetValue(_type, out var rows)) return;
        var targets = new List<UnityEngine.Object>();
        foreach (var row in rows)
            if (row.Target != null) targets.Add(row.Target);
        Selection.objects = targets.ToArray();
        // 여러 대상은 Unity Inspector에서 일괄 편집하고, 내부 Inspector는 대표 대상을 표시한다.
        _target = targets.Count > 0 ? (GameObject)targets[0] : null;
    }

    void RebuildGroups()
    {
        _groups.Clear();
        _types.Clear();
        foreach (var row in _index.Rows)
        {
            if (row.Target == null || (!_includeInactive && !row.Target.activeInHierarchy) ||
                (_roomMode == 1 && row.Room != null) || (_roomMode == 2 && row.Room != _room) ||
                !Matches(row.Path + " " + row.Target.scene.name, _objectSearch)) continue;
            var seen = new HashSet<Type>();
            foreach (Component component in row.Components)
            {
                if (component == null || !seen.Add(component.GetType())) continue;
                Type type = component.GetType();
                if (!_groups.TryGetValue(type, out var rows))
                { rows = new List<ComponentBrowserIndex.Row>(); _groups.Add(type, rows); _types.Add(type); }
                rows.Add(row);
            }
        }
        _types.Sort((a, b) => string.Compare(a.FullName, b.FullName, StringComparison.OrdinalIgnoreCase));
    }

    static bool Matches(string text, string search) => string.IsNullOrEmpty(search) ||
        (text ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
}
