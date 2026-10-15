using System;

/// <summary>
/// 일반 아이템 배치 기록 - 직접 배치 요청과 실제 정착 확인
/// </summary>
public class ItemPlacementMissionRecord
{
    ItemCarry _item;        //추적할 아이템
    ItemPlacementMissionData _setup;        //배치 미션 설정
    string _resultId;       //결과 ID

    ulong _poolRentVersion;		//기록 생성 당시 풀 대여 회차
    PlacementTarget _pendingTarget;     //정착을 확인할 요청 표면
    bool _isSubscribed;     //아이템 이벤트 구독 여부

    /// <summary>
    /// 배치 재검사 처리 함수(검사할 기록)
    /// </summary>
    Action<ItemPlacementMissionRecord> _changedHandler;


    /// <summary>
    /// 추적할 아이템
    /// </summary>
    public ItemCarry Item => _item;

    /// <summary>
    /// 배치 미션 설정
    /// </summary>
    public ItemPlacementMissionData Setup => _setup;

    /// <summary>
    /// 물건 식별에 사용하는 결과 ID
    /// </summary>
    public string ResultId => _resultId;

    /// <summary>
    /// 기록 생성 당시 풀 대여 회차
    /// </summary>
    public ulong PoolRentVersion => _poolRentVersion;


    #region ----- 초기화/구독 -----

    /// <summary>
    /// 일반 아이템 배치 기록 생성
    /// </summary>
    /// <param name="item">추적할 아이템</param>
    /// <param name="setup">배치 미션 설정</param>
    /// <param name="resultId">물건에 유지되는 결과 ID</param>
    /// <param name="changed">배치 재검사 처리</param>
    public ItemPlacementMissionRecord (
        ItemCarry item,
        ItemPlacementMissionData setup,
        string resultId,
        Action<ItemPlacementMissionRecord> changed )
    {
        _item = item;
        _setup = setup;
        _resultId = resultId;
        _poolRentVersion = item.PoolRentVersion;
        _changedHandler = changed;
    }

    /// <summary>
    /// 아이템 이벤트 구독
    /// </summary>
    public void Subscribe ()
    {
        if ( _isSubscribed == true ||
             _item == null ||
             _item.isActiveAndEnabled == false )
            return;

        _item.OnPlaced += HandlePlaced;
        _item.OnHoldChanged += HandleHoldChanged;
        _item.OnPlacementStateChanged += HandlePlacementChanged;
        _item.OnPlacementInterrupted += HandlePlacementInterrupted;
        _item.OnUnavailable += HandleUnavailable;

        _isSubscribed = true;
    }

    /// <summary>
    /// 아이템 이벤트 구독 해제와 배치 대기 정리
    /// </summary>
    public void Unsubscribe ()
    {
        ClearPlacement( );

        if ( _isSubscribed == false )
            return;

        if ( _item != null )
        {
            _item.OnPlaced -= HandlePlaced;
            _item.OnHoldChanged -= HandleHoldChanged;
            _item.OnPlacementStateChanged -= HandlePlacementChanged;
            _item.OnPlacementInterrupted -= HandlePlacementInterrupted;
            _item.OnUnavailable -= HandleUnavailable;
        }

        _isSubscribed = false;
    }

    #endregion

    #region ----- 핸들 -----

    /// <summary>
    /// 직접 배치 요청 표면 저장과 재검사 요청
    /// </summary>
    /// <param name="target">배치 요청에서 선택한 표면</param>
    void HandlePlaced ( PlacementTarget target )
    {
        _pendingTarget =
            _setup.ContainsTarget( target ) == true ? target : null;

        _changedHandler?.Invoke( this );
    }

    /// <summary>
    /// 보유 전환 시 이전 배치 요청 취소
    /// </summary>
    /// <param name="previousType">이전 보유 방식</param>
    /// <param name="currentType">현재 보유 방식</param>
    void HandleHoldChanged (
        HoldType previousType, HoldType currentType )
    {
        //보유 해제만으로 직접 배치 요청을 생성하지 않음
        ClearPlacement( );
        _changedHandler?.Invoke( this );
    }

    /// <summary>
    /// 강제 이동 시 직접 배치 확인 대기 취소
    /// </summary>
    void HandlePlacementInterrupted ()
    {
        ClearPlacement( );
    }

    /// <summary>
    /// 배치 상태 변경 시 재검사 요청
    /// </summary>
    void HandlePlacementChanged ()
    {
        _changedHandler?.Invoke( this );
    }

    /// <summary>
    /// 사용 중단 시 배치 요청 취소
    /// </summary>
    void HandleUnavailable ()
    {
        ClearPlacement( );
        _changedHandler?.Invoke( this );
    }

    #endregion

    #region ----- 배치 확인 -----

    /// <summary>
    /// 직접 배치 확인 대기 취소
    /// </summary>
    public void ClearPlacement ()
    {
        _pendingTarget = null;
    }

    /// <summary>
    /// 직접 배치 요청의 실제 정착 확인
    /// </summary>
    /// <returns>직접 배치 완료 여부</returns>
    public bool CheckPlacement ()
    {
        if ( _pendingTarget == null )
            return false;

        if ( _item == null ||
             _item.isActiveAndEnabled == false ||
             _item.IsHold == true )
        {
            ClearPlacement( );
            return false;
        }

        //정착 전에는 직접 배치 요청 유지
        if ( _item.TryGetSettledPlacement(
            out PlacementTarget target, out _ ) == false )
            return false;

        bool isValid = target == _pendingTarget &&
            _setup.ContainsTarget( target ) == true;

        //정착 결과를 확인한 요청은 한 번만 처리
        ClearPlacement( );
        return isValid;
    }

    #endregion

}
