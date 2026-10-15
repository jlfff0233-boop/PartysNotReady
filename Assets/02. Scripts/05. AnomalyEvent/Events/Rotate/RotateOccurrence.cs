using System;
using System.Collections.Generic;

/// <summary>
/// 회전 변칙 사건 발생 건 - 회전 대상과 알림 연결 정보 보관
/// </summary>
public class RotateOccurrence : AnomalyOccurrence<RotateAnomalyEvent>
{
    RotatePresenter _rotate;        //회전 요청과 현재 상태 확인

    Action<float> _rotateHandler;     //회전 알림 구독/해제에 사용할 콜백


    /// <summary>
    /// 회전 테스트 버튼 수
    /// </summary>
    public override int DebugActionCount => 1;

    /// <summary>
    /// 사건을 적용한 회전 기능
    /// </summary>
    public RotatePresenter Rotate => _rotate;


    /// <summary>
    /// 회전 완료 알림 연결
    /// </summary>
    public Action<float> RotateHandler
    {
        get => _rotateHandler;
        set => _rotateHandler = value;
    }


    /// <summary>
    /// 사건 대상 및 정보 초기화
    /// </summary>
    /// <param name="target">사건 대상</param>
    /// <param name="rotate">사건을 적용할 회전 기능</param>
    /// <param name="stateChangeEvent">사건 진행 모델</param>
    /// <param name="lastTickTime">시간 갱신 시작 시점</param>
    public RotateOccurrence (
        AnomalyTarget target,
        RotatePresenter rotate,
        RotateAnomalyEvent stateChangeEvent,
        double lastTickTime )
        : base( target, stateChangeEvent, lastTickTime )
    {
        _rotate = rotate;
    }

    /// <summary>
    /// 현재 각도와 복구 기준 표시
    /// </summary>
    /// <param name="details">사건 상세 정보를 추가할 목록</param>
    public override void GetDebugDetails ( List<AnomalyDebugDetail> details )
    {
        details.Add( new AnomalyDebugDetail( "사후 처리 남은 시간",
            Status == AnomalyStatus.Active ? $"시작 전 ({RecoveryRemainingTime:F2}초)" : $"{RecoveryRemainingTime:F2}초" ) );
        details.Add( new AnomalyDebugDetail( "만료 처리", "현재 기울기 유지, 사건 종료" ) );
        details.Add( new AnomalyDebugDetail( "현재 각도", _rotate != null ? _rotate.Angle.ToString( "0.##" ) : "대상 없음" ) );
        details.Add( new AnomalyDebugDetail( "해결 기준 각도", "0도 (초기 회전, 다음 사건에도 유지)" ) );
    }

    /// <summary>
    /// 랜덤 회전 테스트의 실행 조건 확인
    /// </summary>
    /// <param name="index">조회할 디버그 동작 번호</param>
    public override AnomalyDebugAction GetDebugAction ( int index )
    {
        if ( index != 0 )
            return base.GetDebugAction( index );

        string reason = Target == null || Target.isActiveAndEnabled == false ||
            _rotate == null || _rotate.CanInteract == false || Event.RequiresNormalLock == false
            ? "활성 사건의 회전 기능을 사용할 수 없습니다." : "";
        return new AnomalyDebugAction( "랜덤 회전", reason );
    }

    /// <summary>
    /// 사용 가능한 랜덤 회전 동작 실행
    /// </summary>
    /// <param name="index">실행할 디버그 동작 번호</param>
    public override bool TryExecuteDebugAction ( int index )
    {
        return GetDebugAction( index ).DisabledReason.Length == 0 && _rotate.TryRotate( );
    }
}
