
/// <summary>
/// 미션 데이터 - 표시 시점의 목표 데이터 보관
/// </summary>
public class MissionGoalDisplayData
{
    string _id;     //목표 아이디
    string _name;       //목표 표시 이름
    int _current;       //현재 달성량
    int _required;      //필요 달성량

    /// <summary>
    /// 목표 아이디
    /// </summary>
    public string Id => _id;

    /// <summary>
    /// 목표 표시 이름
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// 현재 달성량
    /// </summary>
    public int Current => _current;

    /// <summary>
    /// 완료에 필요한 달성량
    /// </summary>
    public int Required => _required;

    /// <summary>
    /// 목표 완료 여부
    /// </summary>
    public bool IsCompleted => _required > 0 && _current >= _required;


    /// <summary>
    /// 목표 표시 시점의 정보를 저장
    /// </summary>
    /// <param name="id">목표 아이디</param>
    /// <param name="name">목표 표시 이름</param>
    /// <param name="current">현재 달성량</param>
    /// <param name="required">필요 달성량</param>
    public MissionGoalDisplayData (
        string id, string name, int current, int required )
    {
        _id = id;
        _name = name;
        _current = current;
        _required = required;
    }
}