using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 풀링 관리 - 프리팹별 풀 조회, 대여와 일괄 반환, 전체 종료
/// </summary>
public class PoolingManager : MonoBehaviour, IGameService
{
    [Header( "----- 풀 설정 -----" )]
    [SerializeField, Min( 1 )] int _initialCount = 10;		//프리팹별 초기 보관 수량
    [SerializeField, Min( 1 )] int _maxPoolSize = 128;		//프리팹별 최대 보관 수량

    ResourceManager _resource;		//경로에 따른 프리팹 조회
    bool _isReady;		//풀링 서비스 준비 여부

    /// <summary>
    /// 원본 프리팹별 객체 풀
    /// </summary>
    Dictionary<GameObject, Pool> _pools = new( );

    #region ----- 초기화/종료 -----

    /// <summary>
    /// 리소스 관리자 연결과 풀 설정 확인, 씬 종료 알림 연결
    /// </summary>
    /// <param name="game">공통 게임 관리자</param>
    /// <returns>초기화 성공 여부</returns>
    public bool Init ( GameManager game )
    {
        if ( _initialCount < 1 || _maxPoolSize < _initialCount )
        {
            Debug.LogError(
                "[풀링 초기화 실패] 초기 수량은 1 이상, 최대 보관 수량은 초기 수량 이상이어야 합니다.", this );
            return false;
        }

        _resource = game.Resource;

        SceneManager.sceneUnloaded += HandleSceneUnloaded;
        _isReady = true;
        return true;
    }


    /// <summary>
    /// 언로드한 씬의 미반환 객체 정리
    /// </summary>
    /// <param name="scene">언로드한 씬</param>
    void HandleSceneUnloaded ( Scene scene )
    {
        List<Pool> pools = new( _pools.Values );

        foreach ( Pool pool in pools )
            pool.DiscardScene( scene );
    }


    /// <summary>
    /// 신규 요청 차단과 모든 풀 종료
    /// </summary>
    public void Shutdown ()
    {
        _isReady = false;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;

        List<Pool> pools = new( _pools.Values );
        _pools.Clear( );

        foreach ( Pool pool in pools )
            pool.Dispose( );

        _resource = null;
    }

    #endregion

    #region ----- 풀 조회/대여 -----

    /// <summary>
    /// 프리팹의 기존 풀을 조회하고 없으면 생성
    /// </summary>
    /// <param name="prefab">원본 프리팹</param>
    /// <returns>해당 프리팹의 풀</returns>
    Pool GetPool ( GameObject prefab )
    {
        if ( _isReady == false )
            throw new InvalidOperationException( "풀링 서비스가 준비되지 않았습니다." );

        if ( prefab == null )
            throw new ArgumentNullException( nameof( prefab ) );

        if ( _pools.TryGetValue( prefab, out Pool pool ) == true )
            return pool;

        pool = new Pool( prefab, _maxPoolSize );
        _pools.Add( prefab, pool );
        return pool;
    }

    /// <summary>
    /// 프리팹별 초기 보관 객체 확보
    /// </summary>
    /// <param name="prefab">원본 프리팹</param>
    /// <param name="count">확보할 보관 수량</param>
    public void PreparePool ( GameObject prefab, int count )
    {
        GetPool( prefab ).PreparePool( count );
    }

    /// <summary>
    /// 설정한 초기 수량만큼 비활성 객체 확보
    /// </summary>
    /// <param name="prefab">원본 프리팹</param>
    public void PreparePool ( GameObject prefab )
    {
        PreparePool( prefab, _initialCount );
    }

    /// <summary>
    /// 프리팹의 풀에서 비활성 객체 대여
    /// </summary>
    /// <param name="prefab">원본 프리팹</param>
    /// <param name="scene">객체를 사용할 씬</param>
    /// <param name="parent">대여 객체의 부모 또는 씬 루트</param>
    /// <returns>초기화할 비활성 객체</returns>
    public GameObject GetFromPool (
        GameObject prefab, Scene scene, Transform parent = null )
    {
        if ( scene.IsValid( ) == false || scene.isLoaded == false )
            throw new ArgumentException( "로드된 사용 씬이 필요합니다.", nameof( scene ) );

        if ( parent != null && parent.gameObject.scene != scene )
            throw new ArgumentException( "부모와 사용 씬이 다릅니다.", nameof( parent ) );

        return GetPool( prefab ).Rent( scene, parent );
    }

    /// <summary>
    /// 경로로 프리팹을 조회하여 비활성 객체 대여
    /// </summary>
    /// <param name="path">Resources 기준 확장자를 제외한 프리팹 경로</param>
    /// <param name="scene">객체를 사용할 씬</param>
    /// <param name="parent">대여 객체의 부모 또는 씬 루트</param>
    /// <returns>초기화할 비활성 객체, 프리팹을 찾지 못하면 null</returns>
    public GameObject GetFromPool (
        string path, Scene scene, Transform parent = null )
    {
        if ( _isReady == false )
            throw new InvalidOperationException( "풀링 서비스가 준비되지 않았습니다." );

        GameObject prefab = _resource.GetPrefab( path );

        if ( prefab == null )
            return null;

        //직접 참조 방식과 같은 프리팹 풀 사용
        return GetFromPool( prefab, scene, parent );
    }

    #endregion

    #region ----- 일괄 반환 -----

    /// <summary>
    /// 지정 씬 또는 전체 대여 객체 반환
    /// </summary>
    /// <param name="scene">대상 씬, null이면 전체</param>
    /// <returns>대상 객체 전체 반환 성공 여부</returns>
    bool ReturnPools ( Scene? scene )
    {
        if ( _isReady == false )
            return false;

        bool completed = true;
        List<Pool> pools = new( _pools.Values );

        foreach ( Pool pool in pools )
        {
            string reason;

            bool returned = scene.HasValue == true
                ? pool.TryReturnScene( scene.Value, out reason )
                : pool.TryReturnAll( out reason );

            if ( returned == true )
                continue;

            completed = false;
            Debug.LogWarning( $"[풀 일괄 반환 실패] {reason}", this );
        }

        return completed;
    }

    /// <summary>
    /// 씬에서 사용 중인 객체를 각자의 풀로 반환
    /// </summary>
    /// <param name="scene">객체를 사용한 씬</param>
    /// <returns>해당 씬 객체 전체 반환 성공 여부</returns>
    public bool AllReturnToPool ( Scene scene )
    {
        return ReturnPools( scene );
    }

    /// <summary>
    /// 모든 대여 객체를 각자의 풀로 반환
    /// </summary>
    /// <returns>전체 반환 성공 여부</returns>
    public bool AllReturnToPool ()
    {
        return ReturnPools( null );
    }

    #endregion
#if UNITY_EDITOR
    /// <summary>
    /// 현재 생성된 풀 목록 복사
    /// </summary>
    /// <param name="pools">조회 결과를 받을 목록</param>
    public void GetDebugPools ( List<Pool> pools )
    {
        pools.Clear( );
        pools.AddRange( _pools.Values );
    }
#endif

}
