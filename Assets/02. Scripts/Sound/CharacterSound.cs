using UnityEngine;

/// <summary>
/// 캐릭터 사운드 - 이동 거리 기반 발소리와 기록 초기화
/// </summary>
public class CharacterSound : SoundBase
{
    [Header ( "----- 발소리 -----" )]
    [SerializeField] SoundData _walkSound;		//이동 발소리
    [SerializeField, Min ( 0.01f )] float _walkDistance = 1.5f;		//걷기 발소리 간격
    [SerializeField, Min ( 0.01f )] float _runDistance = 1f;		//달리기 발소리 간격
    [SerializeField, Min ( 0f )] float _moveThreshold = 0.001f;		//최소 수평 이동량

    float _moveDistance;		//누적 이동 거리


    /// <summary>
    /// 발소리 누적 거리 초기화
    /// </summary>
    protected void ResetFootsteps ()
    {
        _moveDistance = 0f;
    }

    /// <summary>
    /// 사운드와 이동 기록 초기화
    /// </summary>
    public override void ResetSounds ()
    {
        ResetFootsteps( );
        base.ResetSounds( );
    }


    /// <summary>
    /// 실제 이동 거리에 따른 발소리 갱신
    /// </summary>
    /// <param name="distance">이동 거리</param>
    /// <param name="isGrounded">접지 여부</param>
    /// <param name="canMove">이동 가능 여부</param>
    /// <param name="isRunning">달리기 여부</param>
    public void UpdateFootsteps (
        float distance , bool isGrounded ,
        bool canMove , bool isRunning )
    {
        if ( isActiveAndEnabled == false )
            return;

        if ( isGrounded == false ||
             canMove == false ||
             distance <= _moveThreshold )
        {
            ResetFootsteps ( );
            return;
        }

        _moveDistance += distance;

        float stepDistance = Mathf.Max (
            0.01f ,
            isRunning == true ? _runDistance : _walkDistance );		//발소리 간격

        if ( _moveDistance < stepDistance )
            return;

        //긴 프레임에서도 한 번만 재생하고 초과 거리 유지
        _moveDistance %= stepDistance;

        PlaySound ( _walkSound );
    }

    /// <summary>
    /// 지상 이동에 따른 발소리 갱신
    /// </summary>
    /// <param name="mover">지상 이동 모듈</param>
    /// <param name="deltaTime">경과 시간</param>
    /// <param name="canMove">이동음 허용 여부</param>
    public void UpdateMovement (
        CharacterMover mover, float deltaTime, bool canMove )
    {
        if ( canMove == false ||
             mover.CanNavigate == false ||
             deltaTime <= 0f )
        {
            ResetFootsteps( );
            return;
        }

        Vector3 velocity = mover.Velocity;       //실제 이동 속도
        velocity.y = 0f;

        //수직 이동을 제외한 실제 이동 거리 적용
        float distance = velocity.magnitude * deltaTime;
        UpdateFootsteps( distance, true, true, false );
    }

}
