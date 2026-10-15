using System;
using System.Collections.Generic;

/// <summary>
/// 낙하 변칙 사건 발생 건별 대상과 알림 연결 정보
/// </summary>
public class DropOccurrence : AnomalyOccurrence<ItemDropEvent>
{
    ItemCarry _item;        //고정 해제와 보유 상태 확인

    Action<PlacementTarget> _placementHandler;     //배치 알림 구독/해제에 사용할 콜백


    /// <summary>
    /// 고정 해제와 보유 상태를 확인할 아이템
    /// </summary>
    public ItemCarry Item => _item;


    /// <summary>
    /// 배치 완료 알림 연결
    /// </summary>
    public Action<PlacementTarget> PlacementHandler
    {
        get => _placementHandler;
        set => _placementHandler = value;
    }


    /// <summary>
    /// 낙하 변칙 사건 발생 건의 대상과 사건 정보 초기화
    /// </summary>
    /// <param name="target">사건 대상</param>
    /// <param name="item">고정 해제와 보유 상태를 확인할 아이템</param>
    /// <param name="dropEvent">사건 진행 모델</param>
    /// <param name="lastTickTime">시간 갱신 시작 시점</param>
    public DropOccurrence (
        AnomalyTarget target, ItemCarry item,
        ItemDropEvent dropEvent, double lastTickTime )
        : base( target, dropEvent, lastTickTime )
    {
        _item = item;
    }

    /// <summary>
    /// 보유 상태와 정상 배치 대상 표시
    /// </summary>
    /// <param name="details">사건 상세 정보를 추가할 목록</param>
    public override void GetDebugDetails ( List<AnomalyDebugDetail> details )
    {
        details.Add( new AnomalyDebugDetail( "사후 처리 남은 시간",
            Status == AnomalyStatus.Active ? $"시작 전 ({RecoveryRemainingTime:F2}초)" : $"{RecoveryRemainingTime:F2}초" ) );
        details.Add( new AnomalyDebugDetail( "만료 처리", "현재 위치와 물리 유지, 낙하 전 배치 기준 유지" ) );
        details.Add( new AnomalyDebugDetail( "보유 상태", _item != null ? _item.CurrentType.ToString( ) : "대상 없음" ) );
        PlacementTarget placement = Target != null && Target.NormalState != null
            ? Target.NormalState.PlacementTarget : null;
        details.Add( new AnomalyDebugDetail( "정상 배치 대상", placement != null ? placement.name : "없음" ) );
        details.Add( new AnomalyDebugDetail( "복구 방법", "플레이어가 허용된 대상에 직접 배치" ) );
    }
}
