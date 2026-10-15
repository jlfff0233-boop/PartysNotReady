
/// <summary>
/// 보유 수량 확장 - 한 번에 들 수 있는 아이템 수량 증가
/// </summary>
public class CapacityExpandEffect : ISupportItemEffect
{
    CapacityExpandData _data;		//보유 수량 설정
    PlayerItemHolder _holder;       //플레이어 보유

    /// <summary>
    /// 보유 기능 연결
    /// </summary>
    /// <param name="data">보유 수량 설정</param>
    /// <param name="holder">보유 기능</param>
    public CapacityExpandEffect (
        CapacityExpandData data, PlayerItemHolder holder )
    {
        _data = data;
        _holder = holder;
    }

    /// <summary>
    /// 보유 한도 확장
    /// </summary>
    /// <returns>실행 결과</returns>
    public SupportItemUseResult Use ()
    {
        if ( _data.IsValid == false )
            return SupportItemUseResult.Rejected;

        return _holder.TrySetCapacity( _data.Capacity ) == true
            ? SupportItemUseResult.Applied
            : SupportItemUseResult.Rejected;
    }
}
