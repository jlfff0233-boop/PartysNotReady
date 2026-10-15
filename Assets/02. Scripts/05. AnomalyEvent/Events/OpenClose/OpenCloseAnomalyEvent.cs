
/// <summary>
/// 개폐 변칙 사건 모델 - 발생 직전 개폐 상태 복구 판정
/// </summary>
public class OpenCloseAnomalyEvent : AnomalyEvent
{
    bool _isOpenNormal;     //사건 발생 당시 정상 열림 여부

    /// <summary>
    /// 발생 당시 저장한 정상 열림 여부
    /// </summary>
    public bool IsOpenNormal => _isOpenNormal;

    /// <summary>
    /// 복구할 개폐 상태와 사건 시간 설정
    /// </summary>
    /// <param name="isOpenNormal">사건 발생 당시 정상 열림 여부</param>
    /// <param name="times">해결 시간과 사후 복구 시간</param>
    public OpenCloseAnomalyEvent (
        bool isOpenNormal, AnomalyEventTimes times ) : base( times )
    {
        _isOpenNormal = isOpenNormal;
    }

    /// <summary>
    /// 개폐 완료 상태를 비교해 해결 또는 사후 복구 판정
    /// </summary>
    /// <param name="isOpen">개폐 완료 후 현재 열림 여부</param>
    /// <returns>해결 또는 사후 복구 성공 여부</returns>
    public bool TryRestore ( bool isOpen )
    {
        //만료 경계의 입력도 수동 복구로 집계하지 않음
        if ( TryExpire( ) == true )
            return false;

        //항상 닫는 것이 아니라
        //이번 발생 직전의 열림/닫힘 상태로 돌아와야 완료
        return TryComplete( isOpen == _isOpenNormal );
    }
}
