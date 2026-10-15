using System;
using System.Collections.Generic;

/// <summary>
/// 제작 모델 - 투입 재료, 조합 검증, 제작
/// </summary>
public class ProductionModel
{
    ItemData _resultItem;		//완성 결과
    string _recipeId;		//레시피 ID
    int _requiredSuccessCount;		//필요 성공 횟수

    int _failureLossCount;		//실패 시 소실 수량
    int _successCount;		//현재 성공 횟수
    bool _isReady;		//초기화 완료 여부
    string _failureReason = string.Empty;		//초기화 실패 사유

    ProductionState _state = ProductionState.Preparing;		//제작 상태
    ProductionStartResult _startResult =
        ProductionStartResult.Unavailable;		//현재 제작 시작 판정

    /// <summary>
    /// 재료 요구량 딕셔너리(재료, 필요 수량)
    /// </summary>
    Dictionary<ItemData, int> _requirements = new( );
    /// <summary>
    /// 투입량 딕셔너리(재료, 투입 수량)
    /// </summary>
    Dictionary<ItemData, int> _ingredientCounts = new( );
    /// <summary>
    /// 재료 목록
    /// </summary>
    List<ProductIngredientEntry> _ingredients = new( );
    /// <summary>
    /// 등록 재료 ID 집합
    /// </summary>
    HashSet<int> _ingredientInstanceIds = new( );


    /// <summary>
    /// 초기화 완료 여부
    /// </summary>
    public bool IsReady => _isReady;

    /// <summary>
    /// 초기화 실패 사유
    /// </summary>
    public string FailureReason => _failureReason;

    /// <summary>
    /// 현재 제작의 실패 확정 여부
    /// </summary>
    public bool IsForcedFailure => CheckForcedFailure( );

    /// <summary>
    /// 레시피 ID
    /// </summary>
    public string RecipeId => _recipeId;

    /// <summary>
    /// 완성 결과
    /// </summary>
    public ItemData ResultItem => _resultItem;

    /// <summary>
    /// 제작 상태
    /// </summary>
    public ProductionState State => _state;

    /// <summary>
    /// 현재 투입 재료 수
    /// </summary>
    public int IngredientCount => _ingredients.Count;

    /// <summary>
    /// 현재 성공 횟수
    /// </summary>
    public int SuccessCount => _successCount;

    /// <summary>
    /// 필요한 성공 횟수
    /// </summary>
    public int RequiredSuccessCount => _requiredSuccessCount;

    /// <summary>
    /// 다음 제작 미니게임 성공 시 완성 여부
    /// </summary>
    public bool WillCompleteOnNextSuccess =>
        _isReady == true &&
        _state == ProductionState.Processing &&
        _successCount + 1 >= _requiredSuccessCount;

    /// <summary>
    /// 조리 시작 요청 가능 여부
    /// </summary>
    public bool CanBegin => _isReady == true &&
        _state == ProductionState.Preparing &&
        IngredientCount > 0;


    /// <summary>
    /// 제작 상태 생성
    /// </summary>
    /// <param name="recipe">제작 레시피</param>
    /// <param name="reqSuccessCount">완성에 필요한 성공 횟수</param>
    /// <param name="lossCount">실패 시 소실할 재료 수</param>
    public ProductionModel (
        RecipeData recipe, int reqSuccessCount, int lossCount )
    {
        //설정 검사
        if ( CheckSettings(
            recipe, reqSuccessCount,
            lossCount, out _failureReason ) == false )
            return;

        _recipeId = recipe.RecipeId;
        _resultItem = recipe.ResultItem;
        _requiredSuccessCount = reqSuccessCount;
        _failureLossCount = lossCount;

        for ( int i = 0; i < recipe.IngredientCount; i++ )
        {
            //필요한 재료 조회
            IngredientRequirement requirement = recipe.GetIngredient( i );

            //재료 요구량 딕셔너리에 추가
            _requirements.Add( requirement.Item, requirement.RequiredCount );
        }

        _isReady = true;
    }


    #region ----- 재료 등록 -----

    /// <summary>
    /// 제작 대상에 재료 등록
    /// </summary>
    /// <param name="instanceId">재료 객체 ID</param>
    /// <param name="data">재료 데이터</param>
    /// <returns>등록 여부</returns>
    public bool TryAddIngredient (
        int instanceId, ItemData data )
    {
        //준비 상태가 아니면 종료
        if ( _isReady == false || _state != ProductionState.Preparing )
            return false;

        //해시셋에 재료 객체 ID 추가
        if ( data == null ||
             _ingredientInstanceIds.Add( instanceId ) == false )
            return false;

        //재료 목록에 추가
        _ingredients.Add( new ProductIngredientEntry( instanceId , data ) );

        //재료 수량 가져오기
        if ( _ingredientCounts.TryGetValue( data, out int count ) == false )
            count = 0;

        _ingredientCounts [ data ] = count + 1;
        return true;
    }

    /// <summary>
    /// 제작 대상에서 재료 제거
    /// </summary>
    /// <param name="instanceId">재료 객체 ID</param>
    /// <returns>제거 여부</returns>
    public bool TryRemoveIngredient ( int instanceId )
    {
        if ( _isReady == false || _state != ProductionState.Preparing )
            return false;

        for ( int i = 0; i < _ingredients.Count; i++ )
        {
            //대상이 아니면 넘어가기
            if ( _ingredients [ i ].InstanceId != instanceId )
                continue;

            RemoveIngredientAt( i );
            return true;
        }

        return false;
    }

    /// <summary>
    /// 손실 재료 제거/진행 중 제작 중단
    /// </summary>
    /// <param name="instanceId">손실 재료 객체 ID</param>
    /// <returns>재료 제거 여부</returns>
    public bool TryRemoveLostIngredient ( int instanceId )
    {
        if ( _isReady == false ) return false;

        //준비 중, 진행 중 상태가 아니면 종료
        if ( _state != ProductionState.Preparing &&
               _state != ProductionState.Processing )
            return false;

        for ( int i = 0; i < _ingredients.Count; i++ )
        {
            //손실 재료가 아니면 넘어가기
            if ( _ingredients [ i ].InstanceId != instanceId )
                continue;

            //손실된 재료의 등록과 종류별 수량만 제거
            RemoveIngredientAt( i );

            if ( _state == ProductionState.Processing )
            {
                //현재 제작 회차를 취소하고 남은 재료는 유지
                _successCount = 0;
                _startResult = ProductionStartResult.Unavailable;
                _state = ProductionState.Preparing;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// 현재 투입된 지정 재료 수 조회
    /// </summary>
    /// <param name="data">확인할 재료 데이터</param>
    /// <returns>현재 투입 수량</returns>
    public int GetIngredientCount ( ItemData data )
    {
        if ( data == null ||
             _ingredientCounts.TryGetValue( data, out int count ) == false )
            return 0;

        return count;
    }

    /// <summary>
    /// 레시피 지정 재료 요구량 조회
    /// </summary>
    /// <param name="data">확인할 재료 데이터</param>
    /// <returns>필요 수량, Recipe에 없으면 0</returns>
    public int GetRequiredCount ( ItemData data )
    {
        if ( data == null ||
             _requirements.TryGetValue( data, out int count ) == false )
            return 0;

        return count;
    }

    #endregion

    #region ----- 제작 -----

    /// <summary>
    /// 현재 조합을 검사하고 제작 시작
    /// </summary>
    /// <param name="consumableIds">소비 대상 재료 ID 목록</param>
    /// <param name="result">제작 시작 판정</param>
    /// <returns>제작 중 진입 여부</returns>
    public bool TryBegin (
        List<int> consumableIds,
        out ProductionStartResult result )
    {
        //소비 대상이 없으면 종료
        if ( consumableIds == null )
        {
            result = ProductionStartResult.Unavailable;
            return false;
        }

        consumableIds.Clear( );

        //재료가 없거나 현재 제작을 시작할 수 없는 상태면 종료
        if ( CanBegin == false )
        {
            result =
                _isReady == true &&
                _state == ProductionState.Preparing &&
                IngredientCount == 0
                    ? ProductionStartResult.MissingIngredient
                    : ProductionStartResult.Unavailable;

            return false;
        }

        //선택 시 저장한 요구량으로 목록 표시와 같은 조합 규칙 적용
        result = RecipeIngredientChecker.Check(
            _requirements, GetIngredientCount, IngredientCount );

        //조합이 잘못돼도 미니게임은 동일하게 시작
        _startResult = result;
        _state = ProductionState.Processing;
        _successCount = 0;

        return true;
    }

    /// <summary>
    /// 제작 시도 초기화
    /// </summary>
    /// <returns>진행 중 시도 종료 여부</returns>
    public bool TryCancelAttempt ()
    {
        //진행 중 상태가 아니면 종료
        if ( _state != ProductionState.Processing )
            return false;

        _successCount = 0;
        _startResult = ProductionStartResult.Unavailable;
        _state = ProductionState.Preparing;

        return true;
    }

    /// <summary>
    /// 제작 성공 진행/완료 준비
    /// </summary>
    /// <param name="consumableIds">완료 소비 대상 ID 목록</param>
    /// <param name="result">진행 반영/완료 준비 결과</param>
    /// <returns>진행 반영/완료 준비 여부</returns>
    public bool TryAddSuccess (
        List<int> consumableIds,
        out ProductionProgressResult result )
    {
        //기본값 설정
        result = ProductionProgressResult.Unavailable;

        //대상이 없으면 종료
        if ( consumableIds == null )
            return false;

        //중복 추가 방지 초기화
        consumableIds.Clear( );

        //제작 진행 중이 아니면 종료
        if ( _isReady == false || _state != ProductionState.Processing )
            return false;

        //완성 내용물 생성과 재료 소비 전에는 마지막 성공을 확정하지 않음
        if ( WillCompleteOnNextSuccess == true )
        {
            //완료 시 소비할 재료 전체 선택
            SelectAllIngredients( consumableIds );
            result = ProductionProgressResult.Completed;

            return true;
        }

        _successCount++;
        result = ProductionProgressResult.Progressed;

        return true;
    }

    /// <summary>
    /// 제작 실패 시 조합에 따라 소비할 재료 선정
    /// </summary>
    /// <param name="consumableIds">소비 대상 재료 ID 목록</param>
    /// <returns>소비 대상 선정 여부</returns>
    public bool TrySelectFailureIngredients ( List<int> consumableIds )
    {
        if ( consumableIds == null )
            return false;

        //중복 추가 방지 초기화
        consumableIds.Clear( );

        if ( _isReady == false || _state != ProductionState.Processing )
            return false;

        //잘못된 재료 포함 또는 재료 부족이면 전체 소비
        if ( IsForcedFailure == true )
        {
            SelectAllIngredients( consumableIds );
        }
        else
        {
            //정상 조합의 미니게임 실패는 최근 정상 재료부터 소비
            SelectRecentIngredients(
                ingredient => _requirements.ContainsKey( ingredient.Data ),
                _failureLossCount,
                consumableIds );
        }

        return consumableIds.Count > 0;
    }

    /// <summary>
    /// 제작 실패 반영
    /// </summary>
    /// <param name="consumableIds">소비가 완료된 재료 ID 목록</param>
    /// <returns>실패 결과 반영 여부</returns>
    public bool ApplyFailure ( List<int> consumableIds )
    {
        if ( CanApplyFailure( consumableIds ) == false )
            return false;

        RemoveIngredients( consumableIds );

        _successCount = 0;
        _startResult = ProductionStartResult.Unavailable;
        _state = ProductionState.Preparing;

        return true;
    }

    /// <summary>
    /// 제작 완료 반영
    /// </summary>
    /// <param name="consumableIds">소비 완료 재료 ID 목록</param>
    /// <returns>완료 결과 반영 여부</returns>
    public bool ApplyCompletion ( List<int> consumableIds )
    {
        if ( CanApplyCompletion( consumableIds ) == false )
            return false;

        _successCount++;
        RemoveIngredients( consumableIds );

        _startResult = ProductionStartResult.Unavailable;
        _state = ProductionState.Completed;

        return true;
    }

    /// <summary>
    /// 모든 실행 상태 초기화
    /// </summary>
    public void Reset ()
    {
        _ingredients.Clear( );
        _ingredientCounts.Clear( );
        _ingredientInstanceIds.Clear( );

        _successCount = 0;
        _startResult = ProductionStartResult.Unavailable;
        _state = ProductionState.Preparing;
    }

    /// <summary>
    /// 제작 실패 반영 가능 여부
    /// </summary>
    /// <param name="consumableIds">소비할 재료 ID 목록</param>
    /// <returns>실패 결과 반영 가능 여부</returns>
    public bool CanApplyFailure ( List<int> consumableIds )
    {
        return _isReady && _state == ProductionState.Processing &&
            HasIngredients( consumableIds );
    }

    /// <summary>
    /// 제작 완료 반영 가능 여부
    /// </summary>
    /// <param name="consumableIds">소비할 전체 재료 ID 목록</param>
    /// <returns>완료 결과 반영 가능 여부</returns>
    public bool CanApplyCompletion ( List<int> consumableIds )
    {
        return _isReady && _state ==
            ProductionState.Processing &&
            IsForcedFailure == false &&
            WillCompleteOnNextSuccess &&
            HasSameIngredients( consumableIds );
    }

    /// <summary>
    /// 조합 실패 확정 여부
    /// </summary>
    /// <returns>실패 확정 여부</returns>
    bool CheckForcedFailure ()
    {
        return _state == ProductionState.Processing &&
               ( _startResult == ProductionStartResult.MissingIngredient ||
                 _startResult == ProductionStartResult.InvalidIngredient );
    }

    #endregion

    #region ----- 재료 소비 -----

    /// <summary>
    /// 조건에 맞는 최근 재료부터 지정 수량 선택
    /// </summary>
    /// <param name="condition">소비 대상 조건</param>
    /// <param name="count">최대 소비 수량</param>
    /// <param name="consumableIds">선택한 재료 ID 목록</param>
    void SelectRecentIngredients (
        Predicate<ProductIngredientEntry> condition,
        int count, List<int> consumableIds )
    {
        for ( int i = _ingredients.Count - 1; i >= 0 && consumableIds.Count < count; i-- )
        {
            ProductIngredientEntry ingredient = _ingredients [ i ];

            if ( condition( ingredient ) == false )
                continue;

            consumableIds.Add( ingredient.InstanceId );
        }
    }

    /// <summary>
    /// 투입 재료 전체 선택
    /// </summary>
    /// <param name="consumableIds">선택한 재료 ID 목록</param>
    void SelectAllIngredients ( List<int> consumableIds )
    {
        for ( int i = 0; i < _ingredients.Count; i++ )
            consumableIds.Add( _ingredients [ i ].InstanceId );
    }

    /// <summary>
    /// 소비 완료 재료 제거
    /// </summary>
    /// <param name="consumableIds">제거할 재료 ID 목록</param>
    void RemoveIngredients ( List<int> consumableIds )
    {
        for ( int i = _ingredients.Count - 1; i >= 0; i-- )
        {
            if ( consumableIds.Contains( _ingredients [ i ].InstanceId ) == true )
                RemoveIngredientAt( i );
        }
    }

    /// <summary>
    /// 지정 위치의 재료/집계 제거
    /// </summary>
    /// <param name="index">제거할 재료 인덱스</param>
    void RemoveIngredientAt ( int index )
    {
        //재료 가져오기
        ProductIngredientEntry ingredient = _ingredients [ index ];

        //목록에서 제거
        _ingredients.RemoveAt( index );
        _ingredientInstanceIds.Remove( ingredient.InstanceId );

        //투입 수량 가져오기
        int count = _ingredientCounts [ ingredient.Data ] - 1;

        if ( count <= 0 )
            _ingredientCounts.Remove( ingredient.Data );
        else
            _ingredientCounts [ ingredient.Data ] = count;
    }

    /// <summary>
    /// 재료 보유 여부 확인
    /// </summary>
    /// <param name="consumableIds">확인할 재료 ID 목록</param>
    /// <returns>전체 재료 존재 여부</returns>
    bool HasIngredients ( List<int> consumableIds )
    {
        if ( consumableIds == null || consumableIds.Count == 0 )
            return false;

        HashSet<int> ids = new( consumableIds );

        if ( ids.Count != consumableIds.Count )
            return false;

        foreach ( int id in ids )
        {
            if ( _ingredientInstanceIds.Contains( id ) == false )
                return false;
        }

        return true;
    }

    /// <summary>
    /// 같은 재료인지 확인
    /// </summary>
    /// <param name="consumableIds">확인할 전체 재료 ID 목록</param>
    /// <returns>전체 재료 일치 여부</returns>
    bool HasSameIngredients ( List<int> consumableIds )
    {
        return consumableIds != null &&
               consumableIds.Count == _ingredients.Count &&
               HasIngredients( consumableIds );
    }

    #endregion

    #region ----- 검사 -----

    /// <summary>
    /// 레시피 및 제작 설정 검사
    /// </summary>
    /// <param name="recipe">제작 레시피</param>
    /// <param name="reqSuccessCount">필요 성공 횟수</param>
    /// <param name="lossCount">실패 시 소실 수량</param>
    /// <param name="reason">설정 오류 사유</param>
    /// <returns>설정 유효 여부</returns>
    public static bool CheckSettings (
        RecipeData recipe, int reqSuccessCount,
        int lossCount, out string reason )
    {
        reason = string.Empty;

        if ( recipe == null )
        {
            reason = "제작 Recipe가 없습니다.";
            return false;
        }

        if ( string.IsNullOrWhiteSpace( recipe.RecipeId ) )
        {
            reason = "제작 Recipe ID가 없습니다.";
            return false;
        }

        if ( recipe.ResultItem == null )
        {
            reason = "제작 결과 아이템이 없습니다.";
            return false;
        }

        if ( recipe.IngredientCount == 0 )
        {
            reason = "필요한 재료가 없습니다.";
            return false;
        }

        if ( reqSuccessCount <= 0 || lossCount <= 0 )
        {
            reason =
                "성공 횟수와 실패 소실 수량은 1 이상이어야 합니다.";
            return false;
        }

        HashSet<ItemData> ingredients = new( );

        for ( int i = 0; i < recipe.IngredientCount; i++ )
        {
            //재료 필요 수량 가져오기
            IngredientRequirement requirement = recipe.GetIngredient( i );

            if ( requirement == null || requirement.Item == null )
            {
                reason = $"{i + 1}번째 재료 설정이 비어 있습니다.";
                return false;
            }

            if ( ingredients.Add( requirement.Item ) == false )
            {
                reason =
                    $"재료 설정이 중복됩니다: {requirement.Item.ItemName}";
                return false;
            }
        }

        return true;
    }

    #endregion
}
