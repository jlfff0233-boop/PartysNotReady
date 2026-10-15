using UnityEngine;

/// <summary>
/// NPC 행동 상태
/// </summary>
public abstract class NpcState
{
    /// <summary>
    /// 행동 상태 종류
    /// </summary>
    public abstract NpcStateType StateType { get; }

    /// <summary>
    /// 상태 진입
    /// </summary>
    public abstract void Enter ( );

    /// <summary>
    /// 상태 갱신
    /// </summary>
    public abstract void Update ( );

    /// <summary>
    /// 상태 종료
    /// </summary>
    public abstract void Exit ( );
}

/// <summary>
/// NPC 일반 상태 - 일반 행동 모듈 연결과 상위 FSM 전환
/// </summary>
public class NpcNormalState : NpcState
{
    Npc _npc;		//일반 행동 대상

    /// <summary>
    /// 행동 상태 종류
    /// </summary>
    public override NpcStateType StateType => NpcStateType.Normal;

    /// <summary>
    /// 일반 행동 대상 연결
    /// </summary>
    /// <param name="npc">행동 대상</param>
    public NpcNormalState ( Npc npc )
    {
        _npc = npc;
    }

    /// <summary>
    /// 상태 진입
    /// </summary>
    public override void Enter ( )
    {
        _npc.BeginNormal ( );
    }

    /// <summary>
    /// 상태 갱신
    /// </summary>
    public override void Update ( )
    {
        _npc.UpdateNormal ( Time.deltaTime );
    }

    /// <summary>
    /// 상태 종료
    /// </summary>
    public override void Exit ( )
    {
        _npc.EndNormal ( );
    }
}

/// <summary>
/// NPC 음악 반응 행동 - 음악 및 애니메이션 연결 전 상태 골격
/// </summary>
public class NpcExcitedState : NpcState
{
    /// <summary>
    /// 행동 상태 종류
    /// </summary>
    public override NpcStateType StateType => NpcStateType.Excited;

    /// <summary>
    /// 상태 진입
    /// </summary>
    public override void Enter ( )
    {
        //일반 이동 정리는 이전 상태의 Exit에서 처리
    }

    /// <summary>
    /// 상태 갱신
    /// </summary>
    public override void Update ( )
    {
        //음악 반응은 후속 단계에서 연결
    }

    /// <summary>
    /// 상태 종료
    /// </summary>
    public override void Exit ( )
    {
        //음악 반응 종료 처리는 후속 단계에서 연결
    }
}

/// <summary>
/// NPC 포획 중 행동
/// </summary>
public class NpcCapturedState : NpcState
{
    /// <summary>
    /// 행동 상태 종류
    /// </summary>
    public override NpcStateType StateType => NpcStateType.Captured;

    /// <summary>
    /// 상태 진입
    /// </summary>
    public override void Enter ( )
    {
        //포획 관계와 따라가기 시작은 Npc의 포획 수락 과정에서 처리
    }

    /// <summary>
    /// 상태 갱신
    /// </summary>
    public override void Update ( )
    {
        //보유 지점 따라가기는 NpcMover에서 처리
    }

    /// <summary>
    /// 상태 종료
    /// </summary>
    public override void Exit ( )
    {
        //포획 관계 해제와 낙하 및 게임 이탈은 기존 Npc 처리 유지
    }
}
