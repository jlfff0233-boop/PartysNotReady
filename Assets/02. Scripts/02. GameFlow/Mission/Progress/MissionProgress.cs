using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 미션 진행 - 단계별 목표 수량과 달성 이력 관리
/// </summary>
public class MissionProgress
{
    int _stageIndex;        //단계 인덱스
    bool _isActive;     //활성화 여부
    bool _isReady;      //초기화 완료 여부
    string _failureReason = string.Empty;       //초기화 실패 사유

    /// <summary>
    /// 미션 단계 진행 목록
    /// </summary>
    List<StageProgressData> _stages = new ( );
    /// <summary>
    /// 미션 성공 이력의 중복 판정 키 목록
    /// </summary>
    HashSet<string> _results = new ( );


    /// <summary>
    /// 초기화 완료 여부
    /// </summary>
    public bool IsReady => _isReady;
    /// <summary>
    /// 초기화 실패 사유
    /// </summary>
    public string FailureReason => _failureReason;
    /// <summary>
    /// 단계 개수
    /// </summary>
    public int StageCount => _stages.Count;
    /// <summary>
    /// 현재 단계 인덱스
    /// </summary>
    public int CurrentStageIndex => _stageIndex;
    /// <summary>
    /// 단계 완료 여부
    /// </summary>
    public bool IsCompleted => CheckCompleted ( );
    /// <summary>
    /// 활성화 여부
    /// </summary>
    public bool IsActive => _isActive;


    #region ----- 초기화/실행 -----

    /// <summary>
    /// 초기화 실패 처리
    /// </summary>
    /// <param name="reason">실패 사유</param>
    void FailInitialize ( string reason )
    {
        //일부 단계만 구성된 상태는 사용하지 않음
        _stages.Clear ( );
        _isReady = false;
        _failureReason = reason;
    }

    /// <summary>
    /// 단계별 목표와 필요 수량 선정
    /// </summary>
    /// <param name="data">게임 진행 설정</param>
    public MissionProgress ( GameFlowData data )
    {
        if ( data == null )
        {
            FailInitialize ( "게임 진행 설정이 없습니다." );
            return;
        }

        if ( data.CheckSettings ( out string reason ) == false )
        {
            FailInitialize ( reason );
            return;
        }

        for ( int i = 0 ; i < data.StageCount ; i++ )
        {
            StageProgressData stage =
                CreateStage ( data.GetStageSettings ( i ) );		//이번 판의 단계

            _stages.Add ( stage );
        }

        _isReady = true;
    }

    /// <summary>
    /// 미션 시작
    /// </summary>
    public void Begin ( )
    {
        if ( _isReady == false )
            return;

        _isActive = true;
    }

    /// <summary>
    /// 게임 종료 후 완료 개수 수신 차단
    /// </summary>
    public void End ( )
    {
        _isActive = false;
    }

    #endregion

    #region ----- 진행 반영 -----

    /// <summary>
    /// 미션 진행도 반영
    /// </summary>
    /// <param name="missionId">반영할 미션 ID</param>
    /// <param name="resultId">제작 결과 또는 물건 ID</param>
    /// <param name="amount">요청한 완료 개수</param>
    /// <param name="addedAmount">실제로 증가한 개수</param>
    /// <param name="isItemPlacement">일반 아이템 배치 여부</param>
    /// <returns>진행도 반영 여부</returns>
    public bool TryAdd (
        string missionId , string resultId , int amount ,
        out int addedAmount , bool isItemPlacement = false )
    {
        addedAmount = 0;

        //상태 확인
        if ( _isActive == false ||
             IsCompleted == true ||
             string.IsNullOrWhiteSpace ( missionId ) ||
             string.IsNullOrWhiteSpace ( resultId ) ||
             amount <= 0 ||
             ( isItemPlacement == true && amount != 1 ) )
            return false;

        //미션 ID 길이로 미션과 결과 ID의 경계 구분
        string key = $"{missionId.Length}:{missionId}{resultId}";

        //일반 아이템은 음식 이력과 구분하고 현재 단계 포함
        if ( isItemPlacement == true )
            key = $"item:{_stageIndex}:{key}";

        if ( _results.Contains ( key ) == true )
            return false;

        //현재 단계의 진행 데이터 가져오기
        StageProgressData stage = _stages [ _stageIndex ];

        for ( int i = 0 ; i < stage.GoalCount ; i++ )
        {
            //목표 가져오기
            GoalProgressData goal = stage.GetGoal ( i );

            if ( goal.Id != missionId ||
                 goal.TryAdd ( amount , out addedAmount ) == false )
                continue;

            //승인된 결과와 증가량을 단계 전환 전에 확정
            _results.Add ( key );

            //완료 처리
            if ( stage.IsCompleted ( ) == true )
                _stageIndex++;

            return true;
        }

        return false;
    }

    #endregion

    #region ----- 조회 -----

    /// <summary>
    /// 선정한 목표 ID 조회
    /// </summary>
    /// <param name="stageIndex">단계 번호</param>
    /// <param name="goalIndex">목표 번호</param>
    /// <returns>목표 ID</returns>
    public string GetGoalId ( int stageIndex , int goalIndex )
    {
        return _stages [ stageIndex ].GetGoal ( goalIndex ).Id;
    }

    /// <summary>
    /// 단계의 표시 이름 조회
    /// </summary>
    /// <param name="stageIndex">단계 번호</param>
    /// <returns>표시 이름</returns>
    public string GetStageName ( int stageIndex )
    {
        return _stages [ stageIndex ].Name;
    }

    /// <summary>
    /// 단계의 목표 수 조회
    /// </summary>
    /// <param name="stageIndex">단계 번호</param>
    /// <returns>목표 수</returns>
    public int GetGoalCount ( int stageIndex )
    {
        return _stages [ stageIndex ].GoalCount;
    }

    /// <summary>
    /// 목표 표시 정보 조회
    /// </summary>
    /// <param name="stageIndex">단계 번호</param>
    /// <param name="goalIndex">목표 번호</param>
    /// <param name="name">목표 이름</param>
    /// <param name="current">현재 달성량</param>
    /// <param name="required">필요량</param>
    public void GetGoal (
        int stageIndex , int goalIndex ,
        out string name , out int current , out int required )
    {
        GoalProgressData goal =
            _stages [ stageIndex ].GetGoal ( goalIndex );

        name = goal.Name;
        current = goal.Current;
        required = goal.Required;
    }

    #endregion

    #region ----- 미션 생성 -----

    /// <summary>
    /// 검사한 단계 설정으로 미션 진행 데이터 생성
    /// </summary>
    /// <param name="settings">검사한 단계 설정</param>
    /// <returns>생성한 단계</returns>
    StageProgressData CreateStage ( MissionStageSettings settings )
    {
        GoalProgressData [ ] goals = SelectGoals ( settings );		//이번 판의 목표
        return new StageProgressData ( settings.MissionMap.Name , goals );
    }

    /// <summary>
    /// 후보에서 중복 없이 목표와 필요 수량 선정
    /// </summary>
    /// <param name="settings">검사한 단계 설정</param>
    /// <returns>선정한 런타임 목표</returns>
    GoalProgressData [ ] SelectGoals ( MissionStageSettings settings )
    {
        MissionDataMap map = settings.MissionMap;		//미션 후보 목록
        MissionData [ ] candidates = new MissionData [ map.MissionCount ];		//추첨용 후보 복사

        for ( int i = 0 ; i < candidates.Length ; i++ )
            candidates [ i ] = map.GetMission ( i );

        int count = SelectCount ( settings.GoalCountRange );		//선정 개수
        GoalProgressData [ ] goals = new GoalProgressData [ count ];		//런타임 목표

        for ( int i = 0 ; i < count ; i++ )
        {
            int selected = Random.Range ( i , candidates.Length );		//남은 후보의 인덱스
            MissionData mission = candidates [ selected ];		//선정한 미션
            candidates [ selected ] = candidates [ i ];
            candidates [ i ] = mission;

            Vector2Int range = GetRequiredCountRange ( settings , mission );		//적용할 필요 수량 범위
            goals [ i ] = new GoalProgressData (
                mission.Id , mission.Name , SelectCount ( range ) );
        }

        return goals;
    }

    /// <summary>
    /// 미션에 적용할 필요 수량 범위 조회
    /// </summary>
    /// <param name="settings">단계 설정</param>
    /// <param name="mission">미션 설정</param>
    /// <returns>적용할 범위</returns>
    Vector2Int GetRequiredCountRange (
        MissionStageSettings settings , MissionData mission )
    {
        return mission.OverrideRequiredCount == true
            ? mission.RequiredCountRange
            : settings.RequiredCountRange;
    }

    /// <summary>
    /// 양 끝을 포함하는 범위에서 수량 선정
    /// </summary>
    /// <param name="range">검사한 수량 범위</param>
    /// <returns>선정한 수량</returns>
    int SelectCount ( Vector2Int range )
    {
        return Random.Range ( range.x , range.y + 1 );
    }

    #endregion


    /// <summary>
    /// 전체 미션 완료 확인
    /// </summary>
    /// <returns>전체 완료 여부</returns>
    bool CheckCompleted ( )
    {
        //초기화 실패로 빈 목록이 된 경우는 완료가 아님
        return _isReady && _stageIndex >= _stages.Count;
    }

}
