using System;
using UnityEngine;

/// <summary>
/// 아이템 보유 중재 - 들기, 배치와 강제 이동
/// </summary>
[RequireComponent( typeof( ItemWorldController ) )]
public class ItemCarry : MonoBehaviour, ICarryable
{
    [Header( "----- 설정 데이터 -----" )]
    [SerializeField] ItemData _itemData;		//아이템 데이터

    [Header( "----- 배치 방향 -----" )]
    [SerializeField] Vector3 _placementRotationOffset;      //표면 기준 배치 방향 보정 각도

    bool _isChangingHold;		//보유 전환 여부
    bool _releaseOnEnable;		//보유 아이템의 해제 대기
    bool _isCarryLocked;		//운반 잠금 여부
    bool _isStockRegistered;        //공급원 재고 등록 여부
    bool _isNotifyingConsume;		//소비 확정 알림 진행 여부
    ulong _poolRentVersion;		//풀 대여 준비 성공 회차

    ItemData _runtimeItemData;		//실행 중 적용한 아이템 데이터
    CarryModel _carryModel = new( );		//보유 상태 모델
    ItemConsume _consume;		//소비 실패에 대비한 배치와 물리 상태 저장, 복구
    protected ItemWorldController _view;		//아이템 뷰
    Vector3 _placementBoundsSize;		//배치 판정용 크기


    #region ----- 프로퍼티/이벤트 -----

    /// <summary>
    /// 현재 아이템 데이터
    /// </summary>
    public ItemData ItemData =>
        _runtimeItemData != null ? _runtimeItemData : _itemData;

    /// <summary>
    /// 현재 풀 대여 회차
    /// </summary>
    public ulong PoolRentVersion => _poolRentVersion;

    /// <summary>
    /// 배치 판정용 콜라이더 크기
    /// </summary>
    public Vector3 PlacementBoundsSize => _placementBoundsSize;

    /// <summary>
    /// 아이템별 배치 방향 보정
    /// </summary>
    public Quaternion PlacementRotationOffset =>
        Quaternion.Euler( _placementRotationOffset );

    /// <summary>
    /// 현재 보유 방식
    /// </summary>
    public HoldType CurrentType => _carryModel.CurrentType;

    /// <summary>
    /// 보유 여부
    /// </summary>
    public bool IsHold => _carryModel.IsHold;

    /// <summary>
    /// 잠금 여부
    /// </summary>
    public bool IsCarryLocked => _isCarryLocked;

    /// <summary>
    /// 소비 전환 가능 여부
    /// </summary>
    public bool CanChangeConsumeState => CheckCanChangeConsumeState( );

    /// <summary>
    /// 공급원 재고 등록 여부
    /// </summary>
    public bool IsStockRegistered => _isStockRegistered;

    /// <summary>
    /// 비활성 아이템을 포함한 실제 뷰 구성 준비 여부
    /// </summary>
    public bool IsReady => CheckReady( );

    /// <summary>
    /// 보유 변경 이벤트(이전 방식, 현재 방식)
    /// </summary>
    public event Action<HoldType, HoldType> OnHoldChanged;

    /// <summary>
    /// 배치 완료 이벤트(배치 대상)
    /// </summary>
    public event Action<PlacementTarget> OnPlaced;

    /// <summary>
    /// 비활성화에 따른 사용 중단 알림
    /// </summary>
    public event Action OnUnavailable;

    /// <summary>
    /// 배치 위치/물리 상태 변경 이벤트
    /// </summary>
    public event Action OnPlacementStateChanged;

    /// <summary>
    /// 강제 이동에 따른 직접 배치 대기 취소 알림
    /// </summary>
    public event Action OnPlacementInterrupted;

    /// <summary>
    /// 실제 소비 확정 알림(재료 아이템, 소비 원인)
    /// </summary>
    public event Action<ItemCarry, IngredientConsumeReason> OnConsumeConfirmed;

    #endregion

    #region ----- 초기화 -----

    /// <summary>
    /// 비활성 아이템을 포함한 월드 구성 준비 확인
    /// </summary>
    /// <returns>월드 구성 준비 여부</returns>
    bool CheckReady ()
    {
        ItemWorldController view =
            _view != null ? _view : GetComponent<ItemWorldController>( );

        return view != null && view.IsReady;
    }

    /// <summary>
    /// 아이템 뷰 연결 및 초기화
    /// </summary>
    protected virtual void Init ()
    {
        _view = GetComponent<ItemWorldController>( );

        if ( _view != null )
        {
            _view.Init( );
            _consume = new ItemConsume( _view );
        }

    }

    private void Awake ()
    {
        Init( );
    }

    protected virtual void OnEnable ()
    {
        if ( IsReady == false )
            return;

        //부모 비활성화 도중 분리하면
        //아이템만 다시 켜질 수 있어 활성화 이후 해제
        if ( _releaseOnEnable == true )
        {
            if ( TryRelease( ) == true || IsHold == false )
                _releaseOnEnable = false;
        }

        if ( CurrentType == HoldType.World )
            _view.SetHoldCollision( true );

        ItemRegister.Register( this );
    }

    #endregion

    #region ----- 보유/배치 -----

    /// <summary>
    /// 아이템 들기
    /// </summary>
    /// <param name="holdPoint">들기 지점</param>
    /// <param name="type">보유 방식</param>
    /// <param name="ownerColliders">충돌 제외 대상</param>
    /// <returns>들기 성공 여부</returns>
    public bool TryPickUp (
        Transform holdPoint,
        HoldType type = HoldType.View,
        Collider [ ] ownerColliders = null )
    {
        if ( CanChangeHold( ) == false ||
             _carryModel.CanPickUp( type ) == false )
            return false;

        if ( holdPoint == null ||
             holdPoint.gameObject.activeInHierarchy == false ||
             holdPoint.IsChildOf( transform ) == true )
            return false;

        _isChangingHold = true;

        try
        {
            _view.RestoreHoldSettings( CurrentType );
            PrepareHold( );

            //보유자 사이의 전달에서는 최초 충돌 설정 유지
            _view.CaptureHoldSettings( CurrentType == HoldType.None );
            _placementBoundsSize = _view.GetPlacementBoundsSize( );
            _view.Hold( holdPoint, type, ownerColliders );
            ChangeHoldType( type );

            //상태 알림에서 소비되거나 비활성화된 대상을 보유자로 등록하지 않음
            if ( isActiveAndEnabled == false )
                _releaseOnEnable = CurrentType == HoldType.View;

            bool succeeded =
                isActiveAndEnabled == true &&
                CurrentType == type &&
                transform.parent == holdPoint;

            return succeeded;
        }
        finally
        {
            //보유 변경 처리 해제
            _isChangingHold = false;
        }
    }

    /// <summary>
    /// 배치 정보에 따른 아이템 내려놓기
    /// </summary>
    /// <param name="data">배치 정보</param>
    /// <returns>배치 성공 여부</returns>
    public bool TryPutDown ( PlacementData data )
    {
#if UNITY_EDITOR
        RecordPlacement( "요청 진입", data );
#endif
        if ( CanChangeHold( ) == false ||
            _carryModel.CanChange( HoldType.None ) == false )
        {
#if UNITY_EDITOR
            RecordPlacement( "거절: 보유 상태를 변경할 수 없습니다.", data );
#endif
            return false;
        }

        if ( data.Target != null &&
            data.Target.isActiveAndEnabled == false )
        {
#if UNITY_EDITOR
            RecordPlacement( "거절: 배치 대상이 비활성 상태입니다.", data );
#endif
            return false;
        }

        _isChangingHold = true;

        //중간 반환과 예외 발생 시 보유 전환 상태 해제
        try
        {
            _view.RestoreHoldSettings( CurrentType );
            _view.Place( data );

#if UNITY_EDITOR
            RecordPlacement( "부모 및 위치 적용 후", data );
#endif

            //접촉 계산을 마친 루트 위치는 기존 바닥 보정을 중복 적용하지 않음
            if ( _view.IsColliderEnabled == true && data.PositionIsPivot == false )
            {
                //최종 Collider Bounds 기준으로 바닥 높이 보정
                Bounds bounds = _view.GetBounds( );
                Vector3 position = PlacementCalculator.GetPlacementPosition(
                    bounds, data.Position, data.SurfaceNormal );

#if UNITY_EDITOR
                PlayDebugHistory.Record(
                    "놓기 높이 보정", this,
                    $"Bounds 중심: {bounds.center:F3} / 크기: {bounds.size:F3} / " +
                    $"법선: {data.SurfaceNormal:F3} / 보정 위치: {position:F3}" );
#endif

                _view.SetPosition( position );
            }

            _view.SetPlacementPhysics( data.IsFixed );
            ChangeHoldType( HoldType.None );

#if UNITY_EDITOR
            RecordPlacement( "높이 보정 및 보유 해제 후", data );
#endif

            //보유 해제 알림에서 제거된 아이템은 배치 완료로 전달하지 않음
            if ( isActiveAndEnabled == false )
                return false;

            //현재 준비 상태를 먼저 갱신한 뒤 직접 배치 미션 전달
            OnPlacementStateChanged?.Invoke( );

            if ( isActiveAndEnabled == false )
                return false;

            //배치 알림 이벤트 발행
            OnPlaced?.Invoke( data.Target );

            if ( isActiveAndEnabled == false )
                return true;

            //배치 대상이 자신에게 들어온 아이템을 처리할 수 있도록 전달
            data.Target?.NotifyPlaced( this );
            return true;
        }
        finally
        {
            _isChangingHold = false;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// 놓기 단계의 위치와 부모, 보유 상태 기록
    /// </summary>
    /// <param name="step">현재 처리 단계</param>
    /// <param name="data">요청한 배치 정보</param>
    void RecordPlacement ( string step, PlacementData data )
    {
        string parent = transform.parent != null
            ? transform.parent.name : "없음";
        string requestedParent = data.Parent != null
            ? data.Parent.name : "없음";

        PlayDebugHistory.Record(
            "아이템 놓기", this,
            $"{step} / 월드: {transform.position:F3} / " +
            $"로컬: {transform.localPosition:F3} / 부모: {parent} / " +
            $"보유: {CurrentType} / 요청 위치: {data.Position:F3} / " +
            $"요청 부모: {requestedParent}" );
    }
#endif

    /// <summary>
    /// 보유 해제 및 월드 물리 복구
    /// </summary>
    /// <returns>해제 성공 여부</returns>
    public bool TryRelease ()
    {
        //적 비활성화 시에도 기존 월드 보유 해제 경로를 허용
        if ( _view == null ||
             _view.IsReady == false ) return false;

        if ( _isChangingHold == true ||
             _carryModel.CanChange( HoldType.None ) == false )
            return false;

        _isChangingHold = true;

        try
        {
            _view.RestoreHoldSettings( CurrentType );
            _view.Release( );
            ChangeHoldType( HoldType.None );

            return true;
        }
        finally
        {
            //보유 변경 처리 해제
            _isChangingHold = false;
        }
    }

    /// <summary>
    /// 보유 아이템 숨김 또는 표시 복원
    /// </summary>
    /// <param name="hidden">숨김 여부</param>
    public void SetHeldHidden ( bool hidden )
    {
        //숨김은 활성 월드 보유 아이템만 허용
        if ( hidden == true )
        {
            if ( isActiveAndEnabled == false ||
                 CurrentType != HoldType.World )
                return;
        }

        //비활성화 중에도 표시 복원 허용
        if ( _view != null && _view.IsReady == true )
            _view.SetHeldHidden( hidden );
    }

    /// <summary>
    /// 보유 전환 전 기존 동작 정리
    /// </summary>
    protected virtual void PrepareHold ()
    {
    }

    /// <summary>
    /// 외부 기능에 따른 아이템 운반 잠금 설정
    /// </summary>
    /// <param name="isLocked">운반 잠금 여부</param>
    public void SetCarryLocked ( bool isLocked )
    {
        _isCarryLocked = isLocked;
    }

    /// <summary>
    /// 보유 상태 변경 및 이벤트 발행
    /// </summary>
    /// <param name="type">변경할 보유 방식</param>
    void ChangeHoldType ( HoldType type )
    {
        HoldType previousType = CurrentType;

        //변경할 수 없으면 종료
        if ( _carryModel.TryChange( type ) == false ) return;

        OnHoldChanged?.Invoke( previousType, type );
    }

    /// <summary>
    /// 보유 전환 요청 가능 여부 확인
    /// </summary>
    /// <returns>보유 전환 가능 여부</returns>
    bool CanChangeHold ()
    {
        return isActiveAndEnabled &&
                  _view != null && _view.IsReady &&
                  _view.isActiveAndEnabled &&
                  _isChangingHold == false &&
                  _isCarryLocked == false;
    }

    /// <summary>
    /// 활성 객체와 View 준비 여부
    /// </summary>
    /// <returns>활성 View 사용 가능 여부</returns>
    bool HasActiveView ()
    {
        return isActiveAndEnabled && _view != null && _view.IsReady;
    }

    /// <summary>
    /// 정착한 아이템의 배치 대상 조회
    /// </summary>
    /// <param name="placement">현재 지지 표면의 배치 대상</param>
    /// <param name="reason">정착 대기 사유</param>
    /// <returns>현재 상태 저장 가능 여부</returns>
    public bool TryGetSettledPlacement (
        out PlacementTarget placement,
        out string reason )
    {
        placement = null;
        reason = string.Empty;

        if ( HasActiveView( ) == false )
        {
            reason = "활성화되고 초기화된 아이템이 필요합니다.";
            return false;
        }

        if ( IsHold == true )
        {
            reason = "보유 중인 물건의 배치를 기다립니다.";
            return false;
        }

        if ( _view.IsSettled( ) == false )
        {
            reason = "물건이 물리적으로 정착하기를 기다립니다.";
            return false;
        }

        //고정 물건은 기록된 배치 표면 방향 우선 확인
        //기록이 없는 초기 배치는 회전 보정 사용
        //일반 물건은 아래 확인
        if ( _view.TryGetPlacementSupport(
            Vector3.down, PlacementRotationOffset,
            0.05f, out RaycastHit hit ) == false )
        {
            reason = "현재 지지 표면을 기다립니다.";
            return false;
        }

        PlacementTarget current =
            hit.collider.GetComponentInParent<PlacementTarget>( );

        //배치 대상이 있는 표면은 활성 여부와 대표 표면 일치를 확인
        //일반 지형은 배치 대상이 없어도 현재 정착 상태를 저장할 수 있음
        if ( current != null &&
             ( current.isActiveAndEnabled == false ||
               current.SurfaceCollider != hit.collider ) )
        {
            reason = "현재 지지 표면의 배치 대상 설정을 확인해 주세요.";
            return false;
        }

        placement = current;
        return true;
    }

    #endregion

    #region ----- 이동 -----

    /// <summary>
    /// 미보유 아이템 밀어내기
    /// </summary>
    /// <param name="impulse">밀어낼 방향과 충격량</param>
    /// <returns>처리 성공 여부</returns>
    public bool TryPush ( Vector3 impulse )
    {
        if ( HasActiveView( ) == false ||
             _carryModel.CanPush == false )
            return false;

        //밀어내기
        _view.ApplyPush( impulse );

        //이전 직접 배치 요청 취소 후 현재 상태 재검사
        OnPlacementInterrupted?.Invoke( );
        OnPlacementStateChanged?.Invoke( );

        return true;
    }

    /// <summary>
    /// 주변 목적지 탐색 후 아이템 강제 이동
    /// </summary>
    /// <param name="settings">목적지 탐색 설정</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>이동 성공 여부</returns>
    public bool TryDisplace (
        ItemDisplacementSettings settings,
        out string reason )
    {
        reason = null;

        if ( HasActiveView( ) == false )
        {
            reason = "활성화되고 초기화된 아이템이 필요합니다.";
            return false;
        }

        if ( _carryModel.CanDisplace == false )
        {
            reason = "현재 보유 상태에서는 이동할 수 없습니다.";
            return false;
        }

        if ( _view.TryGetDisplacementBounds( out Bounds bounds ) == false )
        {
            reason = "이동 검사에 사용할 충돌 영역이 없습니다.";
            return false;
        }

        if ( ItemDisplacement.TryFindPosition(
            transform, bounds, settings,
            out Vector3 position, out reason ) == false )
            return false;

        //목적지 확보에 성공한 뒤에만 기존 동작과 위치 변경
        PrepareDisplacement( );
        _view.SetDisplacement( position );

        //강제 이동 결과를 이전 직접 배치로 인정하지 않음
        OnPlacementInterrupted?.Invoke( );
        OnPlacementStateChanged?.Invoke( );
        return true;
    }

    /// <summary>
    /// 강제 이동 전 기존 동작 정리
    /// </summary>
    protected virtual void PrepareDisplacement ()
    {
    }

    /// <summary>
    /// 강제 배치 완료 후 기존 동작 정리
    /// </summary>
    public virtual void CompleteForcedPlacement ()
    {
        //강제 배치는 이전 직접 배치 요청을 종료
        OnPlacementInterrupted?.Invoke( );

        //현재 배치 상태 재검사
        OnPlacementStateChanged?.Invoke( );
    }

    #endregion

    #region ----- 풀링 -----

    /// <summary>
    /// 보유와 동작을 종료하고 풀 반환 상태로 정리
    /// </summary>
    /// <param name="reason">반환 준비 실패 사유</param>
    /// <returns>반환 준비 성공 여부</returns>
    public bool TryPreparePoolReturn ( out string reason )
    {
        reason = string.Empty;

        if ( _isChangingHold == true || _isNotifyingConsume == true )
        {
            reason = "아이템 상태 전환 또는 소비 알림이 진행 중입니다.";
            return false;
        }

        //사용 전에 반환하는 비활성 객체도 초기화
        if ( _view == null )
            Init( );

        if ( IsReady == false )
        {
            reason = "아이템 뷰 구성이 준비되지 않았습니다.";
            return false;
        }

        _isChangingHold = true;

        try
        {
            //투척 등 파생 아이템의 동작과 기존 보유 설정부터 복구
            PrepareHold( );
            _view.RestoreHoldSettings( CurrentType );
            ChangeHoldType( HoldType.None );
            _view.Hide( );

            if ( this == null || gameObject.activeSelf == true )
            {
                reason = "반환할 아이템의 비활성화를 확인하지 못했습니다.";
                return false;
            }

            //비활성화 알림이 끝난 뒤 이전 사용 상태 제거
            _view.ResetForPool( );
            _runtimeItemData = null;
            _isCarryLocked = false;
            _releaseOnEnable = false;
            _placementBoundsSize = Vector3.zero;
            return true;
        }
        finally
        {
            _isChangingHold = false;
        }
    }

    /// <summary>
    /// 비활성 대여 아이템의 데이터와 물리 상태 준비
    /// </summary>
    /// <param name="data">이번 대여에 적용할 아이템 데이터</param>
    /// <returns>대여 준비 여부</returns>
    public bool TryPreparePoolRent ( ItemData data )
    {
        if ( gameObject.activeSelf == true || IsHold == true )
            return false;

        //처음부터 비활성이었던 객체도 필요한 모듈 참조를 준비
        if ( _view == null )
            Init( );

        if ( IsReady == false )
            return false;

        _isCarryLocked = false;
        _releaseOnEnable = false;
        SetItemData( data );
        _view.DisablePhysics( );

        //대여 준비가 완료된 경우에만 새 사용 회차로 전환
        _poolRentVersion++;

        return true;
    }

    /// <summary>
    /// 현재 상태를 저장하고 소비를 위해 비활성화
    /// </summary>
    /// <param name="reason">실패 사유</param>
    /// <returns>비활성화 완료 여부</returns>
    public bool TryChangeConsumeState ( out string reason )
    {
        reason = string.Empty;

        if ( CanChangeConsumeState == false )
        {
            reason = "소비 상태로 전환할 수 없는 아이템입니다.";
            return false;
        }

        if ( _consume.TrySaveState( out ItemConsumeState state, out reason ) == false )
            return false;

        state.WasCarryLocked = _isCarryLocked;
        _isChangingHold = true;

        try
        {
            PrepareConsume( );

            if ( isActiveAndEnabled == false )
                return true;

            reason = "아이템 비활성화 콜백 이후 다시 활성화됐습니다.";
        }
        catch ( Exception exception )
        {
            reason = $"아이템 비활성화 중 예외: {exception.Message}";
            Debug.LogException( exception, this );
        }
        finally
        {
            _isChangingHold = false;
        }

        //중간 실패 시 즉시 기존 배치 상태 복구
        if ( TryRestoreConsumeState( state, out string restoreReason ) == false )
        {
            reason += $"\n복구 실패: {restoreReason}";
        }

        return false;
    }

    /// <summary>
    /// 실패한 소비 전환 이전 상태 복구
    /// </summary>
    /// <param name="state">복구할 아이템 상태</param>
    /// <param name="reason">복구 실패 사유</param>
    /// <returns>전체 상태 복구 여부</returns>
    bool TryRestoreConsumeState (
        ItemConsumeState state, out string reason )
    {
        reason = string.Empty;
        if ( gameObject.activeSelf == false )
            gameObject.SetActive( true );

        if ( HasActiveView( ) == false )
        {
            reason = "아이템을 다시 활성화하지 못했습니다.";
            return false;
        }

        bool restored = _consume.TryRestoreState( state, out reason );

        _isCarryLocked = state.WasCarryLocked;

        if ( restored == true )
        {
            //배치 상태 변경 이벤트 발행
            OnPlacementStateChanged?.Invoke( );
            return true;
        }

        return false;
    }

    /// <summary>
    /// 제작 소비를 위한 비활성화
    /// </summary>
    /// <param name="reason">적용 실패 사유</param>
    /// <returns>루트 비활성화 여부</returns>
    public bool TryDeactivateForConsume ( out string reason )
    {
        if ( CheckConsumeState( out reason ) == false )
            return false;

        _isChangingHold = true;

        try
        {
            PrepareConsume( );
        }
        catch ( Exception exception )
        {
            reason = $"아이템 비활성화 중 예외: {exception.Message}";
            Debug.LogException( exception, this );
        }
        finally
        {
            _isChangingHold = false;
        }

        //이미 비활성화된 실제 성공분을 예외나 후속 실패 때문에 복원하지 않음
        if ( this != null && gameObject.activeSelf == false )
            return true;

        reason = "아이템 루트 비활성화를 확인하지 못했습니다.";
        return false;
    }

    /// <summary>
    /// 아이템 데이터 적용
    /// </summary>
    /// <param name="data">적용할 데이터</param>
    public void SetItemData ( ItemData data )
    {
        _runtimeItemData = data;
    }

    /// <summary>
    /// 공급원 재고 등록 상태 설정
    /// </summary>
    /// <param name="isRegistered">등록 여부</param>
    public void SetStockRegistered ( bool isRegistered )
    {
        _isStockRegistered = isRegistered;
    }

    /// <summary>
    /// 소비 확정 알림
    /// </summary>
    /// <param name="reason">소비 원인</param>
    public void NotifyConsumeConfirmed ( IngredientConsumeReason reason )
    {
        bool wasNotifying = _isNotifyingConsume;		//중첩 알림 이전 상태
        _isNotifyingConsume = true;

        try
        {
            OnConsumeConfirmed?.Invoke( this, reason );
        }
        finally
        {
            //모든 구독자의 소비 처리가 끝난 뒤에만 풀 반환 허용
            _isNotifyingConsume = wasNotifying;
        }
    }


    /// <summary>
    /// 소비 전 현재 동작 정리와 비활성화
    /// </summary>
    void PrepareConsume ()
    {
        _isCarryLocked = false;

        _view.RestoreHoldSettings( CurrentType );
        _view.Release( );
        _view.StopMotion( );

        ChangeHoldType( HoldType.None );
        _view.Hide( );
    }


    /// <summary>
    /// 소비 전 배치와 물리 상태 검사
    /// </summary>
    /// <param name="reason">검사 실패 사유</param>
    /// <returns>전환 준비 여부</returns>
    public bool CheckConsumeState ( out string reason )
    {
        reason = string.Empty;

        if ( CanChangeConsumeState == false )
        {
            reason = "소비 상태로 전환할 수 없는 아이템입니다.";
            return false;
        }

        return _consume.CheckState( out reason );
    }

    /// <summary>
    /// 소비 전환 조건 확인
    /// </summary>
    /// <returns>소비 전환 가능 여부</returns>
    bool CheckCanChangeConsumeState ()
    {
        return isActiveAndEnabled == true &&
               _view != null && _view.IsReady &&
               _isChangingHold == false &&
               CurrentType == HoldType.None;
    }

    #endregion

    protected virtual void OnDisable ()
    {
        ItemRegister.Unregister( this );

        //보유 종료 알림 전에 임시 표시와 충돌 설정 정리
        SetHeldHidden( false );

        if ( CurrentType == HoldType.World && IsReady == true )
            _view.SetHoldCollision( false );

        //소비나 외부 비활성화 시 보유자의 참조를 즉시 해제
        _releaseOnEnable = CurrentType == HoldType.View;
        OnUnavailable?.Invoke( );
    }

}
