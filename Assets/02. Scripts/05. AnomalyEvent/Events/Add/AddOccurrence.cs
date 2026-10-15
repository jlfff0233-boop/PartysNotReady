using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아이템 추가 변칙 사건 발생 건 - 등장 기능, 조사 ID, 제거 알림 보관
/// </summary>
public class AddOccurrence : AnomalyOccurrence<AddAnomalyEvent>
{

    PresenceController _presence;        //등장 기능
    int _targetId;      //사건 발생 시 저장한 조사 ID
    /// <summary>
    /// 사건 제거 콜백(사건 ID)
    /// </summary>
    Action<int> _investigatedHandler;

    /// <summary>
    /// 조사 또는 복구 적용 전 시간 확인 콜백
    /// </summary>
    public Action<int> ActionRequestedHandler { get; set; }

    /// <summary>
    /// 대상의 등장 기능
    /// </summary>
    public PresenceController Presence => _presence;

    /// <summary>
    /// 사건 발생 시 저장한 조사 ID
    /// </summary>
    public int TargetId
    {
        get => _targetId;
        set => _targetId = value;
    }

    /// <summary>
    /// 조사 제거 완료 콜백
    /// </summary>
    public Action<int> InvestigatedHandler
    {
        get => _investigatedHandler;
        set => _investigatedHandler = value;
    }

    /// <summary>
    /// 대상과 등장 기능 및 사건 연결
    /// </summary>
    /// <param name="target">사건 관리 대상</param>
    /// <param name="presence">등장 기능</param>
    /// <param name="anomalyEvent">추가 사건 모델</param>
    public AddOccurrence (
        AnomalyTarget target,
        PresenceController presence,
        AddAnomalyEvent anomalyEvent )
        : base( target, anomalyEvent, Time.timeAsDouble )
    {
        _presence = presence;
    }

    /// <summary>
    /// 존재 상태와 조사 ID 표시
    /// </summary>
    /// <param name="details">상세 표시 목록</param>
    public override void GetDebugDetails (
        List<AnomalyDebugDetail> details )
    {
        details.Add( new AnomalyDebugDetail( "사후 처리 남은 시간",
            Status == AnomalyStatus.Active ? $"시작 전 ({RecoveryRemainingTime:F2}초)" : $"{RecoveryRemainingTime:F2}초" ) );
        details.Add( new AnomalyDebugDetail( "만료 처리", "표시 유지, 조사 종료" ) );
        details.Add( new AnomalyDebugDetail(
            "물건 존재",
            _presence != null ? _presence.IsPresent.ToString( ) : "대상 없음" ) );

        details.Add( new AnomalyDebugDetail(
            "실제 표시",
            _presence != null ? _presence.IsVisible.ToString( ) : "대상 없음" ) );

        details.Add( new AnomalyDebugDetail(
            "사건 조사 ID", _targetId.ToString( ) ) );

        details.Add( new AnomalyDebugDetail(
            "복구 방법", "만료 전 E 홀딩 조사로 제거" ) );
        if ( _presence != null )
        {
            details.Add( new AnomalyDebugDetail( "현재 조사 ID", _presence.InvestigationId.ToString( ) ) );
            details.Add( new AnomalyDebugDetail( "조사 가능", _presence.CanInvestigate.ToString( ) ) );
            details.Add( new AnomalyDebugDetail( "공간/표시 대기 사유", _presence.SpaceFailureReason ) );
        }
    }
}
