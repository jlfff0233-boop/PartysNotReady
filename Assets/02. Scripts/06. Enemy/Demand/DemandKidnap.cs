using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아이템 요구 적 납치 모듈 - 표적 선정, 접근과 포획 처리
/// </summary>
public class DemandKidnap
{
    Transform _origin;     //거리와 회전 기준
    CharacterMover _mover;     //회전 적용
    Approacher _approacher;     //표적 접근
    CharacterAttacker _attacker;     //표적 명중 검사
    CaptureController _capturer;     //포획 처리
    Npc _target;     //선정한 표적
    CaptureAttack _captureAttack;     //클립 시점의 명중/장착 진행
    List<Npc> _candidates = new( );     //선정 후보 목록

    /// <summary>
    /// 현재 포획한 대상 유무
    /// </summary>
    public bool HasTarget => _capturer != null && _capturer.HasTarget;

    /// <summary>
    /// 납치 행동에 사용할 기능 연결
    /// </summary>
    /// <param name="origin">거리와 회전 기준</param>
    /// <param name="view">회전을 적용할 뷰</param>
    /// <param name="approacher">표적 접근 기능</param>
    /// <param name="attacker">표적 명중 검사</param>
    /// <param name="capturer">포획 처리 기능</param>
    /// <param name="captureAttack">클립 시점의 포획 공격</param>
    public DemandKidnap (
        Transform origin, CharacterMover view, Approacher approacher,
        CharacterAttacker attacker, CaptureController capturer, CaptureAttack captureAttack )
    {
        _origin = origin;
        _mover = view;
        _approacher = approacher;
        _attacker = attacker;
        _capturer = capturer;
        _captureAttack = captureAttack;
    }


    #region ----- 대상 선정/접근 -----

    /// <summary>
    /// 현재 게임에서 가장 가까운 포획 가능 NPC 선정
    /// </summary>
    /// <returns>선정 성공 여부</returns>
    public bool TrySelectTarget ()
    {
        _target = null;
        NpcRegister.GetCaptureCandidates( _candidates );

        float nearestSqrDist = float.PositiveInfinity;

        foreach ( Npc candidate in _candidates )
        {
            float sqrDist =
                ( candidate.TargetTransform.position - _origin.position )
                .sqrMagnitude;

            if ( sqrDist >= nearestSqrDist )
                continue;

            nearestSqrDist = sqrDist;
            _target = candidate;
        }

        return _target != null;
    }

    /// <summary>
    /// 선정한 NPC 접근 시작
    /// </summary>
    public void BeginChase ()
    {
        _approacher.Begin(
            IsTargetValid( ) ? _target.TargetTransform : null );
    }

    /// <summary>
    /// NPC 접근 진행
    /// </summary>
    /// <param name="deltaTime">경과시간</param>
    /// <returns>접근 결과</returns>
    public ApproachStatus UpdateChase ( float deltaTime )
    {
        if ( IsTargetValid( ) == false )
            return ApproachStatus.Failed;

        return _approacher.UpdateMove( deltaTime );
    }

    /// <summary>
    /// NPC 접근 종료
    /// </summary>
    public void StopChase ()
    {
        _approacher.Stop( );
    }

    /// <summary>
    /// 포획 표적 방향으로 회전
    /// </summary>
    /// <param name="rotateSpeed">회전 속력</param>
    public void LookAtTarget ( float rotateSpeed )
    {
        if ( IsTargetValid( ) == false )
            return;

        Vector3 direction =
            _target.TargetTransform.position - _origin.position;
        direction.y = 0f;

        if ( direction.sqrMagnitude > 0.0001f )
            _mover.Rotate( Quaternion.LookRotation( direction ), rotateSpeed );
    }

    #endregion

    #region ----- 포획 공격 -----

    /// <summary>
    /// 선정한 NPC의 포획 공격 시작
    /// </summary>
    public void BeginAttack ()
    {
        _captureAttack.Begin( _target );
    }

    /// <summary>
    /// 포획 공격 종료와 표적 정리
    /// </summary>
    public void EndAttack ()
    {
        _captureAttack.End( );
        _target = null;
    }

    #endregion


    /// <summary>
    /// 포획 중인 NPC의 납치 완료 요청
    /// </summary>
    /// <returns>포획 대상이 없거나 납치 완료에 성공했으면 true</returns>
    public bool TryCompleteEscape ()
    {
        if ( HasTarget == false )
            return true;

        //납치 완료를 대상에서 수락해야 적도 퇴장 가능
        return _capturer.Target is Npc npc &&
               npc != null &&
               npc.TryCompleteKidnap( _capturer );
    }

    /// <summary>
    /// 적 비활성화 시 공격과 진행 중인 포획 정리
    /// </summary>
    public void Clear ()
    {
        //공격 대기를 먼저 끊은 뒤 실제 포획 취소를 기존 경로로 전달
        _captureAttack.End( );
        _capturer.TryRelease( CaptureResult.Cancelled );
        _target = null;
    }


    #region ----- 상태 검사 -----

    /// <summary>
    /// 납치에 필요한 컴포넌트의 연결과 활성 상태 확인
    /// </summary>
    /// <returns>납치 행동 가능 여부</returns>
    public bool IsReady ()
    {
        return _attacker != null &&
               _attacker.isActiveAndEnabled == true &&
               _capturer != null &&
               _capturer.isActiveAndEnabled == true;
    }

    /// <summary>
    /// 현재 납치 표적의 유효성 확인
    /// </summary>
    /// <returns>포획 가능한 NPC 여부</returns>
    public bool IsTargetValid ()
    {
        return _target != null &&
            _target.CanCapture == true;
    }

    #endregion
}
