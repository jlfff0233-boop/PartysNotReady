using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

/// <summary>
/// 타이틀과 로비 중재 - 설정, 상점, 반입 선택과 플레이 씬 이동
/// </summary>
public class TitleScene : MonoBehaviour
{
    [Header( "----- 설정 -----" )]
    [SerializeField] ShopDataMap _dataMap;        //판매 목록
    [SerializeField] GameDifficultyDataMap _difficultyMap;		//난이도별 설정

    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] TitleView _titleView;		//타이틀 표시와 입력 전달
    [SerializeField] ShopPresenter _shopPresenter;        //상점
    [SerializeField] ItemSelectPresenter _selectPresenter;        //반입 선택
    [SerializeField] DifficultySelectPanel _difficultyPanel;		//난이도 선택

    [FormerlySerializedAs( "_startView" )]
    [SerializeField] LobbyView _lobbyView;        //로비 표시와 입력 전달

    [SerializeField] SettingPanel _settingPanel;		//사용자 설정 패널

    [Header( "----- 배경음 -----" )]
    [SerializeField] AudioClip _titleBgm;		//타이틀과 로비 배경음
    [SerializeField, Min( 0f )] float _bgmFadeDuration = 0.5f;		//각 전환 단계 시간

#if UNITY_EDITOR
    [Header( "----- 디버그 -----" )]
    [SerializeField, Min( 0 )] int _testBalance = 1000000;        //적용할 보유 코인
#endif


    bool _isLoading;        //씬 이동 요청 여부
    bool _isTitleVisible;		//타이틀 표시 상태
    bool _isLobbyInteractable;		//로비 요청 허용 여부
    bool _isShopOpen;		//상점 표시 상태

    /// <summary>
    /// 초기화 전 타이틀과 로비 입력 차단
    /// </summary>
    void Awake ()
    {
        //비활성 뷰도 직접 초기화하고 초기 입력과 표시 상태를 코드에서 통일
        _titleView.Init( );
        _lobbyView.Init( );
        _difficultyPanel.SetInteractable( false );
        _difficultyPanel.SetVisible( false, true );
    }

    /// <summary>
    /// 화면 입력과 완료 알림 연결
    /// </summary>
    void OnEnable ()
    {
        _titleView.OnStartRequested += HandleTitleStartRequested;
        _titleView.OnSettingRequested += HandleTitleSettingRequested;
        _titleView.OnExitRequested += HandleExitRequested;
        _lobbyView.OnHomeRequested += HandleHomeRequested;
        _lobbyView.OnStartRequested += HandleStartRequested;
        _lobbyView.OnShopRequested += HandleShopRequested;
        _lobbyView.OnDifficultyRequested += HandleDifficultyRequested;
        _difficultyPanel.OnConfirmed += HandleDifficultyConfirmed;
        _difficultyPanel.OnCancelled += HandleLobbyReturned;

        _shopPresenter.OnClosed += HandleShopClosed;
        _selectPresenter.OnCancelled += HandleLobbyReturned;
        _selectPresenter.OnConfirmed += HandleSelectionConfirmed;

        _settingPanel.OnClosed += HandleSettingClosed;
    }

    /// <summary>
    /// 화면 입력과 완료 알림 해제
    /// </summary>
    void OnDisable ()
    {
        _titleView.OnStartRequested -= HandleTitleStartRequested;
        _titleView.OnSettingRequested -= HandleTitleSettingRequested;
        _titleView.OnExitRequested -= HandleExitRequested;
        _lobbyView.OnHomeRequested -= HandleHomeRequested;
        _lobbyView.OnStartRequested -= HandleStartRequested;
        _lobbyView.OnShopRequested -= HandleShopRequested;
        _lobbyView.OnDifficultyRequested -= HandleDifficultyRequested;
        _difficultyPanel.OnConfirmed -= HandleDifficultyConfirmed;
        _difficultyPanel.OnCancelled -= HandleLobbyReturned;

        _shopPresenter.OnClosed -= HandleShopClosed;

        _selectPresenter.OnCancelled -= HandleLobbyReturned;
        _selectPresenter.OnConfirmed -= HandleSelectionConfirmed;

        _settingPanel.OnClosed -= HandleSettingClosed;
    }

    /// <summary>
    /// 판매 목록과 난이도 확인 후 타이틀 준비
    /// </summary>
    void Start ()
    {
        //공통 서비스 준비 후 타이틀과 로비 기능 초기화
        GameManager gm = GameManager.Instance;

        if ( gm == null || gm.IsReady == false )
            return;

        if ( _dataMap == null )
        {
            Debug.LogError( "[로비] 판매 목록을 연결해 주세요.", this );
            return;
        }

        if ( _dataMap.Validate( ) == false )
            return;

        GameDifficulty difficulty;		//초기 선택 난이도

        //선택한 난이도가 조회되지 않으면 노멀 난이도로 설정
        if ( GameDifficultySelection.TryGetSelected( out difficulty ) == false )
            difficulty = GameDifficulty.Normal;

        if ( _difficultyPanel.Init( _difficultyMap, difficulty ) == false )
            return;

        _shopPresenter.Init( _dataMap, gm.Save );
        _selectPresenter.Init( _dataMap.Data, gm.Save );
        _settingPanel.Init( gm.Settings );

        //초기 패널을 모두 숨긴 뒤 Canvas를 켜서 로비와 상점이 먼저 노출되지 않도록 처리
        ActivateCanvas( _titleView );
        ActivateCanvas( _lobbyView );

        ShowTitle( );

        //타이틀 준비 완료 후 음악 전환
        gm.Sound.FadeBgm( _titleBgm, _bgmFadeDuration );

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }


    /// <summary>
    /// 뷰가 속한 Canvas 활성화
    /// </summary>
    /// <param name="view">Canvas를 준비할 화면</param>
    void ActivateCanvas ( Component view )
    {
        //비활성 부모도 조회하여 자식 패널을 켰을 때 실제 화면에 표시되도록 준비
        Canvas canvas = view.GetComponentInParent<Canvas>( true );
        canvas.gameObject.SetActive( true );
    }


    /// <summary>
    /// 타이틀 시작 요청으로 로비 열기
    /// </summary>
    void HandleTitleStartRequested ()
    {
        if ( _isTitleVisible == true )
            ShowLobby( );
    }

    /// <summary>
    /// 타이틀에서 설정 열기
    /// </summary>
    void HandleTitleSettingRequested ()
    {
        if ( _isTitleVisible == true )
            OpenSettings( );
    }

    /// <summary>
    /// 로비에서 타이틀 복귀
    /// </summary>
    void HandleHomeRequested ()
    {
        if ( _isLobbyInteractable == true )
            ShowTitle( );
    }

    /// <summary>
    /// 타이틀 종료 요청 처리
    /// </summary>
    void HandleExitRequested ()
    {
        if ( _isTitleVisible == false )
            return;

        _titleView.SetInteractable( false );
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit( );
#endif
    }

    /// <summary>
    /// 상점 닫기와 표시 상태 정리
    /// </summary>
    void CloseShop ()
    {
        if ( _isShopOpen == false )
            return;

        //닫힘 알림에서 로비를 다시 표시하지 않도록 전환 상태부터 정리
        _isShopOpen = false;
        _shopPresenter.Close( );
    }

    /// <summary>
    /// 상점의 닫힘 요청 후 로비 표시
    /// </summary>
    void HandleShopClosed ()
    {
        if ( _isShopOpen == false )
            return;

        _isShopOpen = false;
        ShowLobby( );
    }

    /// <summary>
    /// 로비를 유지하고 상점 닫은 뒤 난이도 선택 열기
    /// </summary>
    void HandleDifficultyRequested ()
    {
        if ( _isLobbyInteractable == false )
            return;

        CloseShop( );
        _difficultyPanel.Open( );
    }

    /// <summary>
    /// 확정한 난이도 전달 후 로비 복귀
    /// </summary>
    /// <param name="difficulty">확정한 난이도</param>
    void HandleDifficultyConfirmed ( GameDifficulty difficulty )
    {
        GameDifficultySelection.SetSelected( difficulty );
        ShowLobby( );
    }

    /// <summary>
    /// 로비 버튼을 유지한 채 상점 표시 전환
    /// </summary>
    void HandleShopRequested ()
    {
        if ( _isLobbyInteractable == false )
            return;

        //로비는 유지하고 열려 있는 난이도 선택 화면만 숨김
        SetLobbyVisible( true );

        if ( _isShopOpen == true )
        {
            CloseShop( );
            return;
        }

        //로비의 시작, 홈, 난이도 버튼을 상점에서도 그대로 사용
        _isShopOpen = true;
        _shopPresenter.Open( );
    }

    /// <summary>
    /// 난이도 확정 후 로비 입력 차단과 반입 선택 요청
    /// </summary>
    void HandleStartRequested ()
    {
        if ( _isLobbyInteractable == false )
            return;

        GameDifficultySelection.SetSelected( _difficultyPanel.SelectedDifficulty );

        CloseShop( );
        SetLobbyInteractable( false );
        _difficultyPanel.SetInteractable( false );
        _difficultyPanel.SetVisible( false );
        _selectPresenter.Open( );
    }

    /// <summary>
    /// 상점 종료 또는 선택 취소 후 로비 입력 복구
    /// </summary>
    void HandleLobbyReturned ()
    {
        ShowLobby( );
    }

    /// <summary>
    /// 난이도와 반입 요청 준비 후 플레이 씬 이동
    /// </summary>
    /// <param name="items">반입 순서별 아이템</param>
    void HandleSelectionConfirmed ( SupportItemData [ ] items )
    {
        if ( _isLoading == true )
            return;

        _isLoading = true;
        bool loaded = false;		//씬 이동 요청 성공 여부
        SupportItemRunRequest request = null;		//이번 이동의 반입 요청

        try
        {
            GameManager game = GameManager.Instance;
            if ( game == null || game.IsReady == false )
            {
                Debug.LogWarning( "[플레이 이동 실패] 공통 서비스가 준비되지 않았습니다.", this );
                return;
            }

            if ( game.Pool.AllReturnToPool( gameObject.scene ) == false )
                return;

            //반환 실패 때 다음 씬의 반입 요청을 남기지 않음
            if ( SupportItemRunRequest.TryPrepare(
                items, game.Save, out request ) == false )
            {
                Debug.LogError(
                    "[플레이 이동 실패] 아직 전달하지 않은 반입 요청이 있습니다.", this );
                return;
            }

            GameDifficultySelection.SetSelected( _difficultyPanel.SelectedDifficulty );
            SceneManager.LoadScene( "Play" );
            loaded = true;
        }
        finally
        {
            if ( loaded == false )
            {
                request?.Cancel( );
                _isLoading = false;
                HandleLobbyReturned( );
            }
        }
    }

    /// <summary>
    /// 설정 종료 후 타이틀 복원
    /// </summary>
    void HandleSettingClosed ()
    {
        ShowTitle( );
    }

    /// <summary>
    /// 타이틀을 숨기고 설정 열기
    /// </summary>
    void OpenSettings ()
    {
        HideTitle( );
        HideLobby( );
        _settingPanel.Open( );
    }

    /// <summary>
    /// 로비를 숨기고 타이틀 표시
    /// </summary>
    void ShowTitle ()
    {
        CloseShop( );
        HideLobby( );
        _isTitleVisible = true;
        _titleView.SetVisible( true );
        _titleView.SetInteractable( true );
    }

    /// <summary>
    /// 타이틀 입력 차단과 화면 숨김
    /// </summary>
    void HideTitle ()
    {
        _isTitleVisible = false;
        _titleView.SetInteractable( false );
        _titleView.SetVisible( false );
    }

    /// <summary>
    /// 로비와 버튼 활성화
    /// </summary>
    void ShowLobby ()
    {
        HideTitle( );
        SetLobbyVisible( true );
        SetLobbyInteractable( true );
    }

    /// <summary>
    /// 로비 입력 차단과 화면 비활성화
    /// </summary>
    void HideLobby ()
    {
        SetLobbyInteractable( false );
        SetLobbyVisible( false );
    }

    /// <summary>
    /// 로비 표시와 난이도 선택 화면 숨김
    /// </summary>
    /// <param name="visible">표시 여부</param>
    void SetLobbyVisible ( bool visible )
    {
        _lobbyView.SetVisible( visible );
        _difficultyPanel.SetInteractable( false );
        _difficultyPanel.SetVisible( false );
    }

    /// <summary>
    /// 로비 버튼 입력 설정
    /// </summary>
    /// <param name="interactable">입력 허용 여부</param>
    void SetLobbyInteractable ( bool interactable )
    {
        _isLobbyInteractable = interactable;
        _lobbyView.SetInteractable( interactable );
    }

#if UNITY_EDITOR
    #region ----- 디버그 -----

    /// <summary>
    /// 테스트 코인 저장 후 상점 갱신
    /// </summary>
    [ContextMenu( "디버그/테스트 코인 적용" )]
    void DebugSetBalance ()
    {
        SupportItemSaveData data;

        if ( Application.isPlaying == true )
        {
            GameManager game = GameManager.Instance;

            if ( game == null || game.IsReady == false )
            {
                Debug.LogWarning(
                    "[상점 테스트] 공통 서비스가 준비되지 않았습니다.", this );
                return;
            }

            data = game.Save.LoadSupportItems( );
            data.Balance = Mathf.Max( 0, _testBalance );
            game.Save.SaveSupportItems( data );
        }
        else
        {
            data = SupportItemSave.Load( );
            data.Balance = Mathf.Max( 0, _testBalance );
            SupportItemSave.Save( data );
        }

        if ( Application.isPlaying == true && _shopPresenter != null )
            _shopPresenter.Refresh( );

        Debug.Log( $"[상점 테스트] 보유 코인 적용: {data.Balance:N0}", this );
    }

    #endregion
#endif
}
