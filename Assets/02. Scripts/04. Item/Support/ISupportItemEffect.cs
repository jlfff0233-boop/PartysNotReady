/// <summary>
/// 서포트 아이템 사용 결과
/// </summary>
public enum SupportItemUseResult
{
    Rejected,       //실행 거절
    Applied,        //효과 적용
    Reserved        //효과 예약
}

/// <summary>
/// 서포트 아이템 효과 실행
/// </summary>
public interface ISupportItemEffect
{
    /// <summary>
    /// 효과 실행
    /// </summary>
    /// <returns>실행 결과</returns>
    SupportItemUseResult Use ();
}