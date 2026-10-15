using System;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 파티 준비 시스템 - 준비 목표 추적과 현재 상태 관리
/// </summary>
public class PreparationSystem : MonoBehaviour
{
    [Header( "----- 설정 데이터 -----" )]
    [SerializeField] PreparationGoalDataMap _goalMap;		//평가할 준비 목표
    [SerializeField] FoodPlacementDataMap _foodMap;		//음식 배치 설정
    [SerializeField] ItemPlacementMissionDataMap _itemMap;		//일반 아이템 배치 미션 설정

    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] ProductionManager _productPresenter;		//제작 결과 알림

    [Header( "----- 상태 재검사 -----" )]
    [SerializeField, Min( 0.05f )] float _recheckInterval = 0.2f;		//물리 이동 상태 재검사 간격

    PreparationGoalData [ ] _goals;		//준비 목표

    bool _isReady;		//초기화 완료 여부
    bool _hasInitialized;		//초기화 시도 여부
    bool _isSubscribed;		//준비 상태 추적 연결 여부
    float _recheckRemaining;		//다음 상태 재검사까지 남은 시간
    string _lastDebug = string.Empty;       //마지막 준비 결과 변경
    ItemPlacementTracker _itemTracker;		//일반 아이템 배치 추적

    PreparationModel _model = new( );		//현재 준비 상태

#if UNITY_EDITOR
    bool _debugCompleted;		//촬영용 준비 점수 완료 적용
#endif

    /// <summary>
    /// 음식 배치 딕셔너리(레시피, 배치 설정)
    /// </summary>
    Dictionary<RecipeData, FoodPlacementData> _setups = new( );
    /// <summary>
    /// 일반 아이템 배치 설정 딕셔너리(아이템 종류, 배치 설정)
    /// </summary>
    Dictionary<ItemData, ItemPlacementMissionData> _itemSetups = new( );
    /// <summary>
    /// 음식 추적 딕셔너리(완성품 아이템, 추적 기록)
    /// </summary>
    Dictionary<ItemCarry, PreparationRecord> _foods = new( );
    /// <summary>
    /// 현재 목표의 유효 물건 리스트
    /// </summary>
    List<ItemCarry> _goalCandidates = new( );
    /// <summary>
    /// 현재 활성 아이템 조회 결과
    /// </summary>
    List<ItemCarry> _itemBuffer = new( );
    /// <summary>
    /// 현재 목표 갱신에서 확인한 정착 표면(아이템, 배치 대상 또는 null)
    /// </summary>
    Dictionary<ItemCarry, PlacementTarget> _goalPlacements = new( );

    /// <summary>
    /// 현재 유효하게 배치된 음식 수
    /// </summary>
    public int CurrentFoodCount => _model.CurrentCount;
    /// <summary>
    /// 생성된 음식 결과 수
    /// </summary>
    public int ProducedFoodCount => _model.ResultCount;
    /// <summary>
    /// 비활성 보존 기록을 포함한 일반 아이템 추적 수
    /// </summary>
    public int TrackedItemCount => _itemTracker?.TrackedItemCount ?? 0;
    /// <summary>
    /// 마지막 준비 결과 변경 내용
    /// </summary>
    public string LastPreparationDebug => _lastDebug;
    /// <summary>
    /// 준비 목표/입력 연결 검사 완료 여부
    /// </summary>
    public bool IsReady => _isReady;
    /// <summary>
    /// 준비 목표의 제작 결과 입력 필요 여부
    /// </summary>
    public bool RequiresProduction => CheckRequiresProduction( );

    /// <summary>
    /// 현재 음식 준비 상태 변경(유효 배치 수)
    /// </summary>
    public event Action<int> OnFoodStateChanged;
    /// <summary>
    /// 유효 배치 결과 알림(배치 미션, 제작 결과 ID)
    /// </summary>
    public event Action<MissionData, string> OnValidPlacement;
    /// <summary>
    /// 일반 아이템 유효 배치 알림(배치 미션, 물건 ID)
    /// </summary>
    public event Action<MissionData, string> OnValidItemPlacement;

    #region ----- 초기화 -----

    /// <summary>
    /// 준비 목표 초기화와 상태 추적 연결
    /// </summary>
    /// <returns>초기화 성공 여부</returns>
    public bool Init ()
    {
        if ( _hasInitialized == true )
            return _isReady;

        _hasInitialized = true;
        _isReady = CheckSettings( );

        if ( _isReady == false )
        {
            _setups.Clear( );
            _itemSetups.Clear( );
            _goals = null;
            return false;
        }

        _itemTracker = new ItemPlacementTracker( gameObject.scene, _itemSetups );

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

    private void Update ()
    {
        if ( _isReady == false || _isSubscribed == false )
            return;

        _recheckRemaining -= Time.deltaTime;

        if ( _recheckRemaining > 0f )
            return;

        _recheckRemaining =
            Mathf.Max( 0.05f, _recheckInterval );

        RecheckAll( );
    }


    /// <summary>
    /// 필요한 음식 결과 연결과 전체 준비 상태 추적 시작
    /// </summary>
    void Subscribe ()
    {
        if ( _isReady == false || _isSubscribed == true )
            return;

        if ( _setups.Count > 0 )
        {
            _productPresenter.OnCompleted += HandleCompleted;
            _productPresenter.OnReset += HandleReset;
            _productPresenter.OnRegistered += HandleRegistered;

            //비활성 중 놓친 완료 또는 초기화 상태 동기화
            foreach ( CookController product in _productPresenter.Products )
                Synchronize( product );
        }

        foreach ( PreparationRecord record in _foods.Values )
            record.Subscribe( );

        _isSubscribed = true;
        _recheckRemaining = 0f;

        _itemTracker.OnChanged += RefreshGoals;
        _itemTracker.OnValidPlacement += HandleItemPlacement;
        _itemTracker.Begin( );

        RecheckAll( );
    }

    /// <summary>
    /// 준비 상태 추적 구독 해제
    /// </summary>
    void Unsubscribe ()
    {
        if ( _isSubscribed == false )
            return;

        if ( _setups.Count > 0 && _productPresenter != null )
        {
            _productPresenter.OnCompleted -= HandleCompleted;
            _productPresenter.OnReset -= HandleReset;
            _productPresenter.OnRegistered -= HandleRegistered;
        }

        //정리 중 발생하는 콜백의 재검사 차단
        _isSubscribed = false;

        foreach ( PreparationRecord record in _foods.Values )
            record.Unsubscribe( );

        _itemTracker.Dispose( );
        _itemTracker.OnChanged -= RefreshGoals;
        _itemTracker.OnValidPlacement -= HandleItemPlacement;
    }

    #endregion


    #region ----- 핸들 -----

    /// <summary>
    /// 완성품 결과를 현재 준비 추적에 등록
    /// </summary>
    /// <param name="result">제작 완료 결과</param>
    void HandleCompleted ( ProductionResult result )
    {
        if ( result == null ||
             result.Recipe == null ||
             result.Item == null ||
             string.IsNullOrWhiteSpace( result.Id ) ||
             _setups.TryGetValue(
                 result.Recipe, out FoodPlacementData setup ) == false )
        {
            RecordPreparationDebug( "제작 완료 결과 또는 배치 설정 없음" );
            return;
        }

        if ( _foods.TryGetValue( result.Item,
            out PreparationRecord previous ) == true )
        {
            //같은 완료 결과는 새로 등록하지 않고 상태만 복구
            if ( previous.Result != null &&
                 string.Equals(
                     previous.Result.Id, result.Id, StringComparison.Ordinal ) == true )
            {
                previous.Subscribe( );
                Recheck( previous );
                return;
            }

            //같은 아이템이 새 결과로 재사용되면 이전 결과 추적 종료
            RemoveRecord( previous );
        }

        if ( _model.TryRegister( result.Id ) == false )
        {
            RecordPreparationDebug( $"등록 거절 / 결과 {result.Id}" );
            return;
        }

        RecordPreparationDebug(
            $"결과 등록 / 결과 {result.Id} / " +
            $"등록 {_model.ResultCount} / 배치 {_model.CurrentCount}" );

        PreparationRecord record = new( result, setup );

        record.SetHandlers(
            target => HandlePlaced( record, target ),
            () => Recheck( record ),
            ( previousType, currentType ) =>
                HandleHoldChanged( record, previousType, currentType ),
            () => HandleUnavailable( record ) );

        _foods [ result.Item ] = record;
        record.Subscribe( );

        Recheck( record );
    }

    /// <summary>
    /// 현재 준비 추적 종료
    /// </summary>
    /// <param name="result">초기화 전 제작 결과</param>
    void HandleReset ( ProductionResult result )
    {
        if ( result == null || result.Item == null )
            return;

        if ( _foods.TryGetValue(
            result.Item, out PreparationRecord record ) == false )
        {
            RecordPreparationDebug(
                $"초기화 결과 기록 없음 / 결과 {result.Id}" );
            return;
        }

        if ( record.Result == null ||
             string.Equals( record.Result.Id, result.Id,
                 StringComparison.Ordinal ) == false )
            return;

        RemoveRecord( record );
    }

    /// <summary>
    /// 배치 요청 기록과 상태 확인
    /// </summary>
    /// <param name="record">배치한 완성품 기록</param>
    /// <param name="target">배치 입력에서 선택한 대상</param>
    void HandlePlaced (
        PreparationRecord record, PlacementTarget target )
    {
        if ( record == null ||
            record.Result == null || record.Setup == null )
            return;

        record.SetPendingTarget(
            record.Setup.ContainsTarget( target ) ? target : null );

        Recheck( record );
    }

    /// <summary>
    /// 배치/배치 요청 정리
    /// </summary>
    /// <param name="record">보유 상태가 바뀐 완성품 기록</param>
    /// <param name="previousType">이전 보유 방식</param>
    /// <param name="currentType">현재 보유 방식</param>
    void HandleHoldChanged (
        PreparationRecord record,
        HoldType previousType, HoldType currentType )
    {
        if ( previousType == HoldType.None &&
             currentType != HoldType.None )
        {
            record.ClearPlacement( );
            SetPlaced( record, false );
        }
        else if ( previousType != HoldType.None &&
                  currentType == HoldType.None )
        {
            record.WaitForPlace( );
        }
    }

    /// <summary>
    /// 손실 및 소비를 준비 상태에 반영
    /// </summary>
    /// <param name="record">사용할 수 없게 된 완성품 기록</param>
    void HandleUnavailable ( PreparationRecord record )
    {
        record.ClearPlacement( );
        SetPlaced( record, false );
    }

    /// <summary>
    /// 일반 아이템 배치 기록과 미션 알림 전달
    /// </summary>
    /// <param name="mission">확정할 배치 미션</param>
    /// <param name="resultId">대여 회차별 결과 ID</param>
    void HandleItemPlacement ( MissionData mission, string resultId )
    {
        RecordPreparationDebug(
            $"일반 아이템 정착 확인 / 물건 {resultId} / " +
            $"미션 {mission.Id}" );

        OnValidItemPlacement?.Invoke( mission, resultId );
    }

    /// <summary>
    /// 등록된 그릇의 완성품 연결
    /// </summary>
    /// <param name="product">등록된 그릇</param>
    void HandleRegistered ( CookController product )
    {
        //등록 해제로 기존 기록은 지우지 않고 재등록 시 현재 결과 확인
        Synchronize( product );
    }

    #endregion

    #region ----- 결과 등록 -----

    /// <summary>
    /// 제작 결과 동기화
    /// </summary>
    /// <param name="product">동기화할 제작 대상</param>
    void Synchronize ( CookController product )
    {
        if ( product == null )
            return;

        ProductionResult current = product.Session.CompletedResult;

        if ( current != null )
        {
            HandleCompleted( current );
            return;
        }

        //비활성 중 초기화된 제작 대상의 이전 기록 검색
        PreparationRecord stale = null;

        foreach ( PreparationRecord record in _foods.Values )
        {
            if ( record?.Result?.Source != product )
                continue;

            stale = record;
            break;
        }

        RemoveRecord( stale );
    }

    /// <summary>
    /// 현재 준비 추적에서 제작 결과 제거
    /// </summary>
    /// <param name="record">제거할 준비 기록</param>
    void RemoveRecord ( PreparationRecord record )
    {
        if ( record == null || record.Result == null )
            return;

        record.Unsubscribe( );

        int previousCount = _model.CurrentCount;

        _model.TryRemove( record.Result.Id );

        if ( record.Result.Item != null )
            _foods.Remove( record.Result.Item );

        RecordPreparationDebug(
            $"기록 해제 / 결과 {record.Result.Id} " +
            $"/ 등록 {_model.ResultCount} / 배치 {_model.CurrentCount}" );

        if ( previousCount != _model.CurrentCount )
            OnFoodStateChanged?.Invoke( _model.CurrentCount );
    }

    #endregion

    #region ----- 준비 목표 -----


    /// <summary>
    /// 모든 준비 목표의 현재 인정 대상 갱신
    /// </summary>
    void RefreshGoals ()
    {
        bool hasCollectedItems = false;		//현재 활성 아이템 조회 여부
        _goalPlacements.Clear( );

        foreach ( PreparationGoalData goal in _goals )
        {
            _goalCandidates.Clear( );

            if ( goal.IsFoodGoal == true )
                CollectFoodGoal( goal );
            else
            {
                //음식 목표만 있는 구성에서는 전체 아이템을 조회하지 않음
                if ( hasCollectedItems == false )
                {
                    ItemRegister.GetActive( _itemBuffer );
                    hasCollectedItems = true;
                }

                CollectPlacedGoal( goal );
            }

            _model.SetGoalItems( goal.GoalId, _goalCandidates );
        }

        _itemBuffer.Clear( );
        _goalPlacements.Clear( );
    }

    /// <summary>
    /// 준비 상태 확인 및 점수 조회
    /// </summary>
    /// <param name="score">0~100 준비 점수</param>
    /// <returns>집계 가능 여부</returns>
    public bool TryGetPreparationScore ( out float score )
    {
        score = 0f;

        if ( _isReady == false )
            return false;

#if UNITY_EDITOR
        if ( _debugCompleted == true )
        {
            score = 100f;
            return true;
        }
#endif

        RecheckAll( );
        return _model.TryGetScore( out score );
    }

    /// <summary>
    /// 음식 목표의 현재 유효 완성품 수집
    /// </summary>
    /// <param name="goal">검사할 음식 목표</param>
    void CollectFoodGoal ( PreparationGoalData goal )
    {
        foreach ( PreparationRecord record in _foods.Values )
        {
            if ( record?.Result == null ||
                 record.Result.Data != goal.ItemData ||
                 record.Result.Item == null ||
                 record.Result.Item.isActiveAndEnabled == false ||
                 _model.IsPlaced( record.Result.Id ) == false )
                continue;

            _goalCandidates.Add( record.Result.Item );
        }
    }

    /// <summary>
    /// 비음식 목표의 실제 정착 대상 수집
    /// </summary>
    /// <param name="goal">검사할 비음식 목표</param>
    void CollectPlacedGoal ( PreparationGoalData goal )
    {
        foreach ( ItemCarry item in _itemBuffer )
        {
            //활성 상태와 목표 종류 확인
            if ( item == null || item.isActiveAndEnabled == false ||
                 item.gameObject.scene != gameObject.scene ||
                 item.IsHold == true || item.ItemData != goal.ItemData )
                continue;

            //같은 아이템의 정착 검사는 이번 목표 갱신에서 한 번만 수행
            if ( _goalPlacements.TryGetValue( item, out PlacementTarget target ) == false )
            {
                if ( item.TryGetSettledPlacement( out target, out _ ) == false )
                    target = null;

                _goalPlacements.Add( item, target );
            }

            if ( target == null ||
                 goal.ContainsLocation( target.PreparationLocation ) == false )
                continue;

            _goalCandidates.Add( item );
        }
    }

    #endregion

    #region ----- 배치 상태 -----

    /// <summary>
    /// 전체 준비 상태 재검사
    /// </summary>
    void RecheckAll ()
    {
        foreach ( PreparationRecord record in _foods.Values )
        {
            //보류한 배치 검사 재개
            record.ResumeCheck( );
            Recheck( record );
        }

        _itemTracker.RecheckAll( );
        RefreshGoals( );
    }

    /// <summary>
    /// 개별 완성품 판정을 집계하고 직접 배치 미션 전달
    /// </summary>
    /// <param name="record">재검사할 완성품 기록</param>
    void Recheck ( PreparationRecord record )
    {
        if ( record == null ||
             record.CheckPlacement( out bool isPlaced, out bool isNewPlacement ) == false )
            return;

        if ( isNewPlacement == true )
        {
            //현재 배치 알림 이후 직접 배치 미션을 전달하는 순서 유지
            SetPlaced( record, true );
            OnValidPlacement?.Invoke(
                record.Setup.PlacementMission, record.Result.Id );
        }

        //미션 콜백 이후에도 기존과 같이 현재 판정 결과 반영
        SetPlaced( record, isPlaced );
    }

    /// <summary>
    /// 현재 준비 배치 상태 변경과 알림
    /// </summary>
    /// <param name="record">변경할 완성품 기록</param>
    /// <param name="isPlaced">유효 배치 여부</param>
    void SetPlaced (
        PreparationRecord record, bool isPlaced )
    {
        if ( record == null || record.Result == null )
            return;

        if ( _model.TrySetPlaced(
            record.Result.Id, isPlaced ) == false )
            return;

        RecordPreparationDebug(
            $"배치 변경 / 결과 {record.Result.Id} " +
            $"/ 배치 {isPlaced} / 현재 {_model.CurrentCount}" );

        //준비 상태 변경 이벤트 발행
        OnFoodStateChanged?.Invoke( _model.CurrentCount );
    }

    #endregion

    /// <summary>
    /// 배치 입력에 연결된 미션 확인
    /// </summary>
    /// <param name="missionId">확인할 미션 ID</param>
    /// <returns>배치 입력 연결 여부</returns>
    public bool ContainsPlacementMission ( string missionId )
    {
        if ( _isReady == false ||
             string.IsNullOrWhiteSpace( missionId ) == true )
            return false;

        foreach ( FoodPlacementData setup in _setups.Values )
        {
            if ( string.Equals(
                setup.PlacementMission.Id, missionId,
                StringComparison.Ordinal ) == true )
                return true;
        }

        foreach ( ItemPlacementMissionData setup in _itemSetups.Values )
        {
            if ( string.Equals(
                setup.PlacementMission.Id, missionId,
                StringComparison.Ordinal ) == true )
                return true;
        }

        return false;
    }

    /// <summary>
    /// 음식 목표 또는 배치 설정 존재 여부 확인
    /// </summary>
    /// <returns>제작 결과 입력 필요 여부</returns>
    bool CheckRequiresProduction ()
    {
        if ( _foodMap != null &&
             _foodMap.Data != null && _foodMap.Data.Count > 0 )
            return true;

        if ( _goalMap == null || _goalMap.Data == null )
            return false;

        foreach ( PreparationGoalData goal in _goalMap.Data )
        {
            if ( goal != null && goal.IsFoodGoal == true )
                return true;
        }

        return false;
    }

    /// <summary>
    /// 일반 아이템 배치 미션 설정 검사
    /// </summary>
    /// <returns>설정 유효 여부</returns>
    bool CheckItemSettings ()
    {
        _itemSetups.Clear( );

        //일반 아이템 배치 미션이 없는 구성 허용
        if ( _itemMap == null )
            return true;

        if ( _itemMap.Data == null || _itemMap.Data.Count == 0 )
        {
            Debug.LogError(
                "[파티 준비 초기화 실패] 일반 아이템 배치 설정이 없습니다.", this );
            return false;
        }

        for ( int i = 0; i < _itemMap.Data.Count; i++ )
        {
            ItemPlacementMissionData setup = _itemMap.Data [ i ];

            if ( setup == null ||
                 setup.ItemData == null ||
                 setup.PlacementMission == null ||
                 string.IsNullOrWhiteSpace( setup.PlacementMission.Id ) ||
                 setup.HasLocation == false )
            {
                Debug.LogError(
                    $"[파티 준비 초기화 실패] " +
                    $"{i + 1}번째 일반 아이템 배치 설정을 확인해 주세요.", this );
                return false;
            }

            //제작 완성 음식은 기존 음식 배치 경로에서 처리
            if ( setup.ItemData is FoodData food &&
                 food.Category == FoodCategory.Prepared )
            {
                Debug.LogError(
                    $"[파티 준비 초기화 실패] " +
                    $"일반 아이템 배치 설정에 완성 음식이 포함됩니다: " +
                    $"{setup.ItemData.name}", this );
                return false;
            }

            if ( _itemSetups.TryAdd( setup.ItemData, setup ) == false )
            {
                Debug.LogError(
                    $"[파티 준비 초기화 실패] " +
                    $"일반 아이템 배치 설정이 중복됩니다: " +
                    $"{setup.ItemData.name}", this );
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 목표 종류별 필수 입력과 배치 설정 검사
    /// </summary>
    /// <returns>초기화 성공 여부</returns>
    bool CheckSettings ()
    {
        _setups.Clear( );

        if ( CheckItemSettings( ) == false )
            return false;

        if ( _goalMap == null || _goalMap.Data == null ||
             _goalMap.Data.Count == 0 )
        {
            Debug.LogError( "[파티 준비 초기화 실패] 준비 목표가 없습니다.", this );
            return false;
        }

        bool hasFoodGoal = false;
        _goals = new PreparationGoalData [ _goalMap.Data.Count ];

        for ( int i = 0; i < _goals.Length; i++ )
        {
            PreparationGoalData goal = _goalMap.Data [ i ];

            if ( goal == null || goal.IsValid == false )
            {
                Debug.LogError(
                    $"[파티 준비 초기화 실패] " +
                    $"{i + 1}번째 준비 목표를 확인해 주세요.", this );
                return false;
            }

            _goals [ i ] = goal;
            hasFoodGoal |= goal.IsFoodGoal;
        }

        //음식 목표가 없어도 명시된 음식 배치 미션은 기존대로 추적
        bool hasFoodSetups = _foodMap != null &&
            _foodMap.Data != null && _foodMap.Data.Count > 0;

        if ( ( hasFoodGoal || hasFoodSetups ) &&
             ( _productPresenter == null ||
               hasFoodSetups == false ) )
        {
            Debug.LogError(
                "[파티 준비 초기화 실패] 배치 준비 설정이 없습니다.", this );
            return false;
        }

        for ( int i = 0; hasFoodSetups && i < _foodMap.Data.Count; i++ )
        {
            FoodPlacementData setup = _foodMap.Data [ i ];

            if ( setup == null ||
                 setup.Recipe == null ||
                 setup.PlacementMission == null ||
                 string.IsNullOrWhiteSpace( setup.PlacementMission.Id ) ||
                 setup.HasLocation == false )
            {
                Debug.LogError(
                    $"[파티 준비 초기화 실패] " +
                    $"{i + 1}번째 배치 설정을 확인해 주세요.", this );

                _setups.Clear( );
                return false;
            }

            if ( _setups.TryAdd( setup.Recipe, setup ) == false )
            {
                Debug.LogError(
                    $"[파티 준비 초기화 실패] 레시피가 중복됩니다: " +
                    $"{setup.Recipe.name}", this );

                _setups.Clear( );
                return false;
            }

            bool hasRecipe = false;

            foreach ( RecipeData recipe in _productPresenter.Recipes )
                hasRecipe |= recipe == setup.Recipe;

            if ( hasRecipe == false )
            {
                Debug.LogError(
                    $"[파티 준비 초기화 실패] 제작 목록에 없는 레시피: " +
                    $"{setup.Recipe.name}", this );

                _setups.Clear( );
                return false;
            }
        }

        //음식 목표의 제작 / 배치 연결 확인
        for ( int i = 0; i < _goals.Length; i++ )
        {
            PreparationGoalData goal = _goals [ i ];

            if ( goal.IsFoodGoal == true )
            {
                bool hasInput = false;

                foreach ( RecipeData recipe in _setups.Keys )
                    hasInput |= recipe.ResultItem == goal.ItemData;

                if ( hasInput == false )
                {
                    Debug.LogError(
                        $"[파티 준비 초기화 실패] " +
                        $"음식 목표의 제작 / 배치 연결 없음: {goal.GoalId}", this );

                    _setups.Clear( );

                    return false;
                }
            }
        }

        if ( _model.TryRegisterGoals( _goals ) == false )
        {
            Debug.LogError(
                "[파티 준비 초기화 실패] " +
                "목표 ID 중복 또는 설정 오류입니다.", this );
            return false;
        }

        return true;
    }

    #region ----- 디버그 -----

#if UNITY_EDITOR
    /// <summary>
    /// 촬영용 준비 점수를 완료 상태로 적용
    /// </summary>
    public void DebugCompletePreparation ()
    {
        if ( Application.isPlaying == false || _isReady == false )
            return;

        _debugCompleted = true;
        RecordPreparationDebug( "촬영용 Preparation 100점 적용 / 실제 배치 상태 유지" );
        Debug.Log( "[촬영 단축키] Preparation 100점 적용. 실제 음식 생성과 배치는 생략합니다.", this );
    }
#endif

    /// <summary>
    /// 준비 로그 기록
    /// </summary>
    /// <param name="message">변경 내용</param>
    void RecordPreparationDebug ( string message )
    {
        _lastDebug = message;
    }

    #endregion
}
