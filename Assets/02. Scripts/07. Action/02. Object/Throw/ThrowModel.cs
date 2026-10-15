using UnityEngine;

/// <summary>
/// 투척 모델 - 진행 상태, 지면 감속과 정지 조건 관리
/// </summary>
public class ThrowModel
{
    readonly float _groundDeceleration;     //지면 이동 감속량
    readonly float _angularDeceleration;     //지면 회전 감속량
    readonly float _stopSpeed;     //이동 정지 기준
    readonly float _stopAngularSpeed;     //회전 정지 기준
    readonly float _stopDelay;     //정지 조건 유지 시간
    bool _isThrown;     //던져졌는지 여부
    bool _isGrounded;     //지면 접촉 여부
    float _stopTimer;     //정지 조건 유지 시간

    /// <summary>
    /// 투척 진행 여부
    /// </summary>
    public bool IsThrown => _isThrown;
    /// <summary>
    /// 현재 물리 단계의 지면 접촉 여부
    /// </summary>
    public bool IsGrounded => _isGrounded;

    /// <summary>
    /// 투척 감속, 정지 기준 초기화
    /// </summary>
    /// <param name="groundDeceleration">지면 이동 감속량</param>
    /// <param name="angularDeceleration">지면 회전 감속량</param>
    /// <param name="stopSpeed">이동 정지 기준</param>
    /// <param name="stopAngularSpeed">회전 정지 기준</param>
    /// <param name="stopDelay">정지 조건 유지 시간</param>
    public ThrowModel (
        float groundDeceleration, float angularDeceleration,
        float stopSpeed, float stopAngularSpeed, float stopDelay )
    {
        _groundDeceleration = groundDeceleration;
        _angularDeceleration = angularDeceleration;
        _stopSpeed = stopSpeed;
        _stopAngularSpeed = stopAngularSpeed;
        _stopDelay = stopDelay;
    }


    /// <summary>
    /// 투척 상태 초기화
    /// </summary>
    public void ResetThrowState ()
    {
        _isThrown = false;
        _isGrounded = false;
        _stopTimer = 0f;
    }

    /// <summary>
    /// 투척 시작
    /// </summary>
    public void BeginThrow ()
    {
        ResetThrowState( );
        _isThrown = true;
    }

    /// <summary>
    /// 접촉면 기준 지면 판정
    /// </summary>
    /// <param name="normal">월드 기준 접촉면의 단위 법선</param>
    public void RecordContact ( Vector3 normal )
    {
        if ( _isThrown == false ) return;

        //벽과 구분하기 위해 위쪽을 향하는 접촉면만 지면으로 판정
        if ( Vector3.Dot( normal, Vector3.up ) >= 0.5f )
            _isGrounded = true;
    }


    /// <summary>
    /// 지면에서 이동 / 회전 감속 후 정지 여부 판정
    /// </summary>
    /// <param name="velocity">현재 이동 속도, 계산 후 이동 속도</param>
    /// <param name="angularVelocity">현재 회전 속도, 계산 후 회전 속도</param>
    /// <param name="deltaTime">물리 갱신 간격</param>
    /// <param name="isSleeping">물리 수면 상태</param>
    /// <returns>완전 정지 적용 여부</returns>
    public bool UpdateDeceleration (
        ref Vector3 velocity, ref Vector3 angularVelocity,
        float deltaTime, bool isSleeping )
    {
        if ( _isThrown == false ) return false;

        //수면 중에는 속도와 정지 시간을 갱신하지 않음
        if ( isSleeping == true )
        {
            _isGrounded = false;
            return false;
        }

        bool isGrounded = _isGrounded;
        //이번 갱신에서 접촉 기록을 소비하고 다음 접촉 콜백에서 다시 기록
        _isGrounded = false;

        if ( isGrounded == false )
        {
            //공중에서는 속도를 건드리지 않으며 연속 정지 시간도 누적하지 않음
            _stopTimer = 0f;
            return false;
        }

        velocity = Vector3.MoveTowards(
            velocity, Vector3.zero, _groundDeceleration * deltaTime );
        angularVelocity = Vector3.MoveTowards(
            angularVelocity, Vector3.zero, _angularDeceleration * deltaTime );

        //이동과 회전이 모두 정지 기준 이하일 때만 시간 누적
        if ( velocity.sqrMagnitude > _stopSpeed * _stopSpeed ||
            angularVelocity.sqrMagnitude > _stopAngularSpeed * _stopAngularSpeed )
        {
            _stopTimer = 0f;
            return false;
        }

        _stopTimer += deltaTime;
        if ( _stopTimer < _stopDelay ) return false;

        //기준 이하의 속도가 설정 시간만큼 유지된 경우에만 완전 정지 요청
        velocity = Vector3.zero;
        angularVelocity = Vector3.zero;
        _stopTimer = 0f;
        return true;
    }


}
