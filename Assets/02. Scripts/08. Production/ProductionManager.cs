using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 제작 관리 - 그릇 등록, 레시피 설정과 제작 결과 전달
/// </summary>
public class ProductionManager : MonoBehaviour
{
    [Header ( "----- 설정 데이터 -----" )]
    [SerializeField] RecipeDataMap _recipeMap;		//전체 레시피

    [Header ( "----- 제작 UI -----" )]
    [SerializeField] ProduceHud _hud;		//제작 화면

    ProduceMinigameDataMap _minigameMap;		//선택한 난이도의 제작 설정
    bool _isReady;		//초기화 성공 여부
    bool _hasInitialized;		//초기화 시도 여부

    /// <summary>
    /// 그릇 목록
    /// </summary>
    List<CookController> _products = new ( );

    /// <summary>
    /// 전체 레시피
    /// </summary>
    public IReadOnlyList<RecipeData> Recipes =>
        _recipeMap == null ? null : _recipeMap.Data;

    /// <summary>
    /// 등록된 그릇 목록
    /// </summary>
    public IReadOnlyList<CookController> Products => _products;

    /// <summary>
    /// 제작 초기화 여부
    /// </summary>
    public bool IsReady => _isReady;

    /// <summary>
    /// 제작 완료 이벤트(제작 결과)
    /// </summary>
    public event Action<ProductionResult> OnCompleted;

    /// <summary>
    /// 제작 결과 초기화 이벤트(제작 결과)
    /// </summary>
    public event Action<ProductionResult> OnReset;


    /// <summary>
    /// 그릇 등록 이벤트(등록한 그릇)
    /// </summary>
    public event Action<CookController> OnRegistered;

    /// <summary>
    /// 미니게임 결과 이벤트(판정 결과)
    /// </summary>
    public event Action<ProduceMinigameResult> OnMinigameResult;


    #region ----- 초기화 -----

    /// <summary>
    /// 선택한 제작 설정과 현재 씬의 그릇 초기화
    /// </summary>
    /// <param name="minigameMap">난이도별 제작 설정</param>
#if UNITY_EDITOR
    /// <param name="allowEmptyProducts">에디터 테스트에서 그릇 없는 구성 허용 여부</param>
#endif

    /// <returns>초기화 성공 여부</returns>
    public bool Init ( ProduceMinigameDataMap minigameMap
#if UNITY_EDITOR
        , bool allowEmptyProducts = false
#endif
        )
    {
        //이미 성공했으면 종료
        if ( _hasInitialized == true )
            return _isReady;

        _hasInitialized = true;

        if ( minigameMap == null )
        {
            Debug.LogError ( "[제작 초기화 실패] 난이도별 제작 설정이 없습니다." , this );
            return false;
        }

        if ( minigameMap.CheckSettings ( out string mapReason ) == false )
        {
            Debug.LogError ( $"[제작 초기화 실패] {mapReason}" , minigameMap );
            return false;
        }

        _minigameMap = minigameMap;

        if ( _recipeMap == null )
        {
            Debug.LogError ( "[제작 초기화 실패] _recipeMap 참조가 없습니다." , this );
            return false;
        }

        if ( Recipes == null || Recipes.Count == 0 )
        {
            Debug.LogError ( $"[제작 초기화 실패] {_recipeMap.name}: 레시피 목록이 비어 있습니다." , _recipeMap );
            return false;
        }

        //그릇 연결 전에 전체 레시피 설정 확인
        for ( int i = 0 ; i < Recipes.Count ; i++ )
        {
            RecipeData recipe = Recipes [ i ];		//검사할 레시피

            if ( recipe == null )
            {
                Debug.LogError ( $"[제작 초기화 실패] {_recipeMap.name}: _data[{i}] 참조가 없습니다." , _recipeMap );
                return false;
            }

            string label = $"{_recipeMap.name} / _data[{i}] / {recipe.name} / ID: {recipe.RecipeId}";		//설정 위치

            //선택한 난이도의 제작 미니게임 가져오기
            if ( TryGetMinigameData ( recipe , out ProduceMinigameData minigame ) == false )
            {
                return false;
            }

            //각 모델의 데이터가 유효한지 검사
            if ( ProductionModel.CheckSettings (
                    recipe , minigame.RequiredSuccessCount ,
                    minigame.FailureLossCount , out string reason ) == false ||
                 ProduceMinigameModel.CheckSettings (
                    minigame.AttemptDuration , minigame.HandleTravelDuration ,
                    minigame.SuccessRangeStart , minigame.SuccessRangeEnd ,
                    minigame.HitPointSpawnInterval , minigame.HitPointLifetime ,
                    out reason ) == false )
            {
                Debug.LogError (
                    $"[제작 초기화 실패] {label}\n{reason}" , this );

                return false;
            }
        }

        //조리 중재자 가져오기
        CookController [ ] cooks = FindObjectsByType<CookController> (
            FindObjectsInactive.Include , FindObjectsSortMode.None );

        foreach ( CookController product in cooks )
        {
            //다른 씬과 풀 보관소의 그릇은 초기 등록에서 제외
            if ( IsOwnedSceneProduct ( product ) == false )
                continue;

            if ( AddProduct ( product ) == false )
            {
                //앞선 그릇의 연결을 남기지 않고 초기화 실패 확정
                ClearProducts ( );

                Debug.LogError ( $"[제작 초기화 실패] " +
                    $"그릇 초기화 실패: {product.name}" , product );

                return false;
            }

        }

        _isReady = _products.Count > 0;

#if UNITY_EDITOR
        if ( _isReady == false && allowEmptyProducts == true )
        {
            Debug.LogWarning ( "[에디터 테스트] 그릇 없이 제작 설정을 준비합니다." , this );
            _isReady = true;
        }
#endif

        if ( _isReady == false )
            Debug.LogError (
                "[제작 초기화 실패] 현재 씬에 그릇이 없습니다." , this );

        return _isReady;
    }

    private void OnDisable ( )
    {
        Stop ( );
    }

    private void OnDestroy ( )
    {
        Stop ( );
    }


    /// <summary>
    /// 등록된 그릇의 결과 구독 및 목록 정리
    /// </summary>
    void ClearProducts ( )
    {
        _isReady = false;

        //초기화 실패 후 재시도는 막고 등록된 대상만 해제
        while ( _products.Count > 0 )
            Unregister ( _products [ _products.Count - 1 ] );
    }

    /// <summary>
    /// 등록된 그릇의 제작 사운드 정리
    /// </summary>
    public void StopSounds ( )
    {
        foreach ( CookController product in _products )
        {
            //파괴된 그릇은 제외
            if ( product == null )
                continue;

            product.StopSound ( );
        }
    }

    /// <summary>
    /// 제작 중단 후 소비 종료를 확인하고 그릇 연결 정리
    /// </summary>
    /// <returns>전체 제작 종료 여부</returns>
    public bool Stop ( )
    {
        _isReady = false;
        bool completed = true;		//소비 처리 종료 여부
        List<CookController> products = new ( _products );

        foreach ( CookController product in products )
        {
            if ( product == null )
            {
                _products.Remove ( product );
                continue;
            }

            //소비 중에는 중단만 예약하고 다음 정리 요청까지 목록 유지
            product.Unbind ( this );
            if ( product.Session.IsConsuming == true )
                completed = false;
        }

        if ( completed == false )
            return false;

        ClearProducts ( );
        return true;
    }

    #endregion


    #region ----- 핸들 -----

    /// <summary>
    /// 제작 완료 결과 전달
    /// </summary>
    /// <param name="result">제작 결과</param>
    void HandleCompleted ( ProductionResult result )
    {
        OnCompleted?.Invoke ( result );
    }

    /// <summary>
    /// 초기화된 결과 전달
    /// </summary>
    /// <param name="result">이전 결과</param>
    void HandleReset ( ProductionResult result )
    {
        OnReset?.Invoke ( result );
    }

    /// <summary>
    /// 미니게임 판정 전달
    /// </summary>
    /// <param name="result">판정 결과</param>
    void HandleMinigameResult ( ProduceMinigameResult result )
    {
        OnMinigameResult?.Invoke ( result );
    }

    #endregion

    #region ----- 제작 -----

    /// <summary>
    /// 그릇 등록
    /// </summary>
    /// <param name="product">등록할 그릇</param>
    /// <returns>등록 여부</returns>
    public bool Register ( CookController product )
    {
        return _isReady && AddProduct ( product );
    }

    /// <summary>
    /// 그릇 초기화와 등록
    /// </summary>
    /// <param name="product">등록할 그릇</param>
    /// <returns>등록 여부</returns>
    bool AddProduct ( CookController product )
    {
        if ( IsOwnedSceneProduct ( product ) == false )
            return false;

        //이미 연결한 대상이라면 종료
        if ( _products.Contains ( product ) == true )
            return true;

        //제작 중재자 연결
        if ( product.Bind ( this ) == false )
            return false;

        //그릇 목록에 추가
        _products.Add ( product );

        product.Session.OnCompleted += HandleCompleted;
        product.Session.OnReset += HandleReset;
        product.Session.Minigame.OnResult += HandleMinigameResult;

        //등록 이벤트 발행
        OnRegistered?.Invoke ( product );

        return _products.Contains ( product );
    }

    /// <summary>
    /// 그릇 등록 해제
    /// </summary>
    /// <param name="product">해제할 그릇</param>
    /// <returns>해제 여부</returns>
    public bool Unregister ( CookController product )
    {
        //목록에서 제거
        if ( _products.Remove ( product ) == false )
            return false;

        product.Session.OnCompleted -= HandleCompleted;
        product.Session.OnReset -= HandleReset;
        product.Session.Minigame.OnResult -= HandleMinigameResult;

        //닫힌 화면의 대상 참조도 정리
        if ( _hud != null )
            _hud.Unbind ( product );

        //완성 결과와 월드 오브젝트는 유지
        product.Unbind ( this );
        return true;
    }

    /// <summary>
    /// 시작자 기준 제작 요청
    /// </summary>
    /// <param name="product">제작 대상</param>
    /// <param name="player">제작 시작자</param>
    /// <returns>시작 여부</returns>
    public bool TryBegin ( CookController product , Player player )
    {
        //시작자와 제작 대상 확인
        if ( _isReady == false ||
             player == null ||
             player.IsActionBlocked == true ||
             product == null ||
             _products.Contains ( product ) == false ||
             player.SupportItems == null )
            return false;

        return player.SupportItems.TryBeginProduction ( product.Session );
    }

    #endregion

    #region ----- 레시피 -----


    /// <summary>
    /// 레시피 화면 열기
    /// </summary>
    /// <param name="product">표시할 그릇</param>
    /// <returns>화면 열림 여부</returns>
    public bool OpenRecipes ( CookController product )
    {
        if ( CanShowRecipes ( product ) == false )
            return false;

        return _hud.Open ( this , product );
    }

    /// <summary>
    /// 레시피 선택 및 적용
    /// </summary>
    /// <param name="product">대상 그릇</param>
    /// <param name="recipe">선택한 레시피</param>
    /// <returns>적용 여부</returns>
    public bool SelectRecipe (
        CookController product , RecipeData recipe )
    {
        if ( _isReady == false ) return false;

        //그릇이나 레시피가 없으면 종료
        if ( product == null || recipe == null ||
            _products.Contains ( product ) == false )
            return false;

        //적용 여부
        bool registered = false;

        foreach ( RecipeData candidate in Recipes )
        {
            //선택한 레시피 확인
            if ( candidate == recipe )
            {
                registered = true;
                break;
            }
        }

        if ( registered == false )
            return false;

        if ( TryGetMinigameData ( recipe , out ProduceMinigameData minigame ) == false )
            return false;

        return product.Session.SelectRecipe ( recipe , minigame );
    }

    /// <summary>
    /// 레시피에 적용할 난이도별 제작 설정 조회
    /// </summary>
    /// <param name="recipe">대상 레시피</param>
    /// <param name="data">적용할 제작 설정</param>
    /// <returns>조회 성공 여부</returns>
    bool TryGetMinigameData ( RecipeData recipe , out ProduceMinigameData data )
    {
        if ( _minigameMap.TryGetData ( recipe.ProduceMinigame , out data ) == true )
            return true;

        Debug.LogError ( $"[제작 설정 조회 실패] {_minigameMap.name}: " +
            $"{recipe.name}의 기본 제작 설정에 대응하는 항목이 없습니다." , recipe );

        return false;
    }


    /// <summary>
    /// 레시피 표시 가능 여부 확인
    /// </summary>
    /// <param name="product">제작 대상</param>
    /// <returns>표시 가능 여부</returns>
    public bool CanShowRecipes ( CookController product )
    {
        return _isReady == true &&
               product != null &&
               _products.Contains ( product ) == true &&
               product.Session.IsActive == true &&
               product.IsBowlHeld == false &&
               product.Session.State == ProductionState.Preparing;
    }

    /// <summary>
    /// 레시피 충족 여부
    /// </summary>
    /// <param name="product">확인할 그릇</param>
    /// <param name="recipe">확인할 레시피</param>
    /// <returns>재료 종류/수량 충족 여부</returns>
    public bool CanCook (
        CookController product , RecipeData recipe )
    {
        //그릇이나 레시피가 없으면 종료
        if ( product == null || recipe == null ||
             _products.Contains ( product ) == false )
            return false;

        return RecipeIngredientChecker.Check (
            recipe , product.Session.GetIngredientCount ,
            product.Session.RegisteredIngredientCount ) == ProductionStartResult.Started;
    }

    #endregion

    /// <summary>
    /// 현재 제작 관리자와 같은 씬에 속한 그릇인지 확인
    /// </summary>
    /// <param name="product">등록할 그릇</param>
    /// <returns>현재 씬 소속 여부</returns>
    bool IsOwnedSceneProduct ( CookController product )
    {
        return product != null && product.gameObject.scene == gameObject.scene;
    }
}
