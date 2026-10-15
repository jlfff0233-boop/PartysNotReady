#if UNITY_EDITOR
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 납치 적 디버그 - 행동 상태 조회와 테스트 실행
/// </summary>
public partial class EnemyKidnap
{
    float _debugApproachElapsed;		//접근 기록 간격의 경과 시간
    float _debugEscapeElapsed;		//도주 기록 간격의 경과 시간

    /// <summary>
    /// 납치 접근 판정값 기록
    /// </summary>
    /// <param name="reason">기록할 전환 또는 진행 사유</param>
    void RecordDebugApproach ( string reason )
    {
        PlayDebugHistory.Record( "납치 접근", this,
            $"{reason} / {_chaser.GetDebugDetails( )}" );
    }

    /// <summary>
    /// 접근 진행을 1초 간격으로 기록하고 종료는 즉시 기록
    /// </summary>
    /// <param name="deltaTime">접근 갱신 시간</param>
    /// <param name="status">접근 갱신 결과</param>
    void UpdateDebugApproach ( float deltaTime, ApproachStatus status )
    {
        _debugApproachElapsed += deltaTime;
        if ( status == ApproachStatus.Moving && _debugApproachElapsed < 1f )
            return;

        _debugApproachElapsed = 0f;
        RecordDebugApproach( status == ApproachStatus.Moving ? "진행" : "종료" );
    }

    /// <summary>
    /// 공격 명중과 포획 상태, 목표 충돌체 기록
    /// </summary>
    /// <param name="reason">공격 기록 사유</param>
    public void RecordDebugAttack ( string reason )
    {
        string targetDetails = $"목표: {EnemyDebugUtility.Name( _target as Component )} / " +
            $"목표 유효: {IsTargetValid( )}";
        if ( _target is Component target && target != null )
        {
            foreach ( Collider collider in target.GetComponentsInChildren<Collider>( ) )
            {
                targetDetails += $" / 충돌체: {collider.name} / 중심: {collider.bounds.center} / " +
                    $"범위: {collider.bounds.size} / 레이어: {collider.gameObject.layer} / " +
                    $"Trigger: {collider.isTrigger} / 공격 검사 대상: {_attacker.IsTargetCollider( collider )}" +
                    _attacker.GetTargetDebugDetails( collider );
            }
        }

        PlayDebugHistory.Record( "납치 공격", this,
            $"{reason} / 명중 후 포획 요청: {CaptureAttempted} / 실제 포획: {_capturer.HasTarget} / " +
            $"단계: {CaptureAttackPhase} / " +
            $"공격 거리: {_data.AttackDist:F2} / 준비: {_data.AttackDelay:F2}초 / " +
            $"판정 시점: AttackHit / 장착 시점: CaptureAttach / " +
            $"{_mover.GetAttackDebugDetails( )} / {_animatorHandler.GetAttackDebugDetails( )} / " +
            $"{_attacker.GetDebugDetails( )} / {targetDetails}" );
    }

    /// <summary>
    /// 도주 진행을 1초 간격으로 기록하고 결과는 즉시 기록
    /// </summary>
    /// <param name="deltaTime">이동 갱신 시간</param>
    /// <param name="status">이동 갱신 결과</param>
    void UpdateDebugEscape ( float deltaTime, RouteStatusType status )
    {
        _debugEscapeElapsed += deltaTime;
        if ( status == RouteStatusType.Moving && _debugEscapeElapsed < 1f )
            return;

        _debugEscapeElapsed = 0f;
        PlayDebugHistory.Record( "납치 도주 진행", this,
            $"{_escapeNavigator.GetDebugDetails( )} / 실제 포획: {_capturer.HasTarget}" );
    }

    /// <summary>
    /// 납치 활동 상세 조회
    /// </summary>
    /// <param name="details">표시 항목</param>
    public override void GetDebugDetails ( List<AnomalyDebugDetail> details )
    {
        base.GetDebugDetails( details );

        if ( _model == null )
            return;

        details.Add( new( "활동 방", EnemyDebugUtility.Name( _model.CurrentRoom ) ) );

        details.Add( new( "방 준비 / 활동 가능",
            $"{_roomActivity.IsReady} / {_roomActivity.CanRun}" ) );

        details.Add( new( "잠복 남은 시간",
            $"{_hider.DebugRemainingTime:F1}초" ) );

        details.Add( new( "RescueLimit / 현재 구조 저지",
            $"{_data.RescueLimit} / {_model.RescueCount}" ) );

        details.Add( new( "납치 허용 / 보복 기준",
            $"{CanKidnap} / {ShouldRetaliate}" ) );

        details.Add( new( "표적", EnemyDebugUtility.Name( _target as Component ) ) );

        details.Add( new( "도주 경로",
            $"{_escapeNavigator.Status} / {_escapeNavigator.FailureReason}" ) );
        details.Add( new( "접근 판정", _chaser.GetDebugDetails( ) ) );
        details.Add( new( "명중 요청 / 실제 포획", $"{CaptureAttempted} / {_capturer.HasTarget}" ) );
        details.Add( new( "도주 목적지", _escapeNavigator.GetDebugDetails( ) ) );
    }

    /// <summary>
    /// 정리된 적의 대상 방 재배치와 활동 모델 준비
    /// </summary>
    /// <param name="room">활동할 방, 없으면 현재 위치 사용</param>
    /// <param name="reason">준비 실패 사유</param>
    /// <returns>활동 준비 성공 여부</returns>
    bool PrepareDebugActivity ( Room room, out string reason )
    {
        reason = string.Empty;
        if ( room != null )
        {
            if ( _roomActivity.TryPrepareDebug( room ) == false )
            {
                reason = "대상 방 재배치 실패. " +
                    "콘솔의 방 이동 실패 사유를 확인해 주세요.";
                return false;
            }

            _model.ResetDebugCounts( room );
            _model.BeginActivity( room );
            _anomalyLinker.Begin( );

            PlayDebugHistory.Record( "테스트", this,
                $"대상 방 재배치: {room.name} / 잠복 대기 생략" );

            return true;
        }
        _hider.Begin( 0f );
        if ( BeginRoomActivity( ) == false )
        {
            reason = "현재 위치의 방 또는 " +
                "잠복 이동 경로를 준비하지 못했습니다.";

            return false;
        }
        _model.ResetDebugCounts( _model.CurrentRoom );
        PlayDebugHistory.Record( "테스트", this,
            $"방 준비: {_model.CurrentRoom.name} / 잠복 대기 생략" );

        return true;
    }

    /// <summary>
    /// 현재 위치 또는 지정 방에서 테스트 활동 준비
    /// </summary>
    /// <param name="reason">준비 실패 사유</param>
    /// <param name="room">재배치할 방, 없으면 현재 활동 준비</param>
    /// <returns>준비 성공 여부</returns>
    public bool TryPrepareDebugActivity ( out string reason, Room room = null )
    {
        //이전 상태와 연결 사건을 정리하고 새 테스트 준비
        room = room != null ? room : _model.CurrentRoom;
        if ( TryResetDebugActivity( out reason ) == false )
            return false;

        return PrepareDebugActivity( room, out reason );
    }

    /// <summary>
    /// 지정 사건 테스트를 위한 시작과 방 활동 준비
    /// </summary>
    /// <param name="room">사건을 발생시킬 방</param>
    /// <param name="reason">준비 실패 사유</param>
    /// <returns>준비 성공 여부</returns>
    public bool TryPrepareDebugEvent ( Room room, out string reason )
    {
        if ( TryPrepareDebugActivity( out reason, room ) == false )
            return false;

        _isPressureApproved = true;
        return true;
    }

    /// <summary>
    /// 지정 방에서 연결 사건 테스트 준비
    /// </summary>
    /// <param name="requestedRoom">테스트 방</param>
    /// <param name="room">실제 적용 방</param>
    /// <param name="reason">준비 실패 사유</param>
    /// <returns>준비 성공 여부</returns>
    public override bool TryPrepareDebugSpawn ( Room requestedRoom, out Room room, out string reason )
    {
        room = requestedRoom;
        return TryPrepareDebugEvent( room, out reason );
    }

    /// <summary>
    /// 구조 저지 기준을 채워 보복 분기 준비
    /// </summary>
    /// <param name="reason">실행 결과</param>
    /// <param name="room">테스트 방, 없으면 현재 활동 방</param>
    /// <returns>준비 성공 여부</returns>
    public bool TryDebugRetaliation ( out string reason, Room room = null )
    {
        if ( TryPrepareDebugActivity( out reason, room ) == false )
            return false;

        for ( int i = 0; i < _model.RequiredCount && ShouldRetaliate == false; i++ )
            _model.RecordRescue( );

        _isPressureApproved = true;
        reason = "구조 저지 기준 적용. 정상 보복 확률과 표적 검사는 유지합니다.";
        PlayDebugHistory.Record( "테스트", this, reason );

        return true;
    }

    /// <summary>
    /// 지정 또는 자동 선택 대상의 방에서 납치 테스트
    /// </summary>
    /// <param name="target">NPC 또는 플레이어, 없으면 자동 선택</param>
    /// <param name="captureNow">접근과 명중을 생략할지 여부</param>
    /// <param name="reason">실행 결과</param>
    /// <param name="autoPlayer">자동 선택 시 플레이어를 찾을지 여부</param>
    /// <returns>시작 성공 여부</returns>
    public bool TryDebugKidnap (
        Component target, bool captureNow,
        out string reason, bool autoPlayer = false )
    {
        //포획 중인 대상도 먼저 해제하여 같은 대상을 반복 테스트할 수 있게 준비
        if ( TryResetDebugActivity( out reason ) == false )
            return false;

        if ( target == null )
            target = autoPlayer == true
                ? EnemyDebugUtility.FindCaptureTarget<Player>( this )
                : EnemyDebugUtility.FindCaptureTarget<Npc>( this );

        if ( target is not ICaptureTarget capture ||
            target is not IAttackTarget attack ||
            capture.CanCapture == false )
        {
            reason = "포획 가능한 NPC 또는 플레이어가 없습니다. " +
                "대상 종류와 포획 상태를 확인해 주세요.";
            return false;
        }

        if ( target.gameObject.scene != gameObject.scene ||
             RoomRegister.TryGetRoom( attack.TargetTransform.position, out Room room ) == false )
        {
            reason = "대상이 같은 씬의 등록된 방 안에 있어야 합니다.";
            return false;
        }

        if ( PrepareDebugActivity( room, out reason ) == false )
            return false;

        _isPressureApproved = true;
        _target = attack;
        _model.SetDebugTargetType( target is Player );

        if ( captureNow == false )
        {
            ChangeState( EnemyStateType.Chase );
            reason = "사건 조건과 표적 추첨 생략. " +
                "접근과 명중부터 실행합니다.";
        }
        else
        {
            //포획 성공 여부를 확인한 뒤 운반용 도주 경로를 선택
            bool captured = _capturer.TryCapture( capture );

            if ( captured == false )
            {
                _model.EndPlayerAttempt( );
                reason = "포획 요청이 거절됐습니다. " +
                    "대상의 포획 가능 상태를 확인해 주세요.";
                return false;
            }
            //포획 콜백에서 활동이 중단됐다면 도주로 덮어쓰지 않음
            if ( CanRun == false )
            {
                reason = "포획 콜백에서 적 활동이 종료됐습니다.";
                return false;
            }
            ChangeState( EnemyStateType.Escape );
            reason = "사건 조건 / 접근 / 명중 생략. " +
                "포획과 도주부터 실행합니다.";
        }
        reason = $"대상: {target.name} / 방: {room.name} / {reason}";
        PlayDebugHistory.Record( "테스트", this, reason );
        return true;
    }
}
#endif
