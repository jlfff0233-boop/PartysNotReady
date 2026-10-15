using UnityEngine;

/// <summary>
/// 납치 적 모델 - 표적 선정, 방별 저지 횟수와 접근 시간 관리
/// </summary>
public partial class KidnapModel : EnemyRoomActivityModel
{
    float _chaseTimeLimit;      //접근 제한시간
    float _elapsedTime;     //경과시간

    float _npcTargetChance;     //NPC 선택 확률
    bool _isPlayerTarget;       //이번 탐색의 플레이어 선택 여부
    bool _canKidnap;     //사건 실패에 따른 납치 허용 여부
    bool _isPlayerAttempt;      //플레이어 납치 시도 여부

    /// <summary>
    /// 납치 가능 여부
    /// </summary>
    public bool CanKidnap => _canKidnap;

    /// <summary>
    /// 플레이어 선택했는지 여부
    /// </summary>
    public bool IsPlayerTarget => _isPlayerTarget;

    /// <summary>
    /// 접근 제한시간 초과 여부
    /// </summary>
    public bool IsChaseExpired => _elapsedTime >= _chaseTimeLimit;

    /// <summary>
    /// 누적 저지 횟수
    /// </summary>
    public int RescueCount => GetCount( CurrentRoom );

    /// <summary>
    /// 저지 기준 도달 여부
    /// </summary>
    public bool HasReachedRescueLimit => HasReachedThreshold( CurrentRoom );

    /// <summary>
    /// 플레이어 납치 시도 여부
    /// </summary>
    public bool IsPlayerAttempt => _isPlayerAttempt;

    /// <summary>
    /// 납치 모델 초기화
    /// </summary>
    /// <param name="chaseTimeLimit">접근 제한시간</param>
    /// <param name="rescueLimit">저지 기준 횟수</param>
    /// <param name="npcTargetChance">NPC 선택 확률</param>
    /// <param name="retaliationChance">플레이어 납치 시도 확률</param>
    public KidnapModel (
        float chaseTimeLimit, int rescueLimit, float npcTargetChance,
        float retaliationChance ) : base( rescueLimit, retaliationChance )
    {
        _chaseTimeLimit = chaseTimeLimit;
        _npcTargetChance = Mathf.Clamp01( npcTargetChance );
    }


    /// <summary>
    /// 납치 허용 상태 초기화
    /// </summary>
    public void BeginHide ()
    {
        //NPC 구조 저지 횟수는 유지
        _canKidnap = false;
        _isPlayerTarget = false;
        EndPlayerAttempt( );
    }

    /// <summary>
    /// 변칙 사건 실패 시 납치 허용
    /// </summary>
    /// <param name="status">사건 결과</param>
    public void ApplyAnomalyResult ( AnomalyStatus status )
    {
        if ( status == AnomalyStatus.Failed )
            _canKidnap = true;
    }


    /// <summary>
    /// 플레이어 대상 시도 시작
    /// </summary>
    public void BeginPlayerAttempt ()
    {
        _isPlayerTarget = true;
        _isPlayerAttempt = true;
    }


    /// <summary>
    /// 플레이어 납치 시도 종료
    /// </summary>
    public void EndPlayerAttempt ()
    {
        //시도가 끝나도 방별 저지 횟수는 유지
        _isPlayerAttempt = false;
    }

    /// <summary>
    /// 확률에 따라 표적 종류 선정
    /// </summary>
    /// <param name="randomValue">0부터 1 사이의 난수</param>
    public void SelectTargetType ( float randomValue )
    {
        //재탐색 전에 이전 플레이어 대상 시도 종료
        EndPlayerAttempt( );

        //Random.value가 1이어도 NPC 확률 100%를 보장
        _isPlayerTarget =
            _npcTargetChance < 1f &&
            randomValue >= _npcTargetChance;

        if ( _isPlayerTarget == true )
            BeginPlayerAttempt( );
    }

    /// <summary>
    /// 저지 횟수 기록
    /// </summary>
    public void RecordRescue ()
    {
        //도주 중 다른 방을 지나도 활동을 시작한 방에 기록
        RecordInterruption( CurrentRoom );
    }


    /// <summary>
    /// 접근 시작
    /// </summary>
    public void Begin ()
    {
        _elapsedTime = 0f;
    }

    /// <summary>
    /// 표적 변경과 무관하게 접근 시간 누적
    /// </summary>
    /// <param name="deltaTime">경과시간</param>
    public void Tick ( float deltaTime )
    {
        _elapsedTime += Mathf.Max( 0f, deltaTime );
    }

}
