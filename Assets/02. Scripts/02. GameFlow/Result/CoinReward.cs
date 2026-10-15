using System;

/// <summary>
/// 게임 결과의 코인 계산과 중복 지급 방지
/// </summary>
public class CoinReward
{
    GameResult _paidResult;     //이미 지급한 게임 결과

    /// <summary>
    /// 최종 점수와 종료 사유로 지급액 계산
    /// </summary>
    /// <param name="result">확정된 게임 결과</param>
    /// <returns>코인 지급액</returns>
    int Calculate ( GameResult result )
    {
        //임시 수치는 게임 경제 조정 때 변경
        const int baseReward = 10;
        const int coinPerPoint = 1;
        const int minimumReward = 5;

        int score = ( int ) Math.Max( 0L, Math.Min( 100L, result.TotalScore ) );
        int amount = baseReward + score * coinPerPoint;

        //손님 전원 이탈은 50%, 절반 코인은 올림
        if ( result.Reason == GameEndReason.AllNpcDeparted )
            amount = ( amount + 1 ) / 2;

        return Math.Max( minimumReward, amount );
    }

    /// <summary>
    /// 확정 결과에 코인 한 번 지급
    /// </summary>
    /// <param name="result">확정된 게임 결과</param>
    /// <param name="amount">지급한 코인</param>
    /// <param name="balance">지급 후 잔액</param>
    /// <returns>지급 성공 여부</returns>
    public bool TryGrant (
        GameResult result, out int amount, out int balance )
    {
        amount = 0;
        balance = Budget.Balance;

        if ( result == null || ReferenceEquals( _paidResult, result ) == true ||
             result.IsScored == false || result.Outcome == GameOutcome.Unscored ||
             result.Reason == GameEndReason.None )
            return false;

        int calculated = Calculate( result );
        if ( Budget.TryAdd( calculated, out balance ) == false )
            return false;

        _paidResult = result;
        amount = calculated;
        return true;
    }
}
