using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

/// <summary>
/// 방과 경로의 원본 연결 및 로드된 NavMesh 검사
/// </summary>
public static class PlayMapSetupChecker
{
    /// <summary>
    /// 원본 배열과 배치된 이동 목적지 검사
    /// </summary>
    /// <param name="context">검사 문맥</param>
    /// <param name="owner">맵 설정 소유자</param>
    public static void Check ( PlayReadinessContext context, Object owner )
    {
        if ( owner is Room room )
        {
            context.Optional( room, "맵", "_hidePoint" );
            using SerializedObject serialized = new( room );
            Vector3 size = serialized.FindProperty( "_size" ).vector3Value;
            Vector3 center = serialized.FindProperty( "_center" ).vector3Value;
            bool valid = ValidVector( size ) == true && ValidVector( center ) == true &&
                size.x > 0f && size.y > 0f && size.z > 0f &&
                room.transform.lossyScale.x != 0f && room.transform.lossyScale.y != 0f && room.transform.lossyScale.z != 0f;
            context.Report( room, "맵", "room-area", "_center / _size / Scale",
                valid == true ? PlayReadinessStatus.Passed : PlayReadinessStatus.Error,
                $"방 중심 {center}, 영역 {size}: 유한한 좌표, 양수 크기와 0이 아닌 Transform Scale 필요" );
            CheckArray( context, room, "_connectedRooms", false );
            Transform hide = serialized.FindProperty( "_hidePoint" ).objectReferenceValue as Transform;
            if ( hide != null )
                CheckPosition( context, room, "_hidePoint", hide.position );
        }
        if ( owner is RouteNode node )
        {
            CheckArray( context, node, "_connections", false );
            CheckPosition( context, node, "Transform.position", node.Position );
        }
        if ( owner is RouteGraph graph )
            CheckGraph( context, graph );
        if ( owner is EnemySceneController )
        {
            using SerializedObject serialized = new( owner );
            foreach ( SerializedProperty property in PlayReadinessContext.EnumerateProperties( serialized ) )
            {
                if ( property.propertyType == SerializedPropertyType.ObjectReference &&
                     property.objectReferenceValue is Transform point )
                    CheckPosition( context, owner, property.propertyPath, point.position );
            }
        }
    }

    /// <summary>
    /// 위치의 각 성분이 유한한지 확인
    /// </summary>
    /// <param name="value">검사 좌표</param>
    /// <returns>유한 여부</returns>
    static bool ValidVector ( Vector3 value )
    {
        return PlayDataSetupChecker.Finite( value.x ) == true &&
            PlayDataSetupChecker.Finite( value.y ) == true && PlayDataSetupChecker.Finite( value.z ) == true;
    }

    /// <summary>
    /// 누락·중복·자기 연결·비활성 원본 항목 검사
    /// </summary>
    /// <param name="context">검사 문맥</param>
    /// <param name="owner">배열 소유자</param>
    /// <param name="field">원본 배열 필드</param>
    /// <param name="required">비어 있을 수 없는 목록 여부</param>
    /// <returns>유효 참조 목록</returns>
    static List<Component> CheckArray ( PlayReadinessContext context, Component owner, string field, bool required )
    {
        List<Component> values = new( );
        using SerializedObject serialized = new( owner );
        SerializedProperty array = serialized.FindProperty( field );
        if ( required == true && array.arraySize == 0 )
            context.Report( owner, "맵", "empty-graph", field, PlayReadinessStatus.Error, "필수 노드 목록이 비어 있습니다." );
        for ( int i = 0; i < array.arraySize; i++ )
        {
            Component value = array.GetArrayElementAtIndex( i ).objectReferenceValue as Component;
            string issue = value == null ? "빈 연결" : value == owner ? "자기 연결" :
                values.Contains( value ) == true ? "중복 연결" :
                value is Behaviour behaviour && PlayReadinessContext.IsConfiguredActive( behaviour ) == false ? "비활성 설정의 연결" : string.Empty;
            context.Report( owner, "맵", "raw-link", $"{field}.Array.data[{i}]",
                issue.Length == 0 ? PlayReadinessStatus.Passed : PlayReadinessStatus.Warning,
                issue.Length == 0 ? $"방향 연결: {value.name}" : $"{issue}: 런타임 조회에서 제외될 수 있습니다.", value );
            if ( value != null && values.Contains( value ) == false )
                values.Add( value );
        }
        return values;
    }

    /// <summary>
    /// 선택 에이전트의 로드된 NavMesh 표본 검사
    /// </summary>
    /// <param name="context">검사 문맥</param>
    /// <param name="owner">위치 소유자</param>
    /// <param name="field">목적지 필드</param>
    /// <param name="position">검사 위치</param>
    static void CheckPosition ( PlayReadinessContext context, Object owner, string field, Vector3 position )
    {
        bool available = owner is Component component && context.CanCheckScene( component ) == true;
        NavMeshQueryFilter filter = new( ) { agentTypeID = context.AgentType, areaMask = NavMesh.AllAreas };
        bool valid = available == true && NavMesh.SamplePosition( position, out _, context.SampleRadius, filter ) == true;
        context.Report( owner, "맵", "nav-position", field,
            available == false ? PlayReadinessStatus.Blocked : valid == true ? PlayReadinessStatus.Passed : PlayReadinessStatus.Warning,
            available == false ? "프리팹/선택 자산 범위에서는 로드된 월드의 NavMesh를 판정하지 않습니다. 씬 범위에서 확인해 주세요."
                : $"AgentType {context.AgentType}, 반경 {context.SampleRadius}: " +
                  ( valid == true ? "NavMesh 표본 존재" : "표본 없음. 베이크·배치·에이전트 종류를 확인해 주세요." ) );
    }

    /// <summary>
    /// 등록 노드 밖 연결과 방향 연결의 도달 범위 검사
    /// </summary>
    /// <param name="context">검사 문맥</param>
    /// <param name="graph">경로 그래프</param>
    static void CheckGraph ( PlayReadinessContext context, RouteGraph graph )
    {
        List<Component> registered = CheckArray( context, graph, "_nodes", true );
        Dictionary<RouteNode, List<RouteNode>> edges = new( );
        foreach ( Component component in registered )
        {
            if ( component is RouteNode node == false || PlayReadinessContext.IsConfiguredActive( node ) == false )
                continue;
            List<RouteNode> links = new( );
            using SerializedObject serialized = new( node );
            SerializedProperty array = serialized.FindProperty( "_connections" );
            for ( int i = 0; i < array.arraySize; i++ )
            {
                RouteNode destination = array.GetArrayElementAtIndex( i ).objectReferenceValue as RouteNode;
                if ( destination == null || destination == node || PlayReadinessContext.IsConfiguredActive( destination ) == false )
                    continue;
                if ( registered.Contains( destination ) == false )
                    context.Report( node, "맵", "outside-graph", $"_connections.Array.data[{i}]",
                        PlayReadinessStatus.Error, $"{graph.name}._nodes에 등록되지 않은 노드로 연결됩니다.", destination );
                else if ( links.Contains( destination ) == false )
                {
                    links.Add( destination );
                    CheckPath( context, node, destination, i );
                }
            }
            edges [ node ] = links;
        }
        if ( edges.Count == 0 )
        {
            context.Report( graph, "맵", "graph-no-active-nodes", "_nodes", PlayReadinessStatus.Blocked,
                "활성 설정의 노드가 없어 도달 범위를 검사할 수 없습니다. 빈 항목과 활성 설정을 확인해 주세요." );
            return;
        }
        foreach ( KeyValuePair<RouteNode, List<RouteNode>> pair in edges )
        {
            if ( edges.Count <= 1 || pair.Value.Count > 0 )
                continue;
            bool incoming = false;
            foreach ( List<RouteNode> links in edges.Values )
            {
                if ( links.Contains( pair.Key ) == true )
                {
                    incoming = true;
                    break;
                }
            }
            if ( incoming == false )
                context.Report( pair.Key, "맵", "isolated-node", "_connections", PlayReadinessStatus.Warning,
                    $"{graph.name} 안에서 들어오거나 나가는 연결이 없는 고립 노드입니다.", graph );
        }
        //첫 활성 노드를 기준으로 방향성을 그대로 유지합니다. 역방향 연결을 강제하지 않습니다.
        RouteNode start = null;
        foreach ( RouteNode node in edges.Keys )
        {
            start = node;
            break;
        }
        HashSet<RouteNode> reached = new( );
        Queue<RouteNode> pending = new( );
        pending.Enqueue( start );
        while ( pending.Count > 0 )
        {
            RouteNode node = pending.Dequeue( );
            if ( reached.Add( node ) == false || edges.TryGetValue( node, out List<RouteNode> next ) == false )
                continue;
            foreach ( RouteNode destination in next )
                pending.Enqueue( destination );
        }
        context.Report( graph, "맵", "graph-reachability", "_nodes",
            reached.Count == edges.Count ? PlayReadinessStatus.Passed : PlayReadinessStatus.Warning,
            $"기준 {start.name}에서 방향 연결로 도달 {reached.Count}/{edges.Count}개. 분리 구역·일방통행이 의도된 구성인지 확인해 주세요.", start );
    }

    /// <summary>
    /// 선언된 노드 연결의 실제 NavMesh 경로 검사
    /// </summary>
    /// <param name="context">검사 문맥</param>
    /// <param name="source">시작 노드</param>
    /// <param name="destination">목표 노드</param>
    /// <param name="index">원본 연결 위치</param>
    static void CheckPath ( PlayReadinessContext context, RouteNode source, RouteNode destination, int index )
    {
        if ( context.CanCheckScene( source ) == false || context.CanCheckScene( destination ) == false )
            return;
        NavMeshQueryFilter filter = new( ) { agentTypeID = context.AgentType, areaMask = NavMesh.AllAreas };
        NavMeshPath path = new( );
        bool valid = NavMesh.SamplePosition( source.Position, out NavMeshHit from, context.SampleRadius, filter ) == true &&
            NavMesh.SamplePosition( destination.Position, out NavMeshHit to, context.SampleRadius, filter ) == true &&
            NavMesh.CalculatePath( from.position, to.position, filter, path ) == true && path.status == NavMeshPathStatus.PathComplete;
        context.Report( source, "맵", "nav-path", $"_connections.Array.data[{index}]",
            valid == true ? PlayReadinessStatus.Passed : PlayReadinessStatus.Warning,
            valid == true ? "선언된 방향 연결의 NavMesh 경로 완성" : "선언된 방향 연결의 NavMesh 경로가 완성되지 않습니다. 표본·베이크·분리 영역을 확인해 주세요.", destination );
    }
}
