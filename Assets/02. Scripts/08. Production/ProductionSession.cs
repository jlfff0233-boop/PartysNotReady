using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 제작 세션 - 진행 상태와 실행 순서 조율
/// </summary>
public class ProductionSession
{
    bool _isBeginning;		//제작 시작 처리 중
    bool _isProductionActive;		//제작 종료 전
    bool _cancelAttemptPending;		//소비 종료 후 시도 중단 예약

    RecipeData _recipe;		//선택 레시피
    CookController _cook;		//제작 대상
    ProductionModel _model;		//제작 상태 모델
    ProductionResult _completedResult;		//제작 완료 결과
    IngredientConsumption _consumption;		//소비 실행


    ProductionIngredients _ingredients;		//실제 재료 등록과 배치 연결

    /// <summary>
    /// 소비 대상 목록(재료 ID)
    /// </summary>
    List<int> _consumableIds = new ( );

    #region ----- 프로퍼티/이벤트 -----

    /// <summary>
    /// 제작 미니게임
    /// </summary>
    public ProduceMinigameController Minigame { get; }


    /// <summary>
    /// 제작 대상
    /// </summary>
    public CookController World => _cook;

    /// <summary>
    /// 등록된 제작 대상 활성 여부
    /// </summary>
    public bool IsActive => CheckIsActive( );

    /// <summary>
    /// 레시피/미니게임 준비 여부
    /// </summary>
    public bool IsReady => CheckIsReady( );

    /// <summary>
    /// 제작 상태
    /// </summary>
    public ProductionState State =>
        _model == null ? ProductionState.Preparing : _model.State;

    /// <summary>
    /// 선택된 레시피
    /// </summary>
    public RecipeData SelectedRecipe => _recipe;

    /// <summary>
    /// 현재 성공 횟수
    /// </summary>
    public int SuccessCount => _model == null ? 0 : _model.SuccessCount;

    /// <summary>
    /// 완료에 필요한 성공 횟수
    /// </summary>
    public int RequiredSuccessCount =>
        _model == null ? 0 : _model.RequiredSuccessCount;

    /// <summary>
    /// 잘못된 조합의 마지막 실패 여부
    /// </summary>
    public bool IsForcedFailure =>
       _model != null && _model.IsForcedFailure;

    /// <summary>
    /// 새 재료의 등록 가능 여부
    /// </summary>
    public bool CanReceiveIngredients => CheckCanReceiveIngredients( );

    /// <summary>
    /// 제작 시작 가능 여부
    /// </summary>
    public bool CanBegin
    {
        get
        {
            return CanBeginProduction ( out _ );
        }
    }

    /// <summary>
    /// 미니게임 진행 가능 여부
    /// </summary>
    public bool CanRunMinigame => CheckCanRunMinigame( );

    /// <summary>
    /// 현재 투입 재료 존재 여부
    /// </summary>
    public bool HasIngredients => _ingredients.Count > 0;

    /// <summary>
    /// 등록된 재료 개수
    /// </summary>
    public int RegisteredIngredientCount => _ingredients.Count;

    /// <summary>
    /// 선택한 완성 아이템
    /// </summary>
    public ItemData ResultItem =>
        _model == null ? null : _model.ResultItem;

    /// <summary>
    /// 레시피의 요구 재료 종류 수
    /// </summary>
    public int RequirementCount =>
        _recipe == null ? 0 : _recipe.IngredientCount;

    /// <summary>
    /// 완성품 폐기 가능 여부
    /// </summary>
    public bool CanDispose => CheckCanDispose( );

    /// <summary>
    /// 폐기 대상 존재 여부
    /// </summary>
    public bool HasDiscardable => HasIngredients || CanDispose;

    /// <summary>
    /// 제작 완료 결과
    /// </summary>
    public ProductionResult CompletedResult => _completedResult;

    /// <summary>
    /// 재료 소비 처리 중 여부
    /// </summary>
    public bool IsConsuming => _consumption != null;

    /// <summary>
    /// 제작 진행 여부
    /// </summary>
    public bool IsProductionActive => _isProductionActive;


    /// <summary>
    /// 제작 시작 이벤트(시작 판정)
    /// </summary>
    public event Action<ProductionStartResult> OnStartResult;

    /// <summary>
    /// 제작 상태 변경 이벤트(제작 상태)
    /// </summary>
    public event Action<ProductionState> OnStateChanged;

    /// <summary>
    /// 등록 재료 변경 이벤트
    /// </summary>
    public event Action OnIngredientsChanged;

    /// <summary>
    /// 제작 완료 이벤트(제작 결과)
    /// </summary>
    public event Action<ProductionResult> OnCompleted;

    /// <summary>
    /// 제작 결과 초기화 이벤트(제작 결과)
    /// </summary>
    public event Action<ProductionResult> OnReset;

    /// <summary>
    /// 제작 종료 알림
    /// </summary>
    public event Action OnProductionEnded;

    #endregion

    /// <summary>
    /// 제작 대상 연결
    /// </summary>
    /// <param name="world">월드 적용을 담당하는 대상</param>
    public ProductionSession ( CookController world )
    {
        _cook = world;
        _ingredients = new ProductionIngredients ( world );
        Minigame = new ProduceMinigameController ( this );
    }

    #region ----- 레시피 -----

    /// <summary>
    /// 레시피와 제작 설정 선택
    /// </summary>
    /// <param name="recipe">선택한 레시피</param>
    /// <param name="minigameData">적용할 제작 설정</param>
    /// <returns>선택 성공 여부</returns>
    public bool SelectRecipe ( RecipeData recipe , ProduceMinigameData minigameData )
    {
        if ( CanReceiveIngredients == false )
            return false;

        if ( recipe == null || minigameData == null )
            return false;

        ProductionModel model = new (
            recipe , minigameData.RequiredSuccessCount ,
            minigameData.FailureLossCount );

        if ( model.IsReady == false )
        {
            Debug.LogError ( $"[레시피 선택 실패] {model.FailureReason}" , _cook );
            return false;
        }

        //이미 배치된 재료 모델 복구
        foreach ( ProductIngredient ingredient in _ingredients.Items.Values )
        {
            if ( ingredient == null )
                return false;

            if ( model.TryAddIngredient (
                ingredient.InstanceId , ingredient.Data ) == false )

                return false;
        }

        if ( Minigame.TryConfigure (
            minigameData , out string reason ) == false )
        {
            Debug.LogError ( $"[레시피 선택 실패] {reason}" , _cook );
            return false;
        }

        _recipe = recipe;
        _model = model;

        OnIngredientsChanged?.Invoke ( );
        OnStateChanged?.Invoke ( State );

        return true;
    }

    #endregion

    #region ----- 재료 -----


    /// <summary>
    /// 재료 등록
    /// </summary>
    /// <param name="ingredient">등록할 재료</param>
    /// <param name="target">재료가 배치된 대상</param>
    /// <returns>등록 여부</returns>
    public bool TryAddIngredient (
        ProductIngredient ingredient , PlacementTarget target )
    {
        //재료 등록 가능 여부 확인
        if ( CanAddIngredient ( ingredient , target ) == false )
            return false;

        int instanceId = ingredient.InstanceId;

        //같은 배치 알림은 중복 등록하지 않음
        if ( _ingredients.TryGet (
            instanceId , out ProductIngredient current ) == true )
        {
            return current == ingredient;
        }

        //재료 추가
        if ( _model != null &&
             _model.TryAddIngredient ( instanceId , ingredient.Data ) == false )
            return false;

        //제작 대상을 운반해도 재료가 함께 이동
        _ingredients.Register ( ingredient );

        OnIngredientsChanged?.Invoke ( );
        return true;
    }

    /// <summary>
    /// 재료 제거
    /// </summary>
    /// <param name="ingredient">제거할 재료</param>
    /// <returns>제거 여부</returns>
    public bool TryRemoveIngredient ( ProductIngredient ingredient )
    {
        //소비 중 등록 변경 차단
        if ( _consumption != null )
            return false;
        if ( _cook.IsInitialized == false )
            return false;

        int instanceId = ingredient.InstanceId;

        //재료 가져오기
        if ( _ingredients.TryGet (
            instanceId , out ProductIngredient current ) == false ||
            current != ingredient )
            return false;

        //대상에서 재료 제거
        if ( _model != null &&
             _model.TryRemoveIngredient ( instanceId ) == false )
            return false;

        //참조 정리
        _ingredients.Unregister ( instanceId );

        OnIngredientsChanged?.Invoke ( );
        return true;
    }

    /// <summary>
    /// 재료 손실 반영
    /// </summary>
    /// <param name="ingredient">손실 확정 재료</param>
    /// <returns>손실 반영 여부</returns>
    public bool TryHandleExternalLoss ( ProductIngredient ingredient )
    {
        if ( _cook.IsInitialized == false )
            return false;

        int instanceId = ingredient.InstanceId;

        //이미 제거됐거나 다른 객체의 알림이면 중복 처리하지 않음
        if ( _ingredients.TryGet ( instanceId ,
            out ProductIngredient current ) == false ||
            current != ingredient )
            return false;

        //소비 종료 시 손실 반영
        if ( _consumption != null &&
             _consumption.RecordExternalLoss ( instanceId ) == true )
        {
            return true;
        }

        bool wasProcessing = State == ProductionState.Processing;

        //레시피를 선택한 경우 모델에도 손실 반영
        if ( _model != null &&
             _model.TryRemoveLostIngredient ( instanceId ) == false )
        {
            return false;
        }
        //등록 참조 정리
        _ingredients.Unregister ( instanceId );

        if ( wasProcessing == true )
        {
            //현재 미니게임만 취소하고 남은 재료의 배치는 유지
            EndProduction ( );
            _consumableIds.Clear ( );
            _ingredients.SetLocked ( false );

            Minigame.NotifyProgress ( );
            Minigame.NotifySuccess ( );
        }

        OnIngredientsChanged?.Invoke ( );

        if ( wasProcessing == true )
            OnStateChanged?.Invoke ( State );

        return true;
    }

    /// <summary>
    /// 재료 등록/실행 상태 초기화
    /// </summary>
    void ClearIngredients ( )
    {
        _cancelAttemptPending = false;

        _ingredients.Clear ( );

        EndProduction ( );

        //완성품은 비활성화돼도 데이터와 완료 상태 유지
        if ( State != ProductionState.Completed )
            _model?.Reset ( );

        //진행 정리 후 빈 그릇은 다시 재료 배치 허용
        if ( _cook != null )
            _cook.SetIngredientPlacement ( State != ProductionState.Completed );

        OnIngredientsChanged?.Invoke ( );
    }


    /// <summary>
    /// 재료의 현재 투입 수량 조회
    /// </summary>
    /// <param name="data">재료 데이터</param>
    /// <returns>현재 투입 수량</returns>
    public int GetIngredientCount ( ItemData data )
    {
        if ( data == null )
            return 0;

        if ( _model != null )
            return _model.GetIngredientCount ( data );

        return _ingredients.GetCount ( data );
    }

    /// <summary>
    /// 지정 재료의 필요 수량 조회
    /// </summary>
    /// <param name="data">확인할 재료 데이터</param>
    /// <returns>필요 수량</returns>
    public int GetRequiredCount ( ItemData data )
    {
        return IsReady == true
            ? _model.GetRequiredCount ( data )
            : 0;
    }

    /// <summary>
    /// 지정 번호의 재료 진행 정보 조회
    /// </summary>
    /// <param name="index">레시피 재료 번호</param>
    /// <param name="progress">재료 진행 정보</param>
    /// <returns>재료 진행 정보 확인 여부</returns>
    public bool TryGetIngredientProgress (
        int index , out ProduceIngredientData progress )
    {
        progress = default;

        if ( IsReady == false || _recipe == null )
            return false;

        IngredientRequirement requirement = _recipe.GetIngredient ( index );

        if ( requirement == null || requirement.Item == null )
            return false;

        progress = new ProduceIngredientData (
            requirement.Item ,
            _model.GetIngredientCount ( requirement.Item ) ,
            requirement.RequiredCount );

        return true;
    }


    /// <summary>
    /// 재료 등록 가능 여부 확인
    /// </summary>
    /// <param name="ingredient">등록할 재료</param>
    /// <param name="target">재료 배치 대상</param>
    /// <returns>등록 가능 여부</returns>
    public bool CanAddIngredient (
        ProductIngredient ingredient , PlacementTarget target )
    {
        //재료를 등록할 수 없으면 종료
        if ( CanReceiveIngredients == false )
            return false;

        //배치 대상이 같지 않으면 종료
        if ( target != _cook.IngredientTarget )
            return false;

        return ingredient != null &&
               ingredient.isActiveAndEnabled == true &&
               ingredient.Data != null;
    }

    #endregion

    #region ----- 제작 -----

    /// <summary>
    /// 제작 시작
    /// </summary>
    /// <param name="result">재료 조합 판정</param>
    /// <returns>제작 시작 여부</returns>
    public bool TryBegin ( out ProductionStartResult result )
    {
        return TryBegin ( 1f , out result );
    }

    /// <summary>
    /// 제작 시작
    /// </summary>
    /// <param name="multiplier">제작 유형에 맞는 효과 배율</param>
    /// <param name="result">재료 조합 판정</param>
    /// <returns>제작 시작 여부</returns>
    public bool TryBegin (
        float multiplier , out ProductionStartResult result )
    {
        result = ProductionStartResult.Unavailable;

        if ( _isBeginning == true || _consumption != null || IsActive == false )
            return false;

        if ( float.IsNaN ( multiplier ) == true ||
             float.IsInfinity ( multiplier ) == true ||
             multiplier < 1f )
            return false;

        //시작 처리에 들어가기 전에 조건 확인
        bool canBegin = CanBeginProduction ( out _ );       //시작 가능 여부

        _isBeginning = true;

        try
        {
            //실패한 재정렬도 시작 요청마다 재시도하고, 완료 알림 후 시작 조건 재확인
            if ( canBegin == true )
            {
                if ( _cook.TryRestackIngredients ( out string reason ) == false )
                {
                    Debug.LogWarning ( $"[제작 시작 실패] 재료 재정렬: {reason}" , _cook );
                    canBegin = false;
                }
                else
                {
                    canBegin = CheckBeginProduction ( out _ );
                }
            }

            if ( canBegin == false )
            {
                //빈 그릇의 시작 결과 유지
                if ( IsReady == true &&
                     State == ProductionState.Preparing &&
                     _model.IngredientCount == 0 )
                {
                    result = ProductionStartResult.MissingIngredient;
                }

                OnStartResult?.Invoke ( result );
                return false;
            }

            bool started = _model.TryBegin ( _consumableIds , out result );

            if ( started == true )
            {
                _isProductionActive = true;
                Minigame.SetDifficultyMultiplier ( multiplier );
                _ingredients.SetLocked ( true );

                Minigame.BeginRound ( );
                Minigame.NotifyProgress ( );
                Minigame.NotifySuccess ( );
                OnStateChanged?.Invoke ( State );
            }

            OnStartResult?.Invoke ( result );

            return started;
        }
        finally
        {
            //제작 시작 처리 해제
            _isBeginning = false;
        }
    }

    /// <summary>
    /// 제작 종료 후 효과 복원
    /// </summary>
    void EndProduction ( )
    {
        bool notify = _isProductionActive;      //종료 알림 여부
        _isProductionActive = false;

        //진행 중지 및 기본값 복원
        Minigame.Stop ( );
        Minigame.ClearDifficultyEffect ( );

        if ( notify == true )
            OnProductionEnded?.Invoke ( );
    }


    /// <summary>
    /// 미니게임 시간 전달
    /// </summary>
    /// <param name="deltaTime">경과시간</param>
    public void Tick ( float deltaTime )
    {
        Minigame.Tick ( deltaTime );
    }

    /// <summary>
    /// 진행 정리
    /// </summary>
    public void Disable ( )
    {
        //시도 중단보다 종료 정리 우선
        _cancelAttemptPending = false;

        if ( _consumption != null )
            _consumption.RequestStop ( );
        else
            ClearIngredients ( );
    }

    /// <summary>
    /// 풀 반환 전에 제작 진행과 완성 결과 정리
    /// </summary>
    /// <param name="reason">정리 실패 사유</param>
    /// <returns>반환 준비 성공 여부</returns>
    public bool TryPreparePoolReturn ( out string reason )
    {
        reason = string.Empty;

        if ( _isBeginning == true )
        {
            reason = "제작 시작 처리가 진행 중입니다.";
            return false;
        }

        //소비 종료 콜백을 마친 다음 기존 반환 대기에서 재시도
        if ( _consumption != null )
        {
            _consumption.RequestStop( );
            reason = "제작 재료 소비 정리를 기다리고 있습니다.";
            return false;
        }

        ProductionResult previousResult = _completedResult;		//이전 완성 결과

        ClearIngredients( );
        _consumableIds.Clear( );
        _cook.ClearContent( );
        _cook.ApplyResultItem( null );
        _completedResult = null;
        _model?.Reset( );
        _recipe = null;
        _model = null;
        _cook.SetIngredientPlacement( true );

        NotifyProductionChanged( );
        if ( previousResult != null )
            OnReset?.Invoke( previousResult );

        return true;
    }

    /// <summary>
    /// 제작 시도 중단
    /// </summary>
    /// <returns>중단/예약 여부</returns>
    public bool TryCancelAttempt ( )
    {
        if ( State != ProductionState.Processing )
            return false;

        if ( _consumption != null )
        {
            //소비 종료까지 중단 예약
            _cancelAttemptPending = true;
            _consumption.RequestStop ( );
            return true;
        }

        if ( _model.TryCancelAttempt ( ) == false )
            return false;

        EndProduction ( );
        _consumableIds.Clear ( );
        _ingredients.SetLocked ( false );

        Minigame.NotifyProgress ( );
        Minigame.NotifySuccess ( );

        OnStateChanged?.Invoke ( State );

        return true;
    }

    /// <summary>
    /// 제작 미니게임 결과 반영
    /// </summary>
    /// <param name="checkedResult">미니게임 판정</param>
    /// <param name="result">반영 후 미니게임 판정</param>
    /// <param name="progress">제작 진행 결과</param>
    /// <returns>성공 또는 실패 처리 반영 여부</returns>
    public bool ApplyMinigameResult (
        ProduceMinigameResult checkedResult ,
        out ProduceMinigameResult result ,
        out ProductionProgressResult progress )
    {
        result = checkedResult;
        progress = ProductionProgressResult.Unavailable;

        //소비 중이거나 제작이 중단된 대상은 판정 반영 불가
        if ( _consumption != null ||
             IsActive == false ||
             State != ProductionState.Processing )
            return false;

        //잘못된 재료 조합은 마지막 성공을 실패로 변경
        if ( result == ProduceMinigameResult.Succeeded &&
             _model.IsForcedFailure == true &&
             _model.WillCompleteOnNextSuccess == true )
        {
            result = ProduceMinigameResult.Failed;
        }

        //실패 판정은 재료 소비와 제작 중단 처리
        if ( result == ProduceMinigameResult.Failed )
        {
            if ( TryCompleteFailure ( ) == true )
                return true;

            //실패 처리를 반영하지 못했으므로 판정 무효화
            result = ProduceMinigameResult.Unavailable;
            return false;
        }

        //성공과 실패 외 판정은 진행에 반영하지 않음
        if ( result != ProduceMinigameResult.Succeeded )
        {
            result = ProduceMinigameResult.Unavailable;
            return false;
        }

        //성공 횟수 반영 또는 마지막 성공의 소비 대상 준비
        if ( _model.TryAddSuccess ( _consumableIds , out progress ) == false )
        {
            result = ProduceMinigameResult.Unavailable;
            return false;
        }

        //마지막 성공은 내용물 생성과 재료 소비까지 끝나야 완료
        if ( progress == ProductionProgressResult.Completed &&
             TryCompleteMinigame ( ) == false )
        {
            //완료 준비 결과는 유지하고 이번 판정만 무효화
            result = ProduceMinigameResult.Unavailable;
            return false;
        }

        return true;
    }

    /// <summary>
    /// 현재 재료와 소비 결과 반영 기능 연결
    /// </summary>
    /// <returns>새 소비 실행</returns>
    IngredientConsumption CreateConsumption ( )
    {
        return new IngredientConsumption (
            _cook , _ingredients.Items , RemoveConsumptionIngredient ,
            _cook.Item , _cook.IngredientRoot , _cook.IngredientTarget );
    }

    /// <summary>
    /// 제작 실패 처리
    /// </summary>
    /// <returns>실패 처리 여부</returns>
    public bool TryCompleteFailure ( )
    {
        if ( _consumption != null || IsActive == false || IsReady == false )
            return false;

        //소비할 재료 선정
        if ( _model.TrySelectFailureIngredients ( _consumableIds ) == false )
            return false;

        //소비 대상 가져오기
        _consumption = CreateConsumption ( );

        //결과 반영 실패/재료 소비 준비 실패 시
        if ( _model.CanApplyFailure ( _consumableIds ) == false ||
            _consumption.TryPrepare ( _consumableIds ) == false )
        {
            Minigame.BeginRound ( );
            Minigame.NotifyProgress ( );
            EndConsume ( false );

            return false;
        }

        //소비 처리 실패 시
        if ( _consumption.Commit ( ) == false )
        {
            EndConsume ( false );
            return false;
        }

        if ( _model.ApplyFailure ( _consumableIds ) == false )
        {
            EndConsume ( false );
            return false;
        }

        EndProduction ( );

        // 소비되지 않은 재료는 다시 집을 수 있도록 복구
        _ingredients.SetLocked ( false );
        NotifyProductionChanged ( );
        EndConsume ( true );

        return true;
    }

    /// <summary>
    /// 제작 완료 처리
    /// </summary>
    /// <returns>제작 완료 처리 여부</returns>
    bool TryCompleteMinigame ( )
    {
        if ( _consumption != null || IsActive == false )
            return false;

        if ( _model.CanApplyCompletion ( _consumableIds ) == false )
            return false;

        _consumption = CreateConsumption ( );

        //내용물 생성 콜백 후 제작 상태 재확인
        if ( _cook.TryShowContent ( _recipe.ResultPrefab ) == false ||
             _consumption.StopRequested ||
             IsActive == false ||
             _cook.HasContent == false ||
             _model.CanApplyCompletion ( _consumableIds ) == false ||
             _consumption.TryPrepare ( _consumableIds ) == false )
        {
            _cook?.ClearContent ( );
            EndConsume ( false );

            return false;
        }

        if ( _consumption.Commit ( ) == false ||
             _cook.HasContent == false ||
             _model.ApplyCompletion ( _consumableIds ) == false )
        {
            _cook.ClearContent ( );
            EndConsume ( false );

            return false;
        }

        CompleteProduct ( );
        EndConsume ( true );

        return true;
    }

    /// <summary>
    /// 제작 완료 확정
    /// </summary>
    void CompleteProduct ( )
    {
        EndProduction ( );
        _cook.ApplyResultItem ( _model.ResultItem );

        //완성 이후 추가 재료 배치 차단
        _cook.SetIngredientPlacement ( false );

        ProductionResult result = new (
            Guid.NewGuid ( ).ToString ( "N" ) ,
            _recipe , _model.ResultItem , _cook.Item , _cook );

        //이벤트를 놓쳐도 재구독 시 복구할 수 있도록 먼저 저장
        _completedResult = result;

        _cook.RecordProductionDebug ( $"완료/결과 {result.Id}" );

        OnIngredientsChanged?.Invoke ( );
        OnStateChanged?.Invoke ( State );

        OnCompleted?.Invoke ( result );
    }

    #endregion

    #region ----- 내용물 -----

    /// <summary>
    /// 내용물 폐기
    /// </summary>
    /// <param name="reason">실패 사유</param>
    /// <returns>폐기 처리 여부</returns>
    public bool TryDispose ( out string reason )
    {
        reason = string.Empty;

        if ( _consumption != null || IsActive == false )
        {
            reason = "소비 처리 중이거나 비활성화된 그릇입니다.";
            return false;
        }

        if ( HasDiscardable == false )
        {
            reason = "폐기할 내용물이 없습니다.";
            return false;
        }

        if ( _cook.Item == null || _cook.Item.isActiveAndEnabled == false )
        {
            reason = "활성화된 그릇 아이템이 필요합니다.";
            return false;
        }

        if ( CanDispose == true )
            return ResetCompleted ( true , out reason );

        if ( _model != null && _ingredients.Count != _model.IngredientCount )
        {
            reason = "그릇의 재료 등록 상태를 확인해 주세요.";
            return false;
        }

        //제작 전 또는 제작 중인 그릇은 담긴 재료 전체를 소비
        _consumableIds.Clear ( );
        _consumableIds.AddRange ( _ingredients.Items.Keys );

        _consumption = CreateConsumption ( );

        if ( _consumption.TryPrepare ( _consumableIds ) == false )
        {
            reason = "담긴 재료를 소비할 수 없습니다.";

            EndConsume ( false );
            return false;
        }

        if ( _consumption.Commit ( ) == false )
        {
            reason = "재료 소비 확정이 중단됐습니다. " +
                "실제 성공분만 반영했습니다.";

            EndConsume ( false );
            return false;
        }

        EndProduction ( );
        _model?.Reset ( );

        _cook.SetIngredientPlacement ( true );
        _cook.RecordProductionDebug ( "담긴 재료 전체 폐기/Preparing" );

        NotifyProductionChanged ( );

        EndConsume ( true );
        return true;
    }

    /// <summary>
    /// 완성품 초기화
    /// </summary>
    /// <param name="reason">초기화 실패 사유</param>
    /// <returns>초기화 완료 여부</returns>
    public bool TryReset ( out string reason )
    {
        return ResetCompleted ( false , out reason );
    }

    /// <summary>
    /// 완성품 초기화
    /// </summary>
    /// <param name="allowHeldItem">보유 중 폐기 허용 여부</param>
    /// <param name="reason">복구 실패 사유</param>
    /// <returns>복구 완료 여부</returns>
    bool ResetCompleted ( bool allowHeldItem , out string reason )
    {
        reason = string.Empty;

        if ( CanDispose == false )
        {
            reason = "초기화할 완성품이 없습니다.";
            return false;
        }

        if ( _cook.Item == null ||
             _cook.Item.isActiveAndEnabled == false )
        {
            reason = "활성화된 완성품 아이템이 필요합니다.";
            return false;
        }

        if ( allowHeldItem == false && _cook.IsBowlHeld == true )
        {
            reason = "완성품을 내려놓은 뒤 초기화해야 합니다.";
            return false;
        }

        _cook.ClearContent ( );

        ProductionResult previousResult = _completedResult;

        _cook.ApplyResultItem ( null );
        _completedResult = null;

        EndProduction ( );
        _model.Reset ( );

        _cook.SetIngredientPlacement ( true );

        _cook.RecordProductionDebug (
            $"완성 내용물 폐기/이전 결과 {previousResult.Id}" );

        NotifyProductionChanged ( );

        OnReset?.Invoke ( previousResult );

        return true;
    }

    /// <summary>
    /// 제작 상태 변경 알림
    /// </summary>
    void NotifyProductionChanged ( )
    {
        Minigame.NotifyProgress ( );
        Minigame.NotifySuccess ( );

        OnIngredientsChanged?.Invoke ( );
        OnStateChanged?.Invoke ( State );
    }

    #endregion

    #region ----- 소비 -----


    /// <summary>
    /// 소비 종료 정리
    /// </summary>
    /// <param name="modelApplied">정상 소비 결과 반영 여부</param>
    void EndConsume ( bool modelApplied )
    {
        IngredientConsumption consumption = _consumption;

        if ( consumption.End ( modelApplied ) == true )
            RefreshInterruptedConsumption ( );

        //중복 예약 방지 초기화
        _consumption = null;
        _consumableIds.Clear ( );

        if ( ( consumption.StopRequested &&
            _cancelAttemptPending == false ) ||
             IsActive == false )
        {
            //종료 시 등록 정리
            ClearIngredients ( );
        }
        //중단 시 재료 보존
        else if ( _cancelAttemptPending == true )
        {
            _cancelAttemptPending = false;

            if ( TryCancelAttempt ( ) == false )
            {
                //남은 재료는 잠금 해제
                EndProduction ( );
                _ingredients.SetLocked ( false );
            }
        }
        else
        {
            _ingredients.SetLocked (
                State == ProductionState.Processing );
        }
    }

    /// <summary>
    /// 소비/손실 재료 제거
    /// </summary>
    /// <param name="instanceId">제거할 재료 ID</param>
    /// <param name="updateModel">모델 재료 제거 여부</param>
    void RemoveConsumptionIngredient (
        int instanceId , bool updateModel )
    {
        _ingredients.Unregister ( instanceId );

        if ( updateModel == true )
            _model?.TryRemoveLostIngredient ( instanceId );
    }

    /// <summary>
    /// 소비 중단 상태 갱신
    /// </summary>
    void RefreshInterruptedConsumption ( )
    {
        //재료 손실로 취소된 제작은 재시작하지 않음
        if ( _cook != null && _cook.isActiveAndEnabled &&
             State == ProductionState.Processing &&
             _consumption.StopRequested == false )
            Minigame.BeginRound ( );
        else
            EndProduction ( );

        NotifyProductionChanged ( );
    }

    #endregion

    #region ----- 검사 -----

    /// <summary>
    /// 등록된 제작 대상 활성 조건 확인
    /// </summary>
    /// <returns>대상 활성 여부</returns>
    bool CheckIsActive ()
    {
        return _cook != null && _cook.Owner != null &&
               _cook.isActiveAndEnabled && _cook.IsInitialized;
    }

    /// <summary>
    /// 레시피와 미니게임 준비 확인
    /// </summary>
    /// <returns>제작 준비 여부</returns>
    bool CheckIsReady ()
    {
        return _model != null && _model.IsReady && Minigame.IsReady;
    }

    /// <summary>
    /// 새 재료 등록 조건 확인
    /// </summary>
    /// <returns>재료 등록 가능 여부</returns>
    bool CheckCanReceiveIngredients ()
    {
        return IsActive && _consumption == null &&
               State == ProductionState.Preparing;
    }

    /// <summary>
    /// 미니게임 진행 조건 확인
    /// </summary>
    /// <returns>미니게임 진행 가능 여부</returns>
    bool CheckCanRunMinigame ()
    {
        return IsActive && _consumption == null &&
               IsReady && State == ProductionState.Processing;
    }

    /// <summary>
    /// 완성품 폐기 조건 확인
    /// </summary>
    /// <returns>폐기 가능 여부</returns>
    bool CheckCanDispose ()
    {
        return IsActive && _consumption == null && IsReady &&
               State == ProductionState.Completed && _completedResult != null;
    }

    /// <summary>
    /// 현재 소비에 준비된 재료인지 확인
    /// </summary>
    /// <param name="ingredient">확인할 재료</param>
    /// <returns>소비 준비 여부</returns>
    public bool IsIngredientPrepared ( ProductIngredient ingredient )
    {
        return _consumption != null && _consumption.Contains( ingredient );
    }

    /// <summary>
    /// 현재 소비를 적용 중인 재료인지 확인
    /// </summary>
    /// <param name="ingredient">확인할 재료</param>
    /// <returns>소비 적용 중 여부</returns>
    public bool IsApplyingIngredient ( ProductIngredient ingredient )
    {
        return _consumption != null && _consumption.IsApplying( ingredient );
    }

    /// <summary>
    /// 제작 시작 조건 확인
    /// </summary>
    /// <param name="reason">시작 불가 사유</param>
    /// <returns>시작 가능 여부</returns>
    public bool CanBeginProduction ( out string reason )
    {
        if ( _isBeginning == true )
        {
            reason = "제작 시작 처리 중입니다.";
            return false;
        }

        return CheckBeginProduction ( out reason );
    }

    /// <summary>
    /// 제작 대상과 재료의 시작 조건 확인
    /// </summary>
    /// <param name="reason">시작 불가 사유</param>
    /// <returns>시작 조건 충족 여부</returns>
    bool CheckBeginProduction ( out string reason )
    {
        reason = string.Empty;

        if ( IsActive == false )
        {
            reason = "제작 대상이 비활성 상태입니다.";
            return false;
        }

        if ( _consumption != null )
        {
            reason = "재료 소비 처리 중입니다.";
            return false;
        }

        if ( _recipe == null )
        {
            reason = "레시피를 선택하지 않았습니다.";
            return false;
        }

        if ( IsReady == false )
        {
            reason = "제작 설정이 준비되지 않았습니다.";
            return false;
        }

        if ( State != ProductionState.Preparing )
        {
            reason = "제작 준비 상태가 아닙니다.";
            return false;
        }

        //실제 시작에 사용하는 모델 기준으로 확인
        if ( _model.CanBegin == false )
        {
            reason = "등록된 재료가 없습니다.";
            return false;
        }

        return true;
    }

    #endregion
}
