using UnityEngine;
using System;

/// <summary>
/// 이동 모듈 - 이동, 점프
/// </summary>
public class Mover : MonoBehaviour
{
    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] CharacterController _ctrl;     //캐릭터 컨트롤러
    [SerializeField] PlayerAnimatorHandler _animatorHandler;		//이동과 점프 애니메이션

    [Header( "----- 이동 설정 -----" )]
    [SerializeField] float _moveSpeed;      //이동 속력
    [SerializeField] float _turnSpeed;      //회전 속력
    [SerializeField, Min( 0f )] float _runSpeed;       //달리기 속력
    [SerializeField] float _gravity;        //중력
    [SerializeField] float _jumpPower;      //점프력
    [SerializeField, Min( 0.01f )] float _releaseDeceleration = 8f;     //포획 해제 후 감속

    object _supportSpeedOwner;		//이동 효과 적용자
    float _supportSpeedMultiplier = 1f;		//서포트 이동 배율

    Transform _camRef;      //카메라 참조
    Vector2 _targetDir;     //목표 이동 방향(x, z)
    float _verticalVelocity;        //y 방향 속도
    bool _isRunning;        //달리기 적용 여부
    bool _isSuspended;      //이동 중단 여부(납치될 때)
    Vector3 _releaseVelocity;       //포획 해제 후 수평 이동 속도
    bool _canMove = true;		//정상 이동 가능 여부


    /// <summary>
    /// 이동 적용 결과 이벤트
    /// </summary>
    public event Action<MoveResult> OnMoveApplied;

    /// <summary>
    /// 실제 점프 시작 이벤트
    /// </summary>
    public event Action OnJumped;

    /// <summary>
    /// 이동 기록 초기화 이벤트
    /// </summary>
    public event Action OnMovementReset;


    private void OnEnable ()
    {
        ResetMovementState( );
    }

    private void Update ()
    {
        UpdateMove( );
    }

    private void OnDisable ()
    {
        if ( _animatorHandler != null )
            _animatorHandler.SetMoveSpeed( 0f );

        ResetMovementState( );
    }


    /// <summary>
    /// 서포트 이동 효과 적용
    /// </summary>
    /// <param name="owner">적용자</param>
    /// <param name="multiplier">이동 배율</param>
    /// <returns>적용 여부</returns>
    public bool TryApplySupportSpeed ( object owner, float multiplier )
    {
        if ( owner == null || _supportSpeedOwner != null ||
             float.IsNaN( multiplier ) == true || float.IsInfinity( multiplier ) == true ||
             multiplier < 1f )
            return false;

        _supportSpeedOwner = owner;
        _supportSpeedMultiplier = multiplier;
        return true;
    }

    /// <summary>
    /// 서포트 이동 효과 복원
    /// </summary>
    /// <param name="owner">적용자</param>
    public void ClearSupportSpeed ( object owner )
    {
        if ( ReferenceEquals( _supportSpeedOwner, owner ) == false )
            return;

        _supportSpeedOwner = null;
        _supportSpeedMultiplier = 1f;
    }


    #region ----- 이동/회전 -----

    /// <summary>
    /// 이동과 회전의 기준 카메라 연결
    /// </summary>
    /// <param name="camRef">방향 계산에 사용할 카메라 Transform</param>
    public void SetCamRef ( Transform camRef )
    {
        _camRef = camRef;
    }

    /// <summary>
    /// 달리기 적용 여부 설정
    /// </summary>
    /// <param name="isRunning">달리기 적용 여부</param>
    public void SetRunning ( bool isRunning )
    {
        _isRunning = isRunning;
    }

    /// <summary>
    /// 이동 입력 저장
    /// </summary>
    /// <param name="dir">좌우와 전후 이동 입력</param>
    public void Move ( Vector2 dir )
    {
        _targetDir = Vector2.ClampMagnitude( dir, 1f );
    }

    /// <summary>
    /// 카메라 기준 수평 이동 방향 계산
    /// </summary>
    Vector3 GetMoveDir ()
    {
        Vector3 forward = _camRef.forward;
        Vector3 right = _camRef.right;

        //카메라의 위아래 기울기를 제외하고 수평 방향만 사용
        forward.y = 0f;
        right.y = 0f;

        forward.Normalize( );
        right.Normalize( );

        return Vector3.ClampMagnitude( right * _targetDir.x + forward * _targetDir.y, 1f );
    }

    /// <summary>
    /// 카메라 정면 방향으로 회전
    /// </summary>
    void RotateToCamRefForward ()
    {
        //이동 입력이 없는 경우 종료
        if ( _targetDir.magnitude < 0.01f ) return;

        //카메라 수평 방향 계산
        Vector3 lookDir = _camRef.forward;
        lookDir.y = 0f;
        lookDir.Normalize( );

        //카메라 정면 회전값 계산
        Quaternion targetRotation = Quaternion.LookRotation( lookDir );

        //현재 회전값에서 목표 회전값으로 보간
        transform.rotation = Quaternion.Slerp(
            transform.rotation, targetRotation, _turnSpeed * Time.deltaTime );
    }

    /// <summary>
    /// 이동 갱신
    /// </summary>
    void UpdateMove ()
    {
        //납치당하는 중에는 이동, 중력과 회전 갱신 중단
        if ( _isSuspended == true ||
             _ctrl.enabled == false ||
             _ctrl.gameObject.activeInHierarchy == false )
        {
            if ( _animatorHandler != null )
                _animatorHandler.SetMoveSpeed( 0f );

            OnMoveApplied?.Invoke( new MoveResult( 0f , false , false , false ) );
            return;
        }

        //지면에 착지해 있는 경우
        if ( _ctrl.isGrounded == true && _verticalVelocity < 0f )
            _verticalVelocity = -1f;

        //중력 적용(시간 당 속력 변화량)
        _verticalVelocity += _gravity * Time.deltaTime;

        //달리기 여부에 따른 수평 이동 속력
        float speed = ( _isRunning == true ? _runSpeed : _moveSpeed ) * _supportSpeedMultiplier;

        //입력 이동과 포획 해제 속도, 중력을 합산해 한 번에 이동
        Vector3 velocity =
            GetMoveDir( ) * speed + _releaseVelocity + Vector3.up * _verticalVelocity;

        //해제 속도가 남아 있는 동안에는 정상 보행으로 처리하지 않음
        bool canMove =
            _canMove == true &&
            _releaseVelocity.sqrMagnitude == 0f;		//정상 이동 여부

        Vector3 previousPosition = _ctrl.transform.position;		//이동 직전 위치

        //컨트롤러를 통해 장애물 충돌을 반영하며 이동
        _ctrl.Move( velocity * Time.deltaTime );

        Vector3 displacement =
            _ctrl.transform.position - previousPosition;		//실제 이동량

        displacement.y = 0f;

        //충돌이 반영된 실제 수평 이동 속력 전달
        if ( _animatorHandler != null )
        {
            Vector3 moveVelocity = _ctrl.velocity;		//실제 이동 속도
            moveVelocity.y = 0f;

            _animatorHandler.SetMoveSpeed( moveVelocity.magnitude );
        }

        //해제 후 수평 이동 속도 감소
        _releaseVelocity = Vector3.MoveTowards(
            _releaseVelocity, Vector3.zero, _releaseDeceleration * Time.deltaTime );

        //카메라 정면 방향으로 회전
        RotateToCamRefForward( );

        //입력 없는 외부 이동은 발소리 거리에서 제외
        float moveDistance =
            _targetDir.sqrMagnitude > 0f
                ? displacement.magnitude
                : 0f;		//정상 이동의 수평 거리

        OnMoveApplied?.Invoke( new MoveResult(
            moveDistance , _ctrl.isGrounded , canMove , _isRunning ) );
    }

    #endregion


    /// <summary>
    /// 점프 시작
    /// </summary>
    public void Jump ()
    {
        //납치당하고 있거나 컨트롤러가 비활성이면 점프 차단
        if ( _isSuspended == true ||
             _ctrl.enabled == false ||
             _ctrl.gameObject.activeInHierarchy == false )
            return;

        //지면이 아니면 종료
        if ( _ctrl.isGrounded == false )
            return;

        _verticalVelocity = _jumpPower;

        //실제 점프가 시작된 경우에만 재생
        if ( _animatorHandler != null )
            _animatorHandler.PlayJump( );

        OnJumped?.Invoke( );
    }


    #region ----- 이동 중단/해제 -----

    /// <summary>
    /// 이동 갱신 중단과 속도 초기화
    /// </summary>
    /// <param name="isSuspended">이동 갱신 중단 여부</param>
    public void SetSuspended ( bool isSuspended )
    {
        if ( _isSuspended == isSuspended )
            return;

        _isSuspended = isSuspended;

        //포획 전 점프와 낙하 속도가 해제 후 이어지지 않도록 초기화
        _verticalVelocity = 0f;

        //이전 해제의 이동 속도가 다음 포획에 남지 않도록 초기화
        _releaseVelocity = Vector3.zero;

        //해제 후 실제 입력은 Player에서 다시 전달
        _targetDir = Vector2.zero;
        _isRunning = false;

        if ( _animatorHandler != null )
            _animatorHandler.SetMoveSpeed( 0f );

        ResetMovementState( );
    }

    /// <summary>
    /// 포획 해제 후 수평 이동 속도 설정
    /// </summary>
    /// <param name="velocity">월드 기준 초기 속도</param>
    public void SetReleaseVelocity ( Vector3 velocity )
    {
        //중력과 수직 속도는 기존 이동 처리를 사용
        velocity.y = 0f;
        _releaseVelocity = velocity;

        ResetMovementState( );
    }

    /// <summary>
    /// 정상 이동 가능 상태 적용
    /// </summary>
    /// <param name="canMove">정상 이동 가능 여부</param>
    public void SetMoveAllowed ( bool canMove )
    {
        if ( _canMove == canMove )
            return;

        _canMove = canMove;
        ResetMovementState( );
    }

    /// <summary>
    /// 이동 결과를 사용하는 기능의 기록 초기화
    /// </summary>
    public void ResetMovementState ()
    {
        OnMovementReset?.Invoke( );
    }

    #endregion
}
