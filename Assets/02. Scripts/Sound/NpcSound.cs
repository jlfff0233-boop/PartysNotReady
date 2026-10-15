using UnityEngine;

/// <summary>
/// NPC 사운드 - 실제 이동에 따른 발소리와 포획 반복음 갱신
/// </summary>
public class NpcSound : CharacterSound
{
    [Header( "----- 포획 -----" )]
    [SerializeField] SoundData _capturedSound;       //포획 중 버둥거림 반복음

    Npc _npc;		//행동과 안전 상태
    NpcMover _mover;		//실제 이동과 낙하 상태

    /// <summary>
    /// NPC와 이동 및 사운드 재생 모듈 연결
    /// </summary>
    /// <param name="npc">사운드 대상 NPC</param>
    /// <param name="mover">NPC 이동 모듈</param>
    /// <param name="soundPlayer">NPC 전용 사운드 재생 모듈</param>
    public void Init (
        Npc npc, NpcMover mover, SoundPlayer soundPlayer )
    {
        base.Init( soundPlayer );
        _npc = npc;
        _mover = mover;
    }

    /// <summary>
    /// 재활성화 시 이전 재생과 이동 기록 정리
    /// </summary>
    void OnEnable ()
    {
        ResetSounds( );
    }

    /// <summary>
    /// 실제 이동과 포획 상태에 따른 사운드 갱신
    /// </summary>
    void LateUpdate ()
    {
        //초기화 전 또는 연결 대상 파괴 후에는 갱신하지 않음
        if ( _npc == null || _mover == null )
            return;

        //NPC 컴포넌트만 비활성화된 경우에도 반복음과 이동 기록 정리
        if ( _npc.isActiveAndEnabled == false )
        {
            SetCaptured( false );
            ResetFootsteps( );
            return;
        }

        bool isCaptured =
            _npc.Status == NpcSafeStatusType.Captured;		//포획 상태

        SetCaptured( isCaptured );

        //포획 따라가기, 낙하와 일반 행동 중단은 발소리에서 제외
        bool canMove =
            _npc.CurrentState == NpcStateType.Normal &&
            _npc.Status == NpcSafeStatusType.Normal &&
            _mover.IsFalling == false &&
            _mover.CanNavigate == true;		//일반 지상 이동 여부

        float distance = 0f;		//이번 프레임 수평 이동 거리

        if ( canMove == true && Time.deltaTime > 0f )
        {
            Vector3 velocity = _mover.Velocity;		//실제 이동 속도
            velocity.y = 0f;
            distance = velocity.magnitude * Time.deltaTime;
        }

        UpdateFootsteps(
            distance, canMove, canMove, false );
    }


    /// <summary>
    /// 포획 반복음 재생 상태 적용
    /// </summary>
    /// <param name="isCaptured">포획 여부</param>
    public void SetCaptured ( bool isCaptured )
    {
        if ( isCaptured == true )
        {
            //포획에 따른 이동은 발소리 거리에서 제외
            ResetFootsteps( );
            StartLoop( _capturedSound );
        }
        else
        {
            StopLoop( );
        }
    }


}
