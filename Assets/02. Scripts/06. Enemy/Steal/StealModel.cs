
using System.Collections.Generic;

/// <summary>
/// 아이템 절도 모델 - 대상 선정, 확보 조건과 재탈취 기회
/// </summary>
public class StealModel
{
    float _searchDist;		//탐색 거리
    float _takeDist;		//아이템 확보 거리
    float _searchTime;		//최대 탐색 시간
    float _retryChance;		//재탈취 확률

    float _elapsedTime;		//탐색 경과 시간
    bool _hasRetried;		//재탈취 기회 사용 여부

    /// <summary>
    /// 탐색 시간 초과 여부
    /// </summary>
    public bool IsSearchExpired => _elapsedTime >= _searchTime;

    /// <summary>
    /// 재탈취 기회 사용 여부
    /// </summary>
    public bool HasRetried => _hasRetried;


    /// <summary>
    /// 아이템 절도 모델 초기화
    /// </summary>
    /// <param name="searchDist">탐색 거리</param>
    /// <param name="takeDist">아이템 확보 거리</param>
    /// <param name="searchTime">최대 탐색 시간</param>
    /// <param name="retryChance">재탈취 확률</param>
    public StealModel (
        float searchDist, float takeDist,
        float searchTime, float retryChance )
    {
        _searchDist = searchDist;
        _takeDist = takeDist;
        _searchTime = searchTime;
        _retryChance = retryChance;
    }

    /// <summary>
    /// 새 등장 탐색 시간과 재탈취 기회 초기화
    /// </summary>
    public void Reset ()
    {
        _elapsedTime = 0f;
        _hasRetried = false;
    }


    /// <summary>
    /// 탐색 시간 초기화
    /// </summary>
    public void BeginSearch ()
    {
        _elapsedTime = 0f;
    }

    /// <summary>
    /// 탐색 시간 누적
    /// </summary>
    /// <param name="deltaTime">경과 시간</param>
    public void UpdateSearch ( float deltaTime )
    {
        _elapsedTime += deltaTime;
    }

    /// <summary>
    /// 가장 가까운 절도 대상 선택
    /// </summary>
    /// <param name="targets">음식 후보 목록</param>
    /// <returns>선택한 후보 번호, 없으면 -1</returns>
    public int SelectTarget ( IReadOnlyList<StealTargetData> targets )
    {
        if ( IsSearchExpired == true )
            return -1;

        int selectedIndex = -1;
        float nearestDist = float.MaxValue;

        for ( int i = 0; i < targets.Count; i++ )
        {
            StealTargetData target = targets [ i ];

            if ( CanTarget( target ) == false ||
                 target.Dist >= nearestDist )
                continue;

            nearestDist = target.Dist;
            selectedIndex = i;
        }

        return selectedIndex;
    }


    /// <summary>
    /// 재탈취 허용과 기회 사용
    /// </summary>
    /// <param name="randomValue">0 이상 1 미만의 난수</param>
    /// <returns>재탈취 허용 여부</returns>
    public bool TryRetry ( float randomValue )
    {
        if ( _hasRetried == true )
            return false;

        if ( randomValue >= _retryChance )
            return false;

        //허용한 재탈취만 기회를 소모하고 탐색 시간을 다시 부여
        _hasRetried = true;
        BeginSearch( );
        return true;
    }


    /// <summary>
    /// 음식 확보 가능 여부
    /// </summary>
    /// <param name="target">최신 대상 정보</param>
    /// <param name="holdDist">보유 지점과 음식 사이 거리</param>
    /// <param name="isBlocked">확보 구간 장애물 여부</param>
    /// <returns>확보 가능 여부</returns>
    public bool CanTake (
        StealTargetData target, float holdDist, bool isBlocked )
    {
        if ( IsSearchExpired == true || CanTarget( target ) == false )
            return false;

        return holdDist <= _takeDist && isBlocked == false;
    }

    /// <summary>
    /// 절도 대상 조건
    /// </summary>
    /// <param name="target">대상 정보</param>
    /// <returns>대상 가능 여부</returns>
    public bool CanTarget ( StealTargetData target )
    {
        return target.IsActive == true &&
               target.HoldType == HoldType.None &&
               target.CanApproach == true &&
               target.Dist <= _searchDist;
    }

    /// <summary>
    /// 출구 도착과 음식 보유에 따른 절도 완료 판정
    /// </summary>
    /// <param name="hasArrived">최종 탈출 지점 도착 여부</param>
    /// <param name="hasStolenFood">훔친 음식의 실제 보유 여부</param>
    /// <returns>절도 완료 여부</returns>
    public bool IsStealComplete ( bool hasArrived, bool hasStolenFood )
    {
        return hasArrived == true && hasStolenFood == true;
    }

}
