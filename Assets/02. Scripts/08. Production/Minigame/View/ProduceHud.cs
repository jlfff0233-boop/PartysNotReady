using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 제작 HUD - 대상 연결, 패널 전환과 결과 알림
/// </summary>
public class ProduceHud : MonoBehaviour
{
    [Header( "----- 패널 -----" )]
    [SerializeField] GameObject _recipePanel;		//레시피 패널
    [SerializeField] GameObject _actionPanel;		//제작 미니게임 패널
    [SerializeField] Button _closeButton;		//레시피 닫기 버튼

    [Header( "----- 결과 알림 -----" )]
    [SerializeField, Min( 0f )] float _noticeDuration = 0.5f;		//결과 알림 시간
    [SerializeField] TMP_Text _noticeText;		//미니게임 결과 알림
    [SerializeField] TextEffect _noticeEffect;		//결과 문구 연출

    [Header( "----- 하위 패널 -----" )]
    [SerializeField] ProduceRecipePanel _recipe;		//레시피 화면
    [SerializeField] ProduceMinigamePanel _minigame;		//미니게임 화면

    CanvasGroup _inputGroup; //메뉴 뒤 제작 입력 차단
    bool _previousInteractable; //차단 전 입력 설정
    bool _previousRaycasts; //차단 전 레이캐스트 설정
    bool _isInputBlocked; //메뉴 입력 차단 여부

    bool _isReady;		//HUD 초기화 여부
    bool _isVisible;		//표시 여부
    bool _hasStarted;		//시작 호출 여부

    float _noticeRemaining;		//알림 남은 시간
    bool _hasNotice;		//결과 알림 표시 여부
    bool _isWaitingEffect;		//실패 연출 종료 대기 여부
    bool _isShowingResult;		//실패 또는 완료 알림 표시 여부
    bool _needsPanelRefresh;		//패널 갱신 예약

    CookController _product;		//제작 대상

    /// <summary>
    /// 커서 위치의 UI 검사 결과 목록
    /// </summary>
    List<RaycastResult> _uiHits = new( );

    /// <summary>
    /// 레시피 화면 열림 여부
    /// </summary>
    public bool IsRecipeOpen => _isReady && _recipePanel.activeSelf;

    /// <summary>
    /// 제작 화면 초기화 완료 여부
    /// </summary>
    public bool IsReady => _isReady;

    /// <summary>
    /// 제작 UI 대상 세션
    /// </summary>
    public ProductionSession ActiveSession =>
        _isVisible && _product != null ? _product.Session : null;

    /// <summary>
    /// 제작 화면 전환 이벤트(표시 여부)
    /// </summary>
    public event Action<bool> OnUiVisibleChanged;


    /// <summary>
    /// 제작 시작 요청(그릇)
    /// </summary>
    public event Action<CookController> OnBeginRequested;

    /// <summary>
    /// 제작 HUD 초기화
    /// </summary>
    void Init ()
    {
        //시작 결과 알림 정리
        CancelNotification ( );

        //양쪽 패널 초기화
        bool recipeReady = _recipe.Init ( );        //레시피 초기화 결과
        bool minigameReady = _minigame.Init ( );        //미니게임 초기화 결과
        _isReady = recipeReady == true && minigameReady == true;

        if ( _isReady == true )
            Close ( );
    }

    private void Awake ()
    {
        Init( );
    }

    private void OnEnable ()
    {
        if ( _isReady == false )
            return;

        _closeButton.onClick.AddListener( Close );
        _noticeEffect.OnFailEnd +=
            HandleNotificationEffectFinished;
        _noticeEffect.OnFailCancel +=
            HandleNotificationEffectCancelled;
        Subscribe( );
        _recipe.Connect( );
        _recipe.OnBeginRequested += HandleBeginRequested;
        _minigame.Connect( );

        if ( _hasStarted == true )
            RefreshAll( );
    }

    private void OnDisable ()
    {
        if ( _isReady == false )
            return;

        _closeButton.onClick.RemoveListener( Close );
        _noticeEffect.OnFailEnd -=
            HandleNotificationEffectFinished;
        _noticeEffect.OnFailCancel -=
            HandleNotificationEffectCancelled;
        Close( );
        _recipe.OnBeginRequested -= HandleBeginRequested;
        _recipe.Disconnect( );
        _minigame.Disconnect( );
        Unsubscribe( );
    }

    /// <summary>
    /// 초기 UI 갱신
    /// </summary>
    private void Start ()
    {
        _hasStarted = true;

        if ( _isReady == true )
            RefreshAll( );
    }

    /// <summary>
    /// 목표 UI와 결과 알림 시간 갱신
    /// </summary>
    private void Update ()
    {
        if ( _isReady == false )
            return;

        _minigame.RefreshTargets( );

        if ( _hasNotice == false )
            return;

        _noticeRemaining = Mathf.Max(
            0f,
            _noticeRemaining - Time.unscaledDeltaTime );

        //표시 시간과 실패 연출이 모두 끝난 경우에만 종료
        if ( _noticeRemaining <= 0f &&
             _isWaitingEffect == false )
        {
            FinishNotification( );
        }
    }

    /// <summary>
    /// 예약된 패널 갱신
    /// </summary>
    private void LateUpdate ()
    {
        if ( _needsPanelRefresh == false )
            return;

        _needsPanelRefresh = false;
        RefreshPanels( );
    }


    /// <summary>
    /// 표시 대상 제작 요청
    /// </summary>
    /// <param name="product">요청한 그릇</param>
    void HandleBeginRequested ( CookController product )
    {
        if ( _isVisible == false || product != _product )
            return;

        OnBeginRequested?.Invoke( product );
    }


    #region ----- 핸들 -----

    /// <summary>
    /// 패널 갱신 예약
    /// </summary>
    /// <param name="state">제작 상태</param>
    void HandleStateChanged ( ProductionState state )
    {
        //결과 이벤트 수신 뒤 패널 전환
        _needsPanelRefresh = true;
    }

    /// <summary>
    /// 제작 미니게임 결과 알림
    /// </summary>
    /// <param name="result">미니게임 판정 결과</param>
    void HandleMinigameResult ( ProduceMinigameResult result )
    {
        if ( _isVisible == false )
            return;

        if ( result == ProduceMinigameResult.Failed )
        {
            ShowNotification(
                "실패!", isFinalResult: true, isFailure: true );
            return;
        }

        if ( result != ProduceMinigameResult.Succeeded )
            return;

        bool isCompleted =
            _product.Session.State == ProductionState.Completed;		//최종 완성 여부

        //마지막 성공은 완성 문구와 강조를 한 번만 표시
        ShowNotification(
            isCompleted == true ? "완성!" : "성공!",
            isFinalResult: isCompleted,
            isFailure: false );
    }

    /// <summary>
    /// 재료 변경 후 패널 갱신 예약
    /// </summary>
    void HandleIngredientsChanged ()
    {
        //LateUpdate 갱신 예약
        _needsPanelRefresh = true;
    }

    #endregion

    #region ----- 패널 -----

    /// <summary>
    /// 지정 제작 대상의 화면 열기
    /// </summary>
    /// <param name="presenter">레시피 목록 중재자</param>
    /// <param name="product">표시할 그릇</param>
    /// <returns>화면 열림 여부</returns>
    public bool Open ( ProductionManager presenter, CookController product )
    {
        if ( _isReady == false ||
            isActiveAndEnabled == false ||
             presenter == null || product == null )
            return false;

        if ( _product != product )
        {
            Unsubscribe( );
            _product = product;
            Subscribe( );
        }

        _recipe.Bind( presenter, product );
        _minigame.Bind( product );

        //이전 결과 알림 종료
        CancelNotification( );
        _needsPanelRefresh = false;

        _isVisible = true;

        RefreshAll( );

        return true;
    }

    /// <summary>
    /// 제작 화면, 결과 알림 종료
    /// </summary>
    public void Close ()
    {
        if ( _isReady == false )
            return;

        _isVisible = false;
        _recipe.Clear( );

        CancelNotification( );
        _needsPanelRefresh = false;

        SetPanels( ProductionState.Completed );
    }


    /// <summary>
    /// 지정 대상의 화면 연결 해제
    /// </summary>
    /// <param name="product">해제할 그릇</param>
    public void Unbind ( CookController product )
    {
        if ( ReferenceEquals( _product, product ) == false )
            return;

        //표시 종료와 대상 구독 해제는 별도 수명
        Unsubscribe( );
        _product = null;
        if ( _recipe != null )
        {
            _recipe.Bind( null, null );
            _recipe.Disconnect( );
        }
        if ( _minigame != null )
        {
            _minigame.Bind( null );
            _minigame.Disconnect( );
        }
        Close( );
    }

    /// <summary>
    /// 제작 세션을 유지하고 화면 입력만 차단
    /// </summary>
    /// <param name="blocked">입력 차단 여부</param>
    public void SetInputBlocked ( bool blocked )
    {
        if ( _isInputBlocked == blocked )
            return;
        if ( _inputGroup == null )
        {
            _inputGroup = GetComponent<CanvasGroup>( );
            if ( _inputGroup == null )
                _inputGroup = gameObject.AddComponent<CanvasGroup>( );
        }
        if ( blocked == true )
        {
            _previousInteractable = _inputGroup.interactable;
            _previousRaycasts = _inputGroup.blocksRaycasts;
        }
        _isInputBlocked = blocked;
        _inputGroup.interactable = blocked == true ? false : _previousInteractable;
        _inputGroup.blocksRaycasts = blocked == true ? false : _previousRaycasts;
    }

    /// <summary>
    /// 제작 화면 표시 설정
    /// </summary>
    /// <param name="isVisible">표시 여부</param>
    public void SetVisible ( bool isVisible )
    {
        _isVisible = isVisible;

        if ( isVisible == false )
            CancelNotification( );

        SetPanels( _product == null
            ? ProductionState.Completed
            : _product.Session.State );
    }

    /// <summary>
    /// 제작 화면 전체 갱신
    /// </summary>
    void RefreshAll ()
    {
        if ( _isReady == false )
            return;

        CancelNotification( );

        if ( _product == null )
        {
            SetPanels( ProductionState.Completed );
            return;
        }

        _recipe.Refresh( );
        _minigame.Refresh( );
        SetPanels( _product.Session.State );
    }

    /// <summary>
    /// 제작 상태/결과를 화면에 반영
    /// </summary>
    void RefreshPanels ()
    {
        if ( _isVisible == false )
            return;

        if ( _product == null )
        {
            Close( );
            return;
        }

        //실패나 완료 직후에는 재료가 없어도 알림 유지
        if ( _isShowingResult == true )
        {
            SetPanels( _product.Session.State );
            return;
        }

        if ( _product.Session.State == ProductionState.Completed )
        {
            Close( );
            return;
        }

        //레시피 화면 복귀 전 재료와 조리 버튼 갱신
        if ( _product.Session.State == ProductionState.Preparing &&
             _recipePanel.activeSelf == false )
        {
            _recipe.Refresh( );
        }

        SetPanels( _product.Session.State );
    }

    /// <summary>
    /// 패널/미니게임 그룹 전환
    /// </summary>
    /// <param name="state">제작 상태</param>
    void SetPanels ( ProductionState state )
    {
        //레시피 표시 여부
        bool recipeVisible =
            _isVisible &&
            _isShowingResult == false &&
            state == ProductionState.Preparing;

        //제작 미니게임 표시 여부
        bool actionVisible =
            _isVisible &&
            ( state == ProductionState.Processing || _isShowingResult );

        //미니게임 입력 화면 표시 여부
        bool minigameVisible =
            actionVisible &&
            _isShowingResult == false &&
            state == ProductionState.Processing;

        //활성화 상태였는지 여부
        bool wasRecipeVisible = _recipePanel.activeSelf;
        bool wasUiVisible = wasRecipeVisible || _actionPanel.activeSelf;

        _recipePanel.SetActive( recipeVisible );
        _actionPanel.SetActive( actionVisible );

        _minigame.SetVisible( minigameVisible );

        bool isUiVisible = recipeVisible || actionVisible;

        //전체 열림 상태뿐 아니라 레시피와 미니게임 사이 전환도 알림
        if ( wasUiVisible != isUiVisible ||
             wasRecipeVisible != recipeVisible )
        {
            OnUiVisibleChanged?.Invoke( isUiVisible );
        }
    }

    /// <summary>
    /// 제작 상태 이벤트 연결
    /// </summary>
    void Subscribe ()
    {
        if ( _product == null )
            return;

        //중복 구독 방지
        Unsubscribe( );

        _product.Session.OnIngredientsChanged += HandleIngredientsChanged;
        _product.Session.OnStateChanged += HandleStateChanged;
        _product.Session.Minigame.OnResult += HandleMinigameResult;
    }

    /// <summary>
    /// 제작 상태 이벤트 연결 해제
    /// </summary>
    void Unsubscribe ()
    {
        if ( _product == null )
            return;
        _product.Session.OnIngredientsChanged -= HandleIngredientsChanged;
        _product.Session.OnStateChanged -= HandleStateChanged;
        _product.Session.Minigame.OnResult -= HandleMinigameResult;
    }


    /// <summary>
    /// 현재 커서가 UI 위에 있는지 확인
    /// </summary>
    /// <returns>UI 위에 있는지 여부</returns>
    public bool IsPointerOverUi ()
    {
        EventSystem eventSystem = EventSystem.current;

        if ( eventSystem == null || Mouse.current == null )
            return false;

        PointerEventData pointer = new( eventSystem )
        {
            position = Mouse.current.position.ReadValue( )
        };

        _uiHits.Clear( );
        eventSystem.RaycastAll( pointer, _uiHits );

        foreach ( RaycastResult hit in _uiHits )
        {
            if ( hit.module is GraphicRaycaster )
                return true;
        }

        return false;
    }

    #endregion

    #region ----- 결과 알림 -----

    /// <summary>
    /// 제작 미니게임 결과 알림 표시
    /// </summary>
    /// <param name="isVisible">표시 여부</param>
    /// <param name="message">표시 문구</param>
    void SetMinigameNotification ( bool isVisible, string message )
    {
        _noticeText.text =
            isVisible == true ? message : string.Empty;

        _noticeText.gameObject.SetActive( isVisible );
    }

    /// <summary>
    /// 결과 알림 취소와 표시 기준 복구
    /// </summary>
    void CancelNotification ()
    {
        _hasNotice = false;
        _isWaitingEffect = false;
        _noticeRemaining = 0f;
        _isShowingResult = false;

        //완료 알림 없이 이전 연출 중단
        _noticeEffect.ResetEffect( );
        SetMinigameNotification( false, string.Empty );
    }

    /// <summary>
    /// 알림 종료 후 제작 화면 정리
    /// </summary>
    void FinishNotification ()
    {
        bool wasShowingResult = _isShowingResult;		//실패 또는 완성 알림 여부

        CancelNotification( );

        //중간 성공은 알림만 종료하고 미니게임 유지
        if ( wasShowingResult == false )
            return;

        //대상이 사라졌거나
        //제작 완료 또는 재료 전체 소비 시 닫기
        if ( _product == null ||
             _product.Session.State == ProductionState.Completed ||
             _product.Session.HasIngredients == false )
        {
            Close( );
            return;
        }

        //실패 후 재료가 남아 있으면 레시피 화면으로 복귀
        RefreshPanels( );
    }

    /// <summary>
    /// 실패 연출 종료 반영
    /// </summary>
    void HandleNotificationEffectFinished ()
    {
        //실제 알림 종료는 Update에서 처리
        _isWaitingEffect = false;
    }

    /// <summary>
    /// 실패 연출 취소 시 종료 대기 해제
    /// </summary>
    void HandleNotificationEffectCancelled ()
    {
        _isWaitingEffect = false;
    }

    /// <summary>
    /// 제작 결과 문구와 지정 연출 표시
    /// </summary>
    /// <param name="message">표시 문구</param>
    /// <param name="isFinalResult">실패 또는 최종 완성 여부</param>
    /// <param name="isFailure">실패 연출 여부</param>
    void ShowNotification (
        string message, bool isFinalResult, bool isFailure )
    {
        CancelNotification( );

        _hasNotice = true;
        _isShowingResult = isFinalResult;
        _noticeRemaining = _noticeDuration;

        SetPanels( _product.Session.State );

        //표시 시간이 0이면 문구와 연출 없이 즉시 정리
        if ( _noticeRemaining <= 0f )
        {
            FinishNotification( );
            return;
        }

        SetMinigameNotification( true, message );

        if ( isFailure == true )
        {
            _noticeEffect.PlayFailure( );
            //이전 자동 연출의 취소 알림 이후 새 실패 연출 대기
            _isWaitingEffect = true;
        }
        else
        {
            //강조가 끝나기 전에 문구를 숨기지 않음
            _noticeRemaining = Mathf.Max(
                _noticeRemaining,
                _noticeEffect.EmphasisDuration );

            _noticeEffect.PlayEmphasis( );
        }
    }

    #endregion
}
