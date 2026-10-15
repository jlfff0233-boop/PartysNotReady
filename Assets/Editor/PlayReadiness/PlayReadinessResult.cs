using UnityEngine;

/// <summary>
/// 준비 점검 결과의 판정 종류
/// </summary>
public enum PlayReadinessStatus
{
    Error,      //필수 구성 오류
    Warning,    //구성 또는 운영 주의
    Passed,     //정적 구성 확인
    Pending,    //실제 플레이 확인 대기
    Blocked     //상위 구성 또는 검사 조건 부족
}

/// <summary>
/// 필드 단위 점검 결과와 이동 대상
/// </summary>
public class PlayReadinessResult
{
    public PlayReadinessStatus Status;        //판정
    public string Group;        //시스템 구역
    public string Rule;        //검사 식별자
    public Object Owner;        //설정 소유자
    public Object Related;        //연결 대상
    public string Field;        //직렬화 경로
    public string Message;        //현재 구성과 필요한 조치
    public string Scope;        //검사 범위
}
