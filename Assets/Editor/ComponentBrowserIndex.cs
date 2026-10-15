using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 오브젝트의 컴포넌트와 현재 방을 캐시한다.
/// </summary>
internal sealed class ComponentBrowserIndex
{
    internal sealed class Row
    {
        internal GameObject Target;
        internal string Path;
        internal Room Room;
        internal bool AmbiguousRoom;
        internal readonly List<Component> Components = new();
    }

    internal readonly List<Row> Rows = new();
    internal readonly List<Room> Rooms = new();

    internal void Refresh(int scope, GameObject[] selection)
    {
        Rows.Clear();
        Rooms.Clear();
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        var objects = new HashSet<GameObject>();
        // Resources 조회 후 로드된 씬만 남겨 에셋 및 Inspector 미리보기를 제외한다.
        foreach (GameObject target in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (target == null || EditorUtility.IsPersistent(target) || !target.scene.IsValid() ||
                !target.scene.isLoaded || (EditorSceneManager.IsPreviewScene(target.scene) &&
                (stage == null || target.scene != stage.scene)) ||
                (target.hideFlags & HideFlags.HideAndDontSave) != 0)
                continue;
            bool inPrefab = stage != null && target.scene == stage.scene;
            if (scope == 3 ? !inPrefab : inPrefab)
                continue;
            if (scope == 1 && target.scene != SceneManager.GetActiveScene())
                continue;
            if (scope == 2 && !IsUnderSelection(target.transform, selection))
                continue;
            objects.Add(target);
        }
        // 방 필터는 선택 범위 바깥의 방도 위치 판정에 사용한다.
        foreach (Room room in Resources.FindObjectsOfTypeAll<Room>())
        {
            if (room == null || EditorUtility.IsPersistent(room) || !room.gameObject.scene.IsValid() ||
                !room.gameObject.scene.isLoaded || (EditorSceneManager.IsPreviewScene(room.gameObject.scene) &&
                (stage == null || room.gameObject.scene != stage.scene)))
                continue;
            bool inPrefab = stage != null && room.gameObject.scene == stage.scene;
            if (scope == 3 ? inPrefab : !inPrefab)
                Rooms.Add(room);
        }
        Rooms.Sort((a, b) => string.Compare(Label(a), Label(b), StringComparison.OrdinalIgnoreCase));
        foreach (GameObject target in objects)
        {
            var row = new Row { Target = target, Path = GetPath(target.transform) };
            target.GetComponents(row.Components);
            // 게임의 Room.Contains와 같은 위치 기준. 겹친 영역은 임의 배정하지 않는다.
            foreach (Room room in Rooms)
            {
                if (room.gameObject.scene != target.scene || !room.Contains(target.transform.position))
                    continue;
                if (row.Room != null)
                {
                    row.Room = null;
                    row.AmbiguousRoom = true;
                    break;
                }
                row.Room = room;
            }
            if (row.Room == null && !row.AmbiguousRoom)
                row.Room = target.GetComponentInParent<Room>(true);
            Rows.Add(row);
        }
        Rows.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase));
    }

    static bool IsUnderSelection(Transform target, GameObject[] selection)
    {
        foreach (GameObject root in selection)
            if (root != null && (target == root.transform || target.IsChildOf(root.transform)))
                return true;
        return false;
    }

    internal static string Label(Room room) => room.gameObject.scene.name + " / " + GetPath(room.transform);

    static string GetPath(Transform target)
    {
        string path = target.name;
        while (target.parent != null)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }
        return path;
    }
}
