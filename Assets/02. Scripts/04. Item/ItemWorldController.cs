using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아이템 월드 제어 - 보유, 물리와 배치 상태 적용
/// </summary>
[RequireComponent( typeof( Rigidbody ) )]
public class ItemWorldController : MonoBehaviour
{
    [SerializeField] Collider _collider;        //아이템 충돌용 콜라이더

    ItemHold _hold;     //보유 레이어와 충돌 설정
    ItemPhysics _physics;       //리지드바디와 투척 물리
    ItemPlacement _placement;       //배치 상태 적용과 형상, 지지 표면 조회

    Vector3 _localBoundsCenter;     //보유 전 콜라이더 로컬 중심

    /// <summary>
    /// 보유 전 콜라이더 로컬 중심
    /// </summary>
    public Vector3 LocalBoundsCenter => _localBoundsCenter;
    /// <summary>
    /// 현재 월드 중심
    /// </summary>
    public Vector3 ThrowPosition => transform.TransformPoint( _localBoundsCenter );
    /// <summary>
    /// 콜라이더 활성화 여부
    /// </summary>
    public bool IsColliderEnabled => IsReady && _collider.enabled;
    /// <summary>
    /// 배치 형상을 계산할 주 충돌체
    /// </summary>
    public Collider PlacementCollider => _collider;
    /// <summary>
    /// 배치와 물리 적용에 필요한 구성 준비 여부
    /// </summary>
    public bool IsReady => _collider != null && GetComponent<Rigidbody>( ) != null &&
        _hold != null && _physics != null && _placement != null;
    /// <summary>
    /// 접촉면 방향 전달 이벤트(접촉 방향)
    /// </summary>
    public event Action<Vector3> OnContact;


    /// <summary>
    /// 보유, 물리와 배치 모듈 최초 초기화
    /// </summary>
    public void Init ()
    {
        //재호출로 현재 외형 연결과 보유, 투척 복구 상태를 변경하지 않음
        if ( _hold != null && _physics != null && _placement != null )
            return;

        Rigidbody rigid = GetComponent<Rigidbody>( );

        _collider = GetCollider( );

        //재호출해도 보유와 투척 복구 정보를 초기화하지 않음
        _hold ??= new ItemHold( transform );
        _physics ??= new ItemPhysics( rigid, _collider );
        _placement ??= new ItemPlacement( transform, _collider, rigid, this );

        UpdateAppearanceBinding( );
    }

    /// <summary>
    /// 접촉면 방향 전달
    /// </summary>
    /// <param name="collision">충돌 정보</param>
    private void OnCollisionStay ( Collision collision )
    {
        //투척 감속 판정을 위해 접촉면 방향을 각각 전달
        for ( int i = 0; i < collision.contactCount; i++ )
            OnContact?.Invoke( collision.GetContact( i ).normal );
    }


    /// <summary>
    /// 교체 모델의 대표 충돌체 연결
    /// </summary>
    /// <param name="collider">활성화한 교체 모델의 대표 충돌체</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>참조 변경 성공 여부</returns>
    public bool TrySetAppearanceCollider (
        Collider collider, out string reason )
    {
        reason = string.Empty;

        //기존 모듈과 저장된 물리 상태는 유지
        Init( );

        if ( IsReady == false )
        {
            reason = "아이템 물리 / 배치 구성이 준비되지 않았습니다.";
            return false;
        }

        Physics.SyncTransforms( );

        if ( CheckAppearanceConfiguration(
            collider, out Vector3 localCenter, out reason ) == false )
            return false;

        //물리 모듈에서 변경 가능 상태를 검사한 뒤 참조를 함께 변경
        if ( _physics.TrySetCollider( collider, out reason ) == false )
            return false;

        _placement.SetCollider( collider );
        _collider = collider;
        _localBoundsCenter = localCenter;

        //다음 집기부터 교체 모델만 포함하고
        //보관 중인 이전 모델은 제외
        UpdateAppearanceBinding( );
        return true;
    }

    /// <summary>
    /// 현재 대표 충돌체의 외형을 보유 모듈에 연결
    /// </summary>
    void UpdateAppearanceBinding ()
    {
        _hold.SetCurrentAppearance(
            _collider != null
                ? _collider.GetComponentInParent<AppearanceComponents>( true )
                : null );
    }

    #region ----- 물리 상태 -----

    /// <summary>
    /// 풀 보관을 위해 숨김, 물리와 배치 기록 정리
    /// </summary>
    public void ResetForPool ()
    {
        _hold.SetHeldHidden( false );
        _physics.SetHidden( false );
        _physics.RestoreOwnerCollision( );
        _physics.DisablePhysics( );
        _placement.ClearPlacement( );
        _localBoundsCenter = Vector3.zero;
    }

    /// <summary>
    /// 현재 이동, 회전 속도 조회
    /// </summary>
    /// <param name="velocity">현재 이동 속도</param>
    /// <param name="angularVelocity">현재 회전 속도</param>
    public void GetVelocity (
        out Vector3 velocity,
        out Vector3 angularVelocity )
    {
        _physics.GetVelocity( out velocity, out angularVelocity );
    }

    /// <summary>
    /// 이동, 회전 속도 적용
    /// </summary>
    /// <param name="velocity">적용할 이동 속도</param>
    /// <param name="angularVelocity">적용할 회전 속도</param>
    public void SetVelocity (
        Vector3 velocity, Vector3 angularVelocity )
    {
        _physics.SetVelocity( velocity, angularVelocity );
    }


    /// <summary>
    /// 물리 이동과 중력 중단
    /// </summary>
    public void DisablePhysics ()
    {
        _physics.DisablePhysics( );
    }

    /// <summary>
    /// 배치 후 고정 여부에 따른 물리 적용
    /// </summary>
    /// <param name="isFixed">물리 고정 여부</param>
    public void SetPlacementPhysics ( bool isFixed )
    {
        _physics.SetPlacementPhysics( isFixed );
    }

    /// <summary>
    /// 속도 제거와 수면 상태 적용
    /// </summary>
    public void StopMotion ()
    {
        _physics.StopMotion( );
    }

    /// <summary>
    /// 아이템 비활성화
    /// </summary>
    public void Hide ()
    {
        gameObject.SetActive( false );
    }


    /// <summary>
    /// 리지드바디 수면 상태 확인
    /// </summary>
    /// <returns>수면 상태 여부</returns>
    public bool IsSleeping ()
    {
        return _physics.IsSleeping( );
    }

    /// <summary>
    /// 활성 아이템의 물리 정착 상태 확인
    /// </summary>
    /// <returns>고정/수면 상태 여부</returns>
    public bool IsSettled ()
    {
        if ( isActiveAndEnabled == false || _physics == null )
            return false;

        return _physics.IsSettled( );
    }

    #endregion

    #region ----- 배치/복구 -----

    /// <summary>
    /// 현재 콜라이더 영역 조회
    /// </summary>
    /// <returns>월드 기준 콜라이더 영역</returns>
    public Bounds GetBounds ()
    {
        return _placement.GetBounds( );
    }

    /// <summary>
    /// 아이템 축 기준 크기 계산
    /// </summary>
    public Vector3 GetPlacementBoundsSize ()
    {
        return _placement.GetPlacementBoundsSize( );
    }

    /// <summary>
    /// 아이템 충돌체 목록 조회
    /// </summary>
    /// <param name="colliders">조회 결과를 받을 목록</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>배치에 사용할 충돌체 존재 여부</returns>
    public bool TryGetPlacementColliders (
        List<Collider> colliders, out string reason )
    {
        //호출 순서와 관계없이 배치 모듈을 준비
        if ( _placement == null )
            Init( );

        if ( IsReady == false )
        {
            reason = "아이템 배치 구성이 준비되지 않았습니다.";
            return false;
        }

        return _placement.TryGetPlacementColliders(
            colliders, out reason );
    }

    /// <summary>
    /// 충돌 영역 조회
    /// </summary>
    /// <param name="bounds">충돌 영역</param>
    /// <returns>유효한 충돌 영역 존재 여부</returns>
    public bool TryGetDisplacementBounds ( out Bounds bounds )
    {
        return _placement.TryGetDisplacementBounds( out bounds );
    }

    /// <summary>
    /// 지정 위치로 이동
    /// </summary>
    /// <param name="position">검사 완료된 루트 월드 위치</param>
    public void SetDisplacement ( Vector3 position )
    {
        //회전 상태 유지
        Quaternion rotation = transform.rotation;

        //물리 정지
        _physics.DisablePhysics( );

        transform.SetParent( null, true );
        transform.SetPositionAndRotation( position, rotation );

        Physics.SyncTransforms( );

        //이동 후에는 고정을 해제한 일반 물리 상태로 유지
        _physics.SetPlacementPhysics( false );
    }

    /// <summary>
    /// 아이템 배치 상태 저장
    /// </summary>
    /// <param name="state">저장한 배치 상태</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>배치 상태 저장 성공 여부</returns>
    public bool TrySavePlacementState (
        out ItemPlacementState state,
        out string reason )
    {
        state = default;
        reason = string.Empty;

        if ( isActiveAndEnabled == false )
        {
            reason = "활성 아이템 뷰가 필요합니다.";
            return false;
        }

        if ( _placement == null )
            Init( );

        if ( IsReady == false )
        {
            reason = "아이템 물리 / 배치 구성이 준비되지 않았습니다.";
            return false;
        }

        return _placement.TrySavePlacementState( out state, out reason );
    }

    /// <summary>
    /// 아이템 배치 상태 복구
    /// </summary>
    /// <param name="state">저장한 배치 상태</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>배치 상태 복구 성공 여부</returns>
    public bool TryRestorePlacementState (
        ItemPlacementState state, out string reason )
    {
        if ( isActiveAndEnabled == false )
        {
            reason = "활성 아이템 뷰가 필요합니다.";
            return false;
        }

        if ( _placement == null )
            Init( );

        if ( IsReady == false )
        {
            reason = "아이템 물리 / 배치 구성이 준비되지 않았습니다.";
            return false;
        }

        return _placement.TryRestorePlacementState( state, out reason );
    }

    /// <summary>
    /// 배치 부모, 위치, 회전 적용
    /// </summary>
    /// <param name="data">배치 정보</param>
    public void Place ( PlacementData data )
    {
        _placement.Place( data );
    }

    /// <summary>
    /// 계산된 배치 위치, 회전과 물리 설정 적용
    /// </summary>
    /// <param name="data">배치 정보</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>배치 적용 성공 여부</returns>
    public bool TryApplyPlacement (
        PlacementData data, out string reason )
    {
        //배치 정보 확인
        if ( CheckPlacementData( data, out reason ) == false )
            return false;

        if ( _physics == null || _placement == null )
            Init( );

        if ( IsReady == false )
        {
            reason = "아이템 물리 / 배치 구성이 준비되지 않았습니다.";
            return false;
        }

        //숨김이나 투척자 충돌 제외가 진행 중이면 적용하지 않음
        if ( _physics.CheckPlacementState( out reason ) == false )
            return false;

        //위치 변경 전에 이동 및 중력 적용 중단
        _physics.DisablePhysics( );

        //위치는 표면이 아니라 보정이 끝난 루트 위치
        //부모, 위치, 회전과 지지 표면 기록을 함께 적용
        Place( data );

        //부모 변경 콜백 등으로 대상이 무효화될 수 있으므로 다시 확인
        if ( CheckPlacementData( data, out reason ) == false )
            return false;

        if ( _physics.CheckPlacementState( out reason ) == false )
            return false;

        _physics.SetPlacementPhysics( data.IsFixed );
        return true;
    }

    /// <summary>
    /// 계산된 배치 위치 적용
    /// </summary>
    /// <param name="position">월드 위치</param>
    public void SetPosition ( Vector3 position )
    {
        transform.position = position;
        Physics.SyncTransforms( );
    }

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
        return _placement.TryGetSupport( direction, checkGap, out hit, showLog );
    }

    /// <summary>
    /// 배치 방향을 반영한 현재 지지 표면 조회
    /// </summary>
    /// <param name="fallbackDirection">기본 탐색 방향</param>
    /// <param name="rotationOffset">아이템 배치 방향 보정</param>
    /// <param name="checkGap">표면 탐색 추가 거리</param>
    /// <param name="hit">현재 지지 표면</param>
    public bool TryGetPlacementSupport (
        Vector3 fallbackDirection, Quaternion rotationOffset,
        float checkGap, out RaycastHit hit )
    {
        return _placement.TryGetPlacementSupport(
            fallbackDirection, rotationOffset, checkGap, out hit );
    }

    #endregion

    #region ----- 보유 -----

    /// <summary>
    /// 아이템과 자식의 들기 전 레이어 복구
    /// </summary>
    public void RestoreItemLayers ()
    {
        _hold.RestoreItemLayers( );
    }

    /// <summary>
    /// 보유자와의 충돌 여부 설정
    /// </summary>
    /// <param name="ignore">충돌 제외 여부</param>
    public void SetHoldCollision ( bool ignore )
    {
        _hold.SetHoldCollision( ignore );
    }

    /// <summary>
    /// 보유 레이어와 충돌 설정 복구
    /// </summary>
    /// <param name="type">복구할 보유 방식</param>
    public void RestoreHoldSettings ( HoldType type )
    {
        _hold.RestoreHoldSettings( type );
    }

    /// <summary>
    /// 보유 전 콜라이더 정보 저장
    /// </summary>
    /// <param name="saveOriginal">원래 활성화 상태 저장 여부</param>
    public void CaptureHoldSettings ( bool saveOriginal )
    {
        _hold.CaptureHoldSettings( saveOriginal );

        //콜라이더 비활성화 전에 현재 중심 저장
        _localBoundsCenter = transform.InverseTransformPoint( GetBounds( ).center );
    }

    /// <summary>
    /// 보유 위치와 레이어, 충돌 설정 적용
    /// </summary>
    /// <param name="holdPoint">보유 위치</param>
    /// <param name="type">적용할 보유 방식</param>
    /// <param name="ownerColliders">충돌 제외 대상</param>
    public void Hold (
        Transform holdPoint, HoldType type, Collider [ ] ownerColliders )
    {
        //보유 중 물리 비활성화
        DisablePhysics( );

        //월드 스케일 유지하며 위치 이동
        transform.SetParent( holdPoint, true );
        transform.SetPositionAndRotation( holdPoint.position, holdPoint.rotation );

        _hold.ApplyHoldSettings( type, ownerColliders );
        Physics.SyncTransforms( );
    }

    /// <summary>
    /// 부모 해제 및 월드 물리 복구
    /// </summary>
    public void Release ()
    {
        transform.SetParent( null, true );
        SetPlacementPhysics( false );
        Physics.SyncTransforms( );
    }

    /// <summary>
    /// 보유 물건의 임시 숨김 설정
    /// </summary>
    /// <param name="hidden">숨김 여부</param>
    public void SetHeldHidden ( bool hidden )
    {
        _hold?.SetHeldHidden( hidden );
    }

    #endregion

    #region ----- 투척 -----

    /// <summary>
    /// 투척 전 감쇠 설정 저장
    /// </summary>
    public void SaveThrowSettings ()
    {
        _physics.SaveThrowSettings( );
    }

    /// <summary>
    /// 투척 전 감쇠 설정 복구
    /// </summary>
    public void RestoreThrowSettings ()
    {
        _physics.RestoreThrowSettings( );
    }

    /// <summary>
    /// 투척자 충돌 제외 및 초기 속도 적용
    /// </summary>
    /// <param name="velocity">초기 속도</param>
    /// <param name="ownerColliders">투척자 콜라이더</param>
    public void SetThrow (
        Vector3 velocity, Collider [ ] ownerColliders )
    {
        _physics.SetThrow( velocity, ownerColliders );
    }

    /// <summary>
    /// 고정 해제 후 충격량 적용
    /// </summary>
    /// <param name="impulse">밀어낼 방향과 충격량</param>
    public void ApplyPush ( Vector3 impulse )
    {
        Release( );
        _physics.ApplyImpulse( impulse );
    }

    /// <summary>
    /// 던진 플레이어와의 충돌 복구
    /// </summary>
    public void RestoreOwnerCollision ()
    {
        _physics.RestoreOwnerCollision( );
    }

    /// <summary>
    /// 충돌 제외 중인 투척자와 영역 겹침 확인
    /// </summary>
    /// <returns>겹치는 투척자 존재 여부</returns>
    public bool HasOwnerOverlap ()
    {
        return _physics.HasOwnerOverlap( );
    }

    #endregion

    #region ----- 상태 검사 -----

    /// <summary>
    /// 초기화된 공통 물리 모듈 조회
    /// </summary>
    /// <returns>공유 물리 모듈, 초기화 전이면 null</returns>
    public ItemPhysics GetPhysics ()
    {
        return _physics;
    }

    /// <summary>
    /// 배치와 물리에 필요한 컴포넌트 구성 확인
    /// </summary>
    /// <param name="reason">구성 오류 사유</param>
    /// <returns>필수 구성 존재 여부</returns>
    public bool CheckConfiguration ( out string reason )
    {
        reason = string.Empty;

        if ( GetCollider( ) == null )
        {
            reason = "아이템 Collider가 없습니다.";
            return false;
        }

        if ( GetComponent<Rigidbody>( ) == null )
        {
            reason = "아이템 Rigidbody가 없습니다.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 지정한 콜라이더 또는 자식 콜라이더 조회
    /// </summary>
    /// <returns>아이템 충돌용 콜라이더</returns>
    Collider GetCollider ()
    {
        return _collider != null
            ? _collider
            : GetComponentInChildren<Collider>( true );
    }

    /// <summary>
    /// 새 대표 충돌체의 소속과 활성 상태 확인
    /// </summary>
    /// <param name="collider">연결할 대표 충돌체</param>
    /// <param name="reason">실패 사유, 성공 시 빈 문자열</param>
    /// <returns>참조 변경을 시도할 수 있는지 여부</returns>
    public bool CheckAppearanceBinding (
        Collider collider, out string reason )
    {
        if ( CheckAppearanceConfiguration( collider, out _, out reason ) == false )
            return false;

        return _physics.CheckAppearanceCollider( collider, out reason );
    }

    /// <summary>
    /// 대표 충돌체 구성과 로컬 중심 확인
    /// </summary>
    /// <param name="collider">연결할 대표 충돌체</param>
    /// <param name="localCenter">확인한 로컬 중심</param>
    /// <param name="reason">구성 오류 사유</param>
    /// <returns>충돌체 구성 유효 여부</returns>
    bool CheckAppearanceConfiguration (
        Collider collider, out Vector3 localCenter, out string reason )
    {
        localCenter = Vector3.zero;
        reason = string.Empty;

        if ( isActiveAndEnabled == false || collider == null )
        {
            reason = "활성 아이템과 연결할 대표 Collider가 필요합니다.";
            return false;
        }

        if ( collider.transform == transform ||
             collider.transform.IsChildOf( transform ) == false )
        {
            reason = "새 Collider는 아이템 루트의 자식에 있어야 합니다.";
            return false;
        }

        if ( collider.enabled == false ||
             collider.gameObject.activeInHierarchy == false ||
             collider.isTrigger == true )
        {
            reason = "활성화된 일반 충돌체가 필요합니다.";
            return false;
        }

        Rigidbody rigid = GetComponent<Rigidbody>( );
        if ( rigid == null || collider.attachedRigidbody != rigid )
        {
            reason = "새 Collider의 Rigidbody가 아이템 루트와 다릅니다.";
            return false;
        }

        //검사에서는 초기화나 참조 변경을 수행하지 않음
        if ( _physics == null )
        {
            reason = "아이템 물리 모듈을 먼저 초기화해 주세요.";
            return false;
        }

        localCenter = transform.InverseTransformPoint( collider.bounds.center );
        if ( ColliderCalculator.CheckFinite( localCenter ) == false )
        {
            reason = "새 Collider의 중심 위치가 유효하지 않습니다.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 배치 정보의 참조와 적용할 값 확인
    /// </summary>
    /// <param name="data">검사를 마친 배치 정보</param>
    /// <param name="reason">실패 사유, 성공 시 빈 문자열</param>
    /// <returns>배치 정보 적용 가능 여부</returns>
    bool CheckPlacementData (
        PlacementData data, out string reason )
    {
        reason = string.Empty;

        if ( isActiveAndEnabled == false )
        {
            reason = "활성 아이템 뷰가 필요합니다.";
            return false;
        }

        return ItemPlacementChecker.CheckPlacementData( transform, data, out reason );
    }

    #endregion

}
