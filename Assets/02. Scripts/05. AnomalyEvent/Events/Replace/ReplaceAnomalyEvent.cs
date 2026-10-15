
/// <summary>
/// 대체 변칙 사건 모델 - 발생 조건과 조사 결과 판정
/// </summary>
public class ReplaceAnomalyEvent : AnomalyEvent
{
    /// <summary>
    /// 대체 변칙 사건의 해결 시간과 사후 복구 시간 설정
    /// </summary>
    /// <param name="times">해결 시간과 사후 처리 시간</param>
    public ReplaceAnomalyEvent ( AnomalyEventTimes times ) : base( times )
    {
    }


    /// <summary>
    /// 해결 또는 사후 복구 처리
    /// </summary>
    /// <param name="targetId">사건 발생 당시 저장한 조사 ID</param>
    /// <param name="investigatedId">복원 완료 알림의 조사 ID</param>
    /// <param name="isChanged">복원 후에도 대체 상태가 남아 있는지 여부</param>
    /// <returns>해결 또는 사후 복구 성공 여부</returns>
    public bool TryRestore (
        int targetId, int investigatedId, bool isChanged )
    {
        //색 변경과 외형 변경 모두
        //같은 조사 ID의 복원이 끝났을 때만 완료로 인정
        if ( targetId != investigatedId || isChanged == true )
            return false;

        //시간은 조사 전 콜백에서 갱신
        //이미 만료됐다면 복원 결과를 인정하지 않음
        if ( TryExpire( ) == true )
            return false;

        return TryComplete( true );
    }

    /// <summary>
    /// 대체 변칙 사건 시작 가능 여부 확인
    /// </summary>
    /// <param name="isReady">현재 시도 가능한 대체 후보 존재 여부</param>
    /// <param name="isChanged">진행 중인 대체 여부</param>
    /// <param name="isHeld">현재 보유 여부</param>
    /// <param name="normalLocked">다른 사건의 정상 기준 잠금 여부</param>
    /// <param name="times">발생 건의 시간 설정</param>
    /// <returns>사건 발생을 시도할 수 있는지 여부</returns>
    public static bool CanStart (
        bool isReady, bool isChanged,
        bool isHeld, bool normalLocked, AnomalyEventTimes times )
    {
        //후보 선택과 실제 적용 가능 여부는 각 기능에서 검사
        return isReady == true &&
               isChanged == false &&
               isHeld == false &&
               normalLocked == false &&
               times.CheckValid( out _ ) == true;
    }

}
