/// <summary>
/// 제작 완료 결과 - 결과, 완성품 정보 전달
/// </summary>
public class ProductionResult
{
    string _id;        //고유 결과 ID
    string _recipeId;        //제작 레시피 ID
    RecipeData _recipe;        //선택한 레시피
    ItemData _data;        //완성품 데이터
    ItemCarry _item;        //완성품 아이템
    CookController _source;        //완료를 전달한 제작 대상

    /// <summary>
    /// 고유 결과 ID
    /// </summary>
    public string Id => _id;

    /// <summary>
    /// 제작 레시피 ID
    /// </summary>
    public string RecipeId => _recipeId;

    /// <summary>
    /// 선택한 레시피
    /// </summary>
    public RecipeData Recipe => _recipe;

    /// <summary>
    /// 완성품 데이터
    /// </summary>
    public ItemData Data => _data;

    /// <summary>
    /// 완성품 아이템
    /// </summary>
    public ItemCarry Item => _item;

    /// <summary>
    /// 완료를 전달한 제작 대상
    /// </summary>
    public CookController Source => _source;


    /// <summary>
    /// 제작 완료 결과 생성
    /// </summary>
    /// <param name="id">고유 결과 ID</param>
    /// <param name="recipe">선택한 레시피</param>
    /// <param name="data">완성품 데이터</param>
    /// <param name="item">완성품 아이템</param>
    /// <param name="source">완료를 전달한 제작 대상</param>
    public ProductionResult (
        string id,
        RecipeData recipe,
        ItemData data,
        ItemCarry item,
        CookController source )
    {
        _id = id;
        _recipe = recipe;
        _recipeId = recipe == null ? string.Empty : recipe.RecipeId;
        _data = data;
        _item = item;
        _source = source;
    }


}
