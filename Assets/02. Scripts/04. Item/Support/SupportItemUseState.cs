
/// <summary>
/// 서포트 아이템 사용 횟수
/// </summary>
public class SupportItemUseState
{
    int _remainingUses;		//남은 횟수

    /// <summary>
    /// 남은 횟수
    /// </summary>
    public int RemainingUses => _remainingUses;

    /// <summary>
    /// 사용 횟수 설정
    /// </summary>
    /// <param name="useCount">사용 횟수</param>
    public SupportItemUseState ( int useCount )
    {
        _remainingUses = useCount;
    }

    /// <summary>
    /// 사용 횟수 차감
    /// </summary>
    public void Consume ()
    {
        if ( _remainingUses > 0 )
            _remainingUses--;
    }
}