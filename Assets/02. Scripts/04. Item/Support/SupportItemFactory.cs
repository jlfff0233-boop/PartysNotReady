using System;

/// <summary>
/// 서포트 아이템 생성 - 설정에 맞는 효과와 실행 기능 연결
/// </summary>
public class SupportItemFactory
{
    PlayerItemHolder _itemHolder;		//아이템 보유 처리
    Mover _mover;		//플레이어 이동
    Func<IngredientSource> _getSource;		//사용 순간 보급 대상 조회
    RescueInteracter _rescueInteracter;		//구조 진행

    /// <summary>
    /// 효과 적용 대상 연결
    /// </summary>
    /// <param name="itemHolder">아이템 보유 처리</param>
    /// <param name="mover">플레이어 이동</param>
    /// <param name="getSource">보급 대상 조회</param>
    /// <param name="rescueInteracter">구조 진행</param>
    public SupportItemFactory (
        PlayerItemHolder itemHolder,
        Mover mover,
        Func<IngredientSource> getSource,
        RescueInteracter rescueInteracter )
    {
        _itemHolder = itemHolder;
        _mover = mover;
        _getSource = getSource;
        _rescueInteracter = rescueInteracter;
    }

    /// <summary>
    /// 서포트 아이템 생성
    /// </summary>
    /// <param name="data">아이템 설정</param>
    /// <param name="stock">재고 기능</param>
    /// <returns>생성한 아이템</returns>
    public SupportItem Create (
        SupportItemData data, IngredientStockManager stock )
    {
        switch ( data )
        {
            case CapacityExpandData capacityData:
                return new SupportItem(
                    capacityData,
                    new CapacityExpandEffect( capacityData, _itemHolder ) );

            case MoveSpeedUpData moveData:
                MoveSpeedUpEffect moveEffect = new( moveData, _mover );
                return new SupportItem(
                    moveData, moveEffect, moveEffect.Tick, moveEffect.End,
                    () => moveEffect.RemainingTime );

            case QuickSupplyData supplyData:
                return new SupportItem(
                    supplyData,
                    new QuickSupplyEffect(
                        supplyData, stock, _getSource ) );

            case ProdDiffDownData produceData:
                ProdDiffDownEffect produceEffect = new( produceData );
                return new SupportItem(
                    produceData, produceEffect, null, produceEffect.End );

            case RescueSpeedUpData rescueData:
                RescueSpeedUpEffect rescueEffect = new(
                    rescueData, _rescueInteracter );
                return new SupportItem(
                    rescueData, rescueEffect, rescueEffect.Tick, rescueEffect.End,
                    () => rescueEffect.RemainingTime );

            default:
                throw new ArgumentOutOfRangeException( nameof( data ) );
        }
    }
}
