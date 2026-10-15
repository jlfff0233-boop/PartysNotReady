/// <summary>
/// 교체 변칙 사건 모델 - 대상별 배치 완료와 해결 진행 관리
/// </summary>
public class SwapAnomalyEvent : AnomalyEvent
{
    public const int TargetCount = 2;     //교체에 참여하는 대상 수

    bool [ ] _completed = new bool [ TargetCount ];     //대상별 완료 기록
    int _completedCount;     //원래 배치 대상으로 돌아간 대상 수

    /// <summary>
    /// 배치를 완료한 대상 수
    /// </summary>
    public int CompletedCount => _completedCount;

    /// <summary>
    /// 두 대상의 배치 완료 여부
    /// </summary>
    public bool IsCompleted => _completedCount == TargetCount;

    /// <summary>
    /// 교체 변칙 사건의 시간 설정 저장
    /// </summary>
    /// <param name="times">해결 시간과 사후 처리 시간</param>
    public SwapAnomalyEvent ( AnomalyEventTimes times ) : base( times )
    {
    }


    /// <summary>
    /// 원래 배치 대상으로 돌아간 대상을 기록하고 전체 해결 판정
    /// </summary>
    /// <param name="targetIndex">배치한 대상 번호, 첫 번째는 0, 두 번째는 1</param>
    /// <param name="isOriginalPlacement">발생 당시 원래 배치 대상과 일치하는지 여부</param>
    /// <param name="isHeld">현재 보유 여부</param>
    /// <returns>해당 대상의 배치를 새로 인정하면 true, 전체 사건 완료 여부와는 별개</returns>
    public bool TryPlace (
        int targetIndex, bool isOriginalPlacement, bool isHeld )
    {
        //사후 복구 시간이 끝났다면
        //이번 배치로 완료 기록을 늘리지 않음
        if ( TryExpire( ) == true )
            return false;

        if ( CheckPlacement(
            targetIndex, isOriginalPlacement, isHeld ) == false )
            return false;

        //종료 알림에서 진행도를 조회할 수 있도록 먼저 기록
        //한 번 완료한 대상은 다시 집거나 옮겨도 완료 유지
        _completed [ targetIndex ] = true;
        _completedCount++;

        //1/2에서는 사건 상태와 타이머를 유지
        //2/2에서만 공통 모델이 Resolved 또는 Recovered로 전환
        if ( IsCompleted )
            TryComplete( true );

        return true;
    }


    /// <summary>
    /// 지정 대상의 배치 완료 여부 조회
    /// </summary>
    /// <param name="targetIndex">대상 번호, 첫 번째는 0, 두 번째는 1</param>
    /// <returns>유효한 대상이며 배치를 완료했는지 여부</returns>
    public bool IsTargetCompleted ( int targetIndex )
    {
        if ( CheckTargetIndex( targetIndex ) == false )
            return false;

        return _completed [ targetIndex ];
    }

    /// <summary>
    /// 교체 대상 번호의 유효성 확인
    /// </summary>
    /// <param name="targetIndex">검사할 대상 번호</param>
    /// <returns>두 대상 중 하나를 가리키는지 여부</returns>
    bool CheckTargetIndex ( int targetIndex )
    {
        return targetIndex >= 0 && targetIndex < TargetCount;
    }

    /// <summary>
    /// 배치 결과를 새로운 대상 완료로 인정할 수 있는지 확인
    /// </summary>
    /// <param name="targetIndex">배치한 대상 번호</param>
    /// <param name="isOriginalPlacement">원래 배치 대상과 일치하는지 여부</param>
    /// <param name="isHeld">현재 보유 여부</param>
    /// <returns>배치 완료 인정 가능 여부</returns>
    bool CheckPlacement (
        int targetIndex, bool isOriginalPlacement, bool isHeld )
    {
        if ( CheckTargetIndex( targetIndex ) == false )
            return false;

        //준비 상태와 이미 종료된 사건에서는 완료 기록을 변경하지 않음
        if ( Status != AnomalyStatus.Active &&
             Status != AnomalyStatus.Failed )
            return false;

        //만료 종료가 보류된 경우에도 수동 완료는 받지 않음
        if ( RequiresAutomaticRecovery )
            return false;

        if ( isOriginalPlacement == false || isHeld )
            return false;

        //같은 대상의 반복 배치로 진행도가 증가하지 않도록 차단
        return _completed [ targetIndex ] == false;
    }

}
