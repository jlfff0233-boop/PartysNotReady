
/// <summary>
/// 접근 상태
/// </summary>
public enum ApproachStatus
{
    Idle,       //접근 대기
    Moving,     //이동 중
    Arrived,    //도착
    Failed      //접근 실패
}

/// <summary>
/// 접근 모델 - 제한 시간, 경로와 도착 판정
/// </summary>
public class ApproachModel
{
    float _timeLimit;		//접근 제한 시간
    float _arriveDist;		//도착 허용 거리
    float _elapsedTime;		//접근 경과 시간
    float _arriveTolerance;		//정지 위치 오차

    ApproachStatus _status;		//접근 상태

    /// <summary>
    /// 접근 상태
    /// </summary>
    public ApproachStatus Status => _status;
    /// <summary>
    /// 접근 실패 이유
    /// </summary>
    public string FailureReason { get; private set; }


    /// <summary>
    /// 접근 설정 저장
    /// </summary>
    /// <param name="timeLimit">접근 제한 시간(초)</param>
    /// <param name="arriveDist">도착 허용 거리</param>
    /// <param name="arriveTolerance">정지 위치 오차 허용 거리</param>
    public ApproachModel (
        float timeLimit, float arriveDist, float arriveTolerance = 0f )
    {
        _timeLimit = timeLimit;
        _arriveDist = arriveDist;
        _arriveTolerance = arriveTolerance;
    }


    /// <summary>
    /// 접근 시작
    /// </summary>
    public void Begin ()
    {
        _elapsedTime = 0f;
        _status = ApproachStatus.Moving;
        FailureReason = null;
    }

    /// <summary>
    /// 접근 중단
    /// </summary>
    public void Stop ()
    {
        _status = ApproachStatus.Idle;
    }

    /// <summary>
    /// 접근 실패와 원인 반영
    /// </summary>
    /// <param name="reason">실패 이유</param>
    public void Fail ( string reason = "이동 요청 실패" )
    {
        FailureReason = reason;
        _status = ApproachStatus.Failed;
    }

    /// <summary>
    /// 제한 시간과 경로에 따른 도착/실패 판정
    /// </summary>
    /// <param name="deltaTime">경과 시간(초)</param>
    /// <param name="pathPending">경로 계산 중 여부</param>
    /// <param name="hasPath">경로 보유 여부</param>
    /// <param name="isComplete">완전한 경로 여부</param>
    /// <param name="remainingDist">경로의 남은 거리</param>
    /// <param name="targetDist">목표 판정 거리</param>
    public void UpdateMove (
        float deltaTime, bool pathPending, bool hasPath,
        bool isComplete, float remainingDist, float targetDist )
    {
        if ( _status != ApproachStatus.Moving )
            return;

        //경로를 기다리는 시간도 접근 제한 시간에 포함
        _elapsedTime += deltaTime;

        if ( _elapsedTime >= _timeLimit )
        {
            Fail( "접근 제한 시간 초과" );
            return;
        }

        //경로 계산 중에는 기다리고, 계산 후 경로가 없으면 실패
        if ( pathPending == true )
            return;

        if ( hasPath == false )
        {
            Fail( "이동 경로가 사라짐" );
            return;
        }

        if ( isComplete == false )
        {
            Fail( "목표까지 완전한 경로가 없음" );
            return;
        }

        //경로 끝에 도착해도 실제 목표가 멀면 도착으로 처리하지 않음
        if ( remainingDist <= _arriveDist + _arriveTolerance &&
             targetDist <= _arriveDist + _arriveTolerance )
            _status = ApproachStatus.Arrived;
    }


}
