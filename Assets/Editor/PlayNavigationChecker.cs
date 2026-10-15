using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

/// <summary>
/// 플레이 경로 검사 - 선택 에이전트 조건의 링크 양 끝과 노드 연결 조회
/// </summary>
public static class PlayNavigationChecker
{
    /// <summary>
    /// 같은 씬의 링크와 지정 그래프를 변경 없이 검사
    /// </summary>
    /// <param name="agent">검사 조건의 에이전트</param>
    /// <param name="graph">검사할 노드 그래프, 없으면 링크만 검사</param>
    /// <param name="sampleDistance">양 끝과 노드 주변 NavMesh 조회 거리</param>
    /// <param name="results">검사 결과 목록</param>
    public static void Check (
        NavMeshAgent agent, RouteGraph graph, float sampleDistance,
        List<AnomalyDebugDetail> results )
    {
        results.Clear( );
        if ( agent == null || agent.isActiveAndEnabled == false || agent.isOnNavMesh == false )
        {
            results.Add( new( "검사 불가", "활성 상태로 NavMesh 위에 배치된 Agent를 선택해 주세요." ) );
            return;
        }

        RoutePathChecker checker = new( agent );
        results.Add( new( "검사 조건", $"{agent.name} / 종류 {agent.agentTypeID} / 영역 {agent.areaMask} / " +
            $"끝점 조회 거리 {sampleDistance} / 자동 링크 통과 {agent.autoTraverseOffMeshLink}" ) );
        results.Add( new( "결과 해석", "양 끝의 완전 경로는 우회로를 사용할 수도 있습니다. 해당 링크의 실제 접속 / 통과 성공을 보장하지 않습니다." ) );

        List<NavMeshLink> links = new( );
        PlaySetupValidator.Collect( agent.gameObject.scene, links );
        results.Add( new( "링크 수", links.Count.ToString( ) ) );
        foreach ( NavMeshLink link in links )
        {
            //패키지의 끝점 계산과 동일하게 오브젝트 Scale은 사용하지 않음
            Vector3 start = link.startTransform != null ? link.startTransform.position :
                link.transform.position + link.transform.rotation * link.startPoint;
            Vector3 end = link.endTransform != null ? link.endTransform.position :
                link.transform.position + link.transform.rotation * link.endPoint;
            string label = $"링크 {link.name} / ID {link.GetInstanceID( )}";
            results.Add( new( label, $"실제 양 끝 {start:F3} → {end:F3} / 폭 {link.width}" ) );
            if ( link.isActiveAndEnabled == false || link.activated == false )
            {
                results.Add( new( label, "링크 비활성 / 다른 경로가 있어도 이 링크는 사용할 수 없음" ) );
                continue;
            }
            if ( link.agentTypeID != agent.agentTypeID ||
                 ( agent.areaMask & ( 1 << link.area ) ) == 0 || link.area == 1 )
            {
                results.Add( new( label, $"Agent 종류 또는 영역 불일치 / 링크 종류 {link.agentTypeID} / 영역 {link.area}" ) );
                continue;
            }

            CheckPair( label + " 정방향", start, end, checker, sampleDistance, results );
            if ( link.bidirectional == true )
                CheckPair( label + " 역방향", end, start, checker, sampleDistance, results );
            else
                results.Add( new( label, "단방향 설정 / 역방향 링크 통과 불가" ) );
        }

        if ( graph == null )
        {
            results.Add( new( "노드 그래프", "그래프 미지정 / 링크 양 끝 검사만 수행" ) );
            return;
        }
        if ( graph.gameObject.scene != agent.gameObject.scene || graph.isActiveAndEnabled == false )
        {
            results.Add( new( "노드 그래프", "Agent와 같은 씬의 활성 그래프를 지정해 주세요." ) );
            return;
        }

        List<RouteNode> nodes = new( );
        graph.GetNodes( nodes );
        results.Add( new( "노드 그래프", $"{graph.name} / 등록 칸 {graph.RegisteredCount} / 활성 고유 노드 {nodes.Count}" ) );
        foreach ( RouteNode node in nodes )
        {
            //일반 조회가 걸러내는 비활성 / 누락 연결도 설정 원본에서 확인
            using SerializedObject settings = new( node );
            SerializedProperty connections = settings.FindProperty( "_connections" );
            if ( connections.arraySize == 0 )
                results.Add( new( node.name, "설정된 출발 연결 없음" ) );
            for ( int i = 0; i < connections.arraySize; i++ )
            {
                RouteNode connected = connections.GetArrayElementAtIndex( i ).objectReferenceValue as RouteNode;
                string label = $"노드 {node.name} → {(connected != null ? connected.name : "없음")}";
                if ( connected == node || graph.Contains( connected ) == false )
                {
                    results.Add( new( label, "누락, 자기 자신, 그래프 밖 또는 비활성 연결" ) );
                    continue;
                }
                CheckPair( label, node.Position, connected.Position, checker, sampleDistance, results );
            }
        }
    }

    /// <summary>
    /// 양 끝 위치를 조회하고 완전 경로와 보정량 표시
    /// </summary>
    /// <param name="label">검사 구간 이름</param>
    /// <param name="start">원본 출발 위치</param>
    /// <param name="end">원본 도착 위치</param>
    /// <param name="checker">Agent 조건의 경로 검사기</param>
    /// <param name="sampleDistance">위치 조회 거리</param>
    /// <param name="results">검사 결과 목록</param>
    static void CheckPair (
        string label, Vector3 start, Vector3 end, RoutePathChecker checker,
        float sampleDistance, List<AnomalyDebugDetail> results )
    {
        bool hasStart = checker.TryGetPosition( start, sampleDistance, out Vector3 from );
        bool hasEnd = checker.TryGetPosition( end, sampleDistance, out Vector3 to );
        if ( hasStart == false || hasEnd == false )
        {
            results.Add( new( label, $"주변 NavMesh 없음 / 출발 {hasStart} {start:F3} / 도착 {hasEnd} {end:F3}" ) );
            return;
        }

        //주변 조회 성공과 원래 링크 끝점의 접속을 혼동하지 않도록 보정량도 표시
        string correction = $"보정 {Vector3.Distance( start, from ):F3} / {Vector3.Distance( end, to ):F3}";
        if ( checker.TryGetDistance( from, to, out float distance ) == false )
        {
            results.Add( new( label, $"{checker.FailureReason} / {correction} / {checker.FailureDetails}" ) );
            return;
        }
        results.Add( new( label, $"양 끝 완전 경로 / 길이 {distance:F2} / {correction}" ) );
    }
}
