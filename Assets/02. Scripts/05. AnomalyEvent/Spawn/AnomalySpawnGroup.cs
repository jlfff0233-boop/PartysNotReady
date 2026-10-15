using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 변칙 발생 그룹 - 적과 사건 선택, 시작 결과 연결
/// </summary>
public class AnomalySpawnGroup
{
    AnomalySpawnData _settings;		//그룹 선택 설정

    /// <summary>
    /// 그룹에 연결된 적
    /// </summary>
    List<Enemy> _enemies = new( );

    /// <summary>
    /// 이번 요청의 시작 가능한 적
    /// </summary>
    List<Enemy> _enemyCandidates = new( );

    /// <summary>
    /// 설정된 종류의 사건 프레젠터
    /// </summary>
    List<AnomalyEventSystem> _presenters = new( );

    /// <summary>
    /// 이번 요청의 시작 가능한 사건 프레젠터
    /// </summary>
    List<AnomalyEventSystem> _eventCandidates = new( );

    bool _isStarting;		//그룹 시작 처리 중

    /// <summary>
    /// 발생 종류 데이터
    /// </summary>
    public AnomalySpawnData Data => _settings;

    /// <summary>
    /// 선택 가중치
    /// </summary>
    public float Weight => _settings.Weight;

    /// <summary>
    /// 재발생 대기시간
    /// </summary>
    public float RepeatDelay => _settings.RepeatDelay;

#if UNITY_EDITOR
    /// <summary>
    /// 등록된 적 목록
    /// </summary>
    public IReadOnlyList<Enemy> DebugEnemies => _enemies;

    /// <summary>
    /// 등록된 사건 목록
    /// </summary>
    public IReadOnlyList<AnomalyEventSystem> DebugPresenters => _presenters;
#endif

    /// <summary>
    /// 선택 설정 연결
    /// </summary>
    /// <param name="settings">발생 종류 설정</param>
    public AnomalySpawnGroup ( AnomalySpawnData settings )
    {
        _settings = settings;
    }

    /// <summary>
    /// 적 후보 등록
    /// </summary>
    /// <param name="enemy">발생 가능한 적</param>
    public void AddEnemy ( Enemy enemy )
    {
        if ( _enemies.Contains( enemy ) == false )
            _enemies.Add( enemy );
    }

    /// <summary>
    /// 사건 후보 등록
    /// </summary>
    /// <param name="presenter">사건 프레젠터</param>
    public void AddPresenter ( AnomalyEventSystem presenter )
    {
        if ( _presenters.Contains( presenter ) == false )
            _presenters.Add( presenter );
    }


    /// <summary>
    /// 요청 방의 대상 하나 시작
    /// </summary>
    /// <param name="room">요청 방</param>
    /// <returns>실제 시작 여부</returns>
    public bool TryStart ( Room room )
    {
        return TryStart( room, null );
    }

    /// <summary>
    /// 지정 적 또는 무작위 후보로 발생 요청
    /// </summary>
    /// <param name="room">요청 방</param>
    /// <param name="requestedEnemy">지정 적, 없으면 무작위</param>
    /// <returns>실제 시작 여부</returns>
    public bool TryStart ( Room room, Enemy requestedEnemy )
    {
        return TryStart( room, requestedEnemy, out _ );
    }

    /// <summary>
    /// 지정 발생의 실제 실패 사유를 호출자에게 전달
    /// </summary>
    /// <param name="room">요청 방</param>
    /// <param name="requestedEnemy">지정 적</param>
    /// <param name="reason">시작 결과 또는 실패 사유</param>
    /// <returns>실제 시작 여부</returns>
    public bool TryStart ( Room room, Enemy requestedEnemy, out string reason )
    {
        reason = string.Empty;
        if ( requestedEnemy != null )
        {
            if ( _enemies.Contains( requestedEnemy ) == false )
            {
                reason = "지정 적이 이 발생 그룹에 등록되어 있지 않습니다.";
                return false;
            }
            if ( CheckStartEnemy( requestedEnemy, room, out reason ) == false )
                return false;
        }
        if ( CanStart( room ) == false )
        {
            if ( _isStarting == true )
                reason = "발생 그룹이 시작 처리 중입니다.";
            else if ( _settings.HasEventOwner == true && HasEventCandidate( room ) == false )
                reason = $"{room.name}: 시작 가능한 연결 사건 후보가 없습니다. 사건 종류 / 대상 / 설정을 확인해 주세요.";
            else
                reason = $"{room.name}: 시작 가능한 적 또는 사건 후보가 없습니다.";
            return false;
        }

        _isStarting = true;

        try
        {
            Enemy selectedEnemy = null;		//선정한 적
            if ( _settings.EnemyType != EnemySpawnType.None )
            {
                foreach ( Enemy enemy in _enemies )
                {
                    if ( CanStartEnemy( enemy, room ) == true )
                        _enemyCandidates.Add( enemy );
                }

                if ( _enemyCandidates.Count == 0 )
                {
                    reason = "시작 가능한 적 후보가 없습니다.";
                    return false;
                }

                if ( requestedEnemy != null && _enemyCandidates.Contains( requestedEnemy ) == false )
                {
                    CheckStartEnemy( requestedEnemy, room, out reason );
                    return false;
                }

                selectedEnemy = requestedEnemy != null ? requestedEnemy :
                    _enemyCandidates [ Random.Range( 0, _enemyCandidates.Count ) ];

                if ( CheckStartEnemy( selectedEnemy, room, out reason ) == false )
                    return false;

                if ( _settings.HasEventOwner == false )
                {
                    bool started = ( ( IAnomalySpawnTarget ) selectedEnemy ).TryStart( room, out reason );
#if UNITY_EDITOR
                    PlayDebugHistory.Record( "발생", selectedEnemy, $"{room.name} / {_settings.name} / 성공: {started}" );
#endif
                    return started;
                }
            }

            foreach ( AnomalyEventSystem presenter in _presenters )
            {
                if ( presenter != null && presenter.CanStart( room ) == true )
                    _eventCandidates.Add( presenter );
            }

            if ( _eventCandidates.Count == 0 )
            {
                reason = $"{room.name}: 시작 가능한 연결 사건 후보가 없습니다.";
                return false;
            }

            AnomalyEventSystem selected =
                _eventCandidates [ Random.Range( 0, _eventCandidates.Count ) ];		//선정한 사건

            if ( selected == null || selected.CanStart( room ) == false )
            {
                reason = "선정한 사건의 시작 조건이 변경됐습니다.";
                return false;
            }

            if ( _settings.EnemyType == EnemySpawnType.None )
            {
                bool started = selected.TryStartTraced( room, out _ );
                if ( started == false )
                    reason = $"{selected.name}: {selected.StartFailureSummary}";
#if UNITY_EDITOR
                PlayDebugHistory.Record( "발생", selected, $"{room.name} / {_settings.name} / 독립 사건 / 성공: {started}" );
#endif
                return started;
            }

            //선정 뒤 변경된 적 상태를 실행 직전에 확인
            if ( CheckStartEnemy( selectedEnemy, room, out reason ) == false )
                return false;

            //사건 결과를 받을 적
            IAnomalyEventOwner owner = ( IAnomalyEventOwner ) selectedEnemy;		
            AnomalyLinker linker = owner.EventLinker;		//시작 전 결과 구독

            if ( linker.TryPrepareStart( selected, out int requestId ) == false )
            {
                reason = "사건 결과 연결을 준비하지 못했습니다.";
                return false;
            }

            try
            {
                if ( selected.TryStartTraced( room, out AnomalyEvent occurrence ) == false )
                {
                    reason = $"{selected.name}: {selected.StartFailureSummary}";
                    return false;
                }

                //적이 종료돼도 실제 발생 성공은 유지
                if ( selectedEnemy != null && selectedEnemy.isActiveAndEnabled == true )
                    linker.CompleteStart( requestId, occurrence, () => owner.HandleEventStarted( room ) );

#if UNITY_EDITOR
                PlayDebugHistory.Record( "발생", selectedEnemy, $"{room.name} / {_settings.name} / 사건: {selected.name} / 연결: {linker.HasActiveEvent}" );
#endif
                return true;
            }
            finally
            {
                linker.CancelStart( requestId );
            }
        }
        finally
        {
            _enemyCandidates.Clear( );
            _eventCandidates.Clear( );
            _isStarting = false;
        }
    }

    /// <summary>
    /// 적의 발생 자격 확인
    /// </summary>
    /// <param name="enemy">검사할 적</param>
    /// <param name="room">요청한 방</param>
    /// <returns>발생 가능 여부</returns>
    bool CanStartEnemy ( Enemy enemy, Room room )
    {
        return CheckStartEnemy( enemy, room, out _ );
    }

    /// <summary>
    /// 실행 방식에 맞는 적의 시작 조건과 거절 사유 검사
    /// </summary>
    /// <param name="enemy">검사할 적</param>
    /// <param name="room">요청 방</param>
    /// <param name="reason">거절 사유</param>
    /// <returns>시작 가능 여부</returns>
    bool CheckStartEnemy ( Enemy enemy, Room room, out string reason )
    {
        reason = string.Empty;
        if ( enemy == null || enemy.isActiveAndEnabled == false )
        {
            reason = "활성 적이 필요합니다.";
            return false;
        }

        if ( _settings.HasEventOwner == true )
        {
            if ( enemy is IAnomalyEventOwner owner )
                return owner.CheckStart( room, out reason );
        }
        else if ( enemy is IAnomalySpawnTarget target )
            return target.CheckStart( room, out reason );

        reason = "발생 그룹 설정과 적의 실행 방식이 다릅니다.";
        return false;
    }

    /// <summary>
    /// 요청 방에 실행 가능한 사건이 있는지 확인
    /// </summary>
    /// <param name="room">요청한 방</param>
    /// <returns>사건 후보 존재 여부</returns>
    bool HasEventCandidate ( Room room )
    {
        foreach ( AnomalyEventSystem presenter in _presenters )
        {
            if ( presenter != null && presenter.CanStart( room ) == true )
                return true;
        }
        return false;
    }

    /// <summary>
    /// 요청 방의 발생 후보 확인
    /// </summary>
    /// <param name="room">요청 방</param>
    /// <returns>후보 존재 여부</returns>
    public bool CanStart ( Room room )
    {
        if ( _isStarting == true )
            return false;

        if ( _settings.EnemyType == EnemySpawnType.None )
            return HasEventCandidate( room );

        if ( _settings.HasEventOwner == true &&
            HasEventCandidate( room ) == false )
            return false;

        foreach ( Enemy enemy in _enemies )
        {
            if ( CanStartEnemy( enemy, room ) == true )
                return true;
        }
        return false;
    }

}
