using UnityEngine;

/// <summary>
/// 아이템 수용량 모델 - 용량 한도 판정과 사용량 관리
/// </summary>
public class ItemCapacityModel
{
    int _capacity;     //최대 수용량
    int _usedCapacity;     //사용 중인 용량

    /// <summary>
    /// 최대 수용량
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// 사용 중인 용량
    /// </summary>
    public int UsedCapacity => _usedCapacity;

    /// <summary>
    /// 남은 수용량
    /// </summary>
    public int RemainingCapacity => _capacity - _usedCapacity;


    /// <summary>
    /// 최대 수용량 설정
    /// </summary>
    /// <param name="capacity">최대 수용량, 음수면 0</param>
    public ItemCapacityModel ( int capacity )
    {
        _capacity = Mathf.Max( 0, capacity );
    }

    /// <summary>
    /// 확보한 물건의 사용 용량 반영
    /// </summary>
    /// <param name="capacityCost">확보한 물건이 차지하는 용량</param>
    /// <returns>사용량 반영 여부</returns>
    public bool TryAdd ( int capacityCost )
    {
        if ( CanAdd( capacityCost ) == false )
            return false;

        _usedCapacity += capacityCost;
        return true;
    }

    /// <summary>
    /// 보유에서 빠진 물건의 사용 용량 차감
    /// </summary>
    /// <param name="capacityCost">확보할 때 기록한 물건의 용량</param>
    /// <returns>사용량 차감 여부</returns>
    public bool TryRemove ( int capacityCost )
    {
        //잘못된 차감 요청으로 사용량이 음수가 되지 않도록 검사
        if ( capacityCost <= 0 || capacityCost > _usedCapacity )
            return false;

        _usedCapacity -= capacityCost;
        return true;
    }

    /// <summary>
    /// 수집한 물건 정리 후 사용량 초기화
    /// </summary>
    public void Clear ()
    {
        _usedCapacity = 0;
    }

    /// <summary>
    /// 남은 공간에 물건을 담을 수 있는지 확인
    /// </summary>
    /// <param name="capacityCost">추가할 물건이 차지하는 용량</param>
    /// <returns>추가 가능 여부</returns>
    public bool CanAdd ( int capacityCost )
    {
        //합산 전에 남은 공간과 비교해 정수 범위 초과도 방지
        return capacityCost > 0 &&
               capacityCost <= RemainingCapacity;
    }
}
