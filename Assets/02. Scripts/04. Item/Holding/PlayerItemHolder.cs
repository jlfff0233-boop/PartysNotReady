using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 플레이어가 완료한 아이템 행동
/// </summary>
public enum ItemActionType
{
    PickUp,     //집기와 공급원 획득
    Place,      //정상 배치
    Throw       //투척
}


/// <summary>
/// 플레이어 아이템 홀더 - 활성 아이템과 표시 지점 관리
/// </summary>
public class PlayerItemHolder : IDisposable
{
    Transform _root;        //기존 보유 지점
    ItemHolder _pending;        //획득 중 보유 모듈
    ItemHolder [ ] _holders = new ItemHolder [ 3 ];     //지점별 개별 보유
    Transform [ ] _points = new Transform [ 3 ];        //런타임 지점
    Bounds [ ] _bounds = new Bounds [ 3 ];      //표시 영역

    float _gap;     //표시 간격
    int _capacity = 1;      //보유 한도
    bool _executing;        //획득 처리 중
    bool _disposed;     //관리 종료

#if UNITY_EDITOR
    float _nextLayoutRecordTime;        //다음 보유 위치 기록 시각
#endif

    /// <summary>
    /// 획득 순서 목록
    /// </summary>
    List<int> _order = new( );

    /// <summary>
    /// 표시 영역 조회용 렌더러
    /// </summary>
    List<Renderer> _renderers = new( );


    /// <summary>
    /// 활성 보유 모듈
    /// </summary>
    public ItemHolder ActiveHolder =>
        _order.Count == 0 ? null : _holders [ _order [ _order.Count - 1 ] ];

    /// <summary>
    /// 활성 아이템
    /// </summary>
    public ICarryable HoldItem => ActiveHolder == null ? null : ActiveHolder.HoldItem;

    /// <summary>
    /// 보유 여부
    /// </summary>
    public bool HasItem => _order.Count > 0;

    /// <summary>
    /// 추가 획득 가능 여부
    /// </summary>
    public bool HasSpace =>
        _disposed == false && _executing == false &&
        _order.Count < _capacity && FindEmptySlot( ) >= 0;

    /// <summary>
    /// 실제 아이템 행동 성공 알림
    /// </summary>
    public event Action<ItemActionType> OnItemActionCompleted;

    /// <summary>
    /// 성공한 행동만 알리고 원래 결과 반환
    /// </summary>
    /// <param name="success">행동 성공 여부</param>
    /// <param name="action">완료한 행동</param>
    /// <returns>행동 성공 여부</returns>
    bool ReportAction ( bool success, ItemActionType action )
    {
        if ( success == true && _disposed == false )
            OnItemActionCompleted?.Invoke( action );

        return success;
    }

    /// <summary>
    /// 보유 지점 생성
    /// </summary>
    /// <param name="root">표시 기준</param>
    /// <param name="gap">표시 간격</param>
    public PlayerItemHolder ( Transform root, float gap )
    {
        _root = root;
        _gap = gap;

        for ( int i = 0; i < _points.Length; i++ )
        {
            Transform point = new GameObject( $"HoldPoint_{i}" ).transform;
            point.SetParent( root, false );
            _points [ i ] = point;
        }
    }

    /// <summary>
    /// 비어 있는 인덱스 조회
    /// </summary>
    /// <returns>인덱스</returns>
    int FindEmptySlot ()
    {
        for ( int i = 0; i < _points.Length; i++ )
        {
            if ( _holders [ i ] == null && _points [ i ] != null &&
                 _points [ i ].childCount == 0 )
                return i;
        }

        //비어 있지 않으면
        return -1;
    }

    /// <summary>
    /// 보유 한도 설정
    /// </summary>
    /// <param name="capacity">보유 한도</param>
    /// <returns>설정 여부</returns>
    public bool TrySetCapacity ( int capacity )
    {
        if ( _disposed == true || _executing == true ||
             _order.Count != 0 || capacity < 1 || capacity > 3 )
            return false;

        _capacity = capacity;
        return true;
    }

    /// <summary>
    /// 보유 종료 반영
    /// </summary>
    /// <param name="index">선택 인덱스</param>
    /// <param name="holder">종료한 보유 모듈</param>
    void HandleHoldEnded ( int index, ItemHolder holder )
    {
        if ( ReferenceEquals( _holders [ index ], holder ) == false )
            return;

        _holders [ index ] = null;
        _order.Remove( index );
    }

    /// <summary>
    /// 잔여 보유 해제와 빈 지점 회수
    /// </summary>
    void CollectPoints ()
    {
        if ( _executing == true )
            return;

        for ( int i = 0; i < _points.Length; i++ )
        {
            Transform point = _points [ i ];

            if ( point == null || _holders [ i ] != null )
                continue;

            for ( int j = point.childCount - 1; j >= 0; j-- )
            {
                Transform child = point.GetChild( j );

                if ( child.TryGetComponent( out ItemCarry item ) == false )
                    continue;

                //비활성 부모에서 분리해 아이템만 켜지는 상황 제외
                if ( child.gameObject.activeSelf == true &&
                     child.gameObject.activeInHierarchy == false )
                    continue;

                if ( item.CurrentType == HoldType.View )
                    item.TryRelease( );
                else if ( item.CurrentType == HoldType.None )
                    child.SetParent( null, true );
            }

            if ( _disposed == true && point.childCount == 0 )
            {
                _points [ i ] = null;
                Object.Destroy( point.gameObject );
            }
        }
    }

    /// <summary>
    /// 비어 있는 위치에 획득
    /// </summary>
    /// <param name="acquire">개별 획득 동작</param>
    /// <returns>등록 여부</returns>
    bool TryAcquire ( Func<ItemHolder, Transform, bool> acquire )
    {
        if ( HasSpace == false )
            return false;

        int index = FindEmptySlot( );
        ItemHolder holder = new( HoldType.View );
        bool registered = false;       //등록 성공 여부

        _executing = true;
        _pending = holder;

        try
        {
            if ( acquire( holder, _points [ index ] ) == false ||
                 _disposed == true || holder.HoldItem is not ItemCarry item ||
                 holder.IsHolding( item ) == false )
                return false;

            //획득마다 새 모듈을 사용해 이전 입력과 구분
            _holders [ index ] = holder;
            _order.Add( index );
            holder.OnHoldEnded += ended => HandleHoldEnded( index, holder );
            registered = true;
            return true;
        }
        finally
        {
            _pending = null;

            if ( registered == false )
                holder.Dispose( );

            _executing = false;
            CollectPoints( );
        }
    }

    /// <summary>
    /// 아이템 획득
    /// </summary>
    /// <param name="item">집을 아이템</param>
    /// <returns>획득 여부</returns>
    public bool TryPickUp ( ItemCarry item )
    {
        bool success = item != null &&
            TryAcquire( ( holder, point ) =>
                holder.TryPickUp( item, point ) ) == true;

        return ReportAction( success, ItemActionType.PickUp );
    }

    /// <summary>
    /// 공급원 재료 획득
    /// </summary>
    /// <param name="source">선택한 공급원</param>
    /// <returns>획득 여부</returns>
    public bool TryTake ( IngredientSource source )
    {
        bool success = source != null && source.CanTake == true &&
            TryAcquire( source.TryTake ) == true;

        return ReportAction( success, ItemActionType.PickUp );
    }


    /// <summary>
    /// 입력 대상 배치
    /// </summary>
    /// <param name="holder">입력 시작 보유 모듈</param>
    /// <param name="item">입력 시작 아이템</param>
    /// <param name="data">배치 정보</param>
    /// <returns>배치 여부</returns>
    public bool TryPutDown (
        ItemHolder holder, ICarryable item, PlacementData data )
    {
        bool success = IsActive( holder, item ) == true &&
            holder.TryPutDown( data ) == true;

        return ReportAction( success, ItemActionType.Place );
    }

    /// <summary>
    /// 활성 투척 기능 조회
    /// </summary>
    /// <returns>투척 기능 또는 null</returns>
    public IThrowable GetThrowTarget ()
    {
        return _disposed == true || ActiveHolder == null
            ? null : ActiveHolder.GetThrowTarget( );
    }

    /// <summary>
    /// 입력 대상 투척
    /// </summary>
    /// <param name="holder">입력 시작 보유 모듈</param>
    /// <param name="item">입력 시작 아이템</param>
    /// <param name="velocity">초기 속도</param>
    /// <param name="colliders">보유자 충돌체</param>
    /// <returns>투척 여부</returns>
    public bool TryThrow (
        ItemHolder holder, ICarryable item,
        Vector3 velocity, Collider [ ] colliders )
    {
        bool success = IsActive( holder, item ) == true &&
            holder.TryThrow( velocity, colliders ) == true;

        return ReportAction( success, ItemActionType.Throw );
    }

    /// <summary>
    /// 보유 관리 종료
    /// </summary>
    public void Dispose ()
    {
        if ( _disposed == false )
        {
            _disposed = true;
            OnItemActionCompleted = null;

            if ( _pending != null )
                _pending.Dispose( );

            for ( int i = 0; i < _holders.Length; i++ )
            {
                ItemHolder holder = _holders [ i ];
                if ( holder != null )
                    holder.Dispose( );

                _holders [ i ] = null;
            }

            _order.Clear( );
        }

        CollectPoints( );
    }

    /// <summary>
    /// 월드 영역 합산
    /// </summary>
    /// <param name="worldBounds">월드 영역</param>
    /// <param name="result">보유 좌표 영역</param>
    void EncapsulateBounds ( Bounds worldBounds, ref Bounds result )
    {
        for ( int i = 0; i < 8; i++ )
        {
            //하위 3비트로 X/Y/Z 부호를 정해 꼭짓점 8개 계산
            Vector3 sign = new(
                ( i & 1 ) == 0 ? -1f : 1f,
                ( i & 2 ) == 0 ? -1f : 1f,
                ( i & 4 ) == 0 ? -1f : 1f );

            Vector3 point = worldBounds.center + 
                Vector3.Scale( worldBounds.extents, sign );

            result.Encapsulate( _root.InverseTransformPoint( point ) );
        }
    }

    /// <summary>
    /// 표시 영역 조회
    /// </summary>
    /// <param name="item">보유 아이템</param>
    /// <returns>보유 좌표 영역</returns>
    Bounds GetDisplayBounds ( ItemCarry item )
    {
        ItemWorldController view = item.GetComponent<ItemWorldController>( );
        Vector3 center = item.transform.TransformPoint( view.LocalBoundsCenter );
        Bounds result = new( _root.InverseTransformPoint( center ), Vector3.zero );

        //아이템 축 기준 월드 반크기
        Vector3 halfSize = item.PlacementBoundsSize * 0.5f;

        for ( int i = 0; i < 8; i++ )
        {
            //하위 3비트로 X/Y/Z 부호를 정해 꼭짓점 8개 계산
            Vector3 sign = new(
                ( i & 1 ) == 0 ? -1f : 1f,
                ( i & 2 ) == 0 ? -1f : 1f,
                ( i & 4 ) == 0 ? -1f : 1f );

            Vector3 point = center +
                item.transform.rotation * Vector3.Scale( halfSize, sign );

            result.Encapsulate( _root.InverseTransformPoint( point ) );
        }

        _renderers.Clear( );
        item.GetComponentsInChildren( false, _renderers );

        foreach ( Renderer renderer in _renderers )
        {
            //보유 영역에는 모델 외형만 포함하고 연출 렌더러는 제외
            if ( renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer )
                continue;

            if ( renderer.enabled == false || renderer.forceRenderingOff == true )
                continue;

            Bounds bounds = renderer.bounds;
            if ( bounds.size.sqrMagnitude <= 0f )
                continue;

            EncapsulateBounds( bounds, ref result );
        }

        return result;
    }

    /// <summary>
    /// 보유 표시 배치
    /// </summary>
    void UpdateLayout ()
    {
        if ( _root == null || _executing == true || _order.Count == 0 )
            return;

        if ( _order.Count == 1 )
        {
            _points [ _order [ 0 ] ].localPosition = Vector3.zero;
            return;
        }

        float width = _gap * ( _order.Count - 1 );       //전체 폭

        foreach ( int index in _order )
        {
            ItemCarry item = _holders [ index ].HoldItem as ItemCarry;
            if ( item == null )
                return;

            _bounds [ index ] = GetDisplayBounds( item );
            width += _bounds [ index ].size.x;
        }

        float left = -width * 0.5f;       //다음 영역의 왼쪽 위치

        foreach ( int index in _order )
        {
            Bounds bounds = _bounds [ index ];
            Vector3 center = new( left + bounds.extents.x, 0f, 0f );

            _points [ index ].localPosition += center - bounds.center;
            left += bounds.size.x + _gap;
        }

#if UNITY_EDITOR
        RecordLayout( );
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// 다중 보유 위치와 표시 영역을 1초 간격으로 기록
    /// </summary>
    void RecordLayout ()
    {
        if ( Time.unscaledTime < _nextLayoutRecordTime )
            return;

        _nextLayoutRecordTime = Time.unscaledTime + 1f;

        foreach ( int index in _order )
        {
            ItemCarry item = _holders [ index ].HoldItem as ItemCarry;
            Transform point = _points [ index ];
            Bounds bounds = _bounds [ index ];

            PlayDebugHistory.Record(
                "보유 위치", item,
                $"보유 수: {_order.Count} / 지점: {point.name} / " +
                $"지점 로컬: {point.localPosition:F3} / 아이템 월드: {item.transform.position:F3} / " +
                $"아이템 로컬: {item.transform.localPosition:F3} / " +
                $"보정 전 영역 중심: {bounds.center:F3} / 크기: {bounds.size:F3}" );
        }
    }
#endif

    /// <summary>
    /// 해제 지점과 표시 갱신
    /// </summary>
    public void Tick ()
    {
        CollectPoints( );

        if ( _disposed == false )
            UpdateLayout( );
    }

    /// <summary>
    /// 활성 보유 확인
    /// </summary>
    /// <param name="holder">입력 시작 보유 모듈</param>
    /// <param name="item">입력 시작 아이템</param>
    /// <returns>활성 보유 일치 여부</returns>
    public bool IsActive ( ItemHolder holder, ICarryable item )
    {
        return _disposed == false && holder != null &&
               ReferenceEquals( ActiveHolder, holder ) == true &&
               holder.IsHolding( item ) == true;
    }

}
