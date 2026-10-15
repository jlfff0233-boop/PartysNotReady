#if UNITY_EDITOR
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 음식 요구 적 디버그 - 행동 상태 조회와 공격, 포획 테스트
/// </summary>
public partial class EnemyDemand
{
    /// <summary>
    /// 음식 요구와 납치 상태 조회
    /// </summary>
    /// <param name="details">표시 항목</param>
    public override void GetDebugDetails ( List<AnomalyDebugDetail> details )
    {
        base.GetDebugDetails( details );
        if ( _model != null )
            details.Add( new( "만족도", $"{_model.CurrentSatisfaction:F1} / {_model.MaxSatisfaction:F1}" ) );
        details.Add( new( "수락 음식", EnemyDebugUtility.Name( _receivedFood ) ) );
        details.Add( new( "포획 / 납치 시도", $"{HasKidnapTarget} / {KidnapAttempted}" ) );
    }


    /// <summary>
    /// 등장 위치에서 실제 난동 공격 한 번 실행
    /// </summary>
    /// <param name="reason">실행 결과</param>
    /// <returns>시작 성공 여부</returns>
    public bool TryDebugAttack ( out string reason )
    {
        if ( PrepareDebugAction( out reason ) == false )
            return false;

        //물건 후보와 접근 지점이 없어도 공격 연출과 명중 검사 확인
        IEnemyState previous = _states [ ( int ) EnemyStateType.Mad ];		//기존 난동 상태
        _states [ ( int ) EnemyStateType.Mad ] = new DemandDebugAttackState( this, previous );
        ChangeState( EnemyStateType.Mad );
        reason = "등장 위치에서 공격 1회 실행. 범위 안 물건에는 실제 피해를 적용하고 퇴장합니다.";
        PlayDebugHistory.Record( "테스트", this, reason );
        return true;
    }

    /// <summary>
    /// 확률 분기를 생략하고 가까운 NPC 포획 실행
    /// </summary>
    /// <param name="captureNow">접근과 명중을 생략할지 여부</param>
    /// <param name="reason">실행 결과</param>
    /// <returns>시작 성공 여부</returns>
    public bool TryDebugKidnap ( bool captureNow, out string reason )
    {
        if ( PrepareDebugAction( out reason ) == false )
            return false;

        if ( _kidnap.IsReady( ) == false )
        {
            reason = "곰의 NPC 공격 기능과 포획 기능 연결 / 활성 상태를 확인해 주세요.";
            ResetAppearance( );
            return false;
        }

        if ( captureNow == false )
        {
            if ( _kidnap.TrySelectTarget( ) == false )
            {
                reason = "포획 가능한 NPC가 없습니다.";
                ResetAppearance( );
                return false;
            }

            ChangeState( EnemyStateType.Chase );
            reason = "NPC 수와 납치 확률 분기 생략. 가까운 NPC 접근 / 명중부터 실행합니다.";
        }
        else
        {
            Npc target = EnemyDebugUtility.FindCaptureTarget<Npc>( this );		//즉시 포획할 NPC
            if ( target == null || _capturer.TryCapture( target ) == false )
            {
                reason = "포획 가능한 NPC가 없거나 포획 요청이 거절됐습니다.";
                ResetAppearance( );
                return false;
            }

            //실제 운반 시작 연출과 기존 납치 퇴장 처리 재사용
            UpdateKidnapAnimation( );
            ChangeState( EnemyStateType.Escape );
            reason = $"대상: {target.name} / 접근과 명중 생략. 실제 포획 / 운반 / 퇴장부터 실행합니다.";
        }

        PlayDebugHistory.Record( "테스트", this, reason );
        return true;
    }

    /// <summary>
    /// 이전 활동을 정리하고 설정된 시작 방에서 곰 표시
    /// </summary>
    /// <param name="reason">준비 실패 사유</param>
    /// <returns>준비 성공 여부</returns>
    bool PrepareDebugAction ( out string reason )
    {
        if ( TryResetDebugActivity( out reason ) == false )
            return false;

        if ( CheckStart( DebugStartRoom, out reason ) == false )
            return false;

        if ( _activation.TryPreparePosition(
            _mover, _spawnPoint, _destinationSampleDist ) == false )
        {
            reason = "등장 지점의 연결과 NavMesh를 확인해 주세요.";
            return false;
        }

        //일반 등장 이동을 요청하지 않고 테스트 동작에서 이동을 한 번만 요청
        //같은 프레임에 등장 경로 취소와 포획 경로 요청이 겹치는 상황 방지
        _appearanceRoom = DebugStartRoom;
        PrepareAppearance( );
        _activation.Show( );
        return true;
    }


    /// <summary>
    /// 디버그 난동 공격 - 명중 검사 후 퇴장과 기존 상태 복원
    /// </summary>
    class DemandDebugAttackState : IEnemyState
    {
        EnemyDemand _enemy;		//공격할 곰
        IEnemyState _previous;		//복원할 기존 난동 상태
        float _elapsed;		//공격 진행 시간

        /// <summary>
        /// 실제 난동 피해 처리에 사용할 상태 종류
        /// </summary>
        public EnemyStateType StateType => EnemyStateType.Mad;


        /// <summary>
        /// 공격 대상과 복원할 상태 연결
        /// </summary>
        /// <param name="enemy">공격할 곰</param>
        /// <param name="previous">기존 난동 상태</param>
        public DemandDebugAttackState ( EnemyDemand enemy, IEnemyState previous )
        {
            _enemy = enemy;
            _previous = previous;
        }

        /// <summary>
        /// 이동을 멈추고 실제 공격 시작
        /// </summary>
        public void Enter ()
        {
            _elapsed = 0f;
            _enemy.Stop( );
            _enemy.BeginAttack( );
        }

        /// <summary>
        /// 설정된 공격 시간 동안 명중 검사 후 퇴장
        /// </summary>
        public void Update ()
        {
            _enemy.CheckAttack( );
            _elapsed += Time.deltaTime;
            if ( _elapsed >= _enemy.AttackDuration )
                _enemy.ChangeState( EnemyStateType.Escape );
        }

        /// <summary>
        /// 공격을 종료하고 원래 난동 상태 복원
        /// </summary>
        public void Exit ()
        {
            _enemy.EndAttack( );
            _enemy.Stop( );
            _enemy._states [ ( int ) EnemyStateType.Mad ] = _previous;
        }
    }
}
#endif
