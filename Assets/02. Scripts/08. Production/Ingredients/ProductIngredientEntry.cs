/// <summary>
/// 제작 재료 항목 - 투입 객체와 아이템 데이터 보관
/// </summary>
public class ProductIngredientEntry
{
    int _instanceId;        //재료 객체 ID
    ItemData _data;        //재료 아이템 데이터

    /// <summary>
    /// 재료 객체 ID
    /// </summary>
    public int InstanceId => _instanceId;

    /// <summary>
    /// 재료 아이템 데이터
    /// </summary>
    public ItemData Data => _data;


    /// <summary>
    /// 제작 재료 등록 정보 생성
    /// </summary>
    /// <param name="instanceId">재료 객체 ID</param>
    /// <param name="data">재료 아이템 데이터</param>
    public ProductIngredientEntry ( int instanceId , ItemData data )
    {
        _instanceId = instanceId;
        _data = data;
    }
}
