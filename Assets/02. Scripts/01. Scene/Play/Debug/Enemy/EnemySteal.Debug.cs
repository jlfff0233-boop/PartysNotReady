#if UNITY_EDITOR
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 절도 적 디버그 - 행동과 보유 상태 조회
/// </summary>
public partial class EnemySteal
{
    /// <summary>
    /// 절도 활동 상세 조회
    /// </summary>
    /// <param name="details">표시 항목</param>
    public override void GetDebugDetails ( 
        List<AnomalyDebugDetail> details )
    {
        base.GetDebugDetails( details );

        details.Add( new( "개별 음식 목표", EnemyDebugUtility.Name( _target ) ) );
        details.Add( new( "공급원 목표", EnemyDebugUtility.Name( _targetSource ) ) );
        details.Add( new( "실제 보유 음식",
            $"{EnemyDebugUtility.Name( _heldItem )} / 보유 확인 {HasHeldFood( )}" ) );
        if ( _targetSource != null )
            details.Add( new( "목표 공급원 재고", _targetSource.GetStockStatus( ) ) );

        details.Add( new( "하강 / 종료 중", $"{_isDescending} / {_isEnding}" ) );

        if ( _model != null )
            details.Add( new( "탐색 만료 / 재시도 사용",
                $"{_model.IsSearchExpired} / {_model.HasRetried}" ) );
    }
}
#endif
