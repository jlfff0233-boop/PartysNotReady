using System;

/// <summary>
/// 레시피 입력 제어 - 제작 대상 유지, 재료 입력과 제작 요청 연결
/// </summary>
public class RecipeInputController
{
    Player _player;		//상호작용 주체
    ProductionManager _productPresenter;		//제작 목록
    ProduceHud _produceHud;     //제작 화면
    CookController _recipeTarget;		//열린 레시피 대상
    CookController _startedRecipeTarget;		//재료 입력 시작 대상

    /// <summary>
    /// 상위 입력 허용 상태 조회
    /// </summary>
    Func<bool> _canInput;

    /// <summary>
    /// 재료 입력 시작 대상 존재 여부
    /// </summary>
    public bool HasIngredientInput => _startedRecipeTarget != null;


    /// <summary>
    /// 제작 시작 요청 처리
    /// </summary>
    /// <param name="product">제작 대상</param>
    void HandleBeginRequested ( CookController product )
    {
        //입력 상태와 표시 중인 제작 대상 확인
        if ( _canInput( ) == false ||
             _player.IsActionBlocked == true ||
             product == null ||
             _productPresenter == null ||
             _produceHud.ActiveSession != product.Session )
            return;

        _productPresenter.TryBegin( product, _player );
    }

    /// <summary>
    /// 제작 입력과 화면 연결
    /// </summary>
    /// <param name="player">상호작용 주체</param>
    /// <param name="productPresenter">제작 목록</param>
    /// <param name="produceHud">제작 화면</param>
    /// <param name="canInput">상위 입력 허용 상태 조회</param>
    public RecipeInputController (
        Player player,
        ProductionManager productPresenter,
        ProduceHud produceHud,
        Func<bool> canInput )
    {
        _player = player;
        _productPresenter = productPresenter;
        _produceHud = produceHud;
        _canInput = canInput;
        _produceHud.OnBeginRequested += HandleBeginRequested;
    }

    /// <summary>
    /// 재료 입력 시작 대상 정리
    /// </summary>
    public void CancelIngredientInteract ()
    {
        _startedRecipeTarget = null;
    }

    /// <summary>
    /// 제작 요청 구독 해제
    /// </summary>
    public void Dispose ()
    {
        if ( _produceHud != null )
            _produceHud.OnBeginRequested -= HandleBeginRequested;

        _recipeTarget = null;
        CancelIngredientInteract( );
    }


    /// <summary>
    /// 레시피를 열 대상 조회
    /// </summary>
    /// <param name="product">제작 대상</param>
    /// <returns>대상 조회 여부</returns>
    public bool TryGetTarget ( out CookController product )
    {
        product = null;

        if ( _productPresenter == null )
            return false;

        //현재 조준 대상 확인
        if ( _player.TryGetRecipeTarget( out CookController target ) == false )
            return false;

        //표시 조건과 거리 확인
        if ( _productPresenter.CanShowRecipes( target ) == false ||
             IsRecipeTargetInRange( target ) == false )
            return false;

        product = target;
        return true;
    }

    /// <summary>
    /// 조준한 대상의 레시피 열기
    /// </summary>
    /// <returns>화면 열림 여부</returns>
    public bool TryOpen ()
    {
        if ( TryGetTarget( out CookController product ) == false )
            return false;

        //화면 전환 알림 전에 대상 저장
        _recipeTarget = product;
        if ( _productPresenter.OpenRecipes( product ) == true )
            return true;

        _recipeTarget = null;
        return false;
    }

    /// <summary>
    /// 제작 전 레시피 화면 닫기
    /// </summary>
    /// <returns>화면 닫힘 여부</returns>
    public bool TryCloseRecipe ()
    {
        if ( _produceHud.IsRecipeOpen == false )
            return false;

        ProductionSession session = _produceHud.ActiveSession;		//화면 전환 전 제작 상태
        if ( session != null && session.State != ProductionState.Preparing )
            return false;

        _produceHud.Close( );
        return true;
    }

    /// <summary>
    /// 화면 종료 시 레시피 대상 정리
    /// </summary>
    /// <param name="isOpen">제작 화면 표시 여부</param>
    public void HandleUiVisibleChanged ( bool isOpen )
    {
        if ( isOpen == false )
            _recipeTarget = null;
    }

    /// <summary>
    /// 제작 시도 취소 후 화면 닫기
    /// </summary>
    public void CancelProduction ()
    {
        if ( _produceHud == null )
            return;

        _produceHud.ActiveSession?.TryCancelAttempt( );
        _produceHud.Close( );
    }

    /// <summary>
    /// 열린 레시피 유지 조건 확인
    /// </summary>
    public void Tick ()
    {
        if ( _produceHud.IsRecipeOpen == false )
            return;

        //대상과 화면 연결 확인
        if ( _player.IsActionBlocked == true ||
             _productPresenter == null ||
             _recipeTarget == null ||
             _produceHud.ActiveSession != _recipeTarget.Session )
        {
            _produceHud.Close( );
            return;
        }

        //제작 시작 후 패널 전환 대기 중에는 유지
        if ( _recipeTarget.Session.State != ProductionState.Preparing )
            return;

        //표시 조건 또는 거리 이탈 시 닫기
        if ( _productPresenter.CanShowRecipes( _recipeTarget ) == false ||
             IsRecipeTargetInRange( _recipeTarget ) == false )
        {
            _produceHud.Close( );
        }
    }


    /// <summary>
    /// 열린 레시피 대상의 재료 상호작용 시작
    /// </summary>
    /// <returns>상호작용 시작 여부</returns>
    public bool BeginIngredientInteract ()
    {
        //레시피 화면에서만 같은 그릇의 재료 집기와 배치 허용
        if ( _produceHud.IsRecipeOpen == false ||
             IsRecipeInputBlocked( ) == true )
            return false;

        if ( _recipeTarget == null ||
             _produceHud.ActiveSession != _recipeTarget.Session )
            return false;

        if ( _player.BeginIngredientInteract( _recipeTarget ) == false )
            return false;

        _startedRecipeTarget = _recipeTarget;
        return true;
    }


    /// <summary>
    /// 레시피 대상 거리 확인
    /// </summary>
    /// <param name="product">제작 대상</param>
    /// <returns>거리 안에 있는지 여부</returns>
    bool IsRecipeTargetInRange ( CookController product )
    {
        if ( product == null )
            return false;

        float distance = _player.ItemInteractDistance;		//상호작용 거리
        float sqrDistance =
            ( product.transform.position - _player.transform.position ).sqrMagnitude;

        return sqrDistance <= distance * distance;
    }

    /// <summary>
    /// 레시피 UI 입력 차단 확인
    /// </summary>
    /// <returns>입력 차단 여부</returns>
    bool IsRecipeInputBlocked ()
    {
        return _produceHud.IsRecipeOpen == true &&
               _produceHud.IsPointerOverUi( ) == true;
    }

    /// <summary>
    /// 입력 시작 때의 레시피 대상이 유지되는지 확인
    /// </summary>
    /// <returns>레시피 재료 상호작용 입력 유지 여부</returns>
    public bool CanContinueIngredientInteract ()
    {
        if ( _produceHud.IsRecipeOpen == false )
            return false;

        if ( _startedRecipeTarget == null ||
             _startedRecipeTarget != _recipeTarget )
            return false;

        return _produceHud.ActiveSession == _startedRecipeTarget.Session &&
               IsRecipeInputBlocked( ) == false;
    }

}
