using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 변칙 발생 시스템 - 방별 압력 갱신, 발생 선정과 요청
/// </summary>
public class AnomalySpawnSystem : MonoBehaviour
{
    [Header ( "----- 컴포넌트 -----" )]
    [SerializeField] Transform [ ] _players;		//방문 상태 확인 대상

    GameFlowController _flow;		//게임 시간과 종료 알림
    AnomalyPressureData _data;		//선택한 난이도의 압력 설정
    AnomalySpawnDataMap _spawnDataMap;		//선택한 난이도의 발생 종류

    float _lastElapsedTime;		//직전 게임 경과 시간
    float _nextCheckTime;		//다음 판정 시간
    float _globalCooldownUntil;		//전체 재발생 가능 시간

    bool _isReady;		//초기화 완료 여부
    bool _isStarting;		//시작 요청 중 재진입 차단

    float [ ] _weights;		//그룹 선택 가중치
    bool [ ] _available;		//요청 방의 그룹 후보 여부

    AnomalySpawnSelector _spawn;		//그룹 선택과 반복 대기
    AnomalySpawnGroup [ ] _groups;		//그룹별 발생 대상

    /// <summary>
    /// 활성 방 목록
    /// </summary>
    List<Room> _rooms = new ( );

    /// <summary>
    /// 방별 압력 딕셔너리(방, 압력 모델)
    /// </summary>
    Dictionary<Room , AnomalyPressureModel> _pressures = new ( );

    /// <summary>
    /// 사건 발생 가능한 방
    /// </summary>
    List<Room> _eligibleRooms = new ( );

    AnomalyRoomSelector _roomSelector = new ( );      //압력 기반 방 선정

#if UNITY_EDITOR
    /// <summary>
    /// 연결된 발생 설정
    /// </summary>
    public AnomalySpawnDataMap DebugSpawnMap => _spawnDataMap;
#endif

#if UNITY_EDITOR
    /// <summary>
    /// 발생 디버그 실행에 필요한 초기화 완료 여부
    /// </summary>
    public bool DebugIsReady => _isReady;
#endif


    #region ----- 초기화 -----

    /// <summary>
    /// 선택한 발생 설정과 활성 방 및 게임 시간 연결
    /// </summary>
    /// <param name="flow">게임 흐름 중재자</param>
    /// <param name="data">압력 설정</param>
    /// <param name="spawnDataMap">발생 종류 설정</param>
    /// <param name="enemies">초기화된 적 목록</param>
    /// <returns>초기화 완료 여부</returns>
    public bool Init (
        GameFlowController flow , AnomalyPressureData data ,
        AnomalySpawnDataMap spawnDataMap , IReadOnlyList<Enemy> enemies )
    {
        if ( _isReady == true ||
             flow == null || data == null ||
             _players == null || _players.Length == 0 )
            return false;

        _flow = flow;
        _data = data;
        _spawnDataMap = spawnDataMap;

        if ( _data.CheckSettings ( out string reason ) == false )
        {
            Debug.LogError ( $"[압력 초기화 실패] {reason}" , this );
            return false;
        }

        //시작 시 등록된 활성 방을 압력 대상으로 고정
        RoomRegister.GetRooms ( _rooms );
        _rooms.RemoveAll ( room => room.gameObject.scene != gameObject.scene );

        if ( _rooms.Count == 0 )
            return false;

        if ( InitSpawn ( enemies ) == false )
            return false;

        //게임 시작마다 방별 압력 상태를 새로 생성
        foreach ( Room room in _rooms )
            _pressures.Add ( room , new AnomalyPressureModel ( ) );

        _lastElapsedTime = 0f;
        _nextCheckTime = _data.CheckInterval;
        _globalCooldownUntil = 0f;
        _isReady = true;

        _flow.OnElapsedTimeChanged += HandleElapsedTime;
        _flow.OnEnded += HandleEnded;
        return true;
    }

    /// <summary>
    /// 발생 그룹 설정과 씬 대상 연결
    /// </summary>
    /// <param name="enemies">초기화된 적 목록</param>
    /// <returns>설정 유효 여부</returns>
    bool InitSpawn ( IReadOnlyList<Enemy> enemies )
    {
        if ( _spawnDataMap == null )
        {
            Debug.LogError ( "[변칙 발생 초기화 실패] " +
                "발생 종류 데이터맵이 비어 있습니다." , this );
            return false;
        }

        if ( _spawnDataMap.CheckSettings ( out string reason ) == false )
        {
            Debug.LogError ( $"[변칙 발생 초기화 실패] {reason}" , this );
            return false;
        }

        _groups = new AnomalySpawnGroup [ _spawnDataMap.Data.Count ];

        //데이터 배열 순서를 그룹 선택 모델의 인덱스로 사용
        for ( int i = 0 ; i < _groups.Length ; i++ )
        {
            AnomalySpawnData data = _spawnDataMap.Data [ i ];

            AnomalySpawnGroup group = new ( data );
            _groups [ i ] = group;
        }

        //씬에서 준비한 비활성 적도 발생 후보로 보관
        foreach ( Enemy enemy in enemies )
        {
            foreach ( AnomalySpawnGroup group in _groups )
            {
                if ( group.Data.Contains ( enemy.SpawnType ) == false )
                    continue;

                bool matches = group.Data.HasEventOwner == true
                    ? enemy is IAnomalyEventOwner
                    : enemy is IAnomalySpawnTarget;		//설정과 적의 실행 방식 일치

                if ( matches == false )
                {
                    Debug.LogError ( $"[변칙 발생 초기화 실패] " +
                        $"{group.Data.name}: {enemy.name}의 " +
                        $"사건 연결 방식을 확인해 주세요." , enemy );
                    return false;
                }
            }

            AddSpawnTarget ( enemy );
        }

        AnomalyEventSystem [ ] presenters =
            FindObjectsByType<AnomalyEventSystem> (
                FindObjectsInactive.Include , FindObjectsSortMode.None );

        foreach ( AnomalyEventSystem presenter in presenters )
        {
            if ( presenter.gameObject.scene != gameObject.scene )
                continue;

            AddSpawnTarget ( presenter );
        }

        _spawn = new AnomalySpawnSelector ( _groups.Length );
        _weights = new float [ _groups.Length ];
        _available = new bool [ _groups.Length ];

        for ( int i = 0 ; i < _groups.Length ; i++ )
            _weights [ i ] = _groups [ i ].Weight;

        return true;
    }

    private void OnDisable ( )
    {
        Stop ( );
    }


    /// <summary>
    /// 발생 연결과 시간 구독 정리
    /// </summary>
    public void Stop ( )
    {
        _isReady = false;

        if ( _flow != null )
        {
            _flow.OnElapsedTimeChanged -= HandleElapsedTime;
            _flow.OnEnded -= HandleEnded;
        }

        //이전 게임의 후보와 압력 상태가 재사용되지 않게 정리
        _pressures.Clear ( );
        _rooms.Clear ( );
        _eligibleRooms.Clear ( );
        _roomSelector.Clear ( );

        _spawn = null;
        _groups = null;
        _weights = null;
        _available = null;
    }


    #endregion

    #region ----- 핸들 -----

    /// <summary>
    /// 압력 갱신 및 발생 요청
    /// </summary>
    /// <param name="elapsedTime">현재 경과 시간</param>
    void HandleElapsedTime ( float elapsedTime )
    {
        if ( _isReady == false || _isStarting == true )
            return;

        UpdatePressures ( elapsedTime );

        //종료 프레임의 시간은 반영하지만 새 발생은 요청하지 않음
        if ( _flow.State != GameFlowState.Playing &&
             _flow.State != GameFlowState.FinalRush )
            return;

        if ( TrySelectRoom (
            elapsedTime , out Room room ) == false )
            return;

        //시작 요청 중 시간 알림이 재진입해도
        //같은 틱에서 다시 요청하지 않음
        _isStarting = true;
        bool started;

        try
        {
            started = TryStartInRoom ( room );
        }
        finally
        {
            _isStarting = false;
        }

        if ( _isReady == false || started == false )
            return;

        //실제 시작 성공 이후에만 압력과 Cooldown 변경
        _pressures [ room ].RecordSuccess ( elapsedTime , _data );
        _globalCooldownUntil =
            elapsedTime + _data.GlobalCooldown;
    }

    /// <summary>
    /// 게임 종료 시 발생 중단
    /// </summary>
    /// <param name="result">확정된 게임 결과</param>
    void HandleEnded ( GameResult result )
    {
        Stop ( );
    }

    #endregion

    #region ----- 방 압력 -----


    /// <summary>
    /// 진행 시간을 방별 압력에 반영
    /// </summary>
    /// <param name="elapsedTime">현재 경과 시간</param>
    void UpdatePressures ( float elapsedTime )
    {
        float deltaTime = Mathf.Max (
            0f , elapsedTime - _lastElapsedTime );

        _lastElapsedTime = elapsedTime;

        foreach ( Room room in _rooms )
        {
            if ( room == null || room.isActiveAndEnabled == false )
                continue;

            _pressures [ room ].Tick (
                elapsedTime , deltaTime , HasPlayer ( room ) , _data );
        }
    }

    /// <summary>
    /// 발생 요청할 방 선정
    /// </summary>
    /// <param name="elapsedTime">현재 게임 시간</param>
    /// <param name="selectedRoom">선정된 방</param>
    /// <returns>방 선정 여부</returns>
    bool TrySelectRoom (
        float elapsedTime , out Room selectedRoom )
    {
        selectedRoom = null;

        if ( elapsedTime < _nextCheckTime )
            return false;

        //프레임이 밀려도 검사 주기를 소급해 여러 번 실행하지 않음
        _nextCheckTime = elapsedTime + _data.CheckInterval;

        if ( elapsedTime < _globalCooldownUntil )
            return false;

        _eligibleRooms.Clear ( );

        foreach ( Room room in _rooms )
        {
            if ( room == null || room.isActiveAndEnabled == false ||
                 _pressures [ room ].CanSpawn ( elapsedTime ) == false ||
                 HasSpawnCandidate ( room ) == false )
                continue;

            _eligibleRooms.Add ( room );
        }

        return _roomSelector.TrySelect (
            _eligibleRooms , _pressures , _data , out selectedRoom );
    }


    /// <summary>
    /// 플레이어 유무 확인
    /// </summary>
    /// <param name="room">확인할 방</param>
    /// <returns>플레이어 존재 여부</returns>
    bool HasPlayer ( Room room )
    {
        foreach ( Transform player in _players )
        {
            if ( player == null )
                continue;

            if ( RoomRegister.TryGetRoom (
                player.position , out Room current ) == true &&
                current == room )
                return true;
        }

        return false;
    }

    #endregion

    #region ----- 발생 그룹 -----

    /// <summary>
    /// 적을 발생 그룹에 연결
    /// </summary>
    /// <param name="enemy">씬의 적</param>
    void AddSpawnTarget ( Enemy enemy )
    {
        foreach ( AnomalySpawnGroup group in _groups )
        {
            if ( group.Data.Contains ( enemy.SpawnType ) == false )
                continue;

            group.AddEnemy ( enemy );
        }
    }

    /// <summary>
    /// 사건 프레젠터를 발생 그룹에 연결
    /// </summary>
    /// <param name="presenter">씬의 사건 프레젠터</param>
    void AddSpawnTarget ( AnomalyEventSystem presenter )
    {
        foreach ( AnomalySpawnGroup group in _groups )
        {
            if ( group.Data.Contains ( presenter.EventType ) == false )
                continue;

            group.AddPresenter ( presenter );
        }
    }


    /// <summary>
    /// 요청 방의 그룹별 시작 가능 상태 갱신
    /// </summary>
    /// <param name="room">요청한 방</param>
    void UpdateSpawnCandidates ( Room room )
    {
        for ( int i = 0 ; i < _groups.Length ; i++ )
            _available [ i ] = _groups [ i ].CanStart ( room );
    }


    /// <summary>
    /// 요청 방의 그룹을 선정해 적 또는 사건 시작
    /// </summary>
    /// <param name="room">요청한 방</param>
    /// <returns>실제 시작 여부</returns>
    bool TryStartInRoom ( Room room )
    {
        UpdateSpawnCandidates ( room );

        float elapsedTime = _flow.ElapsedTime;

        if ( _spawn.TrySelect (
            elapsedTime , _weights , _available ,
            Random.value , out int index ) == false )
            return false;

        if ( _groups [ index ].TryStart ( room ) == false )
        {
#if UNITY_EDITOR
            PlayDebugHistory.Record ( "발생 거절" , this , $"{room.name} / {_groups [ index ].Data.name} / 선정 후 시작 거절" );
#endif
            return false;
        }

        if ( _isReady == false || _spawn == null )
            return true;

        _spawn.RecordSuccess (
            index , elapsedTime , _groups [ index ].RepeatDelay );
        return true;
    }


    /// <summary>
    /// 요청 방에 반복 대기까지 통과한 그룹이 있는지 확인
    /// </summary>
    /// <param name="room">요청한 방</param>
    /// <returns>발생 후보 존재 여부</returns>
    bool HasSpawnCandidate ( Room room )
    {
        UpdateSpawnCandidates ( room );

        return _spawn.TrySelect (
            _flow.ElapsedTime , _weights , _available ,
            0f , out _ ) == true;
    }

    #endregion

    #region ----- 디버그 -----

#if UNITY_EDITOR


    /// <summary>
    /// 지정 방의 실제 후보 조건 검사
    /// </summary>
    /// <param name="room">검사할 방</param>
    /// <param name="details">검사 결과</param>
    public void CheckDebugCandidates ( Room room , List<AnomalyDebugDetail> details )
    {
        if ( _isReady == false || room == null )
        {
            details.Add ( new ( "후보 검사" , "초기화된 발생 관리자와 방이 필요합니다." ) );
            return;
        }
        //명시적인 검사 요청에서만 후보 자격을 평가하며 추첨하지 않음
        foreach ( AnomalySpawnGroup group in _groups )
        {
            details.Add ( new ( group.Data.name , $"{group.Data.EnemyType} / 아래 적과 사건 자격 확인 / 확률과 대기는 별도" ) );
            foreach ( Enemy enemy in group.DebugEnemies )
            {
                if ( enemy == null )
                    continue;
                string reason = string.Empty;
                if ( enemy is EnemyHarm harm )
                    harm.CheckStart ( room , out reason );
                else if ( enemy is EnemyKidnap kidnap )
                    kidnap.CheckStart ( room , out reason );
                else if ( enemy is IAnomalySpawnTarget target )
                    target.CheckStart ( room , out reason );
                details.Add ( new ( enemy.name , reason.Length == 0 ? "적 조건 충족" : reason ) );
            }
            foreach ( AnomalyEventSystem presenter in group.DebugPresenters )
            {
                if ( presenter != null )
                    details.Add ( new ( presenter.name , presenter.CanStart ( room ) ? "사건 후보 있음" : "이 방에서 시작 가능한 대상 없음" ) );
            }
        }
    }

    /// <summary>
    /// 방 압력과 대기 시간 조회
    /// </summary>
    /// <param name="details">표시 항목</param>
    public void GetDebugDetails ( List<AnomalyDebugDetail> details )
    {
        details.Add ( new ( "초기화" , _isReady.ToString ( ) ) );
        if ( _isReady == false )
            return;
        float time = _flow.ElapsedTime;		//현재 게임 시간
        details.Add ( new ( "다음 판정 / 전체 대기" , $"{Mathf.Max ( 0f , _nextCheckTime - time ):F1} / {Mathf.Max ( 0f , _globalCooldownUntil - time ):F1}초" ) );
        foreach ( var entry in _pressures )
        {
            if ( entry.Key == null )
                continue;
            details.Add ( new ( entry.Key.name , $"압력 {entry.Value.Pressure:F1} / 방 대기 {Mathf.Max ( 0f , entry.Value.DebugCooldownUntil - time ):F1}초 / 미방문 {entry.Value.UnvisitedTime:F1}초" ) );
        }
        for ( int i = 0 ; i < _groups.Length ; i++ )
            details.Add ( new ( _groups [ i ].Data.name , $"적 {_groups [ i ].Data.EnemyType} / 등록 적 {_groups [ i ].DebugEnemies.Count} / 사건 {_groups [ i ].DebugPresenters.Count} / 반복 대기 {Mathf.Max ( 0f , _spawn.GetDebugAllowedTime ( i ) - time ):F1}초" ) );
    }

    /// <summary>
    /// 지정 방과 적의 사건 시작 검사 및 요청
    /// </summary>
    /// <param name="room">발생 방</param>
    /// <param name="enemy">지정 적</param>
    /// <param name="reason">실행 결과</param>
    /// <returns>시작 성공 여부</returns>
    public bool TryDebugStart ( Room room , Enemy enemy , out string reason )
    {
        reason = string.Empty;
        if ( Application.isPlaying == false || _isReady == false || _isStarting == true ||
             ( _flow.State != GameFlowState.Playing && _flow.State != GameFlowState.FinalRush ) )
        {
            reason = "발생 관리가 준비된 게임 진행 중에 실행해 주세요.";
            return false;
        }
        if ( room == null || _pressures.ContainsKey ( room ) == false || enemy == null )
        {
            reason = "관리 중인 방과 적을 선택해 주세요.";
            return false;
        }
        for ( int i = 0 ; i < _groups.Length ; i++ )
        {
            if ( _groups [ i ].Data.Contains ( enemy.SpawnType ) == false )
                continue;
            _isStarting = true;
            bool started;		//그룹 시작 결과
            try
            {
                bool prepared = enemy.TryPrepareDebugSpawn( room, out Room appliedRoom, out reason );

                if ( prepared == false )
                {
                    reason = $"활동 준비 실패: {reason}";
                    PlayDebugHistory.Record( "테스트 발생", enemy, reason );
                    return false;
                }

                room = appliedRoom;
                if ( _pressures.ContainsKey( room ) == false )
                {
                    reason = $"{enemy.name}: 적용 방 {room.name}이 발생 관리자에 등록되어 있지 않습니다.";
                    PlayDebugHistory.Record( "테스트 발생", enemy, reason );
                    return false;
                }
                started = _groups [ i ].TryStart ( room , enemy, out reason );
            }
            finally
            {
                _isStarting = false;
            }
            reason = $"{enemy.name} / 적용 방: {room.name} / " +
                ( started ? "기존 활동 정리 후 시작 성공 (확률 / 대기 생략)" : reason );
            if ( started && _isReady )
            {
                float time = _flow.ElapsedTime;
                _spawn.RecordSuccess ( i , time , _groups [ i ].RepeatDelay );
                _pressures [ room ].RecordSuccess ( time , _data );
                _globalCooldownUntil = time + _data.GlobalCooldown;
            }
            PlayDebugHistory.Record ( "테스트 발생" , enemy , reason );
            return started;
        }
        reason = $"{enemy.SpawnType} 발생 그룹이 없습니다. SpawnData의 Enemy Type을 확인해 주세요.";
        return false;
    }

    /// <summary>
    /// 현재 게임 시간으로 압력 발생 판정
    /// </summary>
    [ContextMenu ( "Test Pressure Spawn" )]
    public void TestPressureSpawn ( )
    {
        if ( Application.isPlaying == false || _isReady == false )
            return;

        //판정 주기와 발생 제한을 유지한 동일 경로 사용
        HandleElapsedTime ( _flow.ElapsedTime );

        Debug.Log ( "[압력 발생 검사] 현재 시간으로 판정했습니다. " +
            "판정 주기와 발생 제한을 유지합니다." , this );
    }

#endif

    #endregion

}
