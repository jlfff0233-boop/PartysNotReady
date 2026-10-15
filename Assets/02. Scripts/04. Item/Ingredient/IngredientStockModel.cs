using System.Collections.Generic;


/// <summary>
/// 재료 재고 모델 - 공급 수량과 재료 객체 상태 관리
/// </summary>
public class IngredientStockModel
{
    /// <summary>
    /// 객체 상태
    /// </summary>
    enum InstanceState
    {
        Reserved,       //획득 예약
        Taken,      //가져감
        Consumed        //소비 후 반환 대기
    }

    int _maxCount;      //최대 수량
    int _currentCount;      //현재 수량
    int _reservedCount;      //획득 예약 수량

    float _refillDuration;     //재보충 소요 시간
    RefillTimer _refill = new( );     //공급원별 재보충 진행

    /// <summary>
    /// 객체 상태 딕셔너리(객체 아이디, 객체 상태)
    /// </summary>
    Dictionary<int, InstanceState> _instances = new( );


    /// <summary>
    /// 현재 수량
    /// </summary>
    public int CurrentCount => _currentCount;

    /// <summary>
    /// 최대 수량
    /// </summary>
    public int MaxCount => _maxCount;

    /// <summary>
    /// 재보충 시간 완료 여부
    /// </summary>
    public bool IsRefillElapsed => _refill.IsElapsed;

    /// <summary>
    /// 재보충 진행 여부
    /// </summary>
    public bool IsRefilling => _refill.IsRefilling;

    /// <summary>
    /// 재보충 남은 시간
    /// </summary>
    public float RemainingTime => _refill.RemainingTime;

    /// <summary>
    /// 재보충 완료 진행률
    /// </summary>
    public float RefillProgress => _refill.Progress;

    /// <summary>
    /// 공급원에서 획득 가능한 수량 존재 여부
    /// </summary>
    public bool HasAvailableStock => _currentCount > _reservedCount;


    /// <summary>
    /// 재고 모델 생성
    /// </summary>
    /// <param name="maxCount">공급원 최대 재고 수량</param>
    /// <param name="refillDuration">재보충 소요 시간</param>
    public IngredientStockModel ( int maxCount, float refillDuration )
    {
        _maxCount = maxCount;
        _currentCount = maxCount;
        _refillDuration = refillDuration;
    }

    /// <summary>
    /// 객체 획득 예약
    /// </summary>
    /// <param name="instanceId">객체 아이디</param>
    /// <returns>상태 변경 여부</returns>
    public bool TryReserve ( int instanceId )
    {
        //풀에서 새로 대여한 객체만 예약하고 공급 수량은 유지
        if ( HasAvailableStock == false ||
             _instances.TryAdd( instanceId, InstanceState.Reserved ) == false )
            return false;

        _reservedCount++;
        return true;
    }

    /// <summary>
    /// 실패한 획득 예약과 객체 기록 제거
    /// </summary>
    /// <param name="instanceId">객체 아이디</param>
    /// <returns>상태 변경 여부</returns>
    public bool TryCancelReserve ( int instanceId )
    {
        //객체 상태 가져오기
        if ( _instances.TryGetValue( instanceId, out InstanceState state ) == false ||
             state != InstanceState.Reserved )
            return false;

        _instances.Remove( instanceId );
        _reservedCount--;
        return true;
    }

    /// <summary>
    /// 획득 성공 시 재고 차감
    /// </summary>
    /// <param name="instanceId">획득한 객체 아이디</param>
    /// <returns>획득 확정 여부</returns>
    public bool TryConfirmTaken ( int instanceId )
    {
        if ( _instances.TryGetValue( instanceId, out InstanceState state ) == false ||
             state != InstanceState.Reserved )
            return false;

        _instances [ instanceId ] = InstanceState.Taken;
        _reservedCount--;
        _currentCount--;
        if ( _currentCount == 0 )
            _refill.TryStart( _refillDuration );
        return true;
    }

    /// <summary>
    /// 소비한 객체를 반환 대기로 변경
    /// </summary>
    /// <param name="instanceId">객체 아이디</param>
    /// <returns>신규 소비 반영 여부</returns>
    public bool TryMarkConsumed ( int instanceId )
    {
        //획득한 객체만 소비 처리
        if ( _instances.TryGetValue( instanceId, out InstanceState state ) == false ||
             state != InstanceState.Taken )
            return false;

        //소비 상태로 변경
        _instances [ instanceId ] = InstanceState.Consumed;
        return true;
    }

    /// <summary>
    /// 풀 반환 전 소비 객체 기록 제거
    /// </summary>
    /// <param name="instanceId">소비한 객체 아이디</param>
    /// <returns>기록 제거 여부</returns>
    public bool TryUnregisterConsumed ( int instanceId )
    {
        if ( _instances.TryGetValue( instanceId, out InstanceState state ) == false ||
             state != InstanceState.Consumed )
            return false;
        _instances.Remove( instanceId );
        return true;
    }

    /// <summary>
    /// 시간이 끝난 공급원 재고 복구
    /// </summary>
    /// <returns>재보충 반영 여부</returns>
    public bool TryCompleteRefill ()
    {
        if ( _refill.IsElapsed == false || _currentCount != 0 )
            return false;

        _currentCount = _maxCount;
        _refill.Cancel( );
        return true;
    }


    /// <summary>
    /// 재보충 시간 단축
    /// </summary>
    /// <param name="ratio">감소 비율</param>
    /// <returns>단축 여부</returns>
    public bool TryReduceRefill ( float ratio )
    {
        return _currentCount == 0 && _refill.TryReduceRemaining( ratio ) == true;
    }


    /// <summary>
    /// 진행 중인 재보충 시간 갱신
    /// </summary>
    /// <param name="deltaTime">이번 게임의 경과 시간</param>
    public void Tick ( float deltaTime )
    {
        _refill.Tick( deltaTime );
    }

#if UNITY_EDITOR
    /// <summary>
    /// 객체 상태별 수량 조회
    /// </summary>
    /// <returns>획득 예약, 외부 사용과 반환 대기 수량</returns>
    public string GetDebugInstanceStatus ()
    {
        int taken = 0;
        int consumed = 0;
        foreach ( InstanceState state in _instances.Values )
        {
            if ( state == InstanceState.Taken ) taken++;
            if ( state == InstanceState.Consumed ) consumed++;
        }
        return $"획득 예약 {_reservedCount} / 외부 사용 {taken} / 반환 대기 {consumed}";
    }

    /// <summary>
    /// 공급 수량만 소진하여 재보충 시작
    /// </summary>
    /// <returns>재고 소진 여부</returns>
    public bool TryDebugDeplete ()
    {
        if ( _currentCount == 0 || _reservedCount != 0 )
            return false;
        _currentCount = 0;
        return _refill.TryStart( _refillDuration );
    }
#endif
}
