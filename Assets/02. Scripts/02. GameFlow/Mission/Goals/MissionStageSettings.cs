using System;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 미션 단계 설정 - 후보와 선정 개수 및 기본 필요 수량
/// </summary>
[Serializable]
public class MissionStageSettings
{
    [SerializeField] MissionDataMap _missionMap;		//미션 후보 목록
    [SerializeField] Vector2Int _goalCountRange = Vector2Int.one;		//선정 개수 범위
    [SerializeField] Vector2Int _requiredCountRange = Vector2Int.one;		//기본 필요 수량 범위

    /// <summary>
    /// 미션 후보 목록
    /// </summary>
    public MissionDataMap MissionMap => _missionMap;

    /// <summary>
    /// 선정 개수 범위
    /// </summary>
    public Vector2Int GoalCountRange => _goalCountRange;

    /// <summary>
    /// 기본 필요 수량 범위
    /// </summary>
    public Vector2Int RequiredCountRange => _requiredCountRange;

    /// <summary>
    /// 단계 범위와 미션 후보 검사
    /// </summary>
    /// <param name="reason">설정 오류</param>
    /// <returns>설정 유효 여부</returns>
    public bool CheckSettings ( out string reason )
    {
        reason = string.Empty;

        if ( _missionMap == null )
        {
            reason = "미션 후보 맵을 연결해 주세요.";
            return false;
        }

        if ( CheckCountRange ( _goalCountRange ) == false )
        {
            reason = "선정 개수 범위를 확인해 주세요.";
            return false;
        }

        if ( CheckCountRange ( _requiredCountRange ) == false )
        {
            reason = "기본 필요 수량 범위를 확인해 주세요.";
            return false;
        }

        if ( _missionMap.MissionCount < _goalCountRange.y )
        {
            reason =
                $"최대 선정 개수 {_goalCountRange.y}개보다 " +
                $"미션 후보 {_missionMap.MissionCount}개가 부족합니다.";
            return false;
        }

        HashSet<string> ids = new ( );		//단계 후보의 미션 ID

        for ( int i = 0 ; i < _missionMap.MissionCount ; i++ )
        {
            MissionData mission = _missionMap.GetMission ( i );		//검사할 후보

            if ( mission == null )
            {
                reason = $"{i + 1}번째 미션 후보를 연결해 주세요.";
                return false;
            }

            if ( string.IsNullOrWhiteSpace ( mission.Id ) == true )
            {
                reason = $"{mission.name}의 미션 ID를 설정해 주세요.";
                return false;
            }

            if ( ids.Add ( mission.Id ) == false )
            {
                reason = $"미션 후보 ID가 중복됩니다: {mission.Id}";
                return false;
            }

            if ( mission.OverrideRequiredCount == true &&
                 CheckCountRange ( mission.RequiredCountRange ) == false )
            {
                reason = $"{mission.name}의 개별 필요 수량 범위를 확인해 주세요.";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 추첨할 양수 수량 범위 검사
    /// </summary>
    /// <param name="range">수량 범위</param>
    /// <returns>범위 유효 여부</returns>
    bool CheckCountRange ( Vector2Int range )
    {
        return range.x >= 1 &&
               range.y >= range.x &&
               range.y < int.MaxValue;
    }
}
