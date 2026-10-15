
/// <summary>
/// 운반 아이템 정보 - 보유 모듈과 차지 용량
/// </summary>
public class MultiItemHoldData
{
    ItemHolder _holder;        //보유 모듈
    int _capacityCost;        //확보 당시 차지 용량

    /// <summary>
    /// 보유 모듈
    /// </summary>
    public ItemHolder Holder => _holder;

    /// <summary>
    /// 확보 당시 차지 용량
    /// </summary>
    public int CapacityCost => _capacityCost;


    /// <summary>
    /// 운반 정보 설정
    /// </summary>
    /// <param name="holder">보유 모듈</param>
    /// <param name="capacityCost">차지 용량</param>
    public MultiItemHoldData ( ItemHolder holder, int capacityCost )
    {
        _holder = holder;

        //도중에 SO 값이 바뀌어도 추가와 차감에는 같은 값 사용
        _capacityCost = capacityCost;
    }
}
