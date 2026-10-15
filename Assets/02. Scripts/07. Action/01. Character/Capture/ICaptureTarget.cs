using System;
using UnityEngine;

/// <summary>
/// 포획 종료 결과
/// </summary>
public enum CaptureResult
{
    Rescued,        //구조 또는 탈출 성공
    Kidnapped,      //대상을 포획한 채 출구 도착
    TimedOut,       //탈출 제한 시간 초과
    Cancelled       //비활성화 등에 따른 취소
}

/// <summary>
/// 공통 포획 대상
/// </summary>
public interface ICaptureTarget
{
    /// <summary>
    /// 대상 트랜스폼
    /// </summary>
    Transform TargetTransform { get; }

    /// <summary>
    /// 포획 가능 여부
    /// </summary>
    bool CanCapture { get; }

    /// <summary>
    /// 포획 종료 이벤트(포획 결과)
    /// </summary>
    event Action<CaptureResult> OnCaptureEnded;

    /// <summary>
    /// 포획 해제 요청 이벤트(포획 결과)
    /// </summary>
    event Action<CaptureResult> OnReleaseRequested;

    /// <summary>
    /// 포획자 확인
    /// </summary>
    /// <param name="owner">확인할 포획자</param>
    /// <returns>포획자 여부</returns>
    bool IsCapturedBy ( CaptureController owner );

    /// <summary>
    /// 포획
    /// </summary>
    /// <param name="owner">포획자</param>
    /// <param name="carryPoint">보유 지점</param>
    /// <returns>포획 성공 여부</returns>
    bool TryCapture ( CaptureController owner , Transform carryPoint );

    /// <summary>
    /// 포획 해제와 밀어내기
    /// </summary>
    /// <param name="owner">포획자</param>
    /// <param name="impulse">해제 충격량</param>
    /// <param name="result">종료 결과</param>
    /// <returns>해제 성공 여부</returns>
    bool TryRelease ( CaptureController owner , Vector3 impulse , CaptureResult result );
}
