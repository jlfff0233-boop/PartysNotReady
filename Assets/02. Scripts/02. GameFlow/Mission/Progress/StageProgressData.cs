/// <summary>
/// 미션 단계 진행 - 단계 이름과 목표 목록 보관
/// </summary>
public class StageProgressData
{
    string _name;      //단계 이름
    /// <summary>
    /// 단계별 목표 진행 목록
    /// </summary>
    GoalProgressData [ ] _goals;

    /// <summary>
    /// 단계 이름
    /// </summary>
    public string Name => _name;
    /// <summary>
    /// 목표 수
    /// </summary>
    public int GoalCount => _goals.Length;

    /// <summary>
    /// 단계 진행 상태 생성
    /// </summary>
    /// <param name="name">단계 이름</param>
    /// <param name="goals">단계 목표 목록</param>
    public StageProgressData ( string name, GoalProgressData [ ] goals )
    {
        _name = name;
        _goals = goals;
    }

    /// <summary>
    /// 지정 목표 조회
    /// </summary>
    /// <param name="index">목표 번호</param>
    /// <returns>목표 진행 상태</returns>
    public GoalProgressData GetGoal ( int index )
    {
        return _goals [ index ];
    }

    /// <summary>
    /// 단계 목표 전체 완료 확인
    /// </summary>
    /// <returns>전체 완료 여부</returns>
    public bool IsCompleted ()
    {
        foreach ( GoalProgressData goal in _goals )
        {
            if ( goal.Current < goal.Required )
                return false;
        }

        return true;
    }
}
