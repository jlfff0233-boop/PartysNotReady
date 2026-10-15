/// <summary>
/// 미션 결과 - 단계별 완료 기록
/// </summary>
public class MissionResult
{
    string _name;        //단계 이름
    int _completed;        //완료 목표 수
    int _total;        //전체 목표 수

    /// <summary>
    /// 단계 이름
    /// </summary>
    public string Name => _name;
    /// <summary>
    /// 완료 목표 수
    /// </summary>
    public int Completed => _completed;
    /// <summary>
    /// 전체 목표 수
    /// </summary>
    public int Total => _total;

    /// <summary>
    /// 기록 저장
    /// </summary>
    /// <param name="name">단계 이름</param>
    /// <param name="completed">완료 목표 수</param>
    /// <param name="total">전체 목표 수</param>
    public MissionResult ( string name, int completed, int total )
    {
        _name = name;
        _completed = completed;
        _total = total;
    }
}
