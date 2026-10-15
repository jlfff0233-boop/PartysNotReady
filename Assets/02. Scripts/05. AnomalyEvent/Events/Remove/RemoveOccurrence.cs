using UnityEngine;

using System;
using System.Collections.Generic;

/// <summary>
/// 제거 변칙 사건 발생 건 - 존재 기능, 조사 ID, 복구 알림 보관
/// </summary>
public class RemoveOccurrence : AnomalyOccurrence<RemoveAnomalyEvent>
{

    PresenceController _presence;     //대상의 존재 기능
    int _targetId;     //제거 후 빈자리 조사 ID
    bool _restoreRequested;     //빈자리 조사로 복구를 요청했는지 여부, 복구 완료와 별개
    Action<int> _restoreRequestedHandler;     //복구 요청 콜백
    Action<int> _restoredHandler;     //정상 복구 완료 콜백

    /// <summary>
    /// 조사 또는 복구 적용 전 시간 확인 콜백
    /// </summary>
    public Action<int> ActionRequestedHandler { get; set; }

    /// <summary>
    /// 대상의 존재 기능
    /// </summary>
    public PresenceController Presence => _presence;

    /// <summary>
    /// 사건의 빈자리 조사 ID
    /// </summary>
    public int TargetId
    {
        get => _targetId;
        set => _targetId = value;
    }

    /// <summary>
    /// 정식 빈자리 조사 완료 여부
    /// </summary>
    public bool RestoreRequested
    {
        get => _restoreRequested;
        set => _restoreRequested = value;
    }

    /// <summary>
    /// 복구 요청 콜백
    /// </summary>
    public Action<int> RestoreRequestedHandler
    {
        get => _restoreRequestedHandler;
        set => _restoreRequestedHandler = value;
    }

    /// <summary>
    /// 정상 복구 완료 콜백
    /// </summary>
    public Action<int> RestoredHandler
    {
        get => _restoredHandler;
        set => _restoredHandler = value;
    }

    /// <summary>
    /// 빈자리 조사 테스트 수
    /// </summary>
    public override int DebugActionCount => 1;


    /// <summary>
    /// 대상과 존재 기능 및 사건 연결
    /// </summary>
    /// <param name="target">사건 관리 대상</param>
    /// <param name="presence">대상의 존재 기능</param>
    /// <param name="anomalyEvent">제거 사건 모델</param>
    public RemoveOccurrence (
        AnomalyTarget target, PresenceController presence, RemoveAnomalyEvent anomalyEvent )
        : base( target, anomalyEvent, Time.timeAsDouble )
    {
        _presence = presence;
    }


    /// <summary>
    /// 존재 상태와 복구 진행 정보 표시
    /// </summary>
    /// <param name="details">상세 표시 목록</param>
    public override void GetDebugDetails ( List<AnomalyDebugDetail> details )
    {
        details.Add( new AnomalyDebugDetail( "사후 처리 남은 시간",
            Status == AnomalyStatus.Active ? $"시작 전 ({RecoveryRemainingTime:F2}초)" : $"{RecoveryRemainingTime:F2}초" ) );
        details.Add( new AnomalyDebugDetail( "만료 처리", "숨김 유지, 조사와 재충전 종료" ) );
        details.Add( new AnomalyDebugDetail( "사건 조사 ID", _targetId.ToString( ) ) );

        if ( _presence == null )
        {
            details.Add( new AnomalyDebugDetail( "존재 기능", "대상 없음" ) );
            return;
        }

        details.Add( new AnomalyDebugDetail( "물건 존재", _presence.IsPresent.ToString( ) ) );
        details.Add( new AnomalyDebugDetail( "실제 표시", _presence.IsVisible.ToString( ) ) );
        details.Add( new AnomalyDebugDetail( "현재 조사 ID", _presence.InvestigationId.ToString( ) ) );
        details.Add( new AnomalyDebugDetail( "조사 가능", _presence.CanInvestigate.ToString( ) ) );
        details.Add( new AnomalyDebugDetail( "복구 진행", GetRestoreState( ) ) );
        details.Add( new AnomalyDebugDetail( "공간/표시 대기 사유", _presence.SpaceFailureReason ) );
        details.Add( new AnomalyDebugDetail( "복구 방법", "만료 전 빈자리 조사 후 재충전 완료" ) );
    }

    /// <summary>
    /// 빈자리 조사 테스트 버튼과 실행 불가 사유 제공
    /// </summary>
    /// <param name="index">기능 테스트 순서</param>
    /// <returns>버튼 표시 정보</returns>
    public override AnomalyDebugAction GetDebugAction ( int index )
    {
        return new AnomalyDebugAction(
            "빈자리 조사", CheckCanInvestigate( index ) ? string.Empty : "조사 가능한 발생 건이 아닙니다." );
    }

    /// <summary>
    /// 실제 조사 경로로 복구 요청
    /// </summary>
    /// <param name="index">실행할 기능 테스트 순서</param>
    /// <returns>조사 성공 여부</returns>
    public override bool TryExecuteDebugAction ( int index )
    {
        return CheckCanInvestigate( index ) == true && _presence.TryInvestigate( _targetId );
    }


    /// <summary>
    /// 정상 복구 진행 상태 확인
    /// </summary>
    /// <returns>디버그 표시용 진행 상태</returns>
    string GetRestoreState ()
    {
        if ( _presence.IsPresent == true )
            return "정상 물건";

        if ( _presence.IsWaitingForSpace == true )
            return "정상 복구 공간 대기";

        return _presence.IsRestoring == true ? "재충전 중" : "빈자리 조사 대기";
    }

    /// <summary>
    /// 현재 사건의 빈자리 조사 가능 여부 확인
    /// </summary>
    /// <param name="index">기능 테스트 순서</param>
    /// <returns>조사 가능 여부</returns>
    bool CheckCanInvestigate ( int index )
    {
        return index == 0 && Event.RequiresNormalLock == true &&
               Target != null && Target.isActiveAndEnabled == true && Target.NormalLocked == true &&
               _presence != null && _presence.InvestigationId == _targetId &&
               _presence.InvestigationType == PresenceInvestigationType.Restore &&
               _presence.CanInvestigate == true;
    }


}
