
/// <summary>
/// 공통 서비스 - 게임 시작 준비와 종료 정리
/// </summary>
public interface IGameService
{
    /// <summary>
    /// 서비스 초기화
    /// </summary>
    /// <param name="game">공통 매니저</param>
    /// <returns>초기화 성공 여부</returns>
    bool Init ( GameManager game );
}