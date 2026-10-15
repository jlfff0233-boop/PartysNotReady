#if UNITY_EDITOR
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 방해 적 디버그 - 행동 상태 조회
/// </summary>
public partial class EnemyBothersome
{
    /// <summary>
    /// 공놀이 진행 상태 조회
    /// </summary>
    /// <param name="details">표시 항목</param>
    public override void GetDebugDetails ( List<AnomalyDebugDetail> details )
    {
        base.GetDebugDetails( details );
        details.Add( new( "플레이어 / 공", $"{EnemyDebugUtility.Name( _player )} / {EnemyDebugUtility.Name( _ball )}" ) );
        if ( _playModel != null )
            details.Add( new( "놀이 횟수", $"{CompletedCount} / {_data.ReqCount}" ) );
        if ( _ball != null )
            details.Add( new( "공 보유", HasBall.ToString( ) ) );
    }
}
#endif
