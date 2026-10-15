using System;
using System.Collections.Generic;

/// <summary>
/// 흔들림 변칙 사건 발생 건의 대상과 ID 및 알림 연결 정보
/// </summary>
public class ShakeOccurrence : AnomalyOccurrence<ShakeAnomalyEvent>
{
    ShakePresenter _shake;      //흔들림 기능
    int _shakeId;       //이 사건에서 시작한 흔들림 ID

    /// <summary>
    /// 조사 완료 콜백(흔들림 변칙 사건 ID)
    /// </summary>
    Action<int> _investigatedHandler;


    /// <summary>
    /// 조사 테스트 버튼 수
    /// </summary>
    public override int DebugActionCount => 1;

    /// <summary>
    /// 흔들림 기능
    /// </summary>
    public ShakePresenter Shake => _shake;


    /// <summary>
    /// 해당 발생 건의 흔들림 ID
    /// </summary>
    public int ShakeId
    {
        get => _shakeId;
        set => _shakeId = value;
    }


    /// <summary>
    /// 조사 완료 알림 연결
    /// </summary>
    public Action<int> InvestigatedHandler
    {
        get => _investigatedHandler;
        set => _investigatedHandler = value;
    }


    /// <summary>
    /// 발생 건의 대상과 사건 모델 연결
    /// </summary>
    /// <param name="target">사건 대상</param>
    /// <param name="shake">흔들림 기능</param>
    /// <param name="anomalyEvent">사건 모델</param>
    public ShakeOccurrence (
        AnomalyTarget target ,
        ShakePresenter shake ,
        ShakeAnomalyEvent anomalyEvent )
        : base( target, anomalyEvent, 0d )
    {
        _shake = shake;
    }


    /// <summary>
    /// 흔들림 상태와 발생 건의 조사 ID 표시
    /// </summary>
    /// <param name="details">상세 항목을 추가할 목록</param>
    public override void GetDebugDetails ( List<AnomalyDebugDetail> details )
    {
        details.Add( new AnomalyDebugDetail( "사후 처리 남은 시간",
            Status == AnomalyStatus.Active ? $"시작 전 ({RecoveryRemainingTime:F2}초)" : $"{RecoveryRemainingTime:F2}초" ) );
        details.Add( new AnomalyDebugDetail( "만료 처리", "흔들림 정지 후 사건 종료" ) );
        details.Add( new AnomalyDebugDetail( "흔들림", _shake != null ? _shake.IsShaking.ToString( ) : "대상 없음" ) );
        details.Add( new AnomalyDebugDetail( "사건 조사 ID", _shakeId.ToString( ) ) );
        details.Add( new AnomalyDebugDetail( "현재 조사 ID", _shake != null ? _shake.InvestigationId.ToString( ) : "대상 없음" ) );
    }

    /// <summary>
    /// 동일 흔들림의 조사 가능 여부 확인
    /// </summary>
    /// <param name="index">조회할 테스트 번호, 0은 조사</param>
    public override AnomalyDebugAction GetDebugAction ( int index )
    {
        if ( index != 0 )
            return base.GetDebugAction( index );

        string reason = Target == null || Target.isActiveAndEnabled == false ||
            _shake == null || _shake.CanInvestigate == false ||
            _shake.InvestigationId != _shakeId || Event.RequiresNormalLock == false
            ? "조사할 흔들림이 종료되었거나 ID가 달라졌습니다." : "";
        return new AnomalyDebugAction( "조사 처리", reason );
    }


    /// <summary>
    /// 발생 건의 ID로 기존 조사 요청, 입력 홀딩 검사는 생략
    /// </summary>
    /// <param name="index">실행할 테스트 번호</param>
    /// <returns>조사 성공 여부</returns>
    public override bool TryExecuteDebugAction ( int index )
    {
        return GetDebugAction( index ).DisabledReason.Length == 0 &&
            _shake.TryInvestigate( _shakeId );
    }


}
