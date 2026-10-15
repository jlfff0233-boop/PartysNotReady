using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityTime = UnityEngine.Time;

#region ----- 발생 건 조회 -----

/// <summary>
/// 사건 디버그 인터페이스 - 발생 건 조회와 기능 테스트 요청
/// </summary>
public interface IAnomalyDebugOccurrence
{
    /// <summary>
    /// 동일 대상의 반복 발생을 구분하는 아이디
    /// </summary>
    string DebugId { get; }
    /// <summary>
    /// 대표 대상
    /// </summary>
    AnomalyTarget Target { get; }
    /// <summary>
    /// 생성 시 보관한 대상 이름
    /// </summary>
    string TargetName { get; }
    /// <summary>
    /// 사건 진행 상태
    /// </summary>
    AnomalyStatus Status { get; }
    /// <summary>
    /// 실제 사후 복구 완료 방식
    /// </summary>
    AnomalyRecoveryType RecoveryType { get; }
    /// <summary>
    /// 남은 제한시간
    /// </summary>
    float RemainingTime { get; }
    /// <summary>
    /// 실패 이후 남은 사후 복구 시간
    /// </summary>
    float RecoveryRemainingTime { get; }
    /// <summary>
    /// 사후 처리 만료 대기 여부
    /// </summary>
    bool RequiresAutomaticRecovery { get; }
    /// <summary>
    /// 관련 대상 수
    /// </summary>
    int RelatedTargetCount { get; }
    /// <summary>
    /// 기능 테스트 수
    /// </summary>
    int DebugActionCount { get; }
    /// <summary>
    /// 관련 대상 읽기
    /// </summary>
    /// <param name="index">관련 대상 번호</param>
    Object GetRelatedTarget ( int index );
    /// <summary>
    /// 대상 소실 후에도 유지되는 관련 대상 이름 조회
    /// </summary>
    /// <param name="index">관련 대상 번호</param>
    string GetRelatedTargetName ( int index );
    /// <summary>
    /// 요청 결과와 발생 건의 사건 참조 비교
    /// </summary>
    /// <param name="value">비교할 사건 모델</param>
    bool IsEvent ( AnomalyEvent value );
    /// <summary>
    /// 재사용 목록에 기능별 상세 정보 추가
    /// </summary>
    /// <param name="details">상세 항목을 추가할 목록</param>
    void GetDebugDetails ( List<AnomalyDebugDetail> details );
    /// <summary>
    /// 기능 테스트의 이름과 실행 불가 사유 조회
    /// </summary>
    /// <param name="index">조회할 테스트 번호</param>
    AnomalyDebugAction GetDebugAction ( int index );
    /// <summary>
    /// 기능의 기존 실행 경로로 테스트 요청
    /// </summary>
    /// <param name="index">실행할 테스트 번호</param>
    bool TryExecuteDebugAction ( int index );
}

#endregion

#region ----- 상세/테스트 정보 -----

/// <summary>
/// 디버그 상세 항목 - 이름과 값 보관
/// </summary>
public readonly struct AnomalyDebugDetail
{
    readonly string _label;        //항목 이름
    readonly string _value;        //표시 값

    /// <summary>
    /// 항목 이름
    /// </summary>
    public string Label => _label;
    /// <summary>
    /// 표시 값
    /// </summary>
    public string Value => _value;

    /// <summary>
    /// 상세 표시 항목 생성
    /// </summary>
    /// <param name="label">항목 이름</param>
    /// <param name="value">화면에 출력할 값</param>
    public AnomalyDebugDetail ( string label, string value )
    {
        _label = label;
        _value = value;
    }
}

/// <summary>
/// 기능 테스트 버튼의 읽기 전용 표시 정보
/// </summary>
public readonly struct AnomalyDebugAction
{
    readonly string _label;        //버튼 이름
    readonly string _disabledReason;        //실행 불가 사유, 실행 가능하면 빈 문자열

    /// <summary>
    /// 버튼 이름
    /// </summary>
    public string Label => _label;
    /// <summary>
    /// 실행 불가 사유, 실행 가능하면 빈 문자열
    /// </summary>
    public string DisabledReason => _disabledReason;

    /// <summary>
    /// 버튼 표시 정보 생성
    /// </summary>
    /// <param name="label">테스트 버튼 이름</param>
    /// <param name="disabledReason">실행 불가 사유, 실행 가능하면 빈 문자열</param>
    public AnomalyDebugAction ( string label, string disabledReason )
    {
        _label = label;
        _disabledReason = disabledReason;
    }
}

#endregion

#region ----- 발생 결과 -----

/// <summary>
/// 사건 발생 실패 정보 - 분류, 코드와 설명 보관
/// </summary>
public readonly struct AnomalyStartFailure
{
    readonly string _category;        //Common 또는 기능 이름
    readonly string _code;        //해당 분류 안의 실패 코드
    readonly string _message;        //사용자에게 표시할 설명

    /// <summary>
    /// Common 또는 기능 이름
    /// </summary>
    public string Category => _category;
    /// <summary>
    /// 해당 분류 안의 실패 코드
    /// </summary>
    public string Code => _code;
    /// <summary>
    /// 사용자에게 표시할 설명
    /// </summary>
    public string Message => _message;

    /// <summary>
    /// 실패 정보 생성
    /// </summary>
    /// <param name="category">공통 또는 기능별 실패 분류</param>
    /// <param name="code">분류 내 실패 코드</param>
    /// <param name="message">실패 설명</param>
    public AnomalyStartFailure ( string category, string code, string message )
    {
        _category = category;
        _code = code;
        _message = message;
    }
}

/// <summary>
/// 실제 발생 함수를 실행한 결과
/// </summary>
public readonly struct AnomalyStartResult
{
    readonly bool _success;        //발생 성공 여부
    readonly AnomalyEvent _event;        //발생한 사건, 실패하면 null
    readonly AnomalyTarget _target;        //선택되거나 검사한 대상
    readonly AnomalyStartFailure _failure;        //실패 설명

    /// <summary>
    /// 발생 성공 여부
    /// </summary>
    public bool Success => _success;
    /// <summary>
    /// 발생한 사건, 실패하면 null
    /// </summary>
    public AnomalyEvent Event => _event;
    /// <summary>
    /// 선택되거나 검사한 대상
    /// </summary>
    public AnomalyTarget Target => _target;
    /// <summary>
    /// 실패 설명
    /// </summary>
    public AnomalyStartFailure Failure => _failure;

    /// <summary>
    /// 발생 요청 결과 생성
    /// </summary>
    /// <param name="success">발생 성공 여부</param>
    /// <param name="anomalyEvent">발생한 사건</param>
    /// <param name="target">선택하거나 검사한 대상</param>
    /// <param name="failure">발생 실패 정보</param>
    public AnomalyStartResult (
        bool success, AnomalyEvent anomalyEvent,
        AnomalyTarget target, AnomalyStartFailure failure )
    {
        _success = success;
        _event = anomalyEvent;
        _target = target;
        _failure = failure;
    }
}

#endregion

#region ----- 종료 기록 -----

/// <summary>
/// 사건 종료 기록 - 종료 당시 상태와 상세 정보 보관
/// </summary>
public readonly struct AnomalyDebugRecord
{
    readonly string _id;        //발생 건 아이디
    readonly string _eventName;        //사건 종류
    readonly string _targetName;        //종료 당시 대상 이름
    readonly AnomalyStatus _status;        //종료 상태
    readonly AnomalyRecoveryType _recoveryType;        //종료 당시 사후 복구 완료 방식
    readonly double _time;        //플레이 시작 후 실제 경과 초

    readonly AnomalyDebugDetail[] _details;     //종료 당시 문자열 정보만 보관

    /// <summary>
    /// 종료 당시 상세 항목 수
    /// </summary>
    public int DetailCount => _details != null ? _details.Length : 0;

    /// <summary>
    /// 발생 건 아이디
    /// </summary>
    public string Id => _id;

    /// <summary>
    /// 사건 종류
    /// </summary>
    public string EventName => _eventName;

    /// <summary>
    /// 종료 당시 대상 이름
    /// </summary>
    public string TargetName => _targetName;

    /// <summary>
    /// 종료 상태
    /// </summary>
    public AnomalyStatus Status => _status;

    /// <summary>
    /// 종료 당시 사후 복구 완료 방식
    /// </summary>
    public AnomalyRecoveryType RecoveryType => _recoveryType;

    /// <summary>
    /// 플레이 시작 후 실제 경과 초
    /// </summary>
    public double Time => _time;


    /// <summary>
    /// 저장한 상세 항목 조회
    /// </summary>
    /// <param name="index">상세 항목 번호</param>
    public AnomalyDebugDetail GetDetail ( int index )
    {
        return _details[index];
    }


    /// <summary>
    /// 종료 결과 복사
    /// </summary>
    /// <param name="eventName">사건 종류 이름</param>
    /// <param name="occurrence">정리 전 발생 건</param>
    public AnomalyDebugRecord ( string eventName, IAnomalyDebugOccurrence occurrence )
    {
        _id = occurrence.DebugId;
        _eventName = eventName;
        _targetName = AnomalyDebugViewBuilder.GetTargetNames( occurrence );
        _status = occurrence.Status;
        _recoveryType = occurrence.RecoveryType;
        _time = UnityTime.realtimeSinceStartupAsDouble;
        //대상이나 사건 객체를 보관하지 않고 종료 당시 문자열 정보만 복사
        List<AnomalyDebugDetail> details = new( );
        AnomalyDebugViewBuilder.GetDetails( occurrence, details );
        _details = details.ToArray( );
    }
}

#endregion

#region ----- 플레이 세션 -----

/// <summary>
/// 디버그 세션 - 플레이 실행별 아이디 관리
/// </summary>
public static class AnomalyDebugSession
{
    static Guid _id = Guid.NewGuid( );        //현재 플레이 세션 아이디

    /// <summary>
    /// 현재 플레이 세션 아이디
    /// </summary>
    public static Guid Id => _id;

    [RuntimeInitializeOnLoadMethod( RuntimeInitializeLoadType.SubsystemRegistration )]
    private static void Reset ()
    {
        //도메인 재로드를 꺼도 새 플레이에서 이전 디버그 기록을 구분
        _id = Guid.NewGuid( );
    }
}

#endregion
