/// <summary>
/// 공통 연출의 행동 종류
/// </summary>
public enum FeedbackCue
{
    None = 0,		//진행 중인 연출 없음
    Eat = 1,		//먹기
    Satisfied = 2,		//만족
    Mad = 3,		//분노
    Attack = 4,		//공격
    Capture = 5,		//포획 성공
    Footstep = 6,		//발소리
    Request = 7,		//요구
    Collect = 8,		//수집
    Flight = 9,		//비행
    Take = 10		//음식 확보
}
