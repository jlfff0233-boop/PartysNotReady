using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 씬 제어 - 배치 설정 전달, 준비, 시작과 종료
/// </summary>
public class EnemySceneController : MonoBehaviour
{
    [Header( "----- 공용 목적지 -----" )]
    [SerializeField] Transform _exitPoint;		//강아지와 곰의 공용 퇴장 지점
    [SerializeField] Transform _kidnapEscapePoint;		//여우의 공용 납치 완료 지점

    [Header( "----- 적 배치 -----" )]
    [SerializeField] BothersomeSceneSettings [ ] _bothersomes =
        new BothersomeSceneSettings [ 0 ];		//방해 적 배치
    [SerializeField] DemandSceneSettings [ ] _demands = 
        new DemandSceneSettings [ 0 ];		//요구 적 배치
    [SerializeField] StealSceneSettings [ ] _steals = 
        new StealSceneSettings [ 0 ];		//절도 적 배치
    [SerializeField] HarmSceneSettings [ ] _harms = 
        new HarmSceneSettings [ 0 ];		//공격 적 배치
    [SerializeField] KidnapSceneSettings [ ] _kidnaps = 
        new KidnapSceneSettings [ 0 ];		//납치 적 배치

    /// <summary>
    /// 배치 설정에 등록된 적 목록
    /// </summary>
    List<Enemy> _enemies = new( );

    /// <summary>
    /// 등록 검사를 통과한 적 목록
    /// </summary>
    public IReadOnlyList<Enemy> Enemies => _enemies;

    /// <summary>
    /// 종류별 씬 참조 전달과 적 초기화
    /// </summary>
    /// <param name="player">추적 대상</param>
    /// <param name="lookDir">시선 방향</param>
    /// <param name="routeGraph">공용 경로</param>
    /// <param name="stock">공급원 재고 중재자</param>
    /// <returns>전체 적 준비 성공 여부</returns>
    bool InitEnemies (
        Transform player, Transform lookDir, RouteGraph routeGraph,
        IngredientStockManager stock )
    {
        //방해 적에 배치 설정과 추적 대상, 시선 방향 전달
        foreach ( BothersomeSceneSettings settings in _bothersomes )
        {
            if ( settings.Enemy.Init( settings, player, lookDir, this ) == false )
                return false;
        }

        //요구 적에 배치 설정 전달
        foreach ( DemandSceneSettings settings in _demands )
        {
            if ( settings.Enemy.Init( settings, this ) == false )
                return false;
        }

        //절도 적에 배치 설정 전달
        foreach ( StealSceneSettings settings in _steals )
        {
            if ( settings.Enemy.Init( settings, stock ) == false )
                return false;
        }

        //공격 적에 배치 설정과 공용 경로 전달
        foreach ( HarmSceneSettings settings in _harms )
        {
            if ( settings.Enemy.Init( settings, routeGraph, this, stock ) == false )
                return false;
        }

        //납치 적에 배치 설정과 공용 경로 전달
        foreach ( KidnapSceneSettings settings in _kidnaps )
        {
            if ( settings.Enemy.Init( settings, routeGraph, this ) == false )
                return false;
        }

        return true;
    }

    /// <summary>
    /// 씬에 배치된 적의 설정과 모듈 준비
    /// </summary>
    /// <param name="player">추적 대상</param>
    /// <param name="lookDir">시선 방향</param>
    /// <param name="routeGraph">공용 경로</param>
    /// <param name="stock">공급원 재고 중재자</param>
    /// <returns>전체 적 준비 성공 여부</returns>
    public bool Init (
        Transform player, Transform lookDir, RouteGraph routeGraph,
        IngredientStockManager stock )
    {
        if ( RegisterEnemies( ) == false )
            return false;

        if ( InitEnemies( player, lookDir, routeGraph, stock ) == false )
        {
            Stop( );
            return false;
        }

        return true;
    }


    /// <summary>
    /// 같은 씬의 적을 중복 없이 등록
    /// </summary>
    /// <param name="enemy">등록할 적</param>
    /// <returns>등록 성공 여부</returns>
    bool Register ( Enemy enemy )
    {
        if ( enemy == null )
        {
            Debug.LogError( "[적 등록 실패] " +
                "배치 설정의 적을 연결해 주세요.", this );
            return false;
        }

        if ( enemy.gameObject.scene != gameObject.scene )
        {
            Debug.LogError( $"[적 등록 실패] " +
                $"{enemy.name}: 다른 씬의 적입니다.", enemy );
            return false;
        }

        if ( _enemies.Contains( enemy ) == true )
        {
            Debug.LogError( $"[적 등록 실패] " +
                $"{enemy.name}: 배치 설정이 중복되었습니다.", enemy );
            return false;
        }

        _enemies.Add( enemy );
        return true;
    }

    /// <summary>
    /// 배치 목록 수집과 씬의 미등록 적 확인
    /// </summary>
    /// <returns>등록 구성 유효 여부</returns>
    bool CollectEnemies ()
    {
        //방해 적 배치 등록
        foreach ( BothersomeSceneSettings settings in _bothersomes )
        {
            if ( Register( settings.Enemy ) == false )
                return false;
        }

        //요구 적 배치 등록
        foreach ( DemandSceneSettings settings in _demands )
        {
            if ( Register( settings.Enemy ) == false )
                return false;
        }

        //절도 적 배치 등록
        foreach ( StealSceneSettings settings in _steals )
        {
            if ( Register( settings.Enemy ) == false )
                return false;
        }

        //공격 적 배치 등록
        foreach ( HarmSceneSettings settings in _harms )
        {
            if ( Register( settings.Enemy ) == false )
                return false;
        }

        //납치 적 배치 등록
        foreach ( KidnapSceneSettings settings in _kidnaps )
        {
            if ( Register( settings.Enemy ) == false )
                return false;
        }

        //비활성 적도 배치 설정에서 누락되지 않았는지 확인
        Enemy [ ] found = FindObjectsByType<Enemy>(
            FindObjectsInactive.Include, FindObjectsSortMode.None );

        //현재 씬에서 배치 등록이 누락된 적 확인
        foreach ( Enemy enemy in found )
        {
            if ( enemy.gameObject.scene != gameObject.scene )
                continue;

            if ( _enemies.Contains( enemy ) == true )
                continue;

            Debug.LogError(
                $"[적 등록 실패] {enemy.name}: 배치 설정에 등록되지 않았습니다.", enemy );
            return false;
        }

        return true;
    }

    /// <summary>
    /// 배치 설정을 검사하고 적 목록 등록
    /// </summary>
    /// <returns>전체 적 등록 성공 여부</returns>
    public bool RegisterEnemies ()
    {
        _enemies.Clear( );

        if ( CollectEnemies( ) == true )
            return true;

        //일부만 등록된 목록은 사용하지 않음
        _enemies.Clear( );
        return false;
    }


    /// <summary>
    /// 직접 지정한 지점 또는 공용 지점의 위치 조회
    /// </summary>
    /// <param name="assigned">직접 지정한 지점</param>
    /// <param name="shared">공용 지점</param>
    /// <param name="position">조회한 위치</param>
    /// <returns>활성 지점 조회 성공 여부</returns>
    bool TryGetPoint (
        Transform assigned, Transform shared, out Vector3 position )
    {
        position = default;
        Transform point = ReferenceEquals( assigned, null ) == false ? assigned : shared;

        //직접 지정한 지점이 비활성이면 공용 지점으로 바꾸지 않음
        if ( point == null || point.gameObject.activeInHierarchy == false )
            return false;

        position = point.position;
        return true;
    }

    /// <summary>
    /// 퇴장 위치 조회
    /// </summary>
    /// <param name="assigned">개별 퇴장 지점</param>
    /// <param name="position">퇴장 위치</param>
    /// <returns>조회 성공 여부</returns>
    public bool TryGetExit ( Transform assigned, out Vector3 position )
    {
        return TryGetPoint( assigned, _exitPoint, out position );
    }

    /// <summary>
    /// 도주에 사용할 공용 입구 위치 조회
    /// </summary>
    /// <param name="position">입구 위치</param>
    /// <param name="reason">조회 실패 사유</param>
    /// <returns>입구 조회 성공 여부</returns>
    public bool TryGetEntrance ( out Vector3 position, out string reason )
    {
        position = default;
        reason = string.Empty;
        if ( _kidnapEscapePoint == null || _kidnapEscapePoint.gameObject.activeInHierarchy == false )
        {
            reason = "공용 입구 지점에 활성 지점을 연결해 주세요.";
            return false;
        }
        position = _kidnapEscapePoint.position;
        return true;
    }

    /// <summary>
    /// 납치 완료 위치 조회
    /// </summary>
    /// <param name="assigned">개별 도주 지점</param>
    /// <param name="position">납치 완료 위치</param>
    /// <param name="reason">조회 실패 사유</param>
    /// <returns>조회 성공 여부</returns>
    public bool TryGetKidnapEscape (
        Transform assigned, out Vector3 position, out string reason )
    {
        position = default;
        reason = string.Empty;
        bool hasAssigned = ReferenceEquals( assigned, null ) == false;
        Transform point = hasAssigned == true ? assigned : _kidnapEscapePoint;
        string source = hasAssigned == true ? "개별" : "공용";		//선택한 지점의 설정 위치

        if ( point == null )
            reason = ReferenceEquals( point, null ) == true
                ? $"{source} 납치 완료 지점이 연결되지 않았습니다."
                : $"{source} 납치 완료 지점이 파괴되었습니다.";
        else if ( point.gameObject.activeInHierarchy == false )
            reason = $"{source} 납치 완료 지점 {point.name}이 계층상 비활성 상태입니다.";
        else
        {
            position = point.position;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 현재 활동 방의 도주 위치 조회
    /// </summary>
    /// <param name="assigned">개별 도주 지점</param>
    /// <param name="room">현재 활동 방</param>
    /// <param name="position">도주 위치</param>
    /// <returns>조회 성공 여부</returns>
    public bool TryGetRoomEscape (
        Transform assigned, Room room, out Vector3 position )
    {
        Transform shared = room != null && room.isActiveAndEnabled == true
            ? room.HidePoint : null;
        return TryGetPoint( assigned, shared, out position );
    }

    /// <summary>
    /// 적 시작 허용 해제와 활동 정리
    /// </summary>
    public void Stop ()
    {
        foreach ( Enemy enemy in _enemies )
        {
            //씬 해제 중 먼저 파괴된 적은 제외
            if ( enemy != null )
                enemy.End( );
        }
    }


    /// <summary>
    /// 준비된 적에 플레이 시작 전달
    /// </summary>
    public void Begin ()
    {
        foreach ( Enemy enemy in _enemies )
            enemy.Begin( );
    }
}
