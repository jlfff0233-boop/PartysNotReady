using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 선택 타입의 모든 컴포넌트에 기존 커스텀 Inspector를 제공한다.
/// </summary>
internal sealed class ComponentBrowserInspectorPanel : IDisposable
{
    readonly List<Editor> _editors = new();
    GameObject _target;
    Type _type;

    internal void SetTarget(GameObject target, Type type)
    {
        if (_target == target && _type == type &&
            (target == null || type == null || target.GetComponents(type).Length == _editors.Count) &&
            _editors.TrueForAll(e => e != null && e.target != null))
            return;
        Dispose();
        _target = target;
        _type = type;
        if (target == null || type == null)
            return;
        foreach (Component component in target.GetComponents(type))
            if (component != null && component.GetType() == type)
                _editors.Add(Editor.CreateEditor(component));
    }

    internal void Draw()
    {
        if (_target == null)
        {
            EditorGUILayout.HelpBox("목록에서 오브젝트를 선택하세요.", MessageType.Info);
            return;
        }
        EditorGUILayout.ObjectField("선택 대상", _target, typeof(GameObject), true);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Hierarchy 선택")) Selection.activeGameObject = _target;
            if (GUILayout.Button("Ping")) EditorGUIUtility.PingObject(_target);
            if (GUILayout.Button("포커스") && SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.Frame(new Bounds(_target.transform.position, Vector3.one * 2f), false);
        }
        foreach (Editor editor in _editors)
        {
            if (editor == null || editor.target == null) continue;
            EditorGUILayout.Space();
            editor.DrawHeader();
            editor.OnInspectorGUI();
        }
    }

    public void Dispose()
    {
        foreach (Editor editor in _editors)
            if (editor != null) UnityEngine.Object.DestroyImmediate(editor);
        _editors.Clear();
        _target = null;
        _type = null;
    }
}
