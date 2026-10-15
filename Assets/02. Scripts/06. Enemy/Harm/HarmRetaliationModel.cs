using System.Collections.Generic;

/// <summary>
/// 공격 적 보복 모델 - 사건 저지 집계와 방별 보복 판정
/// </summary>
public class HarmRetaliationModel : EnemyRoomActivityModel
{
    HashSet<AnomalyEvent> _countedEvents = new( );     //이번 활동에서 집계한 사건

    /// <summary>
    /// 현재 방의 누적 저지 횟수
    /// </summary>
    public int ResolvedCount => GetCount( CurrentRoom );

    /// <summary>
    /// 새 보복 분기 가능 여부
    /// </summary>
    public bool CanAttack => HasReachedThreshold( CurrentRoom );


    /// <summary>
    /// 저지 기준과 보복 확률 설정
    /// </summary>
    /// <param name="requiredCount">보복 분기에 필요한 추가 저지 횟수</param>
    /// <param name="retaliationChance">보복 선택 확률</param>
    public HarmRetaliationModel (
        int requiredCount, float retaliationChance )
        : base( requiredCount, retaliationChance )
    {
    }

    /// <summary>
    /// 지정한 방에서 활동 시작
    /// </summary>
    /// <param name="room">활동할 방</param>
    /// <returns>활동 시작 여부</returns>
    public bool Begin ( Room room )
    {
        //지난 사건의 중복 검사 목록 정리
        //방별 누적 기록은 유지
        _countedEvents.Clear( );
        return BeginActivity( room );
    }

    /// <summary>
    /// 현재 방의 사건 저지 기록
    /// </summary>
    /// <param name="occurrence">결과가 발생한 사건</param>
    /// <param name="status">사건 결과</param>
    /// <returns>저지 기록 추가 여부</returns>
    public bool TryRegisterResult (
        AnomalyEvent occurrence, AnomalyStatus status )
    {
        if ( occurrence == null || CurrentRoom == null ||
             status != AnomalyStatus.Resolved )
            return false;

        //같은 사건의 결과를 중복 집계하지 않음
        if ( _countedEvents.Add( occurrence ) == false )
            return false;

        RecordInterruption( CurrentRoom );
        return true;
    }

    /// <summary>
    /// 목표와의 거리에 따른 추격 중단 판정
    /// </summary>
    /// <param name="targetDist">목표와의 수평 거리</param>
    /// <param name="loseTargetDist">추격 중단 거리</param>
    /// <returns>추격 중단 여부</returns>
    public bool StopChase ( float targetDist, float loseTargetDist )
    {
        return targetDist > loseTargetDist;
    }
}
