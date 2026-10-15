using System;

/// <summary>
/// 급속 보급 효과 - 아이템 재보충 시간 단축
/// </summary>
public class QuickSupplyEffect : ISupportItemEffect
{
    QuickSupplyData _data;		//효과 설정
    IngredientStockManager _stock;        //재고 기능
    Func<IngredientSource> _getSource;      //사용 순간 대상 조회

    /// <summary>
    /// 보급 기능 연결
    /// </summary>
    /// <param name="data">효과 설정</param>
    /// <param name="stock">재고 기능</param>
    /// <param name="getSource">대상 조회</param>
    public QuickSupplyEffect (
        QuickSupplyData data,
        IngredientStockManager stock,
        Func<IngredientSource> getSource )
    {
        _data = data;
        _stock = stock;
        _getSource = getSource;
    }

    /// <summary>
    /// 재보충 단축
    /// </summary>
    /// <returns>실행 결과</returns>
    public SupportItemUseResult Use ()
    {
        //데이터가 유효하지 않거나 재고가 없으면 사용 불가
        if ( _data.IsValid == false || _stock == null )
            return SupportItemUseResult.Rejected;

        //재료 공급원 가져오기
        IngredientSource source = _getSource( );

        //재보충 시간 단축
        return source != null &&
               _stock.TryReduceRefill( source, _data.ReductionRatio ) == true
            ? SupportItemUseResult.Applied
            : SupportItemUseResult.Rejected;
    }
}
