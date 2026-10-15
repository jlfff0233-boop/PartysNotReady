
/// <summary>
/// 제작 재료 진행 정보
/// </summary>
public readonly struct ProduceIngredientData
{
    readonly ItemData _data;        //재료 데이터
    readonly int _currentCount;        //현재 투입 수량
    readonly int _requiredCount;        //필요 수량

    /// <summary>
    /// 재료 데이터
    /// </summary>
    public ItemData Data => _data;

    /// <summary>
    /// 현재 투입 수량
    /// </summary>
    public int CurrentCount => _currentCount;

    /// <summary>
    /// 필요 수량
    /// </summary>
    public int RequiredCount => _requiredCount;


    /// <summary>
    /// 제작 재료 진행 정보 생성
    /// </summary>
    /// <param name="data">재료 데이터</param>
    /// <param name="currentCount">현재 투입 수량</param>
    /// <param name="requiredCount">필요 수량</param>
    public ProduceIngredientData (
        ItemData data , int currentCount , int requiredCount )
    {
        _data = data;
        _currentCount = currentCount;
        _requiredCount = requiredCount;
    }
}