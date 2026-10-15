using System;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 미션 결과 기록 - 제작과 배치 결과를 게임 진행에 전달
/// </summary>
public class MissionResultRecorder : MonoBehaviour
{
    [Header( "----- 제작 미션 -----" )]
    [SerializeField] CookGoalDataMap _cookGoalMap;      //제작 목표 설정
    [SerializeField] ProductionManager _productPresenter;      //제작 결과 알림

    [Header( "----- 배치 결과 -----" )]
    [SerializeField] PreparationSystem _preparation;      //유효 배치 알림

    GameFlowController _gameFlow;		//미션 진행 대상
    string _lastMissionDebug = string.Empty;      //마지막 미션 반영 결과

    /// <summary>
    /// 레시피별 목표(레시피, 미션)
    /// </summary>
    Dictionary<RecipeData, MissionData> _goals = new( );

    bool _isReady;      //초기화 완료 여부
    bool _hasInitialized;		//초기화 시도 여부
    bool _isSubscribed;      //완료 이벤트 구독 여부


    /// <summary>
    /// 마지막 미션 반영 결과
    /// </summary>
    public string LastMissionDebug => _lastMissionDebug;

    /// <summary>
    /// 미션 입력 준비 완료 여부
    /// </summary>
    public bool IsReady => _isReady;

    /// <summary>
    /// 제작 결과 입력 필요 여부
    /// </summary>
    public bool RequiresProduction =>
        _cookGoalMap != null && _cookGoalMap.Data != null && _cookGoalMap.Data.Count > 0;

    #region ----- 초기화/구독 -----

    /// <summary>
    /// 미션 입력 초기화와 결과 연결
    /// </summary>
    /// <param name="flow">게임 흐름 중재자</param>
    /// <returns>초기화 성공 여부</returns>
    public bool Init ( GameFlowController flow )
    {
        if ( _hasInitialized == true )
            return _isReady;

        _gameFlow = flow;
        _hasInitialized = true;
        _isReady = CheckSettings( );

        if ( _isReady == false )
        {
            _goals.Clear( );
            return false;
        }

        if ( isActiveAndEnabled == true )
            Subscribe( );

        return true;
    }

    private void OnEnable ()
    {
        Subscribe( );
    }

    private void OnDisable ()
    {
        Unsubscribe( );
    }

    /// <summary>
    /// 남은 결과 구독 정리
    /// </summary>
    void OnDestroy ()
    {
        Unsubscribe( );
    }


    /// <summary>
    /// 제작과 배치 결과 이벤트 구독
    /// </summary>
    void Subscribe ()
    {
        if ( _isReady == false || _isSubscribed == true )
            return;

        if ( RequiresProduction == true )
            _productPresenter.OnCompleted += HandleCompleted;
        _preparation.OnValidPlacement += HandlePlaced;
        _preparation.OnValidItemPlacement += HandleItemPlaced;

        _isSubscribed = true;
    }

    /// <summary>
    /// 제작과 배치 결과 이벤트 구독 해제
    /// </summary>
    void Unsubscribe ()
    {
        if ( _isSubscribed == false )
            return;

        if ( _productPresenter != null )
            _productPresenter.OnCompleted -= HandleCompleted;
        if ( _preparation != null )
        {
            _preparation.OnValidPlacement -= HandlePlaced;
            _preparation.OnValidItemPlacement -= HandleItemPlaced;
        }

        _isSubscribed = false;
    }


    /// <summary>
    /// 미션 후보의 완료 입력 연결 검사
    /// </summary>
    /// <returns>입력 연결 유효 여부</returns>
    bool CheckMissionInputs ()
    {
        if ( _preparation.IsReady == false )
        {
            Debug.LogError(
                "[미션 입력 연결 실패] 파티 준비 초기화를 먼저 완료해 주세요.", this );
            return false;
        }

        //선택한 난이도 설정
        GameDifficultySettings settings = _gameFlow.Settings;

        if ( settings == null || settings.Flow == null )
        {
            Debug.LogError(
                "[미션 입력 연결 실패] 선택된 게임 진행 설정이 없습니다.", this );
            return false;
        }

        //검사할 미션 후보 구성
        GameFlowData flow = settings.Flow;

        if ( flow.StageCount == 0 )
        {
            Debug.LogError(
                "[미션 입력 연결 실패] 미션 단계가 없습니다.", this );
            return false;
        }

        for ( int stage = 0; stage < flow.StageCount; stage++ )
        {
            //단계의 미션 후보
            MissionDataMap map = flow.GetMissionMap( stage );

            if ( map == null || map.MissionCount == 0 )
            {
                Debug.LogError(
                    $"[미션 입력 연결 실패] " +
                    $"{stage + 1}단계 미션 후보가 없습니다.", this );
                return false;
            }

            for ( int i = 0; i < map.MissionCount; i++ )
            {
                //검사할 후보 미션
                MissionData mission = map.GetMission( i );

                if ( mission == null ||
                     string.IsNullOrWhiteSpace( mission.Id ) == true )
                {
                    Debug.LogError(
                        $"[미션 입력 연결 실패] " +
                        $"{stage + 1}단계 {i + 1}번째 미션을 확인해 주세요.", this );
                    return false;
                }

                //완료 입력 연결 여부
                bool hasInput =
                    _preparation.ContainsPlacementMission( mission.Id );

                foreach ( MissionData cooking in _goals.Values )
                {
                    if ( string.Equals(
                        cooking.Id, mission.Id,
                        StringComparison.Ordinal ) == false )
                        continue;

                    hasInput = true;
                    break;
                }

                if ( hasInput == false )
                {
                    Debug.LogError(
                        $"[미션 입력 연결 실패] " +
                        $"{stage + 1}단계의 완료 입력이 없습니다: " +
                        $"{mission.Name} / {mission.Id}", this );
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// 제작 대상별 미션 연결 검사
    /// </summary>
    /// <returns>초기화 성공 여부</returns>
    bool CheckSettings ()
    {
        _goals.Clear( );

        if ( _gameFlow == null )
        {
            Debug.LogError(
                "[제작 미션 초기화 실패] 게임 흐름 중재자가 전달되지 않았습니다.", this );
            return false;
        }

        if ( _preparation == null )
        {
            Debug.LogError( "[배치 미션 연결 실패] PreparationSystem를 연결해 주세요.", this );
            return false;
        }

        //제작 입력이 없어도 배치 미션 후보의 연결은 검사
        if ( RequiresProduction == false )
            return CheckMissionInputs( );

        if ( _productPresenter == null )
        {
            Debug.LogError(
                "[제작 미션 초기화 실패] 제작 결과 알림을 연결해 주세요.", this );
            return false;
        }

        for ( int i = 0; i < _cookGoalMap.Data.Count; i++ )
        {
            CookGoalData goal = _cookGoalMap.Data [ i ];

            if ( goal == null ||
                 goal.Recipe == null ||
                 goal.Mission == null ||
                 string.IsNullOrWhiteSpace( goal.Mission.Id ) )
            {
                Debug.LogError(
                    $"[제작 미션 초기화 실패] {i + 1}번째 연결을 확인해 주세요.", this );
                _goals.Clear( );
                return false;
            }

            bool hasRecipe = false;
            foreach ( RecipeData recipe in _productPresenter.Recipes )
                hasRecipe |= recipe == goal.Recipe;

            if ( hasRecipe == false )
            {
                Debug.LogError( $"[제작 미션 초기화 실패] 제작 목록에 없는 레시피: {goal.Recipe.name}", this );
                _goals.Clear( );
                return false;
            }

            //같은 레시피가 여러 미션에 중복 반영되지 않도록 차단
            if ( _goals.TryAdd(
                goal.Recipe, goal.Mission ) == false )
            {
                Debug.LogError(
                    $"[제작 미션 초기화 실패] 레시피가 중복됩니다: " +
                    $"{goal.Recipe.name}", this );
                _goals.Clear( );
                return false;
            }
        }

        return CheckMissionInputs( );
    }

    #endregion

    #region ----- 미션 반영 -----

    /// <summary>
    /// 제작 완료 결과를 연결된 미션에 한 번 반영
    /// </summary>
    /// <param name="result">제작 완료 결과</param>
    void HandleCompleted ( ProductionResult result )
    {
        if ( result == null ||
             result.Recipe == null ||
             string.IsNullOrWhiteSpace( result.Id ) ||
             _goals.TryGetValue(
                 result.Recipe, out MissionData mission ) == false )
        {
            RecordMissionDebug( "제작 완료 결과 또는 미션 연결 없음" );
            return;
        }

        TryRecord( mission, result.Id );
    }

    /// <summary>
    /// 확인된 배치 결과를 미션에 반영
    /// </summary>
    /// <param name="mission">배치 미션</param>
    /// <param name="resultId">배치한 결과 ID</param>
    void HandlePlaced ( MissionData mission, string resultId )
    {
        TryRecord( mission, resultId );
    }

    /// <summary>
    /// 일반 아이템 배치 결과를 현재 단계에 반영
    /// </summary>
    /// <param name="mission">배치 미션</param>
    /// <param name="resultId">물건 ID</param>
    void HandleItemPlaced ( MissionData mission, string resultId )
    {
        TryRecord( mission, resultId, isItemPlacement: true );
    }

    /// <summary>
    /// 결과 한 개를 미션에 기록
    /// </summary>
    /// <param name="mission">반영할 미션</param>
    /// <param name="resultId">제작 결과 또는 물건 ID</param>
    /// <param name="isItemPlacement">일반 아이템 배치 여부</param>
    /// <returns>미션 반영 여부</returns>
    public bool TryRecord (
        MissionData mission, string resultId,
        bool isItemPlacement = false )
    {
        if ( _isReady == false ||
             _gameFlow == null || mission == null ||
             string.IsNullOrWhiteSpace( mission.Id ) ||
             string.IsNullOrWhiteSpace( resultId ) )
        {
            RecordMissionDebug( $"반영 불가 / 결과 {resultId}" );
            return false;
        }

        bool accepted = _gameFlow.TryAddMission(
            mission.Id, resultId, 1, isItemPlacement );
        RecordMissionDebug(
            $"결과 {resultId} / 미션 {mission.Id} / 반영 {accepted} / 게임 {_gameFlow.State}" );
        return accepted;
    }

    /// <summary>
    /// 미션 반영 결과를 보관하고 설정 시 로그 출력
    /// </summary>
    /// <param name="message">반영 결과</param>
    void RecordMissionDebug ( string message )
    {
        _lastMissionDebug = message;
    }

    #endregion
}
