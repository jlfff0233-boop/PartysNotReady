/// <summary>
/// 소비 재료 항목
/// </summary>
public struct IngredientConsumptionEntry
{
    int _id;        //재료 ID

    ProductIngredient _ingredient;        //소비 재료
    ItemCarry _item;        //재료 아이템

    /// <summary>
    /// 재료 ID
    /// </summary>
    public int Id => _id;

    /// <summary>
    /// 소비 재료
    /// </summary>
    public ProductIngredient Ingredient => _ingredient;

    /// <summary>
    /// 재료 아이템
    /// </summary>
    public ItemCarry Item => _item;


    /// <summary>
    /// 소비 대상 재료 기록 생성
    /// </summary>
    /// <param name="id">재료 ID</param>
    /// <param name="ingredient">소비 재료</param>
    /// <param name="item">재료 아이템</param>
    public IngredientConsumptionEntry ( int id , ProductIngredient ingredient , ItemCarry item )
    {
        _id = id;
        _ingredient = ingredient;
        _item = item;
    }
}
