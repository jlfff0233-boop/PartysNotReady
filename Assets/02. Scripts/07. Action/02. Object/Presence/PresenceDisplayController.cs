using UnityEngine;

/// <summary>
/// 존재 표시 제어 - 대상 표시, 물리와 조사 영역 전환
/// </summary>
public class PresenceDisplayController : MonoBehaviour
{
    [Header( "----- 표시 대상 -----" )]
    [SerializeField] GameObject _target;        //등장/숨기기 대상
    [SerializeField] Collider _collider;        //콜라이더

    [Header( "----- 빈자리 조사 -----" )]
    [SerializeField] Collider _restoreCheckCollider;     //물건이 없을 때 조사할 영역

    ItemPhysics _physics;       //숨김 중 정지할 물리 모듈
    bool _isInitialized;        //초기 표시와 물리 적용 완료 여부
    PresenceRestoreDisplay _restoreDisplay = new( );     //복구 대기와 충돌체 상태 관리
    AppearanceComponents _appearance;     //현재 표시할 외형, 일반 대상은 null

    /// <summary>
    /// 대상 표시 여부
    /// </summary>
    public bool IsVisible => CheckVisible( );

    /// <summary>
    /// 표시 대상과 콜라이더 사용 가능 여부
    /// </summary>
    public bool IsReady => CheckReady( out _ );

    /// <summary>
    /// 빈자리 조사 영역 설정 여부
    /// </summary>
    public bool IsRestoreReady => CheckRestoreReady( );


    /// <summary>
    /// 등장 공간 검사에 사용할 콜라이더
    /// </summary>
    public Collider Collider => _collider;

    #region ----- 초기화 -----

    /// <summary>
    /// 물리 모듈 연결
    /// </summary>
    void InitPhysics ()
    {
        if ( _physics != null ) return;

        //아이템은 같은 물리 모듈을 공유하고, 독립 대상만 별도 모듈 사용
        if ( TryGetComponent( out ItemWorldController itemView ) == true )
        {
            itemView.Init( );
            _physics = itemView.GetPhysics( );
        }
        else if ( TryGetComponent( out Rigidbody rigid ) == true )
            _physics = new ItemPhysics( rigid, _collider );
    }

    /// <summary>
    /// 초기 표시와 물리 상태 적용
    /// </summary>
    /// <param name="isVisible">초기에 물건을 표시할지 여부</param>
    /// <param name="reason">초기화 실패 사유</param>
    /// <returns>초기화 성공 여부</returns>
    public bool Init ( bool isVisible, out string reason )
    {
        reason = string.Empty;

        //성공한 초기화는 다시 실행하지 않아 현재 표시 상태를 보존
        if ( _isInitialized == true )
            return true;

        //초기화에서는 OnEnable 호출 완료를 요구하지 않음
        if ( CheckDisplayConfig( out reason ) == false )
            return false;

        if ( CheckDisplayColliderState( out reason ) == false )
            return false;

        if ( TryInitializeRestoreArea( out reason ) == false )
            return false;

        //초기 씬 외형도 전체 충돌체 검사에 포함
        _appearance = _target.GetComponent<AppearanceComponents>( );
        if ( _appearance != null && AppearanceChecker.Check( _appearance, out reason ) == false )
            return false;

        if ( _appearance != null && _appearance.MainCollider != _collider )
        {
            reason = "Presence와 초기 외형의 대표 Collider를 동일하게 연결해 주세요.";
            return false;
        }

        InitPhysics( );
        _restoreDisplay.SetColliders( _target.GetComponentsInChildren<Collider>( true ) );

        if ( ApplyVisibility( isVisible, out reason ) == false )
            return false;

        _isInitialized = true;
        return true;
    }


    /// <summary>
    /// 빈자리 조사 영역 초기화
    /// </summary>
    /// <param name="reason">조사 영역 초기화 실패 사유</param>
    /// <returns>조사 영역 초기화 성공 여부</returns>
    bool TryInitializeRestoreArea ( out string reason )
    {
        reason = string.Empty;

        //빈자리 조사를 사용하지 않으면 조사 영역 연결은 선택 사항
        if ( _restoreCheckCollider == null )
            return true;

        if ( CheckRestoreConfig( out reason ) == false )
            return false;

        GameObject restoreObject = _restoreCheckCollider.gameObject;

        //다른 기능이 있는 부모 오브젝트는 자동으로 활성화하지 않음
        if ( restoreObject.transform.parent.gameObject.activeInHierarchy == false )
        {
            reason = "복구 조사 영역의 부모가 비활성 상태입니다. 활성 관리 루트 아래에 구성해 주세요.";
            return false;
        }

        //오브젝트가 켜지는 순간에도
        //불필요한 충돌과 탐지가 발생하지 않도록 먼저 차단
        _restoreCheckCollider.enabled = false;
        restoreObject.SetActive( true );

        if ( _restoreCheckCollider == null || restoreObject == null )
        {
            reason = "활성화 중 복구 조사 영역 또는 Collider가 제거됐습니다.";
            return false;
        }

        if ( restoreObject.activeInHierarchy == false ||
             _restoreCheckCollider.enabled == true )
        {
            reason = "복구 조사 영역의 초기 상태가 다른 코드에 의해 변경됐습니다. 조사 전용 자식으로 구성해 주세요.";
            return false;
        }

        return true;
    }


    #endregion

    #region ----- 표시/숨김 -----

    /// <summary>
    /// 외형과 충돌 참조 적용
    /// </summary>
    /// <param name="appearance">연결할 외형</param>
    /// <param name="reason">물리 연결 실패 사유</param>
    bool ApplyAppearance ( AppearanceComponents appearance, out string reason )
    {
        reason = string.Empty;
        Collider [ ] colliders = new Collider [ appearance.Colliders.Count ];
        for ( int i = 0; i < colliders.Length; i++ )
        {
            colliders [ i ] = appearance.Colliders [ i ];
        }

        //실패 가능한 물리 연결을 먼저 완료하고 표시 참조를 함께 변경
        if ( _physics != null && _physics.TrySetCollider( appearance.MainCollider, out reason ) == false )
            return false;

        _target = appearance.gameObject;
        _collider = appearance.MainCollider;
        _appearance = appearance;
        _restoreDisplay.SetColliders( colliders );
        return true;
    }

    /// <summary>
    /// 새 외형 참조 연결
    /// </summary>
    /// <param name="appearance">연결할 외형</param>
    /// <param name="reason">연결 실패 사유</param>
    /// <returns>참조 교체 성공 여부</returns>
    public bool TrySetAppearance ( AppearanceComponents appearance, out string reason )
    {
        if ( CheckAppearanceBinding( appearance, false, out reason ) == false )
            return false;

        return ApplyAppearance( appearance, out reason );
    }

    /// <summary>
    /// 이전 외형 참조 복구
    /// </summary>
    /// <param name="appearance">교체 직전 외형</param>
    /// <param name="reason">복구 실패 사유</param>
    public bool TryRestoreAppearance ( AppearanceComponents appearance, out string reason )
    {
        if ( CheckAppearanceBinding( appearance, true, out reason ) == false )
            return false;

        return ApplyAppearance( appearance, out reason );
    }


    /// <summary>
    /// 대상 활성 상태와 물리 상태 적용
    /// </summary>
    /// <param name="isVisible">표시하려면 true, 숨기려면 false</param>
    /// <param name="reason">표시 변경 실패 사유</param>
    /// <returns>표시 변경 성공 여부</returns>
    bool ApplyVisibility ( bool isVisible, out string reason )
    {
        reason = string.Empty;

        //설정 검사를 통과한 대상도 부모가 꺼져 있으면 표시 적용을 거절
        if ( _target.transform.parent.gameObject.activeInHierarchy == false )
        {
            reason = "Target의 부모 오브젝트를 활성화해 주세요.";
            return false;
        }

        //충돌체를 숨기기 전에 낙하를 정지
        if ( isVisible == false )
            _physics?.SetHidden( true );

        _target.SetActive( isVisible );
        if ( _target == null )
        {
            reason = "표시 변경 중 Target이 제거됐습니다.";
            return false;
        }

        //표시 실패 시에도 실제 충돌체 활성 상태에 맞춰 물리 유지
        _physics?.SetHidden( _target.activeSelf == false );
        if ( IsVisible != isVisible )
        {
            reason = "표시 변경 후 Target의 활성 상태가 요청과 다릅니다. 부모 상태와 활성 변경 코드를 확인해 주세요.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 대상 표시 또는 숨김
    /// </summary>
    /// <param name="isVisible">표시하려면 true, 숨기려면 false</param>
    /// <param name="reason">표시 변경 실패 사유</param>
    /// <returns>표시 변경 성공 여부</returns>
    public bool TrySetVisible ( bool isVisible, out string reason )
    {
        if ( CheckReady( out reason ) == false )
            return false;

        if ( _restoreDisplay.IsActive == true )
        {
            reason = "복구 대기 표시는 정상 복구 경로에서만 변경할 수 있습니다.";
            return false;
        }

        return ApplyVisibility( isVisible, out reason );
    }

    /// <summary>
    /// 대상 표시 또는 숨김
    /// </summary>
    /// <param name="isVisible">표시하려면 true, 숨기려면 false</param>
    /// <returns>표시 변경 성공 여부</returns>
    public bool TrySetVisible ( bool isVisible )
    {
        return TrySetVisible( isVisible, out _ );
    }


    /// <summary>
    /// 등장/복구 공간 확인
    /// </summary>
    /// <param name="obstacleMask">장애물 레이어</param>
    /// <param name="reason">공간 검사 실패 사유</param>
    /// <returns>등장 가능한 공간 여부</returns>
    public bool IsSpaceClear ( LayerMask obstacleMask, out string reason )
    {
        if ( CheckReady( out reason ) == false )
            return false;

        //일반 대상은 대표 콜라이더, 외형 연결 대상은 사용 중인 충돌체 전체 검사
        if ( _appearance == null )
            return PresenceSpaceChecker.IsClear( _collider, obstacleMask, out reason );

        bool hasShape = false;
        for ( int i = 0; i < _restoreDisplay.Colliders.Count; i++ )
        {
            Collider collider = _restoreDisplay.Colliders [ i ];
            if ( collider == null )
            {
                reason = "현재 외형의 충돌체가 제거됐습니다.";
                return false;
            }

            //미리보기에서 강제로 끈 Collider는 복구 전 설정을 기준으로 검사
            bool enabled = _restoreDisplay.IsColliderEnabled( i );
            if ( enabled == false || collider.isTrigger ||
                 CheckDisplayPath( collider.transform ) == false )
                continue;

            hasShape = true;
            if ( PresenceSpaceChecker.IsClear( collider, obstacleMask, out reason ) == false )
                return false;
        }

        reason = hasShape ? string.Empty : "검사할 외형 충돌체가 없습니다.";
        return hasShape;
    }

    #endregion

    #region ----- 복구 -----

    /// <summary>
    /// 복구 대기 표시 취소
    /// </summary>
    public void CancelRestoreDisplay ()
    {
        _restoreDisplay.Cancel( _target, _physics );
    }

    /// <summary>
    /// 복구 대기 물건 표시
    /// </summary>
    /// <param name="reason">대기 표시 실패 사유</param>
    /// <returns>대기 표시 성공 여부</returns>
    public bool TryBeginRestoreDisplay ( out string reason )
    {
        if ( CheckReady( out reason ) == false )
            return false;

        if ( _restoreDisplay.IsActive == true || IsVisible == true )
        {
            reason = "숨겨진 물건만 복구 대기 표시를 시작할 수 있습니다.";
            return false;
        }

        _restoreDisplay.Begin( _target, _physics );

        if ( CheckReady( out reason ) == false || IsVisible == false )
        {
            if ( string.IsNullOrEmpty( reason ) )
                reason = "복구 대기 물건이 활성화되지 않았습니다.";

            CancelRestoreDisplay( );
            return false;
        }

        return true;
    }

    /// <summary>
    /// 복구 대기 종료와 충돌 / 물리 복원
    /// </summary>
    /// <param name="reason">정상 복구 실패 사유</param>
    /// <returns>정상 복구 성공 여부</returns>
    public bool TryCompleteRestoreDisplay ( out string reason )
    {
        if ( CheckReady( out reason ) == false )
            return false;

        if ( _restoreDisplay.IsActive == false || IsVisible == false )
        {
            reason = "복구 대기 중인 표시 대상이 없습니다.";
            return false;
        }

        //공간 검사는 호출부에서 완료하며 충돌체 복원 후 물리 재개
        _restoreDisplay.Complete( _physics );
        return true;
    }

    /// <summary>
    /// 빈자리 조사 영역 활성 상태 적용
    /// </summary>
    /// <param name="isEnabled">조사 영역 활성 여부</param>
    public void SetRestoreInvestigation ( bool isEnabled )
    {
        //잘못 연결한 다른 오브젝트의 콜라이더는 변경하지 않음
        if ( CheckRestoreConfig( out _ ) == false )
            return;

        _restoreCheckCollider.enabled =
            isEnabled == true &&
            CheckRestoreReady( ) == true &&
            IsVisible == false;
    }

    #endregion

    #region ----- 설정/상태 검사 -----

    /// <summary>
    /// 빈자리 조사 영역 설정 확인
    /// </summary>
    /// <param name="reason">조사 영역 설정 실패 사유</param>
    /// <returns>빈자리 조사 영역 설정 여부</returns>
    bool CheckRestoreConfig ( out string reason )
    {
        return PresenceSettingsChecker.CheckRestoreConfig(
            transform, _target, _collider, _restoreCheckCollider, out reason );
    }

    /// <summary>
    /// 빈자리 조사 영역 사용 가능 여부 확인
    /// </summary>
    /// <returns>빈자리 조사 준비 여부</returns>
    bool CheckRestoreReady ()
    {
        if ( CheckReady( out _ ) == false ||
             CheckRestoreConfig( out _ ) == false )
            return false;

        return _restoreCheckCollider.gameObject.activeInHierarchy;
    }

    /// <summary>
    /// 외형 연결 또는 실패 복구 가능 여부 확인
    /// </summary>
    /// <param name="appearance">연결할 외형</param>
    /// <param name="isRollback">실패한 현재 표시 대신 복구 대상 검사 여부</param>
    /// <param name="reason">연결 불가 사유</param>
    public bool CheckAppearanceBinding (
        AppearanceComponents appearance, bool isRollback, out string reason )
    {
        reason = string.Empty;
        if ( isRollback == false && CheckReady( out reason ) == false )
            return false;

        //복구에서는 손상된 새 외형의 표시 참조를 검사하지 않음
        if ( _isInitialized == false || isActiveAndEnabled == false ||
             _restoreDisplay.IsActive == true || ( isRollback == false && IsVisible == false ) )
        {
            reason = "정상 표시 중에만 외형 참조를 변경할 수 있습니다.";
            return false;
        }

        if ( AppearanceChecker.Check( appearance, out reason ) == false )
            return false;

        Transform next = appearance.transform;
        if ( next == transform || next.IsChildOf( transform ) == false ||
             next.parent.gameObject.activeInHierarchy == false ||
             appearance.MainCollider.attachedRigidbody != GetComponent<Rigidbody>( ) )
        {
            reason = "외형은 활성 관리 루트의 자식이며 같은 Rigidbody를 사용해야 합니다.";
            return false;
        }

        if ( _restoreCheckCollider != null )
        {
            //복구 시 손상된 현재 대상 대신 이전 외형을 기준으로 조사 영역 검사
            if ( PresenceSettingsChecker.CheckRestoreConfig(
                transform, appearance.gameObject, appearance.MainCollider,
                _restoreCheckCollider, out reason ) == false )
                return false;
        }

        return _physics == null || _physics.CheckAppearanceCollider( appearance.MainCollider, out reason );
    }

    /// <summary>
    /// 표시 루트를 제외한 충돌체 자식 경로의 활성 상태 확인
    /// </summary>
    /// <param name="target">검사할 충돌체 Transform</param>
    /// <returns>표시 시 활성화할 경로 여부</returns>
    bool CheckDisplayPath ( Transform target )
    {
        while ( target != null && target != _target.transform )
        {
            if ( target.gameObject.activeSelf == false )
                return false;
            target = target.parent;
        }
        return target == _target.transform;
    }

    /// <summary>
    /// 대상의 실제 표시 여부 확인
    /// </summary>
    /// <returns>대상 표시 여부</returns>
    bool CheckVisible ()
    {
        return _target != null && _target.activeInHierarchy;
    }

    /// <summary>
    /// 정상 표시와 복구 대기에 맞는 콜라이더 상태 확인
    /// </summary>
    /// <param name="reason">콜라이더 상태 실패 사유</param>
    /// <returns>현재 상태에 맞는 콜라이더 설정 여부</returns>
    bool CheckDisplayColliderState ( out string reason )
    {
        reason = string.Empty;

        //대기 중 꺼둔 콜라이더는 설정 누락이 아닌 정상적인 상태
        bool expectedEnabled = _restoreDisplay.IsActive == false;
        if ( _collider.enabled != expectedEnabled )
        {
            reason = _restoreDisplay.IsActive
                ? "복구 대기 중 표시 Collider가 외부에서 활성화됐습니다."
                : "표시 대상 Collider의 Enabled를 켜 주세요.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 표시 대상과 콜라이더 설정 확인
    /// </summary>
    /// <param name="reason">준비 실패 사유</param>
    /// <returns>표시 기능 준비 여부</returns>
    bool CheckDisplayConfig ( out string reason )
    {
        return PresenceSettingsChecker.CheckDisplayConfig(
            transform, _target, _collider, out reason );
    }

    /// <summary>
    /// 초기화 완료와 실행 중 표시 가능 여부 확인
    /// </summary>
    /// <param name="reason">실행 불가 사유</param>
    /// <returns>표시 기능 사용 가능 여부</returns>
    bool CheckReady ( out string reason )
    {
        reason = string.Empty;

        if ( _isInitialized == false )
        {
            reason = "존재 기능의 초기 표시 적용이 완료되지 않았습니다.";
            return false;
        }

        if ( isActiveAndEnabled == false )
        {
            reason = "PresenceDisplayController 또는 관리 오브젝트가 실행 가능한 활성 상태가 아닙니다.";
            return false;
        }

        if ( CheckDisplayConfig( out reason ) == false )
            return false;

        if ( CheckDisplayColliderState( out reason ) == false )
            return false;

        if ( _target.transform.parent.gameObject.activeInHierarchy == false )
        {
            reason = "Target의 부모 오브젝트를 활성화해 주세요.";
            return false;
        }

        return true;
    }

    #endregion

}
