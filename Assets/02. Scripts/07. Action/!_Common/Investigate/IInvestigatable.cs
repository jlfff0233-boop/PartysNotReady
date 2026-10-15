
/// <summary>
/// 조사 가능한 대상
/// </summary>
public interface IInvestigatable
{
    /// <summary>
    /// 상호작용 행동 이름
    /// </summary>
    string ActionName { get; }

    /// <summary>
    /// 조사 가능 여부
    /// </summary>
    bool CanInvestigate { get; }

    /// <summary>
    /// 현재 조사 건 ID
    /// </summary>
    int InvestigationId { get; }

    /// <summary>
    /// 시작 ID와 일치하는 조사 완료 요청
    /// </summary>
    /// <param name="expectedInvestigationId">조사 시작 시 저장한 ID</param>
    /// <returns>조사 성공 여부</returns>
    bool TryInvestigate ( int expectedInvestigationId );
}
