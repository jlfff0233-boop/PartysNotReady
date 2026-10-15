using System;

/// <summary>
/// 미션 목표 진행 - 목표 정보와 현재 완료 수량 보관
/// </summary>
public class GoalProgressData
{
    string _id;      //목표 ID
    string _name;      //표시 이름
    int _required;      //필요 수량
    int _current;      //현재 완료 수량

    /// <summary>
    /// 목표 ID
    /// </summary>
    public string Id => _id;
    /// <summary>
    /// 표시 이름
    /// </summary>
    public string Name => _name;
    /// <summary>
    /// 필요 수량
    /// </summary>
    public int Required => _required;
    /// <summary>
    /// 현재 완료 수량
    /// </summary>
    public int Current => _current;

    /// <summary>
    /// 목표 진행 상태 생성
    /// </summary>
    /// <param name="id">목표 ID</param>
    /// <param name="name">표시 이름</param>
    /// <param name="required">필요 수량</param>
    public GoalProgressData ( string id, string name, int required )
    {
        _id = id;
        _name = name;
        _required = required;
    }

    /// <summary>
    /// 남은 필요 수량 안에서 완료 개수 반영
    /// </summary>
    /// <param name="amount">요청한 완료 개수</param>
    /// <param name="addedAmount">실제로 증가한 개수</param>
    /// <returns>진행도 증가 여부</returns>
    public bool TryAdd ( int amount, out int addedAmount )
    {
        addedAmount = 0;

        if ( amount <= 0 || _current >= _required )
            return false;

        //목표 필요 수량을 넘는 요청은 남은 수량만 반영
        addedAmount = Math.Min( amount, _required - _current );
        _current += addedAmount;
        return true;
    }
}
