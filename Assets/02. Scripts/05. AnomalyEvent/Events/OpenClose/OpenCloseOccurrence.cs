using System;
using System.Collections.Generic;

/// <summary>
/// 개폐 변칙 사건 발생 건 - 개폐 대상과 알림 연결 정보 보관
/// </summary>
public class OpenCloseOccurrence : AnomalyOccurrence<OpenCloseAnomalyEvent>
{
    OpenCloseController _openClose;      //개폐 요청과 현재 상태 확인

    /// <summary>
    /// 개폐 완료 콜백(열림 여부)
    /// </summary>
    Action<bool> _openHandler;


    /// <summary>
    /// 개폐 테스트 버튼 수
    /// </summary>
    public override int DebugActionCount => 3;

    /// <summary>
    /// 대상의 개폐 중재자
    /// </summary>
    public OpenCloseController OpenClose => _openClose;


    /// <summary>
    /// 개폐 완료 알림 연결
    /// </summary>
    public Action<bool> OpenHandler
    {
        get => _openHandler;
        set => _openHandler = value;
    }


    /// <summary>
    /// 개폐 변칙 사건의 대상과 알림 정보 초기화
    /// </summary>
    /// <param name="target">사건 대상</param>
    /// <param name="openClose">대상의 개폐 중재자</param>
    /// <param name="stateChangeEvent">사건 진행 모델</param>
    /// <param name="lastTickTime">시간 갱신 시작 시점</param>
    public OpenCloseOccurrence (
        AnomalyTarget target,
        OpenCloseController openClose,
        OpenCloseAnomalyEvent stateChangeEvent,
        double lastTickTime )
        : base( target, stateChangeEvent, lastTickTime )
    {
        _openClose = openClose;
    }

    /// <summary>
    /// 현재 개폐와 복구 기준 표시
    /// </summary>
    /// <param name="details">사건 상세 정보를 추가할 목록</param>
    public override void GetDebugDetails ( List<AnomalyDebugDetail> details )
    {
        details.Add( new AnomalyDebugDetail( "사후 처리 남은 시간",
            Status == AnomalyStatus.Active ? $"시작 전 ({RecoveryRemainingTime:F2}초)" : $"{RecoveryRemainingTime:F2}초" ) );
        details.Add( new AnomalyDebugDetail( "만료 처리", "현재 개폐 상태 유지 후 사건 종료" ) );
        details.Add( new AnomalyDebugDetail( "현재 열림", _openClose != null ? _openClose.IsOpen.ToString( ) : "대상 없음" ) );
        details.Add( new AnomalyDebugDetail( "복구할 열림", Event.IsOpenNormal.ToString( ) ) );
    }

    /// <summary>
    /// 개폐 테스트의 실행 조건 확인
    /// </summary>
    /// <param name="index">열기 0, 닫기 1, 전환 2</param>
    public override AnomalyDebugAction GetDebugAction ( int index )
    {
        string label = index == 0 ? "열기" : index == 1 ? "닫기" : "전환";
        string reason = Target == null || Target.isActiveAndEnabled == false ||
            _openClose == null || _openClose.CanInteract == false || Event.RequiresNormalLock == false
            ? "활성 사건의 개폐 기능을 사용할 수 없습니다." : "";

        if ( reason.Length == 0 && index < 2 && _openClose.IsOpen == ( index == 0 ) )
            reason = "이미 요청한 상태입니다.";

        return index >= 0 && index < DebugActionCount
            ? new AnomalyDebugAction( label, reason ) : base.GetDebugAction( index );
    }

    /// <summary>
    /// 선택한 열기/닫기/전환 동작 실행
    /// </summary>
    /// <param name="index">열기 0, 닫기 1, 전환 2</param>
    public override bool TryExecuteDebugAction ( int index )
    {
        if ( GetDebugAction( index ).DisabledReason.Length > 0 )
            return false;

        return _openClose.TrySetOpen( index == 2 ? !_openClose.IsOpen : index == 0 );
    }
}
