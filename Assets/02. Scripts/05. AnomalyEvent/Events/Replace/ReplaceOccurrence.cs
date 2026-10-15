using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 대체 변칙 사건 발생 건 - 대상, 유형, 조사 ID와 구독 정보 보관
/// </summary>
public class ReplaceOccurrence : AnomalyOccurrence<ReplaceAnomalyEvent>
{
    readonly ReplaceController _replace;     //대상의 대체 기능
    readonly int _targetId;     //발생 당시 조사 ID
    readonly ReplaceType _type;     //발생 당시 선택한 유형
    string _originalName;     //복원 정보가 정리된 뒤에도 남길 발생 당시 기준 이름


    /// <summary>
    /// 대상의 대체 기능
    /// </summary>
    public ReplaceController Replace => _replace;

    /// <summary>
    /// 사건 발생 당시 조사 ID
    /// </summary>
    public int TargetId => _targetId;

    /// <summary>
    /// 조사 전 시간 확인 콜백(조사 ID)
    /// </summary>
    public Action<int> ActionRequestedHandler { get; set; }

    /// <summary>
    /// 조사 복원 완료 콜백(조사 ID)
    /// </summary>
    public Action<int> InvestigatedHandler { get; set; }


    /// <summary>
    /// 발생한 대체 변칙 사건의 대상과 조사 정보 저장
    /// </summary>
    /// <param name="target">사건 대상</param>
    /// <param name="replace">대체를 적용한 일반 기능</param>
    /// <param name="targetId">변경 성공 후 발급된 조사 ID</param>
    /// <param name="anomalyEvent">대체 변칙 사건 모델</param>
    public ReplaceOccurrence (
        AnomalyTarget target, ReplaceController replace,
        int targetId, ReplaceAnomalyEvent anomalyEvent )
        : base( target, anomalyEvent, Time.timeAsDouble )
    {
        _replace = replace;
        _targetId = targetId;
        _type = replace.Type;
        //복원이나 만료로 원본 참조가 정리된 뒤에도 디버그에서 확인할 수 있도록 이름 보관
        if ( _type == ReplaceType.Color )
        {
            MaterialChangePresenter material = replace.GetComponent<MaterialChangePresenter>( );
            _originalName = material != null && material.Original != null
                ? material.Original.name : "복원 기준 없음";
        }
        else
        {
            AppearanceChangeController appearance = replace.GetComponent<AppearanceChangeController>( );
            _originalName = appearance != null && appearance.Original != null
                ? appearance.Original.name : "복원 기준 없음";
        }
    }


    /// <summary>
    /// 대체 변칙 사건의 디버그 상세 항목 추가
    /// </summary>
    /// <param name="details">상세 표시 항목을 추가할 목록</param>
    public override void GetDebugDetails (
        List<AnomalyDebugDetail> details )
    {
        //사후 복구 시간이 끝나면 변경 상태를 유지하고 조사를 종료하는 정책 안내
        string remaining = Status == AnomalyStatus.Ready ||
                           Status == AnomalyStatus.Active
            ? $"시작 전 ({RecoveryRemainingTime:F2}초)"
            : $"{RecoveryRemainingTime:F2}초";

        details.Add( new AnomalyDebugDetail(
            "사후 처리 남은 시간", remaining ) );
        details.Add( new AnomalyDebugDetail(
            "만료 처리", "변경된 색 또는 외형 유지, 조사 종료" ) );
        details.Add( new AnomalyDebugDetail( "대체 유형",
            _type.ToString( ) ) );
        details.Add( new AnomalyDebugDetail( "대체 진행",
            _replace != null ? _replace.IsChanged.ToString( ) : "대상 없음" ) );
        details.Add( new AnomalyDebugDetail(
            "사건 조사 ID", _targetId.ToString( ) ) );
        details.Add( new AnomalyDebugDetail( "발생 당시 복원 기준", _originalName ) );
        details.Add( new AnomalyDebugDetail(
            "복구 방법", "만료 전 E 홀딩 조사로 발생 직전 상태 복원" ) );

        if ( _replace != null &&
             string.IsNullOrEmpty( _replace.FailureReason ) == false )
        {
            details.Add( new AnomalyDebugDetail(
                "최근 요청 실패", _replace.FailureReason ) );
        }

        if ( _replace == null )
            return;

        details.Add( new AnomalyDebugDetail( "현재 조사 ID", _replace.InvestigationId.ToString( ) ) );
        details.Add( new AnomalyDebugDetail( "조사 가능", _replace.CanInvestigate.ToString( ) ) );
        //발생 당시 유형에 맞춰 현재 매트리얼 또는 외형 정보 조회
        if ( _type == ReplaceType.Color )
        {
            MaterialChangePresenter material = _replace.GetComponent<MaterialChangePresenter>( );
            string currentName = material != null && material.TryGetCurrentMaterial( out Material current ) && current != null
                ? current.name : "표시 매트리얼 없음";
            details.Add( new AnomalyDebugDetail( "현재 매트리얼", currentName ) );
        }
        else
        {
            AppearanceChangeController appearance = _replace.GetComponent<AppearanceChangeController>( );
            details.Add( new AnomalyDebugDetail( "현재 외형",
                appearance != null && appearance.Current != null ? appearance.Current.name : "표시 외형 없음" ) );
            if ( appearance != null )
                details.Add( new AnomalyDebugDetail( "외형 복원 실패 사유", appearance.FailureReason ) );
        }
    }


}
