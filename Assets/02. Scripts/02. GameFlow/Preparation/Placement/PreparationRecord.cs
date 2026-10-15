using System;

/// <summary>
/// 준비 실행 기록 - 제작 결과의 배치 판정, 배치 대기와 이벤트 구독 관리
/// </summary>
public class PreparationRecord
{
    ProductionResult _result;      //추적할 제작 완료 결과
    FoodPlacementData _setup;      //유효 배치 설정
    bool _isSubscribed;      //아이템 이벤트 구독 여부
    bool _isWaitingForPlacedEvent;      //보유 해제 후 배치 이벤트 대기 여부
    PlacementTarget _pendingPlacementTarget;      //실제 정착을 확인할 배치 대상

    /// <summary>
    /// 배치 처리 함수(배치 대상)
    /// </summary>
    Action<PlacementTarget> _placedHandler;
    /// <summary>
    /// 배치 상태 변경 처리 함수(전달값 없음)
    /// </summary>
    Action _placementChangedHandler;
    /// <summary>
    /// 보유 변경 처리 함수(이전 보유 방식, 현재 보유 방식)
    /// </summary>
    Action<HoldType, HoldType> _holdHandler;
    /// <summary>
    /// 사용 불가 처리 함수(전달값 없음)
    /// </summary>
    Action _unavailableHandler;

    /// <summary>
    /// 제작 완료 결과
    /// </summary>
    public ProductionResult Result => _result;
    /// <summary>
    /// 적용할 배치 설정
    /// </summary>
    public FoodPlacementData Setup => _setup;
    /// <summary>
    /// 상태 확인 대기 대상
    /// </summary>
    public PlacementTarget PendingPlacementTarget => _pendingPlacementTarget;
    /// <summary>
    /// 배치 이벤트 대기 여부
    /// </summary>
    public bool IsWaitingForPlacedEvent => _isWaitingForPlacedEvent;


    /// <summary>
    /// 완성품 추적 기록 생성
    /// </summary>
    /// <param name="result">제작 결과</param>
    /// <param name="setup">배치 설정</param>
    public PreparationRecord ( ProductionResult result, FoodPlacementData setup )
    {
        _result = result;
        _setup = setup;
    }

    #region ----- 이벤트 -----

    /// <summary>
    /// 아이템 이벤트 처리 함수 등록
    /// </summary>
    /// <param name="placed">배치 처리</param>
    /// <param name="changed">배치 상태 변경 처리</param>
    /// <param name="hold">보유 변경 처리</param>
    /// <param name="unavailable">사용 불가 처리</param>
    public void SetHandlers (
        Action<PlacementTarget> placed, Action changed,
        Action<HoldType, HoldType> hold, Action unavailable )
    {
        if ( _placedHandler != null || placed == null || changed == null ||
             hold == null || unavailable == null )
            return;

        _placedHandler = placed;
        _placementChangedHandler = changed;
        _holdHandler = hold;
        _unavailableHandler = unavailable;
    }

    /// <summary>
    /// 음식 아이템 이벤트 구독
    /// </summary>
    public void Subscribe ()
    {
        if ( _isSubscribed == true || _result?.Item == null ||
             _placedHandler == null )
            return;

        ItemCarry item = _result.Item;
        item.OnPlaced += _placedHandler;
        item.OnPlacementStateChanged += _placementChangedHandler;
        item.OnHoldChanged += _holdHandler;
        item.OnUnavailable += _unavailableHandler;
        _isSubscribed = true;
    }

    /// <summary>
    /// 음식 아이템 이벤트 구독 해제
    /// </summary>
    public void Unsubscribe ()
    {
        if ( _isSubscribed == false || _result?.Item == null )
            return;

        ItemCarry item = _result.Item;
        item.OnPlaced -= _placedHandler;
        item.OnPlacementStateChanged -= _placementChangedHandler;
        item.OnHoldChanged -= _holdHandler;
        item.OnUnavailable -= _unavailableHandler;
        _isSubscribed = false;
    }

    #endregion

    #region ----- 배치 확인 -----

    /// <summary>
    /// 배치 이벤트 대기 시작
    /// </summary>
    public void WaitForPlace ()
    {
        _isWaitingForPlacedEvent = true;
    }

    /// <summary>
    /// 배치 이벤트 대기 종료
    /// </summary>
    public void ResumeCheck ()
    {
        _isWaitingForPlacedEvent = false;
    }

    /// <summary>
    /// 실제 배치 확인 대상 저장
    /// </summary>
    /// <param name="target">확인할 배치 대상</param>
    public void SetPendingTarget ( PlacementTarget target )
    {
        _isWaitingForPlacedEvent = false;
        _pendingPlacementTarget = target;
    }

    /// <summary>
    /// 배치 확인 대기 취소
    /// </summary>
    public void ClearPlacement ()
    {
        _isWaitingForPlacedEvent = false;
        _pendingPlacementTarget = null;
    }

    /// <summary>
    /// 대상 소비 후 실제 대상과 비교
    /// </summary>
    /// <param name="target">실제 정착 대상</param>
    /// <returns>대상에 정착했는지 여부</returns>
    public bool TakePendingTarget ( PlacementTarget target )
    {
        bool matches = _pendingPlacementTarget == target;
        _pendingPlacementTarget = null;
        return matches;
    }

    /// <summary>
    /// 완성품 배치 확인
    /// </summary>
    /// <param name="isPlaced">배치 여부</param>
    /// <param name="isNewPlacement">새 배치 요청 확인인지 여부</param>
    /// <returns>집계 상태 갱신 가능 여부</returns>
    public bool CheckPlacement (
        out bool isPlaced, out bool isNewPlacement )
    {
        isPlaced = false;
        isNewPlacement = false;

        if ( _result == null || _setup == null )
            return false;

        ItemCarry item = _result.Item;

        if ( item == null ||
            item.isActiveAndEnabled == false ||
            item.IsHold == true )
        {
            ClearPlacement( );
            return true;
        }

        //정착 전에는 직접 배치 요청을 보존
        if ( item.TryGetSettledPlacement(
            out PlacementTarget target, out _ ) == false )
            return true;

        if ( _setup.ContainsTarget( target ) == false )
        {
            ClearPlacement( );
            return true;
        }

        //보유 해제 직후에는
        //배치 이벤트가 전달될 때까지 집계를 보류
        if ( _isWaitingForPlacedEvent == true )
            return false;

        if ( _pendingPlacementTarget != null )
            isNewPlacement = TakePendingTarget( target );

        isPlaced = true;
        return true;
    }

    #endregion
}
