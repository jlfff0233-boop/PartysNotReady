using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 경로 그래프 편집 - 노드 등록, 연결 설정과 구성 검사
/// </summary>
[CustomEditor ( typeof ( RouteGraph ) )]
public class RouteGraphEditor : Editor
{
    string _report;
    readonly List<RouteNode> _issues = new ( );

    public override void OnInspectorGUI ( )
    {
        DrawDefaultInspector ( );

        RouteGraph graph = ( RouteGraph ) target;

        EditorGUILayout.Space ( );
        EditorGUILayout.HelpBox (
            "자식 등록은 기존 목록을 유지합니다.\n" +
            "선택 노드 연결은 Hierarchy에서 선택한 같은 그래프의 노드에 적용합니다.\n" +
            "검사는 설정 연결만 확인하며 실제 NavMesh 통행을 보장하지 않습니다." ,
            MessageType.Info );

        using ( new EditorGUI.DisabledScope ( Application.isPlaying ) )
        {
            if ( GUILayout.Button ( "자식 노드 등록" ) )
                RegisterChildren ( graph );

            if ( GUILayout.Button ( "선택 노드끼리 양방향 연결" ) )
                ConnectSelected ( graph , false );

            if ( GUILayout.Button ( "선택 노드의 역방향 연결 보완" ) )
                ConnectSelected ( graph , true );
        }

        if ( GUILayout.Button ( "등록 / 연결 검사" ) )
            InspectGraph ( graph );

        if ( string.IsNullOrEmpty ( _report ) == false )
            EditorGUILayout.HelpBox ( _report , MessageType.Info );

        foreach ( RouteNode node in _issues )
        {
            if ( node == null )
                continue;

            if ( GUILayout.Button ( $"확인: {node.name}" ) )
            {
                Selection.activeGameObject = node.gameObject;
                EditorGUIUtility.PingObject ( node );
            }
        }
    }


    #region ----- 직렬화 조회와 편집 -----

    /// <summary>
    /// 비활성 노드와 빈 참조를 포함한 원본 배열 조회
    /// </summary>
    /// <param name="owner">배열을 소유한 객체</param>
    /// <param name="field">직렬화 필드명</param>
    /// <returns>원본 순서의 노드 목록</returns>
    List<RouteNode> ReadNodes ( Object owner , string field )
    {
        SerializedObject data = new ( owner );
        data.Update ( );

        SerializedProperty array = data.FindProperty ( field );
        List<RouteNode> result = new ( );

        for ( int i = 0 ; i < array.arraySize ; i++ )
            result.Add (
                array.GetArrayElementAtIndex ( i ).objectReferenceValue
                as RouteNode );

        return result;
    }

    /// <summary>
    /// 기존 배열을 유지하면서 없는 노드만 추가
    /// </summary>
    /// <param name="owner">배열을 소유한 객체</param>
    /// <param name="field">직렬화 필드명</param>
    /// <param name="nodes">추가할 노드</param>
    /// <returns>추가한 참조 수</returns>
    int AppendNodes (
        Object owner , string field , IEnumerable<RouteNode> nodes )
    {
        SerializedObject data = new ( owner );
        data.Update ( );

        SerializedProperty array = data.FindProperty ( field );
        HashSet<RouteNode> existing = new ( ReadNodes ( owner , field ) );
        int added = 0;

        foreach ( RouteNode node in nodes )
        {
            if ( node == null || existing.Add ( node ) == false )
                continue;

            int index = array.arraySize;
            array.arraySize++;
            array.GetArrayElementAtIndex ( index ).objectReferenceValue = node;
            added++;
        }

        //SerializedProperty 편집으로 Undo와 프리팹 재정의 처리
        if ( added > 0 )
            data.ApplyModifiedProperties ( );

        return added;
    }

    /// <summary>
    /// 가장 가까운 상위 그래프 조회
    /// </summary>
    /// <param name="node">소속을 확인할 노드</param>
    /// <returns>계층상 소속 그래프, 없으면 null</returns>
    RouteGraph FindParentGraph ( RouteNode node )
    {
        Transform current = node.transform;

        while ( current != null )
        {
            if ( current.TryGetComponent ( out RouteGraph graph ) )
                return graph;

            current = current.parent;
        }

        return null;
    }

    #endregion

    #region ----- 노드 등록과 연결 -----

    /// <summary>
    /// 다른 하위 그래프를 제외하고 자식 노드 추가
    /// </summary>
    /// <param name="graph">등록할 그래프</param>
    void RegisterChildren ( RouteGraph graph )
    {
        List<RouteNode> candidates = new ( );

        foreach ( RouteNode node in
                 graph.GetComponentsInChildren<RouteNode> ( true ) )
        {
            if ( FindParentGraph ( node ) == graph )
                candidates.Add ( node );
        }

        int added = AppendNodes ( graph , "_nodes" , candidates );

        _issues.Clear ( );
        _report = $"자식 후보: {candidates.Count} / 새 등록: {added}\n" +
                  "기존 참조와 수동 등록 노드는 유지했습니다.";
    }

    /// <summary>
    /// 선택한 등록 노드의 연결 추가
    /// </summary>
    /// <param name="graph">편집할 그래프</param>
    /// <param name="reverseOnly">기존 연결의 역방향만 추가할지 여부</param>
    void ConnectSelected ( RouteGraph graph , bool reverseOnly )
    {
        HashSet<RouteNode> registered = new (
            ReadNodes ( graph , "_nodes" ) );
        HashSet<RouteNode> selected = new ( );

        foreach ( GameObject obj in Selection.gameObjects )
        {
            if ( obj.TryGetComponent ( out RouteNode node ) &&
                 registered.Contains ( node ) )
                selected.Add ( node );
        }

        _issues.Clear ( );

        if ( selected.Count < ( reverseOnly ? 1 : 2 ) )
        {
            _report = reverseOnly
                ? "이 그래프에 등록된 노드를 하나 이상 선택하세요."
                : "서로 연결할 등록 노드를 둘 이상 선택하세요.";
            return;
        }

        //여러 노드의 변경을 한 번의 Undo로 묶음
        Undo.IncrementCurrentGroup ( );
        int group = Undo.GetCurrentGroup ( );
        Undo.SetCurrentGroupName ( "경로 노드 연결 추가" );

        int added = 0;

        foreach ( RouteNode source in selected )
        {
            if ( reverseOnly )
            {
                //A의 기존 A→B 연결을 기준으로 B→A만 보완
                foreach ( RouteNode destination in
                         ReadNodes ( source , "_connections" ) )
                {
                    if ( destination == null || destination == source ||
                         registered.Contains ( destination ) == false )
                        continue;

                    added += AppendNodes (
                        destination , "_connections" ,
                        new RouteNode [ ] { source } );
                }
            }
            else
            {
                List<RouteNode> destinations = new ( selected );
                destinations.Remove ( source );

                added += AppendNodes (
                    source , "_connections" , destinations );
            }
        }

        Undo.CollapseUndoOperations ( group );
        SceneView.RepaintAll ( );
        _report = $"선택한 등록 노드: {selected.Count} / 추가 연결: {added}\n" +
                  "기존 연결은 유지했습니다.";
    }

    #endregion

    #region ----- 구성 검사 -----

    /// <summary>
    /// 검사 결과에 노드와 사유 추가
    /// </summary>
    /// <param name="lines">결과 문장 목록</param>
    /// <param name="node">관련 노드</param>
    /// <param name="reason">검사 내용</param>
    void AddIssue (
        List<string> lines , RouteNode node , string reason )
    {
        lines.Add ( $"{node.name}: {reason}" );

        if ( _issues.Contains ( node ) == false )
            _issues.Add ( node );
    }

    /// <summary>
    /// 원본 등록과 연결을 검사하고 분리된 그룹 확인
    /// </summary>
    /// <param name="graph">검사할 그래프</param>
    void InspectGraph ( RouteGraph graph )
    {
        _issues.Clear ( );

        List<string> lines = new ( );
        HashSet<RouteNode> nodes = new ( );

        foreach ( RouteNode node in ReadNodes ( graph , "_nodes" ) )
        {
            if ( node == null )
                lines.Add ( "노드 목록: 빈 참조" );
            else if ( nodes.Add ( node ) == false )
                AddIssue ( lines , node , "중복 등록" );
        }

        Dictionary<RouteNode , HashSet<RouteNode>> outgoing = new ( );
        Dictionary<RouteNode , HashSet<RouteNode>> neighbours = new ( );

        foreach ( RouteNode node in nodes )
        {
            outgoing.Add ( node , new HashSet<RouteNode> ( ) );
            neighbours.Add ( node , new HashSet<RouteNode> ( ) );

            if ( node.isActiveAndEnabled == false )
                AddIssue ( lines , node , "비활성: 실행 중 경로에서 제외" );
        }

        foreach ( RouteNode source in nodes )
        {
            foreach ( RouteNode destination in
                     ReadNodes ( source , "_connections" ) )
            {
                if ( destination == null )
                {
                    AddIssue ( lines , source , "연결에 빈 참조" );
                    continue;
                }

                if ( destination == source )
                {
                    AddIssue ( lines , source , "자기 자신 연결" );
                    continue;
                }

                if ( nodes.Contains ( destination ) == false )
                {
                    AddIssue (
                        lines , source ,
                        $"그래프 미등록 대상 연결: {destination.name}" );
                    continue;
                }

                if ( outgoing [ source ].Add ( destination ) == false )
                {
                    AddIssue (
                        lines , source ,
                        $"중복 연결: {destination.name}" );
                    continue;
                }

                //그룹 검사는 방향을 무시한 연결성만 확인
                neighbours [ source ].Add ( destination );
                neighbours [ destination ].Add ( source );
            }
        }

        foreach ( RouteNode source in nodes )
        {
            if ( neighbours [ source ].Count == 0 )
                AddIssue ( lines , source , "들어오거나 나가는 연결이 없는 고립 노드" );

            foreach ( RouteNode destination in outgoing [ source ] )
            {
                if ( outgoing [ destination ].Contains ( source ) == false )
                    AddIssue (
                        lines , source ,
                        $"단방향 정보: → {destination.name} (오류로 단정하지 않음)" );
            }
        }

        HashSet<RouteNode> visited = new ( );
        int groups = 0;

        foreach ( RouteNode root in nodes )
        {
            if ( visited.Add ( root ) == false )
                continue;

            groups++;
            Queue<RouteNode> queue = new ( );
            List<string> names = new ( );
            queue.Enqueue ( root );

            while ( queue.Count > 0 )
            {
                RouteNode node = queue.Dequeue ( );
                names.Add ( node.name );

                foreach ( RouteNode next in neighbours [ node ] )
                {
                    if ( visited.Add ( next ) )
                        queue.Enqueue ( next );
                }
            }

            lines.Add ( $"그룹 {groups}: {string.Join ( ", " , names )}" );
        }

        _report = $"고유 등록 노드: {nodes.Count} / 연결 그룹: {groups}\n" +
                  "그룹은 비활성 노드를 포함하고 방향을 무시한 설정 기준입니다.\n" +
                  "하나의 그룹이어도 양방향 도달이나 NavMesh 통행을 보장하지 않습니다.\n\n" +
                  string.Join ( "\n" , lines );
    }

    #endregion
}