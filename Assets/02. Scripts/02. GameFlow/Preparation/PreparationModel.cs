using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 파티 준비 모델 - 준비 결과 등록과 현재 유효 배치 관리
/// </summary>
public class PreparationModel
{
    /// <summary>
    /// 준비 결과 목록
    /// </summary>
    HashSet<string> _resultIds = new( );
    /// <summary>
    /// 배치 결과 목록
    /// </summary>
    HashSet<string> _placedResultIds = new( );
    /// <summary>
    /// 목표별 유효 물건 딕셔너리(목표, 아이템 해시셋)
    /// </summary>
    Dictionary<string, HashSet<ItemCarry>> _goalItems = new( );        //목표별 유효 물건
    PreparationGoalData [ ] _goals;        //준비 목표 설정 배열

    /// <summary>
    /// 등록된 준비 결과 수
    /// </summary>
    public int ResultCount => _resultIds.Count;

    /// <summary>
    /// 현재 유효하게 배치된 결과 수
    /// </summary>
    public int CurrentCount => _placedResultIds.Count;


    /// <summary>
    /// 새 준비 결과 등록
    /// </summary>
    /// <param name="resultId">등록할 고유 결과 ID</param>
    /// <returns>신규 등록 여부</returns>
    public bool TryRegister ( string resultId )
    {
        if ( string.IsNullOrWhiteSpace( resultId ) )
            return false;

        return _resultIds.Add( resultId );
    }

    /// <summary>
    /// 결과의 현재 유효 배치 상태 변경
    /// </summary>
    /// <param name="resultId">변경할 고유 결과 ID</param>
    /// <param name="isPlaced">유효 배치 여부</param>
    /// <returns>배치 상태 변경 여부</returns>
    public bool TrySetPlaced ( string resultId, bool isPlaced )
    {
        if ( _resultIds.Contains( resultId ) == false )
            return false;

        return isPlaced
            ? _placedResultIds.Add( resultId )
            : _placedResultIds.Remove( resultId );
    }


    /// <summary>
    /// 준비 목표 등록
    /// </summary>
    /// <param name="goals">목표 설정</param>
    /// <returns>등록 성공 여부</returns>
    public bool TryRegisterGoals ( PreparationGoalData [ ] goals )
    {
        if ( _goals != null || goals == null || goals.Length == 0 )
            return false;

        HashSet<string> ids = new( );

        foreach ( PreparationGoalData goal in goals )
        {
            if ( goal == null || goal.IsValid == false ||
                 ids.Add( goal.GoalId ) == false )
                return false;
        }

        _goals = goals;

        foreach ( PreparationGoalData goal in goals )
            _goalItems.Add( goal.GoalId, new HashSet<ItemCarry>( ) );

        return true;
    }

    /// <summary>
    /// 목표의 현재 유효 물건을 새 검사 결과로 교체
    /// </summary>
    /// <param name="goalId">목표 ID</param>
    /// <param name="items">현재 유효 물건</param>
    public void SetGoalItems ( string goalId, List<ItemCarry> items )
    {
        if ( _goalItems.TryGetValue(
            goalId, out HashSet<ItemCarry> current ) == false )
            return;

        current.Clear( );

        foreach ( ItemCarry item in items )
        {
            if ( item != null )
                current.Add( item );
        }
    }

    /// <summary>
    /// 목표의 현재 인정 수량
    /// </summary>
    /// <param name="goalId">목표 ID</param>
    /// <returns>중복을 제외한 수량</returns>
    public int GetGoalCount ( string goalId )
    {
        return _goalItems.TryGetValue(
            goalId, out HashSet<ItemCarry> items )
            ? items.Count : 0;
    }

    /// <summary>
    /// 목표 상한과 카테고리 가중치로 준비 점수 계산
    /// </summary>
    /// <param name="score">0~100 준비 점수</param>
    /// <returns>집계 가능 여부</returns>
    public bool TryGetScore ( out float score )
    {
        score = 0f;

        if ( _goals == null || _goals.Length == 0 )
            return false;

        HashSet<PreparationCategoryData> categories = new( );
        float weightedTotal = 0f;
        float totalWeight = 0f;

        foreach ( PreparationGoalData categoryGoal in _goals )
        {
            PreparationCategoryData category = categoryGoal.Category;

            if ( categories.Add( category ) == false )
                continue;

            float ratioTotal = 0f;
            int goalCount = 0;

            foreach ( PreparationGoalData goal in _goals )
            {
                if ( goal.Category != category )
                    continue;

                ratioTotal += Mathf.Clamp01(
                    ( float ) GetGoalCount( goal.GoalId ) / goal.RequiredCount );
                goalCount++;
            }

            weightedTotal +=
                ratioTotal / goalCount * category.Weight;
            totalWeight += category.Weight;
        }

        if ( totalWeight <= 0f )
            return false;

        score = weightedTotal / totalWeight * 100f;
        return true;
    }

    /// <summary>
    /// 재사용되는 결과의 현재 상태와 등록 정보 제거
    /// </summary>
    /// <param name="resultId">제거할 고유 결과 ID</param>
    /// <returns>등록 정보 제거 여부</returns>
    public bool TryRemove ( string resultId )
    {
        if ( string.IsNullOrWhiteSpace( resultId ) )
            return false;

        _placedResultIds.Remove( resultId );
        return _resultIds.Remove( resultId );
    }

    /// <summary>
    /// 음식 결과의 현재 유효 배치 확인
    /// </summary>
    /// <param name="resultId">제작 결과 ID</param>
    /// <returns>유효 배치 여부</returns>
    public bool IsPlaced ( string resultId )
    {
        return _placedResultIds.Contains( resultId );
    }

}
