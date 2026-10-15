using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이동 변칙 사건 발생 건 - 이동 위치와 배치 알림 연결 정보 보관
/// </summary>
public class DisplaceOccurrence : AnomalyOccurrence<DisplaceAnomalyEvent>
{
    ItemCarry _item;        //대상 아이템
    Vector3 _originalPosition;      //사건 발생 전 위치
    Vector3 _movedPosition;     //사건으로 이동한 직후 위치
    Action<PlacementTarget> _placementHandler;     //배치 알림 구독/해제에 사용할 콜백

    /// <summary>
    /// 이동과 보유 상태를 확인할 아이템
    /// </summary>
    public ItemCarry Item => _item;

    /// <summary>
    /// 이동 적용 직후 위치
    /// </summary>
    public Vector3 MovedPosition
    {
        get => _movedPosition;
        set => _movedPosition = value;
    }

    /// <summary>
    /// 배치 완료 알림 연결
    /// </summary>
    public Action<PlacementTarget> PlacementHandler
    {
        get => _placementHandler;
        set => _placementHandler = value;
    }

    /// <summary>
    /// 이동 변칙 사건 발생 건의 대상과 사건 정보 초기화
    /// </summary>
    /// <param name="target">사건 대상</param>
    /// <param name="item">운반 기능</param>
    /// <param name="anomalyEvent">사건 모델</param>
    public DisplaceOccurrence (
        AnomalyTarget target , ItemCarry item ,
        DisplaceAnomalyEvent anomalyEvent )
        : base ( target , anomalyEvent , Time.timeAsDouble )
    {
        _item = item;
        _originalPosition = item.transform.position;
        _movedPosition = _originalPosition;
    }

    /// <summary>
    /// 이동 위치와 현재 보유 상태 및 복구 방법 표시
    /// </summary>
    /// <param name="details">상세 표시 목록</param>
    public override void GetDebugDetails ( List<AnomalyDebugDetail> details )
    {
        details.Add ( new AnomalyDebugDetail (
            "발생 전 위치" , _originalPosition.ToString ( "F2" ) ) );

        details.Add ( new AnomalyDebugDetail (
            "이동 직후 위치" , _movedPosition.ToString ( "F2" ) ) );

        details.Add ( new AnomalyDebugDetail (
            "보유 상태" ,
            _item != null ? _item.CurrentType.ToString ( ) : "대상 없음" ) );

        details.Add ( new AnomalyDebugDetail (
            "복구 방법" , "집어서 허용된 표면에 다시 배치" ) );
    }
}
