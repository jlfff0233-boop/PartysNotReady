using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 제작 레시피 뷰 - 목록, 상세와 제작 준비 표시
/// </summary>
public class ProduceRecipePanel : MonoBehaviour
{
    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] TMP_Text _currentRecipeText;		//선택 레시피 이름
    [SerializeField] TMP_Text [ ] _ingredientTexts;		//재료 텍스트

    [SerializeField] Transform _recipeListRoot;		//목록과 상세 내용 부모
    [SerializeField] TMP_Text _recipeTextPrefab;		//목록과 상세 텍스트
    [SerializeField] TMP_Dropdown _sortDropdown;		//목록 정렬

    [SerializeField] Button _prevButton;		//이전 레시피
    [SerializeField] Button _nextButton;		//다음 레시피
    [SerializeField] Button _cookButton;		//조리 시작 버튼

    [Header( "----- 클릭 사운드 -----" )]
    [SerializeField] SoundPlayer _soundPlayer;		//패널과 독립적인 UI 공용 재생기
    [SerializeField] SoundData _clickSound;		//동적 레시피 버튼 클릭음

    [Header( "----- 목록 색상 -----" )]
    [SerializeField] Color _cookableColor = new( 0.08f, 0.4f, 0.22f, 1f );		//조리 가능 레시피 색상
    [SerializeField] Color _uncookableColor = new( 0.35f, 0.35f, 0.35f, 1f );		//조리 불가 레시피 색상

    bool _isReady;		//UI 초기화 여부
    bool _isRecipeDetail;		//레시피 상세 표시 여부
    CookController _product;		//제작 대상
    ProductionManager _productPresenter;		//레시피 목록 중재자

    /// <summary>
    /// 현재 정렬된 레시피
    /// </summary>
    List<RecipeData> _recipes = new( );

    /// <summary>
    /// 현재 목록 갱신의 조리 가능 판정(레시피, 충족 여부)
    /// </summary>
    Dictionary<RecipeData, bool> _cookableRecipes = new( );

    /// <summary>
    /// 생성한 목록 텍스트
    /// </summary>
    List<TMP_Text> _recipeTexts = new( );

    /// <summary>
    /// 제작 시작 요청(그릇)
    /// </summary>
    public event Action<CookController> OnBeginRequested;


    #region ----- 초기화 -----

    /// <summary>
    /// 레시피 UI 초기화
    /// </summary>
    /// <returns>초기화 성공 여부</returns>
    public bool Init ()
    {
        if ( _isReady == true )
            return true;

        _sortDropdown.ClearOptions( );
        _sortDropdown.AddOptions( new List<string>
        {
            "이름 오름차순", "이름 내림차순", "조리 가능 우선", "조리 불가 우선"
        } );

        _isReady = true;
        return true;
    }

    /// <summary>
    /// 제작 대상 연결
    /// </summary>
    /// <param name="presenter">레시피 목록 중재자</param>
    /// <param name="product">제작 대상</param>
    public void Bind ( ProductionManager presenter, CookController product )
    {
        Disconnect( );
        _productPresenter = presenter;
        _product = product;
        _isRecipeDetail = false;
        Connect( );
    }

    /// <summary>
    /// 입력과 제작 상태 구독
    /// </summary>
    public void Connect ()
    {
        if ( _isReady == false )
            return;

        //중복 구독 방지
        Disconnect( );
        _cookButton.onClick.AddListener( HandleCookClicked );
        _prevButton.onClick.AddListener( HandlePreviousClicked );
        _nextButton.onClick.AddListener( HandleNextClicked );
        _sortDropdown.onValueChanged.AddListener( HandleSortChanged );

        if ( _product == null )
            return;

        _product.Session.OnIngredientsChanged += HandleIngredientsChanged;
        _product.Session.OnStateChanged += HandleStateChanged;
    }

    /// <summary>
    /// 입력과 제작 상태 구독 해제
    /// </summary>
    public void Disconnect ()
    {
        if ( _isReady == false )
            return;

        _cookButton.onClick.RemoveListener( HandleCookClicked );
        _prevButton.onClick.RemoveListener( HandlePreviousClicked );
        _nextButton.onClick.RemoveListener( HandleNextClicked );
        _sortDropdown.onValueChanged.RemoveListener( HandleSortChanged );

        if ( _product == null )
            return;

        _product.Session.OnIngredientsChanged -= HandleIngredientsChanged;
        _product.Session.OnStateChanged -= HandleStateChanged;
    }

    #endregion

    #region ----- 핸들 -----

    /// <summary>
    /// 상세 화면의 이전 레시피 선택
    /// </summary>
    void HandlePreviousClicked ()
    {
        //현재 인덱스 가져오기
        int index = _recipes.IndexOf( _product.Session.SelectedRecipe );

        if ( index > 0 )
            SelectRecipe( _recipes [ index - 1 ] );
    }

    /// <summary>
    /// 상세 화면의 다음 레시피 선택
    /// </summary>
    void HandleNextClicked ()
    {
        //현재 인덱스 가져오기
        int index = _recipes.IndexOf( _product.Session.SelectedRecipe );

        if ( index >= 0 && index + 1 < _recipes.Count )
            SelectRecipe( _recipes [ index + 1 ] );
    }

    /// <summary>
    /// 정렬 변경 시 목록 갱신
    /// </summary>
    /// <param name="value">드롭다운 값</param>
    void HandleSortChanged ( int value )
    {
        if ( _isRecipeDetail == false )
            RefreshRecipeList( );
    }

    /// <summary>
    /// 제작 버튼 갱신
    /// </summary>
    /// <param name="state">제작 상태</param>
    void HandleStateChanged ( ProductionState state )
    {
        RefreshCookButton( );
    }

    /// <summary>
    /// 재료와 레시피 표시 갱신
    /// </summary>
    void HandleIngredientsChanged ()
    {
        RefreshRecipeList( );
        RefreshIngredients( );
        RefreshCookButton( );
    }

    /// <summary>
    /// 제작 시작 입력
    /// </summary>
    void HandleCookClicked ()
    {
        OnBeginRequested?.Invoke( _product );
        RefreshCookButton( );
    }

    #endregion

    #region ----- 레시피 -----

    /// <summary>
    /// 레시피 화면 갱신
    /// </summary>
    public void Refresh ()
    {
        if ( _isReady == false || _product == null )
            return;

        RefreshRecipeName( );
        RefreshRecipeList( );
        RefreshIngredients( );
        RefreshCookButton( );
    }

    /// <summary>
    /// 레시피 표시 정리
    /// </summary>
    public void Clear ()
    {
        _isRecipeDetail = false;
        _recipes.Clear( );
        _cookableRecipes.Clear( );
        ClearRecipeTexts( );
    }

    /// <summary>
    /// 목록 정렬, 현재 화면 갱신
    /// </summary>
    void RefreshRecipeList ()
    {
        ClearRecipeTexts( );
        _recipes.Clear( );
        _cookableRecipes.Clear( );

        if ( _productPresenter == null || _product == null ||
             _productPresenter.Recipes == null )
            return;

        //이번 갱신의 판정을 정렬과 색상 표시에서 함께 사용
        foreach ( RecipeData recipe in _productPresenter.Recipes )
        {
            _recipes.Add( recipe );
            _cookableRecipes [ recipe ] = _productPresenter.CanCook( _product, recipe );
        }

        //선택 기준으로 정렬
        _recipes.Sort( CompareRecipes );

        if ( _isRecipeDetail == true )
        {
            RecipeData selected = _product.Session.SelectedRecipe;
            if ( selected == null )
                return;

            for ( int i = 0; i < selected.IngredientCount; i++ )
            {
                //선택한 레시피에 필요한 재료 가져오기
                IngredientRequirement requirement = selected.GetIngredient( i );

                if ( requirement.Item == null )
                    continue;

                TMP_Text text = Instantiate( _recipeTextPrefab, _recipeListRoot );

                text.gameObject.SetActive( true );
                text.text =
                    $"{requirement.Item.ItemName} {requirement.RequiredCount}";

                Button detailButton = text.GetComponent<Button>( );

                if ( detailButton != null )
                    detailButton.interactable = false;

                _recipeTexts.Add( text );
            }

            return;
        }

        foreach ( RecipeData recipe in _recipes )
        {
            TMP_Text text = Instantiate( _recipeTextPrefab, _recipeListRoot );

            text.gameObject.SetActive( true );
            text.text = recipe.ResultItem.ItemName;
            text.color = _cookableRecipes [ recipe ]
                ? _cookableColor : _uncookableColor;
            text.raycastTarget = true;

            Button button = text.GetComponent<Button>( );

            if ( button == null )
                button = text.gameObject.AddComponent<Button>( );

            //중복 방지 구독 해제
            button.onClick.RemoveAllListeners( );
            RecipeData selected = recipe;       //선택한 레시피
            button.onClick.AddListener( () => HandleRecipeClicked( selected ) );

            _recipeTexts.Add( text );
        }
    }

    /// <summary>
    /// 동적 레시피 버튼 클릭음 재생 후 상세 화면 표시
    /// </summary>
    /// <param name="recipe">선택한 레시피</param>
    void HandleRecipeClicked ( RecipeData recipe )
    {
        if ( _soundPlayer != null )
            _soundPlayer.PlayOneShot( _clickSound );

        SelectRecipe( recipe );
    }

    /// <summary>
    /// 선택한 레시피의 상세 화면 표시
    /// </summary>
    /// <param name="recipe">선택한 레시피</param>
    void SelectRecipe ( RecipeData recipe )
    {
        bool selected =
            _productPresenter.SelectRecipe( _product, recipe );       //선택 결과

        if ( selected == false )
        {
            //거절 후 현재 시작 조건 확인
            RefreshCookButton( );
            return;
        }

        _isRecipeDetail = true;
        RefreshRecipeName( );
        RefreshRecipeList( );
        RefreshIngredients( );
        RefreshCookButton( );
    }

    /// <summary>
    /// 선택 레시피 이름 표시
    /// </summary>
    void RefreshRecipeName ()
    {
        _currentRecipeText.gameObject.SetActive( _isRecipeDetail );
        _sortDropdown.gameObject.SetActive( _isRecipeDetail == false );
        _prevButton.gameObject.SetActive( _isRecipeDetail );
        _nextButton.gameObject.SetActive( _isRecipeDetail );

        ItemData result = _product.Session.ResultItem;

        _currentRecipeText.text =
            result == null
                ? string.Empty
                : $"{result.ItemName} 레시피";
    }

    /// <summary>
    /// 재료별 현재 수량과 필요 수량 표시
    /// </summary>
    void RefreshIngredients ()
    {
        if ( _isReady == false )
            return;

        for ( int i = 0; i < _ingredientTexts.Length; i++ )
        {
            TMP_Text text = _ingredientTexts [ i ];

            if ( text == null )
                continue;

            if ( i >= _product.Session.RequirementCount ||
                 _product.Session.TryGetIngredientProgress(
                     i, out ProduceIngredientData progress ) == false )
            {
                text.text = string.Empty;
                text.gameObject.SetActive( false );
                continue;
            }

            text.text =
                $"{progress.Data.ItemName} " +
                $"{progress.CurrentCount} / {progress.RequiredCount}";

            text.gameObject.SetActive( true );
        }
    }

    /// <summary>
    /// 조리 버튼 입력 가능 여부 갱신
    /// </summary>
    void RefreshCookButton ()
    {
        if ( _isReady == false )
            return;

        //현재 제작 조건 반영
        bool canBegin =
            _product != null &&
            _product.Session.CanBeginProduction( out _ );

        _cookButton.interactable = canBegin;
    }

    /// <summary>
    /// 이전 목록 텍스트 정리
    /// </summary>
    void ClearRecipeTexts ()
    {
        foreach ( TMP_Text text in _recipeTexts )
        {
            if ( text != null )
            {
                text.gameObject.SetActive( false );
                Destroy( text.gameObject );
            }
        }

        _recipeTexts.Clear( );
    }

    /// <summary>
    /// 레시피 정렬 순서 비교
    /// </summary>
    /// <param name="left">첫 번째 레시피</param>
    /// <param name="right">두 번째 레시피</param>
    /// <returns>정렬 순서</returns>
    int CompareRecipes ( RecipeData left, RecipeData right )
    {
        int sort = _sortDropdown.value;
        int result = 0;

        if ( sort == 0 || sort == 1 )
        {
            result = string.Compare(
                left.ResultItem.ItemName,
                right.ResultItem.ItemName,
                StringComparison.Ordinal );

            if ( sort == 1 )
                result = -result;
        }
        else
        {
            bool first = _cookableRecipes [ left ];
            bool second = _cookableRecipes [ right ];

            result = first.CompareTo( second );

            if ( sort == 2 )
                result = -result;
        }

        return result != 0
            ? result
            : string.Compare(
            left.RecipeId, right.RecipeId, StringComparison.Ordinal );
    }

    #endregion
}
