using System;
using UnityEngine;

/// <summary>
/// 제작에 필요한 아이템과 수량
/// </summary>
[Serializable]
public class IngredientRequirement
{
    [SerializeField] ItemData _item;		//필요한 재료
    [SerializeField, Min( 1 )] int _requiredCount = 1;		//필요 수량


    /// <summary>
    /// 필요한 재료
    /// </summary>
    public ItemData Item => _item;

    /// <summary>
    /// 필요한 수량
    /// </summary>
    public int RequiredCount => Mathf.Max( 1, _requiredCount );
}

/// <summary>
/// 제작 레시피 데이터 - 제작 아이디, 요구 재료와 결과
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/Production/RecipeData" )]
public class RecipeData : ScriptableObject
{
    [SerializeField] string _recipeId;		//제작 아이디
    [SerializeField] ItemData _resultItem;		//완성 결과
    [SerializeField] IngredientRequirement [ ] _ingredients;		//필요 재료
    [SerializeField] GameObject _resultPrefab;		//완성 시 그릇 안에 표시할 내용물
    [SerializeField] ProduceMinigameData _produceMinigame;		//제작 미니게임 설정

    /// <summary>
    /// 제작 아이디
    /// </summary>
    public string RecipeId => _recipeId;

    /// <summary>
    /// 완성 결과
    /// </summary>
    public ItemData ResultItem => _resultItem;

    /// <summary>
    /// 필요한 재료 종류 수
    /// </summary>
    public int IngredientCount => _ingredients == null ? 0 : _ingredients.Length;

    /// <summary>
    /// 완성물 프리팹
    /// </summary>
    public GameObject ResultPrefab => _resultPrefab;

    /// <summary>
    /// 제작 미니게임 설정
    /// </summary>
    public ProduceMinigameData ProduceMinigame => _produceMinigame;

    /// <summary>
    /// 필요한 재료 조회
    /// </summary>
    /// <param name="index">재료 번호</param>
    /// <returns>재료 요구 정보, 범위 밖이면 null</returns>
    public IngredientRequirement GetIngredient ( int index )
    {
        if ( index < 0 || index >= IngredientCount )
            return null;

        return _ingredients [ index ];
    }
}
