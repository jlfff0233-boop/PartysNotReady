using UnityEngine;

/// <summary>
/// 적 공통 - 상태 초기화와 전환
/// </summary>
public abstract partial class Enemy : MonoBehaviour
{
    Room _startRoom;		//최초 발생 승인 방

    bool _isReady;		//초기화 완료 여부
    bool _isRunning;		//플레이 시작 허용 여부
    bool _isActive;		//구독과 활성 모듈 준비 여부

    protected IEnemyState [ ] _states;		//상태 객체 배열
    protected IEnemyState _currentState;		//현재 상태

    /// <summary>
    /// 현재 상태
    /// </summary>
    public IEnemyState CurrentState => _currentState;

    /// <summary>
    /// 최초 발생 승인 방
    /// </summary>
    public Room StartRoom => _startRoom;

    /// <summary>
    /// 초기화 완료 여부
    /// </summary>
    public bool IsReady => _isReady;

    /// <summary>
    /// 행동 실행 가능 여부
    /// </summary>
    protected bool CanRun =>
        _isReady == true && _isRunning == true &&
        _isActive == true && isActiveAndEnabled == true;

    /// <summary>
    /// 자동 발생 대상 종류
    /// </summary>
    public virtual EnemySpawnType SpawnType => EnemySpawnType.None;

    /// <summary>
    /// 해당 적이 처음 진입할 상태
    /// </summary>
    protected abstract EnemyStateType InitialState { get; }
    /// <summary>
    /// 시작 시 초기 상태에 자동 진입할지 여부
    /// </summary>
    protected virtual bool AutoStart => true;

    /// <summary>
    /// 상태 종류별 객체를 저장할 배열 생성
    /// </summary>
    protected virtual void Init ()
    {
        _states = new IEnemyState [ ( int ) EnemyStateType.Count ];
    }

    /// <summary>
    /// 시작 방을 연결하고 적 모듈 준비
    /// </summary>
    /// <param name="startRoom">최초 발생 승인 방</param>
    /// <returns>초기화 성공 여부</returns>
    protected bool Init ( Room startRoom )
    {
        if ( startRoom == null )
        {
            Debug.LogError( "[적 초기화 실패] " +
                $"{name}: 시작 방을 연결해 주세요.", this );
            return false;
        }

        _startRoom = startRoom;
        Init( );
        _isReady = true;

        //초기화 전에 실행된 OnEnable의 구독 처리
        if ( isActiveAndEnabled == true )
            Resume( );

        return true;
    }

    void OnEnable ()
    {
        Resume( );
    }

    void OnDisable ()
    {
        Suspend( );
    }

    void Update ()
    {
        if ( CanRun == true )
            _currentState?.Update( );
    }


    /// <summary>
    /// 이벤트 구독과 활성 모듈 준비
    /// </summary>
    protected virtual void Activate ()
    {
    }

    /// <summary>
    /// 이벤트 구독 해제와 활동 정리
    /// </summary>
    protected abstract void Deactivate ();

    /// <summary>
    /// 준비된 적의 구독과 행동 재개
    /// </summary>
    void Resume ()
    {
        if ( _isReady == false || _isActive == true )
            return;

        _isActive = true;
        Activate( );

        //게임 시작 이후 재활성화된 적만 최초 상태로 복귀
        if ( CanRun == true && AutoStart == true )
            ChangeState( InitialState );
    }

    /// <summary>
    /// 현재 행동 중단과 활성 모듈 정리
    /// </summary>
    void Suspend ()
    {
        if ( _isActive == false )
            return;

        //정리 중 발생하는 알림으로 이전 행동을 이어가지 않음
        _isActive = false;
        _currentState = null;
        Deactivate( );
    }


    /// <summary>
    /// 이번 등장 활동을 정리하고 다음 발생을 기다리도록 재준비
    /// </summary>
    protected void ResetAppearance ()
    {
        //현재 상태를 해제하고 구독과 진행 중인 활동 정리
        Suspend( );

        //초기화와 플레이 시작 허용은 유지한 채 구독 복구
        Resume( );
    }

    /// <summary>
    /// 플레이 시작 허용과 최초 상태 진입
    /// </summary>
    public void Begin ()
    {
        if ( _isReady == false || _isRunning == true )
            return;

        //비활성 적도 이후 활성화될 때 시작할 수 있도록 기록
        _isRunning = true;

        if ( CanRun == true && AutoStart == true )
            ChangeState( InitialState );
    }

    /// <summary>
    /// 플레이 시작 허용 해제와 적 활동 종료
    /// </summary>
    public void End ()
    {
        _isRunning = false;
        _isReady = false;
        Suspend( );
    }


    /// <summary>
    /// 현재 상태를 종료하고 지정 상태에 진입
    /// </summary>
    /// <param name="type">변경할 상태 타입</param>
    public void ChangeState ( EnemyStateType type )
    {
        if ( CanRun == false )
            return;

        //기존 상태가 새 상태와 동일하면 종료
        if ( _currentState != null && _currentState.StateType == type )
            return;

        //배열 범위를 벗어난 상태 번호는 제외
        int stateIndex = ( int ) type;
        if ( type < 0 || stateIndex >= _states.Length )
            return;


#if UNITY_EDITOR
        PlayDebugHistory.Record( "적", this, 
            $"{_currentState?.StateType.ToString( ) ?? "미시작"} → {type}" );
#endif
        //기존 상태 종료
        if ( _currentState != null )
            _currentState.Exit( );

        //상태 변경
        _currentState = _states [ stateIndex ];

        //새 상태 진입
        _currentState.Enter( );
    }


    /// <summary>
    /// 최초 발생 방 자격 검사
    /// </summary>
    /// <param name="room">요청 방</param>
    /// <returns>시작 방 일치 여부</returns>
    protected bool IsStartRoom ( Room room )
    {
        return room != null && room.isActiveAndEnabled == true &&
            room == _startRoom;
    }

    /// <summary>
    /// 새 등장에 필요한 실행 상태와 고정 시작 방 검사
    /// </summary>
    /// <param name="room">요청 방</param>
    /// <param name="reason">거절 사유</param>
    /// <returns>새 등장 가능 여부</returns>
    protected bool CheckAppearanceStart ( Room room, out string reason )
    {
        reason = string.Empty;
        if ( CanRun == false )
            reason = "초기화 / 시작 허용 / 활성 상태를 확인해 주세요.";
        else if ( IsStartRoom( room ) == false )
            reason = $"시작 방 불일치: 요청 {room?.name ?? "없음"} / 설정 {_startRoom?.name ?? "미연결"}";
        else if ( _currentState != null )
            reason = $"이미 활동 중입니다: {_currentState.StateType}";
        return reason.Length == 0;
    }

}
