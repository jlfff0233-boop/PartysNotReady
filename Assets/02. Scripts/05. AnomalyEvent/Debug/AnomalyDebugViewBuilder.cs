using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 변칙 사건 표시 구성 - 대상과 상세 조회, 실패 사유와 콘솔 출력
/// </summary>
public static class AnomalyDebugViewBuilder
{

    #region ----- 대상과 상세 조회 -----

    /// <summary>
    /// 모든 관련 대상 이름을 표시 문구로 구성
    /// </summary>
    /// <param name="occurrence">조회할 발생 건</param>
    public static string GetTargetNames ( IAnomalyDebugOccurrence occurrence )
    {
        List<string> names = new( );
        for ( int i = 0; i < occurrence.RelatedTargetCount; i++ )
            names.Add( $"대상 {i + 1}: {occurrence.GetRelatedTargetName( i )}" );

        return names.Count > 0 ? string.Join( " / ", names ) : occurrence.TargetName;
    }

    /// <summary>
    /// 사건별 상세와 각 대상의 정상 기준 상태 조회
    /// </summary>
    /// <param name="occurrence">조회할 발생 건</param>
    /// <param name="details">조회 결과를 받을 목록, 기존 항목은 비움</param>
    public static void GetDetails (
        IAnomalyDebugOccurrence occurrence,
        List<AnomalyDebugDetail> details )
    {
        details.Clear( );
        occurrence.GetDebugDetails( details );

        //현재 표시와 종료 기록에 같은 조회 경로 사용
        //상태를 변경하거나 실제 정착 / 공간 검사를 실행하지 않음
        for ( int i = 0; i < occurrence.RelatedTargetCount; i++ )
        {
            string label = $"대상 {i + 1}";
            details.Add( new AnomalyDebugDetail( label, occurrence.GetRelatedTargetName( i ) ) );
            AnomalyTarget target = occurrence.GetRelatedTarget( i ) as AnomalyTarget;
            if ( target == null )
            {
                details.Add( new AnomalyDebugDetail( label + " 상태", "대상 소실" ) );
                continue;
            }

            details.Add( new AnomalyDebugDetail( label + " 잠금", target.NormalLocked.ToString( ) ) );
            details.Add( new AnomalyDebugDetail( label + " 보유", target.IsHeld.ToString( ) ) );
            PlacementTarget placement = target.NormalState?.PlacementTarget;
            details.Add( new AnomalyDebugDetail( label + " 정상 배치",
                placement != null ? placement.name : "배치 대상 없음" ) );
            details.Add( new AnomalyDebugDetail( label + " 기준 갱신",
                target.NormalRefreshPending ? target.NormalRefreshReason : "대기 없음" ) );
        }
    }

    #endregion

    #region ----- 실패 정보 구성 -----

    /// <summary>
    /// 사건 발생 실패 사유
    /// </summary>
    public enum StartFailure
    {
        PresenterInactive,     //사건 중재자가 비활성화되어 있음
        TargetUnavailable,     //활성 상태의 대상이 없음
        TargetMissing,     //어노말리 대상이 없음
        TargetInactive,     //어노말리 대상이 비활성화되어 있음
        NormalLocked,     //다른 사건으로 정상 기준이 잠겨 있음
        InvalidTimeLimit,     //제한시간이 유효하지 않음
        OpenNormalMissing,     //정상 개폐 기준이 없음
        OpenCloseUnavailable,     //개폐 기능을 사용할 수 없음
        NormalStateMismatch,     //현재 개폐 상태가 정상 기준과 다름
        StateChangeFailed,     //개폐 상태 적용 실패
        PreparationCancelled,     //개폐 도중 발생 준비 취소
        InvalidAfterStateChange,     //개폐 완료 후 대상 상태 무효
        StartFailed,     //사건 시작에 실패함
        CarryMissing,     //ItemCarry가 없음
        CarryInactive,     //ItemCarry가 비활성화되어 있음
        ItemHeld,     //아이템 보유 중
        PlacementNormalMissing,     //저장된 정상 배치 상태가 없음
        PlacementMissing,     //정상 상태에 저장된 배치 대상이 없음
        PlacementInactive,     //배치 대상이 비활성화되어 있음
        SurfaceMissing,     //배치 대상의 표면 Collider가 없음
        SurfaceInactive,     //표면 Collider 또는 오브젝트가 비활성화되어 있음
        SurfaceTrigger,     //표면 Collider가 Trigger임
        OnFloor,     //Floor 위에 있어 추가 낙하할 수 없음
        SurfaceNotUp,     //지지 표면이 윗면 조건을 충족하지 않음 / 위쪽 내적 기준: 0.7 이상
        InvalidImpulse,     //밀기 충격량이 0 이하임
        PushFailed,     //아이템 고정 해제와 충격량 적용에 실패함
        RotateUnavailable,     //회전 기능을 사용할 수 없음
        RotateNotNormal,     //초기 회전과 다름
        InvalidRotationRange,     //발생 회전 범위가 유효하지 않음
        RotationFailed,     //회전 적용 실패
        RotationPreparationCancelled,     //회전 준비 중 취소
        InvalidAfterRotation,     //회전 적용 후 대상 상태 무효
    }

    /// <summary>
    /// 실패 사유를 표시 문구로 변환
    /// </summary>
    /// <param name="reason">발생 실패 사유</param>
    /// <returns>콘솔에 표시할 문구</returns>
    static string GetFailureMessage ( StartFailure reason )
    {
        return reason switch
        {
            StartFailure.PresenterInactive => "사건 중재자가 비활성화되어 있음",
            StartFailure.TargetUnavailable => "활성 상태의 대상이 없음",
            StartFailure.TargetMissing => "어노말리 대상이 없음",
            StartFailure.TargetInactive => "어노말리 대상이 비활성화되어 있음",
            StartFailure.NormalLocked => "다른 사건으로 정상 기준이 잠겨 있음",
            StartFailure.InvalidTimeLimit => "제한시간이 유효하지 않음",
            StartFailure.OpenNormalMissing => "정상 개폐 기준이 없음",
            StartFailure.OpenCloseUnavailable => "개폐 기능을 사용할 수 없음",
            StartFailure.NormalStateMismatch => "현재 개폐 상태가 정상 기준과 다름",
            StartFailure.StateChangeFailed => "개폐 상태 적용에 실패함",
            StartFailure.PreparationCancelled => "개폐 도중 발생 준비가 취소됨",
            StartFailure.InvalidAfterStateChange => "개폐 완료 후 대상 상태가 유효하지 않음",
            StartFailure.StartFailed => "사건 시작에 실패함",
            StartFailure.CarryMissing => "ItemCarry가 없음",
            StartFailure.CarryInactive => "ItemCarry가 비활성화되어 있음",
            StartFailure.ItemHeld => "아이템 보유 중",
            StartFailure.PlacementNormalMissing => "저장된 정상 배치 상태가 없음",
            StartFailure.PlacementMissing => "정상 상태에 저장된 배치 대상이 없음",
            StartFailure.PlacementInactive => "배치 대상이 비활성화되어 있음",
            StartFailure.SurfaceMissing => "배치 대상의 표면 Collider가 없음",
            StartFailure.SurfaceInactive => "표면 Collider 또는 오브젝트가 비활성화되어 있음",
            StartFailure.SurfaceTrigger => "표면 Collider가 Trigger임",
            StartFailure.OnFloor => "Floor 위에 있어 추가 낙하할 수 없음",
            StartFailure.SurfaceNotUp => "지지 표면이 윗면 조건을 충족하지 않음 / 위쪽 내적 기준: 0.7 이상",
            StartFailure.InvalidImpulse => "밀기 충격량이 0 이하임",
            StartFailure.PushFailed => "아이템 고정 해제와 충격량 적용에 실패함",
            StartFailure.RotateUnavailable => "회전 기능을 사용할 수 없음",
            StartFailure.RotateNotNormal => "현재 회전이 초기 회전과 다름",
            StartFailure.InvalidRotationRange => "발생 각도는 0도 초과 360도 미만이며 최대값은 최소값 이상이어야 함",
            StartFailure.RotationFailed => "회전 적용에 실패함",
            StartFailure.RotationPreparationCancelled => "회전 도중 발생 준비가 취소됨",
            StartFailure.InvalidAfterRotation => "회전 완료 후 대상 상태가 유효하지 않음",
            _ => reason.ToString( )
        };
    }

    /// <summary>
    /// 기존 코드를 분류와 설명이 있는 실패 결과로 변환
    /// </summary>
    /// <typeparam name="T">추가 실패 정보 형식</typeparam>
    /// <param name="eventName">기능별 실패의 분류 이름</param>
    /// <param name="reason">실패 코드</param>
    /// <param name="detail">추가 설명, null이면 생략</param>
    public static AnomalyStartFailure CreateFailure<T> (
        string eventName, StartFailure reason, T detail )
    {
        string message = GetFailureMessage( reason );
        if ( detail is not null )
            message += $" / 상세: {detail}";

        //여러 사건이 공유하는 조건만 Common으로 묶고 나머지는 사건 이름 사용
        bool common = reason == StartFailure.PresenterInactive ||
            reason == StartFailure.TargetUnavailable ||
            reason == StartFailure.TargetMissing ||
            reason == StartFailure.TargetInactive ||
            reason == StartFailure.NormalLocked ||
            reason == StartFailure.InvalidTimeLimit ||
            reason == StartFailure.ItemHeld ||
            reason == StartFailure.StartFailed;

        return new AnomalyStartFailure(
            common ? "Common" : eventName, reason.ToString( ), message );
    }

    /// <summary>
    /// 기능별 실패 코드를 공통 enum 확장 없이 설명으로 변환
    /// </summary>
    /// <param name="category">기능별 실패 분류</param>
    /// <param name="code">기능에서 반환한 실패 코드</param>
    public static AnomalyStartFailure CreateFeatureFailure ( string category, string code )
    {
        string message = code switch
        {
            "Inactive" => "사건 Presenter가 비활성화되어 있습니다.",
            "TargetUnavailable" => "활성 상태의 대상이 없습니다.",
            "NormalMissing" => "정상 배치 정보가 없습니다.",
            "Locked" => "다른 사건으로 대상이 잠겨 있습니다.",
            "InvalidTimeLimit" => "제한시간이 유효하지 않습니다.",
            "Unavailable" => "해당 기능을 사용할 수 없습니다.",
            "AlreadyActive" => "이미 동작 중인 대상입니다.",
            "ApplyFailed" => "기능 적용에 실패했습니다.",
            "PreparationCancelled" => "준비 중 사건이 취소됐습니다.",
            "InvalidAfterApply" => "기능 적용 후 대상이 유효하지 않습니다.",
            "StartFailed" => "사건을 시작하지 못했습니다.",
            "ViewMissing" => "ItemView가 없습니다.",
            "SupportMissing" => "검사 범위에서 현재 지지 표면을 찾을 수 없습니다.",
            "SupportMismatch" => "현재 지지 표면이 저장된 정상 배치 표면과 다릅니다.",
            _ => code
        };

        bool common = code == "Inactive" || code == "TargetUnavailable" ||
            code == "Locked" || code == "InvalidTimeLimit" || code == "StartFailed";
        return new AnomalyStartFailure( common ? "Common" : category, code, message );
    }

    #endregion
}
