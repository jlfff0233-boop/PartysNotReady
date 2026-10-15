using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 풀 - 프리팹별 객체 생성, 대여, 반환과 종료
/// </summary>
public class Pool
{
    GameObject _prefab;		//생성할 프리팹
    Transform _storage;		//비활성 객체 보관 위치
    ObjectPool<GameObject> _objects;		//Unity 객체 풀

    int _maxSize;		//최대 보관 수량
    bool _isDisposed;		//전체 정리 여부

    /// <summary>
    /// 대여 객체와 해당 객체를 사용하는 씬 핸들
    /// </summary>
    Dictionary<Poolable, int> _rentals = new( );

#if UNITY_EDITOR
    string _debugReturnFailure;		//최근 반환 실패 사유

    /// <summary>
    /// 원본 프리팹
    /// </summary>
    public GameObject DebugPrefab => _prefab;

    /// <summary>
    /// 비활성 객체 보관 위치
    /// </summary>
    public Transform DebugStorage => _storage;

    /// <summary>
    /// 마지막 반환 실패 사유
    /// </summary>
    public string DebugReturnFailure => _debugReturnFailure;
#endif


    #region ----- 생성/보관 -----

    /// <summary>
    /// 비활성 객체 생성과 풀 연결
    /// </summary>
    /// <returns>새로 생성한 객체</returns>
    GameObject CreateItem ()
    {
        //비활성 부모 아래 생성하여 초기화 전 OnEnable 실행 방지
        GameObject item = Object.Instantiate(
            _prefab, _storage );

        item.SetActive( false );

        Poolable poolable = item.GetOrAddComponent<Poolable>( );
        poolable.Init( this );

#if UNITY_EDITOR
        PlayDebugHistory.RecordPool( "풀 생성", _prefab, $"객체 ID {item.GetInstanceID( )}" );
#endif
        return item;
    }

    /// <summary>
    /// 반환 객체 비활성화와 보관 위치 복귀
    /// </summary>
    /// <param name="item">반환한 객체</param>
    void StoreItem ( GameObject item )
    {
        item.SetActive( false );
        item.transform.SetParent( _storage, false );

        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;
        item.transform.localScale = _prefab.transform.localScale;
    }

    /// <summary>
    /// 풀 연결 해제와 객체 제거
    /// </summary>
    /// <param name="item">제거할 객체</param>
    void DestroyItem ( GameObject item )
    {
        if ( item == null )
            return;

        Poolable poolable = item.GetComponent<Poolable>( );

        _rentals.Remove( poolable );
        poolable.ClearPool( );

#if UNITY_EDITOR
        PlayDebugHistory.RecordPool( "풀 제거", _prefab, $"객체 ID {item.GetInstanceID( )}" );
#endif
        item.SetActive( false );
        Object.Destroy( item );
    }

    /// <summary>
    /// 별도 보관 객체와 Unity 객체 풀 생성
    /// </summary>
    /// <param name="prefab">생성할 프리팹</param>
    /// <param name="maxSize">최대 보관 수량</param>
    public Pool ( GameObject prefab, int maxSize )
    {
        _prefab = prefab;
        _maxSize = maxSize;

        GameObject storage = new( $"Pool_{prefab.name}" );
        storage.SetActive( false );
        Object.DontDestroyOnLoad( storage );

        _storage = storage.transform;

        _objects = new ObjectPool<GameObject>(
            createFunc: CreateItem,
            actionOnGet: null,
            actionOnRelease: StoreItem,
            actionOnDestroy: DestroyItem,
            collectionCheck: true,
            defaultCapacity: Mathf.Min( 10, maxSize ),
            maxSize: maxSize );
    }

    /// <summary>
    /// 파괴된 보관 객체를 제외하고 객체 확보
    /// </summary>
    /// <returns>사용 가능한 비활성 객체</returns>
    GameObject TakeItem ()
    {
        GameObject item;

        do
        {
            item = _objects.Get( );
        }
        while ( item == null );

        return item;
    }

    /// <summary>
    /// 목표 수량만큼 비활성 객체 확보
    /// </summary>
    /// <param name="count">확보할 보관 수량</param>
    public void PreparePool ( int count )
    {
        if ( count < 0 || count > _maxSize )
            throw new ArgumentOutOfRangeException( nameof( count ) );

        if ( _objects.CountInactive >= count )
            return;

        List<GameObject> items = new( count );

        try
        {
            //서로 다른 객체를 확보한 뒤 함께 반환
            for ( int i = 0; i < count; i++ )
                items.Add( TakeItem( ) );
        }
        finally
        {
            foreach ( GameObject item in items )
                _objects.Release( item );
        }
    }

    #endregion

    #region ----- 대여/반환 -----

    /// <summary>
    /// 비활성 객체를 사용 씬으로 이동하고 대여 등록
    /// </summary>
    /// <param name="scene">객체를 사용할 씬</param>
    /// <param name="parent">대여 객체의 부모 또는 씬 루트</param>
    /// <returns>초기화할 비활성 객체</returns>
    public GameObject Rent ( Scene scene, Transform parent )
    {
        GameObject item = TakeItem( );
        Poolable poolable = item.GetComponent<Poolable>( );

        _rentals.Add( poolable, scene.handle );

        //씬 이동은 루트 객체에서 수행
        item.transform.SetParent( null, false );

        if ( item.scene != scene )
            SceneManager.MoveGameObjectToScene( item, scene );

        item.transform.SetParent( parent, false );
#if UNITY_EDITOR
        PlayDebugHistory.RecordPool( "풀 대여", _prefab,
            $"객체 ID {item.GetInstanceID( )} / 사용 씬 {scene.name}" );
#endif
        return item;
    }

    /// <summary>
    /// 반환 준비 후 대여 객체 보관
    /// </summary>
    /// <param name="item">반환할 풀 객체</param>
    /// <param name="reason">반환 실패 사유</param>
    /// <returns>반환 성공 여부, 이미 반환한 객체는 성공</returns>
    public bool TryReturn ( Poolable item, out string reason )
    {
        reason = string.Empty;

        if ( _isDisposed == true )
        {
            reason = "이미 종료한 풀입니다.";
            return false;
        }

        if ( _rentals.ContainsKey( item ) == false )
            return true;

#if UNITY_EDITOR
        int returnedId = item.gameObject.GetInstanceID( );
#endif
        if ( item.TryPrepareReturn( out reason ) == false )
        {
#if UNITY_EDITOR
            string failure = $"객체 ID {returnedId} / {reason}";
            if ( DebugReturnFailure != failure )
                PlayDebugHistory.RecordPool( "풀 반환 실패", _prefab, failure );
            _debugReturnFailure = failure;
#endif
            return false;
        }

        //반환 준비 중 객체가 제거되거나 풀이 종료된 경우 보관하지 않음
        if ( item == null || _isDisposed == true )
        {
            reason = "반환 준비 중 객체 또는 풀이 종료되었습니다.";
            return false;
        }

        //비활성화 콜백보다 먼저 대여 기록 해제
        _rentals.Remove( item );
        _objects.Release( item.gameObject );
#if UNITY_EDITOR
        _debugReturnFailure = string.Empty;
        PlayDebugHistory.RecordPool( "풀 반환", _prefab, $"객체 ID {returnedId}" );
#endif
        return true;
    }

    /// <summary>
    /// 조건에 맞는 대여 객체 반환
    /// </summary>
    /// <param name="sceneHandle">대상 씬 핸들, null이면 전체</param>
    /// <param name="reason">첫 번째 반환 실패 사유</param>
    /// <returns>대상 객체 전체 반환 성공 여부</returns>
    bool TryReturnItems ( int? sceneHandle, out string reason )
    {
        reason = string.Empty;
        bool completed = true;

        //반환 중 원본 딕셔너리가 변경되므로 복사 목록 사용
        List<KeyValuePair<Poolable, int>> rentals = new( _rentals );

        foreach ( var entry in rentals )
        {
            if ( sceneHandle.HasValue == true &&
                 entry.Value != sceneHandle.Value )
                continue;

            Poolable item = entry.Key;

            if ( item == null )
            {
                _rentals.Remove( item );
                continue;
            }

            string itemName = item.name;

            if ( item.TryReturnToPool( out string itemReason ) == true )
                continue;

            if ( completed == true )
                reason = $"{itemName}: {itemReason}";

            completed = false;
        }

        return completed;
    }

    /// <summary>
    /// 지정 씬의 대여 객체 반환
    /// </summary>
    /// <param name="scene">객체를 사용한 씬</param>
    /// <param name="reason">첫 번째 반환 실패 사유</param>
    /// <returns>해당 씬 객체 전체 반환 성공 여부</returns>
    public bool TryReturnScene ( Scene scene, out string reason )
    {
        return TryReturnItems( scene.handle, out reason );
    }

    /// <summary>
    /// 전체 대여 객체 반환
    /// </summary>
    /// <param name="reason">첫 번째 반환 실패 사유</param>
    /// <returns>전체 반환 성공 여부</returns>
    public bool TryReturnAll ( out string reason )
    {
        return TryReturnItems( null, out reason );
    }

    #endregion

    #region ----- 종료 -----

    /// <summary>
    /// 외부에서 파괴된 객체의 대여 기록 해제
    /// </summary>
    /// <param name="item">파괴된 풀 객체</param>
    public void Clear ( Poolable item )
    {
        _rentals.Remove( item );
    }

    /// <summary>
    /// 이미 언로드된 씬의 미반환 객체와 기록 제거
    /// </summary>
    /// <param name="scene">언로드한 씬</param>
    public void DiscardScene ( Scene scene )
    {
        List<Poolable> items = new( );

        foreach ( var entry in _rentals )
        {
            if ( entry.Value == scene.handle )
                items.Add( entry.Key );
        }

        foreach ( Poolable item in items )
            _rentals.Remove( item );

        foreach ( Poolable item in items )
        {
            //정상 반환 시점을 놓친 객체는 재사용하지 않음
            if ( item != null )
                DestroyItem( item.gameObject );
        }
    }

    /// <summary>
    /// 대여 객체와 보관 객체 및 보관 위치 전체 제거
    /// </summary>
    public void Dispose ()
    {
        if ( _isDisposed == true )
            return;

        _isDisposed = true;

        List<Poolable> rentals = new( _rentals.Keys );
        _rentals.Clear( );

        foreach ( Poolable item in rentals )
        {
            if ( item != null )
                DestroyItem( item.gameObject );
        }

        _objects.Dispose( );

        if ( _storage != null )
            Object.Destroy( _storage.gameObject );
    }

    #endregion
#if UNITY_EDITOR
    /// <summary>
    /// 대여 객체와 보관 객체 목록 복사
    /// </summary>
    /// <param name="rented">대여 객체 목록</param>
    /// <param name="stored">보관 객체 목록</param>
    public void GetDebugObjects ( List<GameObject> rented, List<GameObject> stored )
    {
        rented.Clear( );
        stored.Clear( );

        foreach ( Poolable item in _rentals.Keys )
        {
            if ( item != null )
                rented.Add( item.gameObject );
        }

        if ( _storage == null )
            return;

        foreach ( Transform child in _storage )
            stored.Add( child.gameObject );
    }
#endif

}
