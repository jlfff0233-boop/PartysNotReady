
/// <summary>
/// 자금 - 코인 잔액 저장
/// </summary>
public static class Budget
{
    const string BalanceKey = "3DRabbitPartyMulti.CoinBalance";     //로컬 저장 키

    /// <summary>
    /// 현재 보유 코인
    /// </summary>
    public static int Balance =>
        GameManager.Instance.Save.LoadSupportItems( ).Balance;

    /// <summary>
    /// 코인 지급과 잔액 저장
    /// </summary>
    /// <param name="amount">지급할 코인</param>
    /// <param name="balance">지급 후 잔액</param>
    /// <returns>지급 성공 여부</returns>
    public static bool TryAdd ( int amount, out int balance )
    {
        //세이브 데이터 가져오기
        SaveManager save = GameManager.Instance.Save;
        SupportItemSaveData data = save.LoadSupportItems( );

        //지급 실패 시 반환할 기존 잔액
        balance = data.Balance;

        //유효한 값인지 확인
        if ( amount <= 0 || balance > int.MaxValue - amount )
            return false;

        //코인 추가 후 저장
        data.Balance += amount;
        save.SaveSupportItems( data );

        //지급 후 잔액 반환
        balance = data.Balance;
        return true;
    }
}
