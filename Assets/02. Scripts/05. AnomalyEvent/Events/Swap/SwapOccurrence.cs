using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 교체 변칙 사건 발생 건 - 두 대상, 원래 배치 대상과 구독 정보 보관
/// </summary>
public class SwapOccurrence : AnomalyOccurrence<SwapAnomalyEvent>
{
    AnomalyTarget _targetB;     //두 번째 사건 대상, 첫 번째는 공통 Target 사용
    ItemCarry _itemA;     //첫 번째 아이템의 보유 기능
    ItemCarry _itemB;     //두 번째 아이템의 보유 기능
    PlacementTarget _originalPlacementA;     //첫 번째 아이템이 돌아가야 하는 배치 대상
    PlacementTarget _originalPlacementB;     //두 번째 아이템이 돌아가야 하는 배치 대상
    string _targetNameB;     //두 번째 대상이 사라진 뒤에도 표시할 이름


    /// <summary>
    /// 첫 번째 사건 대상
    /// </summary>
    public AnomalyTarget TargetA => Target;

    /// <summary>
    /// 두 번째 사건 대상
    /// </summary>
    public AnomalyTarget TargetB => _targetB;

    /// <summary>
    /// 첫 번째 아이템의 보유 기능
    /// </summary>
    public ItemCarry ItemA => _itemA;

    /// <summary>
    /// 두 번째 아이템의 보유 기능
    /// </summary>
    public ItemCarry ItemB => _itemB;

    /// <summary>
    /// 첫 번째 아이템의 원래 배치 대상
    /// </summary>
    public PlacementTarget OriginalPlacementA => _originalPlacementA;

    /// <summary>
    /// 두 번째 아이템의 원래 배치 대상
    /// </summary>
    public PlacementTarget OriginalPlacementB => _originalPlacementB;

    /// <summary>
    /// 첫 번째 대상의 배치 알림 콜백
    /// </summary>
    public Action<PlacementTarget> PlacementHandlerA { get; set; }

    /// <summary>
    /// 두 번째 대상의 배치 알림 콜백
    /// </summary>
    public Action<PlacementTarget> PlacementHandlerB { get; set; }

    /// <summary>
    /// 두 번째 대상의 무효화 알림 콜백
    /// </summary>
    public Action InvalidatedHandlerB { get; set; }

    /// <summary>
    /// 발생 당시 교체 대상 수
    /// </summary>
    public override int RelatedTargetCount => SwapAnomalyEvent.TargetCount;


    /// <summary>
    /// 두 아이템과 발생 당시 원래 배치 대상 저장
    /// </summary>
    /// <param name="targetA">첫 번째 사건 대상</param>
    /// <param name="itemA">첫 번째 아이템의 보유 기능</param>
    /// <param name="originalPlacementA">첫 번째 아이템의 원래 배치 대상</param>
    /// <param name="targetB">두 번째 사건 대상</param>
    /// <param name="itemB">두 번째 아이템의 보유 기능</param>
    /// <param name="originalPlacementB">두 번째 아이템의 원래 배치 대상</param>
    /// <param name="anomalyEvent">교체 변칙 사건의 진행 모델</param>
    public SwapOccurrence (
        AnomalyTarget targetA, ItemCarry itemA, PlacementTarget originalPlacementA,
        AnomalyTarget targetB, ItemCarry itemB, PlacementTarget originalPlacementB,
        SwapAnomalyEvent anomalyEvent )
        : base( targetA, anomalyEvent, Time.timeAsDouble )
    {
        _targetB = targetB;
        _itemA = itemA;
        _itemB = itemB;
        _originalPlacementA = originalPlacementA;
        _originalPlacementB = originalPlacementB;
        //대상이 사라져도 사건 이력에서 두 번째 대상 이름을 확인할 수 있도록 보관
        _targetNameB = targetB != null ? targetB.name : "대상 없음";

        //A의 무효화 콜백과 사건 상태 콜백은 공통 발생 건의 속성 사용
        //실제 알림 구독과 잠금은 사건 프레젠터에서 처리
    }


    /// <summary>
    /// 두 대상의 현재 이름 또는 보관한 이름 조회
    /// </summary>
    /// <param name="index">첫 번째 대상은 0, 두 번째 대상은 1</param>
    public override string GetRelatedTargetName ( int index )
    {
        if ( index == 1 )
            return _targetB != null ? _targetB.name : _targetNameB;

        return base.GetRelatedTargetName( index );
    }

    /// <summary>
    /// 대상 번호에 해당하는 사건 대상 조회
    /// </summary>
    /// <param name="index">대상 번호, 첫 번째는 0, 두 번째는 1</param>
    /// <returns>해당 대상, 번호가 유효하지 않으면 null</returns>
    public override Object GetRelatedTarget ( int index )
    {
        //한 대상이 사라져도 A/B 번호를 바꾸지 않음
        //사건 모델의 대상별 완료 기록과 같은 번호를 유지
        return index switch
        {
            0 => TargetA,
            1 => TargetB,
            _ => null
        };
    }

    /// <summary>
    /// 교체 진행도와 대상별 원래 배치 정보 추가
    /// </summary>
    /// <param name="details">상세 표시 항목을 추가할 목록</param>
    public override void GetDebugDetails ( List<AnomalyDebugDetail> details )
    {
        //A/B 순서는 모델의 완료 기록과 같게 유지하여 각 물건의 복구 진행을 구분
        string remaining = Status == AnomalyStatus.Ready ||
                           Status == AnomalyStatus.Active
            ? $"시작 전 ({RecoveryRemainingTime:F2}초)"
            : $"{RecoveryRemainingTime:F2}초";

        details.Add( new AnomalyDebugDetail(
            "사후 처리 남은 시간", remaining ) );
        details.Add( new AnomalyDebugDetail(
            "배치 완료", $"{Event.CompletedCount}/{SwapAnomalyEvent.TargetCount}" ) );
        details.Add( new AnomalyDebugDetail( "첫 번째 대상", TargetName ) );
        details.Add( new AnomalyDebugDetail( "두 번째 대상", _targetNameB ) );
        details.Add( new AnomalyDebugDetail( "첫 번째 원래 배치 대상",
            _originalPlacementA != null ? _originalPlacementA.name : "대상 없음" ) );
        details.Add( new AnomalyDebugDetail( "두 번째 원래 배치 대상",
            _originalPlacementB != null ? _originalPlacementB.name : "대상 없음" ) );
        details.Add( new AnomalyDebugDetail(
            "첫 번째 배치 완료", Event.IsTargetCompleted( 0 ).ToString( ) ) );
        details.Add( new AnomalyDebugDetail(
            "두 번째 배치 완료", Event.IsTargetCompleted( 1 ).ToString( ) ) );
        details.Add( new AnomalyDebugDetail(
            "복구 방법", "각 물건을 발생 당시 원래 배치 대상에 내려놓기" ) );
        details.Add( new AnomalyDebugDetail(
            "만료 처리", "현재 배치 또는 정착 상태를 정상 기준으로 채택" ) );
        details.Add( new AnomalyDebugDetail(
            "복구 완료 방식", Event.RecoveryType.ToString( ) ) );
    }


}
