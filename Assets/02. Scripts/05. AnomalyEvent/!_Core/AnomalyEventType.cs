
/// <summary>
/// 변칙 사건 종류 - 사건별 설정 조회 기준
/// </summary>
public enum AnomalyEventType
{
    Drop,         //낙하
    OpenClose,    //개폐
    Rotate,       //회전
    Shake,        //흔들림
    Displace,     //이동
    Add,          //아이템 추가
    Remove,       //아이템 제거
    Replace,      //대체
    Swap,         //교체
}
