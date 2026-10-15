using System;
using UnityEngine;


/// <summary>
/// 변칙 대상 - 정상 상태 저장과 배치 알림 연결
/// </summary>
public class AnomalyTarget : MonoBehaviour
{
    ItemCarry _item;
    AnomalyTargetState _normalState;        //정상 상태

    bool _normalLocked;     //사건 진행 중 정상 상태 갱신 잠금
    bool _hasStarted;     //최초 초기화 이후 재활성화 구분
    bool _normalRefreshPending;     //초기 배치와 이동 사건 종료 후 정착 대기
    string _normalRefreshReason = string.Empty;     //정상 기준을 아직 갱신하지 못한 이유

    /// <summary>
    /// 정상 기준 갱신 대기 여부
    /// </summary>
    public bool NormalRefreshPending => _normalRefreshPending;

    /// <summary>
    /// 정상 기준 갱신 대기 사유
    /// </summary>
    public string NormalRefreshReason => _normalRefreshReason;

    /// <summary>
    /// 저장된 정상 상태
    /// </summary>
    public AnomalyTargetState NormalState => _normalState;

    /// <summary>
    /// 정상 상태 갱신 잠금 여부
    /// </summary>
    public bool NormalLocked => _normalLocked || _normalRefreshPending;

    /// <summary>
    /// 사건이 정상 상태를 잠근 상태인지 여부
    /// </summary>
    public bool IsEventLocked => _normalLocked;

    /// <summary>
    /// 대상에 연결된 아이템의 현재 보유 여부
    /// </summary>
    public bool IsHeld => _item != null && _item.IsHold;

    /// <summary>
    /// 배치 시도 완료 알림(배치 대상)
    /// </summary>
    public event Action<PlacementTarget> OnPlacementAttempt;
    /// <summary>
    /// 무효화 알림 이벤트
    /// </summary>
    public event Action OnInvalidated;

    /// <summary>
    /// 초기 정상 기준 설정
    /// </summary>
    void InitNormalState ()
    {
        //배치 알림으로 이미 저장한 정상 기준은 유지
        if ( _normalState != null )
            return;

        //운반 기능이 없는 대상은 표면 조회 없이 현재 상태 저장
        if ( _item == null )
        {
            SaveNormalState( null );
            return;
        }

        //운반 대상은 실제 지지 표면을 조회하고 실패하면 정착 대기
        RequestNormalStateRefresh( );
        TryRefreshNormalState( );
    }

    private void Awake ()
    {
        //운반 기능이 있는 대상은 배치와 보유 알림을 함께 추적
        _item = GetComponent<ItemCarry>( );
    }

    void Update ()
    {
        //정상 기준 갱신을 기다리는 운반 대상만 조회
        if ( _normalRefreshPending == false || _item == null )
            return;

        TryRefreshNormalState( );
    }

    void OnEnable ()
    {
        //운반 기능이 있는 대상의 배치와 보유 알림 연결
        if ( _item != null )
        {
            _item.OnPlaced += HandlePlaced;
            _item.OnHoldChanged += HandleHoldChanged;
        }

        //재활성화된 대상도 최초 초기화와 같은 경로로 현재 배치 확인
        if ( _hasStarted == true )
            InitNormalState( );

        AnomalyRegister.Register( this );
    }

    private void OnDisable ()
    {
        //후보 등록과 알림을 해제한 뒤 기존 정상 기준을 비우고 무효화 전달
        AnomalyRegister.Unregister( this );

        if ( _item != null )
        {
            _item.OnPlaced -= HandlePlaced;
            _item.OnHoldChanged -= HandleHoldChanged;
        }

        _normalState = null;
        _normalRefreshPending = false;
        _normalRefreshReason = string.Empty;
        OnInvalidated?.Invoke( );
    }

    private void Start ()
    {
        _hasStarted = true;
        InitNormalState( );
    }


    /// <summary>
    /// 정상 기준 갱신
    /// </summary>
    /// <returns>정상 기준 저장 성공 여부</returns>
    bool TryRefreshNormalState ()
    {
        //사건 진행 중에는 현재 상태를 정상 기준으로 저장하지 않음
        if ( _normalLocked == true )
            return false;

        if ( _item.TryGetSettledPlacement(
            out PlacementTarget placement, out _normalRefreshReason ) == false )
            return false;

        SaveNormalState( placement );
        return true;
    }


    #region ----- 정상 기준 관리 -----

    /// <summary>
    /// 현재 월드 상태와 배치 정보를 정상 기준으로 저장
    /// </summary>
    /// <param name="target">현재 배치 대상</param>
    public void SaveNormalState ( PlacementTarget target )
    {
        if ( _normalLocked == true )
            return;

        _normalRefreshPending = false;

        _normalRefreshReason = string.Empty;

        _normalState = new AnomalyTargetState(
            transform.position, transform.rotation,
            gameObject.activeSelf, transform.parent,
            target );
    }

    /// <summary>
    /// 이동 사건 종료 후 정착한 상태를 정상 기준으로 저장하도록 요청
    /// </summary>
    public void RequestNormalStateRefresh ()
    {
        //정착 대기 중에도 NormalLocked는 true이므로 새 사건 후보에서 제외
        _normalRefreshPending = true;
        _normalRefreshReason = "배치 완료 또는 물리 정착을 기다립니다.";
    }

    /// <summary>
    /// 정상 상태 갱신 잠금 설정
    /// </summary>
    /// <param name="isLocked">잠금 여부</param>
    public void SetNormalLocked ( bool isLocked )
    {
        //동일한 잠금 요청은 로그를 반복하지 않음
        _normalLocked = isLocked;
    }

    #endregion

    #region ----- 대상 알림 -----

    /// <summary>
    /// 다른 적이 대상을 보유하면 사건 무효화
    /// </summary>
    /// <param name="previous">이전 보유 방식</param>
    /// <param name="current">현재 보유 방식</param>
    void HandleHoldChanged ( HoldType previous, HoldType current )
    {
        //플레이어가 집는 것은 해결 과정이므로 취소하지 않음
        if ( current != HoldType.World )
            return;

        _normalState = null;
        _normalRefreshPending = false;
        _normalRefreshReason = string.Empty;
        OnInvalidated?.Invoke( );
    }

    /// <summary>
    /// 실제 배치 완료 처리
    /// </summary>
    /// <param name="target">배치한 대상</param>
    void HandlePlaced ( PlacementTarget target )
    {
        //사건 중에는 정상 상태를 덮어쓰지 않고 해결 시도로 전달
        if ( _normalLocked == true )
        {
            OnPlacementAttempt?.Invoke( target );

            //해결 알림 중 잠금이 풀렸는지 재확인한 뒤 실제 배치 결과 저장
            //같은 알림 중 대상이 무효화됐다면 정상 기준을 되살리지 않음
            if ( _normalLocked == false && _normalState != null )
                SaveNormalState( target );

            return;
        }

        SaveNormalState( target );
    }

    #endregion

}
