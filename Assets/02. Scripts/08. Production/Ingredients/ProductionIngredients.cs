using System.Collections.Generic;

/// <summary>
/// 제작 재료 관리 - 등록 관계와 배치 연결, 집기 잠금
/// </summary>
class ProductionIngredients
{
    CookController _cook;		//재료를 담은 제작 대상

    /// <summary>
    /// 등록 재료 딕셔너리(재료 ID, 실제 재료)
    /// </summary>
    Dictionary<int , ProductIngredient> _items = new ( );

    /// <summary>
    /// 등록된 실제 재료 수
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    /// 소비와 레시피 동기화에 사용하는 읽기 전용 등록 목록
    /// </summary>
    public IReadOnlyDictionary<int , ProductIngredient> Items => _items;


    /// <summary>
    /// 재료 배치 대상 연결
    /// </summary>
    /// <param name="cook">제작 대상</param>
    public ProductionIngredients ( CookController cook )
    {
        _cook = cook;
    }

    /// <summary>
    /// 재료 등록과 그릇 따라가기 적용
    /// </summary>
    /// <param name="ingredient">등록 조건을 확인한 재료</param>
    public void Register ( ProductIngredient ingredient )
    {
        _items.Add ( ingredient.InstanceId , ingredient );
        _cook.Attach ( ingredient );
    }

    /// <summary>
    /// 재료 등록 해제와 제작 연결 정리
    /// </summary>
    /// <param name="instanceId">해제할 재료 ID</param>
    public void Unregister ( int instanceId )
    {
        if ( _items.Remove ( instanceId , out ProductIngredient ingredient ) == false )
            return;

        if ( ingredient != null )
            ingredient.ClearProduct ( _cook );

        //집기와 손실 콜백 안에서는 위치를 변경하지 않고 요청만 전달
        _cook.RequestIngredientRestack ( );
    }

    /// <summary>
    /// 전체 재료의 제작 연결 정리
    /// </summary>
    public void Clear ( )
    {
        foreach ( ProductIngredient ingredient in _items.Values )
            ingredient.ClearProduct ( _cook );

        _items.Clear ( );
        _cook.CancelIngredientRestack ( );
    }

    /// <summary>
    /// 등록 재료의 집기 잠금 적용
    /// </summary>
    /// <param name="isLocked">집기 잠금 여부</param>
    public void SetLocked ( bool isLocked )
    {
        foreach ( ProductIngredient ingredient in _items.Values )
            ingredient.SetLocked ( _cook , isLocked );
    }

    /// <summary>
    /// 등록 재료 조회
    /// </summary>
    /// <param name="instanceId">재료 ID</param>
    /// <param name="ingredient">등록된 재료</param>
    /// <returns>등록 여부</returns>
    public bool TryGet ( int instanceId , out ProductIngredient ingredient )
    {
        return _items.TryGetValue ( instanceId , out ingredient );
    }

    /// <summary>
    /// 지정 데이터의 실제 등록 수량 조회
    /// </summary>
    /// <param name="data">재료 데이터</param>
    /// <returns>등록 수량</returns>
    public int GetCount ( ItemData data )
    {
        int count = 0;		//일치하는 재료 수
        foreach ( ProductIngredient ingredient in _items.Values )
        {
            if ( ingredient != null && ingredient.Data == data )
                count++;
        }

        return count;
    }
}
