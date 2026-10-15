/// <summary>
/// 정산 항목 - 이름과 점수 보관
/// </summary>
public class GameScoreResult
{
    string _name;        //항목 이름
    int _points;        //점수(감점은 음수)
    bool _isExcluded;        //평가 제외 여부

    /// <summary>
    /// 항목 이름
    /// </summary>
    public string Name => _name;
    /// <summary>
    /// 점수(감점은 음수)
    /// </summary>
    public int Points => _points;
    /// <summary>
    /// 평가 제외 여부
    /// </summary>
    public bool IsExcluded => _isExcluded;

    /// <summary>
    /// 정산 항목 저장
    /// </summary>
    /// <param name="name">항목 이름</param>
    /// <param name="points">점수</param>
    /// <param name="isExcluded">평가 제외 여부</param>
    public GameScoreResult (
        string name, int points, bool isExcluded = false )
    {
        _name = name;
        _points = points;
        _isExcluded = isExcluded;
    }
}
