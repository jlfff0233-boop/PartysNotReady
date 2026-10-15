
/// <summary>
/// 경로 이동 상태
/// </summary>
public enum RouteStatusType
{
    Idle,       //시작 전
    Moving,     //이동 또는 경로 재계산 대기
    Arrived,        //최종 목적지 도착
    Failed,     //이동 실패
    Cancelled       //외부 요청으로 취소
}