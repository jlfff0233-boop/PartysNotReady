
/// <summary>
/// 상점 모델 - 구매 조건, 결제 수량 처리
/// </summary>
public class ShopModel
{
    public const int MinPurchaseCount = 1;        //최소 구매 수량
    public const int MaxPurchaseCount = 100;        //최대 구매 수량

    ShopDataMap _dataMap;        //판매 목록


    /// <summary>
    /// 판매 목록 연결
    /// </summary>
    /// <param name="dataMap">판매 목록</param>
    public ShopModel ( ShopDataMap dataMap )
    {
        _dataMap = dataMap;
    }

    /// <summary>
    /// 구매 총가격 계산
    /// </summary>
    /// <param name="data">판매 아이템</param>
    /// <param name="count">구매 수량</param>
    /// <returns>총가격</returns>
    public static long GetTotalCost ( SupportItemData data, int count )
    {
        return ( long ) data.Cost * count;
    }

    /// <summary>
    /// 코인/아이템 보유량 변경
    /// </summary>
    /// <param name="id">아이템 ID</param>
    /// <param name="count">구매 수량</param>
    /// <param name="save">현재 저장 데이터</param>
    /// <param name="message">구매 결과 안내</param>
    /// <returns>구매 성공 여부</returns>
    public bool TryPurchase (
        string id, int count,
        SupportItemSaveData save, out string message )
    {
        if ( count < MinPurchaseCount || count > MaxPurchaseCount )
        {
            message = $"구매 수량은 {MinPurchaseCount}~{MaxPurchaseCount}개입니다.";
            return false;
        }

        if ( _dataMap.TryGetData( id, out SupportItemData data ) == false )
        {
            message = "판매 목록에 없는 아이템입니다.";
            return false;
        }

        //총가격은 long으로 계산해 곱셈 오버플로 방지
        long totalCost = GetTotalCost( data, count );

        if ( totalCost > save.Balance )
        {
            message = $"코인이 부족합니다. 필요 코인: {totalCost:N0}";
            return false;
        }

        //보유량 상한은 인벤토리에서 검사
        if ( save.Inventory.TryChangeCount( id, count ) == false )
        {
            int remaining =
                SupportItemInventory.MaxCount - save.Inventory.GetCount( id );

            message =
                $"보유 한도는 {SupportItemInventory.MaxCount}개입니다.\n" +
                $"추가 보유 가능한 수량: {remaining}개";
            return false;
        }

        //잔액 이하인 가격만 int로 변환
        save.Balance -= ( int ) totalCost;
        message = $"{data.ItemName} {count}개를 구매했습니다.";
        return true;
    }
}