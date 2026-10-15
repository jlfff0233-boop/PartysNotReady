using System;
using UnityEngine;

/// <summary>
/// 포획 공격 - 클립 요청에 따른 명중, 장착과 동작 종료 관리
/// </summary>
public class CaptureAttack
{
    enum AttackPhase
    {
        Idle,           //공격 없음
        WaitAnimation,  //공격 상태 진입 대기
        WaitHit,        //명중 요청 대기
        WaitAttach,     //명중 대상 장착 대기
        Recovery,       //공격 동작 복귀 대기
        Finished        //다음 행동 전환 가능
    }

    AttackAnimatorHandler _animatorHandler;  //공격 재생과 클립 알림
    CharacterAttacker _attacker;             //선정한 목표 명중 검사
    CaptureController _capturer;             //실제 포획과 캐리 포인트 연결
    UnityEngine.Object _owner;               //설정 오류 로그 대상
    Action _onCaptured;                      //포획 성공 후 표시 갱신
    float _animationStartTimeout;            //공격 상태 진입 제한시간

    AttackPhase _phase;                     //현재 진행 단계
    ICaptureTarget _pendingCapture;         //명중 후 장착을 기다리는 대상
    float _elapsedTime;                     //애니메이션 진입 대기시간
    bool _hitRequested;                     //이번 프레임의 명중 요청
    bool _attachRequested;                  //이번 프레임의 장착 요청
    bool _attempted;                        //이번 공격의 실제 포획 요청 기록

    /// <summary>
    /// 이전 공격이 복귀하여 새 공격을 시작할 수 있는지 여부
    /// </summary>
    public bool CanBegin =>
        ( _phase == AttackPhase.Idle || _phase == AttackPhase.Finished ) &&
        _animatorHandler.IsAttackPlaying == false;

    /// <summary>
    /// 공격 동작 종료 여부
    /// </summary>
    public bool IsFinished => _phase == AttackPhase.Finished;

    /// <summary>
    /// 실제 포획 요청을 실행했는지 여부
    /// </summary>
    public bool Attempted => _attempted;

#if UNITY_EDITOR
    /// <summary>
    /// 현재 공격 진행 단계
    /// </summary>
    public string DebugPhase => _phase.ToString( );
#endif

    /// <summary>
    /// 공격 실행 모듈과 포획 성공 알림 연결
    /// </summary>
    /// <param name="animatorHandler">공격 애니메이션</param>
    /// <param name="attacker">목표 명중 검사</param>
    /// <param name="capturer">포획 처리</param>
    /// <param name="owner">설정 오류 로그 대상</param>
    /// <param name="onCaptured">포획 성공 후 표시 갱신</param>
    /// <param name="animationStartTimeout">공격 상태 진입 제한시간</param>
    public CaptureAttack (
        AttackAnimatorHandler animatorHandler, CharacterAttacker attacker,
        CaptureController capturer, UnityEngine.Object owner,
        Action onCaptured, float animationStartTimeout )
    {
        _animatorHandler = animatorHandler;
        _attacker = attacker;
        _capturer = capturer;
        _owner = owner;
        _onCaptured = onCaptured;
        _animationStartTimeout = animationStartTimeout;
    }

    /// <summary>
    /// 준비가 끝난 목표에 포획 공격 시작
    /// </summary>
    /// <param name="target">선정한 NPC 또는 플레이어</param>
    public void Begin ( IAttackTarget target )
    {
        _pendingCapture = null;
        _elapsedTime = 0f;
        _hitRequested = false;
        _attachRequested = false;
        _attempted = false;
        _phase = AttackPhase.WaitAnimation;

        _attacker.SetTarget( target );
        //이번 공격에서만 구독하여 일반 공격과 종료된 클립의 알림을 분리
        _attacker.OnCharacterHit += HandleCharacterHit;
        _animatorHandler.OnAttackHit += HandleAttackHit;
        _animatorHandler.OnCaptureAttach += HandleCaptureAttach;
        _animatorHandler.PlayAttack( );
    }

    /// <summary>
    /// 최종 손 자세에서 명중, 장착과 동작 종료를 순서대로 처리
    /// </summary>
    /// <param name="deltaTime">게임 시간 기준 경과시간</param>
    public void LateUpdateAttack ( float deltaTime )
    {
        if ( _phase == AttackPhase.Idle || _phase == AttackPhase.Finished )
            return;
        //일시정지 중에는 대기시간과 이미 받은 요청을 함께 보존
        if ( deltaTime <= 0f )
            return;

        //낮은 프레임률에서 두 이벤트가 함께 와도 명중부터 처리
        if ( _hitRequested == true )
        {
            _hitRequested = false;
            CheckHit( );
        }
        else if ( _phase == AttackPhase.WaitAnimation &&
                  _animatorHandler.HasEnteredAttack == true )
        {
            _phase = AttackPhase.WaitHit;
        }

        if ( _attachRequested == true )
        {
            _attachRequested = false;
            AttachTarget( );
        }

        //포획 콜백에서 적이 중단되면 End가 적용한 Idle을 덮어쓰지 않음
        if ( _phase == AttackPhase.Idle )
            return;

        if ( _phase == AttackPhase.WaitAnimation )
        {
            _elapsedTime += deltaTime;
            if ( _elapsedTime < _animationStartTimeout )
                return;

            Debug.LogError( "[포획 공격 실패] Attack 상태에 진입하지 못했습니다.", _owner );
            Finish( );
            return;
        }

        if ( _animatorHandler.IsAttackPlaying == true )
            return;

        //이벤트 누락을 타이머 포획으로 대체하지 않고 설정 오류로 표시
        if ( _phase == AttackPhase.WaitHit )
            Debug.LogError( "[포획 공격 실패] AttackHit 이벤트가 없습니다.", _owner );
        else if ( _phase == AttackPhase.WaitAttach )
            Debug.LogError( "[포획 공격 실패] CaptureAttach 이벤트가 없습니다.", _owner );

        Finish( );
    }

    /// <summary>
    /// 클립의 명중 요청 기록
    /// </summary>
    void HandleAttackHit ()
    {
        if ( _phase == AttackPhase.WaitAnimation || _phase == AttackPhase.WaitHit )
            _hitRequested = true;
    }

    /// <summary>
    /// 클립의 장착 요청 기록
    /// </summary>
    void HandleCaptureAttach ()
    {
        //같은 프레임의 명중 요청은 아직 처리 전일 수 있음
        if ( _phase == AttackPhase.WaitAttach || _hitRequested == true )
            _attachRequested = true;
    }

    /// <summary>
    /// 실제 명중 대상 보관
    /// </summary>
    /// <param name="target">선정한 목표 중 범위에 들어온 대상</param>
    void HandleCharacterHit ( IAttackTarget target )
    {
        //CharacterAttacker가 목표 일치와 중복 명중을 검사
        //입력 목표는 ICaptureTarget을 구현한 NPC 또는 플레이어로 제한
        _pendingCapture = (ICaptureTarget)target;
    }

    /// <summary>
    /// 명중 요청을 한 번의 범위 검사로 변환
    /// </summary>
    void CheckHit ()
    {
        _phase = AttackPhase.WaitAttach;
        //자동 동기화가 꺼져 있으므로 이동한 대상의 충돌체부터 반영
        Physics.SyncTransforms( );
        _attacker.BeginAttack( );
        _attacker.CheckHit( );
        _attacker.EndAttack( );

        if ( _pendingCapture == null )
            _phase = AttackPhase.Recovery;
    }

    /// <summary>
    /// 명중 대상을 현재 캐리 포인트에 포획
    /// </summary>
    void AttachTarget ()
    {
        if ( _phase != AttackPhase.WaitAttach )
            return;

        ICaptureTarget target = _pendingCapture;
        //포획 시작 콜백보다 먼저 요청을 소비하여 중복 장착 방지
        _pendingCapture = null;
        _phase = AttackPhase.Recovery;
        _attempted = true;

        //명중 후 대상이 사라지거나 먼저 포획된 경우는 기존 모듈에서 검사
        if ( _capturer.TryCapture( target ) == false )
            return;

        _onCaptured( );
    }

    /// <summary>
    /// 공격 정리 후 다음 행동 전환 허용
    /// </summary>
    void Finish ()
    {
        End( );
        _phase = AttackPhase.Finished;
    }

    /// <summary>
    /// 상태 종료 또는 비활성화에 따른 공격 요청 정리
    /// </summary>
    public void End ()
    {
        if ( _phase == AttackPhase.Idle )
            return;

        _phase = AttackPhase.Idle;
        _pendingCapture = null;
        _hitRequested = false;
        _attachRequested = false;

        _attacker.OnCharacterHit -= HandleCharacterHit;
        _animatorHandler.OnAttackHit -= HandleAttackHit;
        _animatorHandler.OnCaptureAttach -= HandleCaptureAttach;
        _attacker.SetTarget( null );
        _animatorHandler.CancelAttackRequest( );
        //포획 관계는 유지하여 도주에서 운반하고 기존 결과 경로에서만 해제
    }
}
