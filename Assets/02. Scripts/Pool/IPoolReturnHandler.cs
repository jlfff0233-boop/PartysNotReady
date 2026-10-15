
/// <summary>
/// 풀 반환 준비 - 기능별 상태와 외부 연결 정리
/// </summary>
public interface IPoolReturnHandler
{
    /// <summary>
    /// 객체를 풀에 반환할 수 있도록 사용 상태 정리
    /// </summary>
    /// <param name="reason">반환 준비 실패 사유</param>
    /// <returns>반환 준비 성공 여부</returns>
    bool TryPreparePoolReturn ( out string reason );

}