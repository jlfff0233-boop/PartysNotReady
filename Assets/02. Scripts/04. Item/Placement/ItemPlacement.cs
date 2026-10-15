using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아이템 배치 모듈 - 배치 상태 저장과 적용, 충돌 영역과 지지 표면 조회
/// </summary>
public class ItemPlacement
{
    Transform _root;
    Collider _collider;     //현재 배치 형상과 지지 표면 조회 기준
    Rigidbody _rigid;
    Object _context;        //기존 로그의 선택 대상
    Collider _placedSurface;        //마지막으로 내려놓은 표면
    Vector3 _placedLocalNormal;     //표면 회전을 따라가는 배치 법선


    /// <summary>
    /// 아이템 형상 조회와 지지 표면 검사 참조 연결
    /// </summary>
    /// <param name="root">아이템 루트</param>
    /// <param name="collider">아이템 충돌용 콜라이더</param>
    /// <param name="rigid">자기 충돌 제외 기준</param>
    /// <param name="context">로그 선택 대상</param>
    public ItemPlacement (
        Transform root, Collider collider, Rigidbody rigid, Object context )
    {
        _root = root;
        _collider = collider;
        _rigid = rigid;
        _context = context;
    }

    /// <summary>
    /// 배치와 지지 표면 조회에 사용할 대표 충돌체 연결
    /// </summary>
    /// <param name="collider">ItemView에서 검증한 대표 충돌체</param>
    public void SetCollider ( Collider collider )
    {
        _collider = collider;
    }

    /// <summary>
    /// 내려놓은 표면과 방향 저장
    /// </summary>
    /// <param name="data">완료한 배치 정보</param>
    void SetSurface ( PlacementData data )
    {
        _placedSurface = data.Target != null ? data.Target.SurfaceCollider : null;
        _placedLocalNormal = _placedSurface != null
            ? _placedSurface.transform.InverseTransformDirection( data.SurfaceNormal )
            : Vector3.zero;
    }

    #region ----- 배치 상태 -----

    /// <summary>
    /// 이전 배치 표면과 방향 기록 해제
    /// </summary>
    public void ClearPlacement ()
    {
        _placedSurface = null;
        _placedLocalNormal = Vector3.zero;
    }

    /// <summary>
    /// 아이템 배치 상태 저장
    /// </summary>
    /// <param name="state">저장한 배치 상태, 실패 시 기본값</param>
    /// <param name="reason">실패 사유, 성공 시 빈 문자열</param>
    /// <returns>배치 상태 저장 성공 여부</returns>
    public bool TrySavePlacementState (
        out ItemPlacementState state, out string reason )
    {
        state = default;
        reason = string.Empty;

        if ( _root == null || _root.gameObject.activeInHierarchy == false )
        {
            reason = "활성 아이템 루트가 필요합니다.";
            return false;
        }

        ItemPlacementState current =
            new ItemPlacementState( _root, _placedSurface, _placedLocalNormal );

        if ( ItemPlacementChecker.CheckPlacementState( _root, current, out reason ) == false )
            return false;

        state = current;
        return true;
    }

    /// <summary>
    /// 아이템 배치 상태 복구
    /// </summary>
    /// <param name="state">같은 아이템에서 저장한 배치 상태</param>
    /// <param name="reason">실패 사유, 성공 시 빈 문자열</param>
    /// <returns>배치 상태 복구 성공 여부</returns>
    public bool TryRestorePlacementState (
        ItemPlacementState state, out string reason )
    {
        if ( ItemPlacementChecker.CheckPlacementState( _root, state, out reason ) == false )
            return false;

        //호출부에서 물리를 정지한 상태로 복구
        //부모 복귀 후 저장한 로컬 배율과 월드 위치와 회전을 적용
        _root.SetParent( state.Parent, false );
        _root.localScale = state.LocalScale;
        _root.SetPositionAndRotation( state.Position, state.Rotation );

        //최초 씬 배치처럼 표면 기록이 없었던 상태도 그대로 복구
        _placedSurface = state.Surface;
        _placedLocalNormal = state.LocalNormal;
        Physics.SyncTransforms( );

        return true;
    }

    /// <summary>
    /// 배치 부모, 위치, 회전 적용
    /// </summary>
    /// <param name="data">배치 정보</param>
    public void Place ( PlacementData data )
    {
        SetSurface( data );
        //배치 후 부모와 월드 위치 적용
        _root.SetParent( data.Parent, true );
        _root.SetPositionAndRotation( data.Position, data.Rotation );
        Physics.SyncTransforms( );
    }

    #endregion

    #region ----- 형상 조회 -----

    /// <summary>
    /// 현재 콜라이더 영역 조회
    /// </summary>
    /// <returns>월드 기준 콜라이더 영역</returns>
    public Bounds GetBounds ()
    {
        //변경된 트랜스폼을 월드에 반영
        Physics.SyncTransforms( );
        return _collider.bounds;
    }

    /// <summary>
    /// 배치 회전 검사에 사용할 아이템 축 기준 크기 계산
    /// </summary>
    public Vector3 GetPlacementBoundsSize ()
    {
        Physics.SyncTransforms( );
        Quaternion inverseRotation = Quaternion.Inverse( _root.rotation );
        BoxCollider box = _collider as BoxCollider;
        Bounds worldBounds = _collider.bounds;
        Bounds bounds = default;

        //상자는 실제 꼭짓점을 사용하고 다른 콜라이더는 월드 Bounds로 보수적으로 계산
        for ( int i = 0; i < 8; i++ )
        {
            Vector3 sign = new Vector3(
                ( i & 1 ) == 0 ? -1f : 1f,
                ( i & 2 ) == 0 ? -1f : 1f,
                ( i & 4 ) == 0 ? -1f : 1f );
            Vector3 point = box != null
                ? box.transform.TransformPoint( box.center + Vector3.Scale( box.size * 0.5f, sign ) )
                : worldBounds.center + Vector3.Scale( worldBounds.extents, sign );
            Vector3 localPoint = inverseRotation * ( point - _root.position );

            if ( i == 0 )
                bounds = new Bounds( localPoint, Vector3.zero );
            else
                bounds.Encapsulate( localPoint );
        }

        return bounds.size;
    }

    /// <summary>
    /// 배치 검사에 사용할 아이템 충돌체 목록 조회
    /// </summary>
    /// <param name="colliders">조회 결과를 받을 목록, 기존 내용은 제거</param>
    /// <param name="reason">실패 사유, 성공 시 빈 문자열</param>
    /// <returns>배치에 사용할 충돌체 존재 여부</returns>
    public bool TryGetPlacementColliders (
        List<Collider> colliders, out string reason )
    {
        reason = string.Empty;

        if ( colliders == null )
        {
            reason = "충돌체 목록을 받을 공간이 필요합니다.";
            return false;
        }

        //이전 요청의 충돌체가 이번 검사에 섞이지 않도록 초기화
        colliders.Clear( );

        if ( _root == null || _root.gameObject.activeInHierarchy == false ||
             _rigid == null || _rigid.transform != _root )
        {
            reason = "활성 아이템 루트와 루트에 연결된 Rigidbody가 필요합니다.";
            return false;
        }

        //활성 자식 충돌체만 조회해 비활성 교체 모델은 제외
        _root.GetComponentsInChildren<Collider>( false, colliders );

        //목록을 뒤에서 정리하여 제거하지 않은 항목의 순서를 유지
        for ( int i = colliders.Count - 1; i >= 0; i-- )
        {
            Collider collider = colliders [ i ];

            if ( collider == null ||
                 collider.enabled == false ||
                 collider.gameObject.activeInHierarchy == false ||
                 collider.isTrigger == true ||
                 collider.attachedRigidbody != _rigid )
            {
                colliders.RemoveAt( i );
            }
        }

        if ( colliders.Count == 0 )
        {
            reason = "배치 검사에 사용할 활성 일반 충돌체가 없습니다.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 강제 이동 검사에 사용할 전체 충돌 영역 조회
    /// </summary>
    /// <param name="bounds">월드 기준 충돌 영역</param>
    /// <returns>유효한 충돌 영역 존재 여부</returns>
    public bool TryGetDisplacementBounds ( out Bounds bounds )
    {
        bounds = default;
        Physics.SyncTransforms( );

        Collider [ ] colliders = _root.GetComponentsInChildren<Collider>( );
        bool found = false;

        foreach ( Collider collider in colliders )
        {
            //콜라이더 확인
            if ( collider.enabled == false || collider.isTrigger == true || collider.attachedRigidbody != _rigid )
                continue;

            if ( found == false )
            {
                bounds = collider.bounds;
                found = true;
            }
            else
                //같은 리지드바디의 충돌 영역을 하나로 합산
                bounds.Encapsulate( collider.bounds );
        }

        return found == true &&
               bounds.size.x > 0f && bounds.size.y > 0f && bounds.size.z > 0f;
    }

    #endregion

    #region ----- 지지 표면 -----

    /// <summary>
    /// 현재 아이템을 지지하는 표면 확인
    /// </summary>
    /// <param name="direction">표면 검사 방향</param>
    /// <param name="checkGap">접촉 허용 간격</param>
    /// <param name="hit">가장 가까운 표면 정보</param>
    /// <param name="showLog">실패 원인 출력 여부</param>
    /// <returns>현재 지지 표면을 찾았는지 여부</returns>
    public bool TryGetSupport (
        Vector3 direction,
        float checkGap, out RaycastHit hit, bool showLog = true )
    {
        hit = default;
        Physics.SyncTransforms( );
        Bounds bounds = _collider.bounds;

        //비활성 또는 빈 충돌 영역은 지지 표면 조회에서 제외
        if ( _collider.enabled == false ||
             _collider.gameObject.activeInHierarchy == false ||
             _collider.isTrigger == true ||
             bounds.size.sqrMagnitude <= 0.0001f )
        {
            if ( showLog == true )
            {
                Debug.LogWarning(
                    $"[낙하 발생 불가] {_collider.name} 아이템 Collider 조건 확인 / " +
                    $"Collider 활성: {_collider.enabled} / 오브젝트 활성: {_collider.gameObject.activeInHierarchy} / " +
                    $"Trigger: {_collider.isTrigger} / 영역 크기 제곱: {bounds.size.sqrMagnitude} (0.0001 초과 필요)", _context );
            }
            return false;
        }

        if ( direction.sqrMagnitude <= 0.0001f )
        {
            return false;
        }

        direction.Normalize( );

        //아이템 중심에서 검사 방향의 외곽까지 거리 계산
        Vector3 axis = new Vector3(
            Mathf.Abs( direction.x ), Mathf.Abs( direction.y ), Mathf.Abs( direction.z ) );

        float checkDist = Vector3.Dot( bounds.extents, axis ) + checkGap;
        RaycastHit [ ] hits = Physics.RaycastAll(
            bounds.center, direction, checkDist,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore );

        float nearestDist = float.MaxValue;

        for ( int i = 0; i < hits.Length; i++ )
        {
            Collider candidate = hits [ i ].collider;

            //아이템 자신의 콜라이더는 제외
            if ( candidate.attachedRigidbody == _rigid )
                continue;

            if ( hits [ i ].distance >= nearestDist )
                continue;

            nearestDist = hits [ i ].distance;
            hit = hits [ i ];
        }

        //저장된 배치 표면과 무관하게 현재 가장 가까운 지지 표면 반환
        if ( hit.collider == null )
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 배치 방향에 따른 지지 표면 조회
    /// </summary>
    /// <param name="fallbackDirection">기본 표면 탐색 방향</param>
    /// <param name="rotationOffset">아이템 배치 방향 보정</param>
    /// <param name="checkGap">표면 탐색 추가 거리</param>
    /// <param name="hit">실제로 확인한 지지 표면</param>
    public bool TryGetPlacementSupport (
        Vector3 fallbackDirection, Quaternion rotationOffset,
        float checkGap, out RaycastHit hit )
    {
        hit = default;

        //고정 배치는 내려놓을 때 받은 방향을 우선 사용하되 실제 접촉도 재확인
        //낙하하거나 던진 물건은 예전 벽 방향을 이어 쓰지 않음
        if ( _rigid != null && _rigid.isKinematic == true )
        {
            if ( _placedSurface != null && _placedSurface.enabled == true &&
                 _placedSurface.gameObject.activeInHierarchy == true )
            {
                Vector3 normal = _placedSurface.transform.TransformDirection( _placedLocalNormal );
                if ( TryGetSupport( -normal, checkGap, out hit, false ) == true &&
                     hit.collider == _placedSurface )
                    return true;
            }

            //최초 씬 배치는 보정 회전을 제거한 아이템 방향으로 벽과 윗면 확인
            Vector3 direction = -( _root.rotation * Quaternion.Inverse( rotationOffset ) * Vector3.up );
            if ( TryGetSupport( direction, checkGap, out hit, false ) == true )
                return true;
        }

        return TryGetSupport( fallbackDirection, checkGap, out hit, false );
    }

    #endregion

}
