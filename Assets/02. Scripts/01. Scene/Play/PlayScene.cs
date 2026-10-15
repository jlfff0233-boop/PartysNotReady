using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System;

/// <summary>
/// 플레이 씬 중재 - 시작 승인과 종료, 사건 설정과 보상 연결
/// </summary>
public class PlayScene : MonoBehaviour
{
    [Header( "----- 설정 데이터 -----" )]
    [SerializeField] EventSettingsDataMap _eventSettingsDataMap;		//변칙 사건 설정

    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] Player _player;		//플레이어
    [SerializeField] InputHandler _inputHandler;		//플레이어 입력
    [SerializeField] CameraFocus _cameraFocus;		//시점 대상
    [SerializeField] Camera _camera;		//플레이 카메라
    [SerializeField] PlayerHud _playerHud;		//로컬 플레이어 HUD
    [SerializeField] ProduceHud _produceHud;		//로컬 플레이어 제작 HUD
    [SerializeField] GameObject _canvasHud;     //HUD 루트
    [SerializeField] ScreenEffect _screenEffect;        //포획과 스턴 화면 연출
    [SerializeField] CameraEffect _cameraEffect;		//스턴 카메라 연출
    [SerializeField] StunEffect _stunEffect;		//스턴 넘어짐 연출
    [SerializeField] CaptureEffect _captureEffect;		//플레이어 납치 자세 연출

    [Header( "----- 일시정지 -----" )]
    [SerializeField] PausePanel _pausePanel; //일시정지 메뉴
    [SerializeField] ExitPanel _exitPanel; //나가기 팝업
    [SerializeField] SettingPanel _settingPanel; //공통 설정
    [SerializeField] UnityEngine.UI.Button _pauseButton; //메뉴 진입 버튼, 선택

    [Header( "----- 공통 경로 -----" )]
    [SerializeField] RouteGraph _routeGraph;		//공용 경로 그래프

    [Header( "----- NPC 공용 설정 -----" )]
    [SerializeField] Room _npcPartyRoom;		//NPC 기본 거점
    [SerializeField] MaterialMap _npcAppearanceMap;		//NPC 무늬 후보 목록

    [Header( "----- 프레젠터 -----" )]
    [SerializeField] GameFlowController _flow;		//게임 흐름 중재자
    [SerializeField] IngredientStockManager _ingredientStock;		//재고 중재자
    [SerializeField] EnemySceneController _enemyScene;		//적 배치와 초기화
    [SerializeField] AnomalySpawnSystem _pressure;		//발생 압력 중재자
    [SerializeField] MoodSystem _mood;		//분위기 진행 중재자
    [SerializeField] ResultEvaluationSystem _resultEvaluation;		//결과 집계 중재자
    [SerializeField] ProductionManager _product;		//제작 목록 중재자
    [SerializeField] MissionResultRecorder _mission;		//미션 입력 준비 확인
    [SerializeField] PreparationSystem _preparation;        //준비 목표 입력 확인

    [Header( "----- 서포트 아이템 -----" )]
    [SerializeField] SupportItemData [ ] _supportTests = new SupportItemData [ 0 ];		//테스트 서포트 아이템 목록

    [Header( "----- 배경음 -----" )]
    [SerializeField] AudioClip _playBgm;		//플레이 배경음
    [SerializeField] AudioClip _resultBgm;		//결과 화면 배경음
    [SerializeField, Min( 0f )] float _bgmFadeDuration = 0.5f;      //각 전환 단계 시간

#if UNITY_EDITOR
    PlaySceneTestSettings _testSettings;		//이번 플레이의 에디터 테스트 설정

#endif


    bool _timeStopped;		//시간 정지 적용 여부
    float _prevTimeScale;       //정지 전 시간 배율
    bool _isReturning;        //씬 이동 요청 여부
    bool _isPlayStopped;		//플레이 동작 중지 여부
    bool _isQuitting;		//애플리케이션 종료 여부

    AnomalyEventSystem [ ] _anomalyPresenters;       //씬 변칙 사건
    /// <summary>
    /// 현재 씬에서 초기화할 NPC
    /// </summary>
    List<Npc> _npcs = new( );
    PlayPauseController _pause;		//일시정지 중재
    PlayExitCoordinator _exit;		//제작과 재고 정리, 씬 객체 반환
    PlayScreenEffectController _screenEffects;		//플레이어 상태 연출 중재
    PlayInputController _playInput;		//플레이 입력
    PlayLogPresenter _playLog;		//플레이 로그

    PlayerItemHolder _interact;		//아이템 보유
    SoundManager _sound;		//공통 배경음 재생
    PlaySupportController _playSupport;		//서포트 준비와 효과 수명
    SupportItemRunRequest _runRequest;		//이번 플레이의 반입 요청
    CoinReward _coinReward = new( );		//게임 종료 코인 보상


#if UNITY_EDITOR
    /// <summary>
    /// AI 실행 제외 테스트 여부
    /// </summary>
    bool IsAiTest => _testSettings != null && _testSettings.DisableAi == true;
#endif


    #region ----- 초기화 -----

    /// <summary>
    /// 같은 씬의 활성 및 비활성 NPC에 공용 설정 전달
    /// </summary>
    /// <returns>모든 NPC 준비 성공 여부</returns>
    bool InitNpcs ()
    {
        _npcs.Clear( );
        foreach ( Npc npc in FindObjectsByType<Npc>(
            FindObjectsInactive.Include, FindObjectsSortMode.None ) )
        {
            if ( npc.gameObject.scene != gameObject.scene )
                continue;

            if ( npc.Init( _npcPartyRoom, _routeGraph, _npcAppearanceMap ) == false )
                return false;

            _npcs.Add( npc );
        }

        return true;
    }

    /// <summary>
    /// 씬에 배치된 적 준비
    /// </summary>
    /// <returns>준비 성공 여부</returns>
    bool InitEnemies ()
    {
#if UNITY_EDITOR
        if ( IsAiTest == true )
            return true;
#endif

        if ( _enemyScene == null )
        {
            Debug.LogError( "[플레이 시작 실패] " +
                "적 씬 프레젠터를 연결해 주세요.", this );
            return false;
        }

        return _enemyScene.Init(
            _player.transform, _cameraFocus.transform, _routeGraph, _ingredientStock );
    }

    /// <summary>
    /// 난이도와 플레이 시스템 준비
    /// </summary>
    /// <returns>준비 성공 여부</returns>
    bool InitPlay ()
    {
        //공통 서비스 준비 실패도 기존 시작 실패 경로로 전달
        GameManager gm = GameManager.Instance;

        if ( gm == null || gm.IsReady == false )
            return false;

        //화면, 카메라와 스턴 연출 준비 후 플레이어 상태 알림 연결
        if ( _screenEffects.Init( ) == false )
            return false;

        _sound = gm.Sound;

        bool settingsReady;		//난이도 설정 준비 결과
        string reason;		//설정 준비 실패 사유

        //선택한 난이도 조회
        if ( GameDifficultySelection.TryGetSelected(
            out GameDifficulty difficulty ) == true )
        {
            settingsReady = _flow.TrySelectDifficulty( difficulty, out reason );
        }
        else
        {
            settingsReady = _flow.TryPrepareSettings( out reason );
        }

        if ( settingsReady == false )
        {
            Debug.LogError( $"[플레이 시작 실패] {reason}", this );
            return false;
        }

        //공급원과 비활성 아이템 준비 후 그릇 연결
        if ( InitItems( ) == false || InitProduction( ) == false )
            return false;

        //제작 준비를 마친 뒤 공급 그릇의 등록 경로 연결
        if ( _ingredientStock != null )
            _ingredientStock.BindProduction( _product );

        if ( InitNpcs( ) == false || InitEnemies( ) == false )
            return false;

        if ( _preparation.Init( ) == false ||
             _mission.Init( _flow ) == false )
            return false;

        //분위기와 평가가 구독하기 전에 시작 손님 확정
        if ( NpcRegister.TrySetInitialNpcs( gameObject.scene ) == false )
        {
            Debug.LogError( "[플레이 시작 실패] " +
                "시작 NPC가 없거나 이미 확정되었습니다.", this );
            return false;
        }

        if ( InitAnomalys( ) == false )
            return false;

        _flow.OnEnded += HandlePlayEnd;
        _flow.OnReturnRequested += HandleReturnRequested;

        GameDifficultySettings settings = _flow.Settings;		//선택한 난이도 설정

        if ( _flow.Init( _resultEvaluation, PrepareEnd ) == false ||
             InitPressure( ) == false ||
             _mood.Init(
                 _flow, settings.Mood, _anomalyPresenters ) == false ||
             _resultEvaluation.Init( settings.Score, _anomalyPresenters ) == false )
        {
            Debug.LogError(
                "[플레이 시작 실패] 게임 진행 설정을 확인해 주세요.", this );
            return false;
        }

        return true;
    }

    /// <summary>
    /// 게임 시간과 초기화된 적의 자동 발생 연결
    /// </summary>
    /// <returns>발생 준비 성공 여부</returns>
    bool InitPressure ()
    {
#if UNITY_EDITOR
        if ( IsAiTest == true )
            return true;
#endif

        GameDifficultySettings settings = _flow.Settings;		//선택한 난이도 설정
        return _pressure.Init(
            _flow, settings.Pressure, settings.SpawnMap,
            _enemyScene.Enemies );
    }

    /// <summary>
    /// 변칙 사건 프레젠터 초기화
    /// </summary>
    /// <returns>초기화 완료 여부</returns>
    bool InitAnomalys ()
    {
        if ( _eventSettingsDataMap == null )
        {
            Debug.LogError( "[PlayScene] 사건 설정 맵이 없습니다.", this );
            return false;
        }

        //비활성화 상태인 것도 전부 추가
        List<AnomalyEventSystem> presenters = new( );
        AnomalyEventSystem [ ] found =
            FindObjectsByType<AnomalyEventSystem>(
            FindObjectsInactive.Include, FindObjectsSortMode.None );

        foreach ( AnomalyEventSystem presenter in found )
        {
            if ( presenter.gameObject.scene != gameObject.scene )
                continue;

            //초기화 실패 시 이전 설정을 사용하지 않도록 먼저 해제
            presenter.Init( null );

            //프레젠터 타입에 맞는 설정 가져오기
            if ( _eventSettingsDataMap.TryGetSettings(
                presenter.EventType,
                out AnomalyEventSettings settings,
                out string reason ) == false )
            {
                Debug.LogError( $"[PlayScene] {presenter.name}: {reason}", presenter );
                return false;
            }

            presenter.Init( settings );
            presenters.Add( presenter );
        }

        _anomalyPresenters = presenters.ToArray( );
        return true;
    }

    /// <summary>
    /// 제작이 필요한 구성의 화면과 제작 목록 준비
    /// </summary>
    /// <returns>준비 성공 여부</returns>
    bool InitProduction ()
    {
        //제작 콘텐츠가 없는 구성에는 재고와 제작 HUD를 강제하지 않음
        bool requiresProduction =
            _mission.RequiresProduction ||
            _preparation.RequiresProduction ||
            _product != null;		//제작 기능 필요 여부

        if ( requiresProduction == false )
            return true;

        if ( _product == null || _produceHud == null )
        {
            Debug.LogError(
                "[플레이 시작 실패] 제작 목록과 제작 HUD를 연결해 주세요.", this );
            return false;
        }

        if ( _produceHud.IsReady == false )
        {
            Debug.LogError( $"[플레이 시작 실패] 제작 HUD 초기화 실패: {_produceHud.name}", _produceHud );
            return false;
        }

#if UNITY_EDITOR
        return _product.Init( _flow.Settings.MinigameMap,
            _testSettings != null && _testSettings.AllowMissingItemContent == true );
#else
        return _product.Init( _flow.Settings.MinigameMap );
#endif
    }

    /// <summary>
    /// 공급원 재고와 씬 아이템 준비
    /// </summary>
    /// <returns>준비 성공 여부</returns>
    bool InitItems ()
    {
        //공급원 설정과 재고 등록
        if ( _ingredientStock != null )
        {
#if UNITY_EDITOR
            bool initialized = _ingredientStock.Init(
                _testSettings != null &&
                _testSettings.AllowMissingItemContent == true );		//재고 준비 결과
#else
            bool initialized = _ingredientStock.Init( );		//재고 준비 결과
#endif

            if ( initialized == false )
            {
                Debug.LogError( $"[플레이 시작 실패] " +
                    $"공급원 재고 초기화 실패: {_ingredientStock.name}", _ingredientStock );
                return false;
            }
        }

        //비활성 선배치 아이템의 실제 구성 확인
        foreach ( ItemCarry item in FindObjectsByType<ItemCarry>(
            FindObjectsInactive.Include, FindObjectsSortMode.None ) )
        {
            if ( item.gameObject.scene != gameObject.scene )
                continue;

            ItemWorldController view = item.GetComponent<ItemWorldController>( );

            if ( view != null )
                view.Init( );

            if ( item.IsReady == true )
                continue;

            Debug.LogError( $"[플레이 시작 실패] 아이템 구성 확인: {item.name}", item );
            return false;
        }

        return true;
    }

    #endregion


    private void Awake ()
    {
        _exit = new PlayExitCoordinator(
            gameObject.scene, _product, _ingredientStock, StopPlay, this );
        _screenEffects = new PlayScreenEffectController(
            _player, _screenEffect, _cameraEffect, _stunEffect, _captureEffect );

#if UNITY_EDITOR
        _testSettings = GetComponent<PlaySceneTestSettings>( );
#endif

        _runRequest = SupportItemRunRequest.TakePending( );

        if ( _player == null ) return;

        _interact = new PlayerItemHolder( _player.HoldPoint, _player.HoldGap );
        PlayerSupportItems supportItems = new( );		//이번 플레이의 보유 목록
        _player.SetSupportItems( supportItems );

        //플레이어 초기화
        _player.Init( _interact, _cameraFocus.transform, _camera );
        _playSupport = new PlaySupportController(
            _player, supportItems, _inputHandler, _playerHud, _ingredientStock );
        _playInput = new PlayInputController(
            _player, _inputHandler, _cameraFocus,
            _playerHud, _produceHud, _product );
#if UNITY_EDITOR
        _inputHandler.OnRestartInput += RestartPlay;
        _inputHandler.OnDebugInput += HandleDebugInput;
#endif
    }

    private void Start ()
    {
        if ( _pausePanel == null || _exitPanel == null || _settingPanel == null )
        {
            Debug.LogError( "[플레이 시작 실패] 일시정지 패널, 나가기 팝업과 설정 패널을 연결해 주세요.", this );
            CancelStart( );
            return;
        }
        GameManager game = GameManager.Instance;
        if ( game == null || game.IsReady == false )
        {
            CancelStart( );
            return;
        }
        _settingPanel.Init( game.Settings );
        //현재는 싱글, 멀티 연결 시 세션 정보로 월드 정지 여부 전달
        _pause = new PlayPauseController(
            _pausePanel, _exitPanel, _settingPanel, _playInput, _produceHud, true );
        _pause.OnTitleRequested += HandleAbandonToTitleRequested;
        _pause.OnQuitRequested += HandleQuitRequested;
        _inputHandler.OnPauseInput += HandlePauseRequested;
        if ( _pauseButton != null )
            _pauseButton.onClick.AddListener( HandlePauseRequested );
        if ( InitPlay( ) == false )
        {
            CancelStart( );
            return;
        }

        if ( _produceHud != null )
            _produceHud.Close( );

        //서포트 준비와 반입 차감 후 게임 시작
        if ( TryBeginGame( ) == false )
        {
            CancelStart( );
            return;
        }

        //시작 알림 중 종료된 경우 입력 연결 차단
        if ( _playSupport.IsRunning == false )
            return;

        //초기 분위기 표시 후 실제 변화만 알림으로 갱신
        _flow.SetMood( _mood.Current );
        _mood.OnMoodChanged += _flow.SetMood;

        _playLog = new PlayLogPresenter(
            _flow, _product, _anomalyPresenters );
        _playLog.Begin( );
        _playInput.Begin( );

        _player.OnRoomChanged -= HandlePlayerRoomChanged;
        _player.OnRoomChanged += HandlePlayerRoomChanged;
        _player.RefreshRoom( );
        HandlePlayerRoomChanged( _player.CurrentRoom );

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void LateUpdate ()
    {
        if ( _isPlayStopped == true )
            return;

        _playSupport.Tick( Time.deltaTime );

        _interact.Tick( );
        _playInput.Tick( );
    }

    /// <summary>
    /// 플레이어 방 변경을 진행 화면에 전달
    /// </summary>
    /// <param name="room">현재 방</param>
    void HandlePlayerRoomChanged ( Room room )
    {
        if ( _isPlayStopped == false )
            _flow.SetLocation( room );
    }

    private void OnDestroy ()
    {
        if ( _player != null )
            _player.OnRoomChanged -= HandlePlayerRoomChanged;
        if ( _inputHandler != null )
            _inputHandler.OnPauseInput -= HandlePauseRequested;
        if ( _pauseButton != null )
            _pauseButton.onClick.RemoveListener( HandlePauseRequested );
        _pause?.Dispose( );
        _screenEffects?.Dispose( );

        _runRequest?.Cancel( );

        //정상 이동은 로드 전에 정리하고 외부 씬 해제만 여기서 보완
        GameManager game = GameManager.Instance;

        if ( _exit.IsPrepared == false &&
            _isQuitting == false &&
             game != null && game.IsReady == true &&
             gameObject.scene.isLoaded == true )
        {
            TryPrepareSceneExit( );
        }

        _playInput?.Suspend( );
        _playSupport?.End( );

        if ( _interact != null )
            _interact.Dispose( );

        if ( _playLog != null )
            _playLog.Stop( );

        _playInput?.Dispose( );

        if ( _flow != null )
        {
            _flow.OnEnded -= HandlePlayEnd;
            _flow.OnReturnRequested -= HandleReturnRequested;
        }

        if ( _mood != null )
        {
            if ( _flow != null )
                _mood.OnMoodChanged -= _flow.SetMood;
            _mood.Stop( );
        }

        if ( _resultEvaluation != null )
            _resultEvaluation.Stop( );

        if ( _enemyScene != null )
            _enemyScene.Stop( );

#if UNITY_EDITOR
        if ( _inputHandler != null )
        {
            _inputHandler.OnRestartInput -= RestartPlay;
            _inputHandler.OnDebugInput -= HandleDebugInput;
        }
#endif

        //재시작과 씬 이동 시 정지 전 시간 배율 복원
        if ( _timeStopped == true )
            Time.timeScale = _prevTimeScale;
    }

    /// <summary>
    /// 애플리케이션 종료에서는 씬 이동용 반환 생략
    /// </summary>
    private void OnApplicationQuit ()
    {
        _isQuitting = true;
    }

    /// <summary>
    /// 진행 중 메뉴 입력 처리
    /// </summary>
    void HandlePauseRequested ()
    {
        if ( _isPlayStopped == false && _playSupport != null &&
             _playSupport.IsRunning == true && _isReturning == false )
            _pause?.HandleEscape( );
    }

    /// <summary>
    /// 보상 정산 없이 타이틀 복귀
    /// </summary>
    void HandleAbandonToTitleRequested ()
    {
        HandleReturnRequested( );
        if ( _isReturning == false )
            _pause.AllowExitRetry( );
    }

    /// <summary>
    /// 플레이 정리 후 애플리케이션 종료
    /// </summary>
    void HandleQuitRequested ()
    {
        if ( TryPrepareSceneExit( ) == false )
        {
            _pause.AllowExitRetry( );
            return;
        }
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit( );
#endif
    }

    /// <summary>
    /// 결과 확정 후 플레이 종료
    /// </summary>
    /// <param name="result">확정된 결과</param>
    void HandlePlayEnd ( GameResult result )
    {
        //결과 확정 후 결과 화면 음악으로 전환
        _sound.FadeBgm( _resultBgm, _bgmFadeDuration );

        bool granted = _coinReward.TryGrant(
            result, out int amount, out int balance );

        _flow.ShowReward( amount, balance, granted );

        if ( granted == false )
        {
            if ( result == null || result.IsScored == false ||
                 result.Outcome == GameOutcome.Unscored )
            {
                Debug.LogWarning(
                    $"[코인 보상] 점수 미정산으로 미지급/보유: {balance}", this );
            }
            else
            {
                Debug.LogWarning(
                    $"[코인 보상] 중복 지급 또는 잔액 한도로 미지급/보유: {balance}",
                    this );
            }
        }

        TryPrepareSceneExit( );
        _canvasHud.SetActive( false );
    }

    /// <summary>
    /// 결과 확인 후 시작 화면으로 이동
    /// </summary>
    void HandleReturnRequested ()
    {
        if ( _isReturning == true )
            return;

        _isReturning = true;
        bool loaded = false;		//씬 이동 요청 성공 여부

        try
        {
            if ( TryPrepareSceneExit( ) == false )
                return;

            SceneManager.LoadScene( "Title", LoadSceneMode.Single );
            loaded = true;
        }
        finally
        {
            if ( loaded == false )
                _isReturning = false;
        }
    }


    /// <summary>
    /// 서포트 준비와 반입 차감 후 게임 시작
    /// </summary>
    /// <returns>게임 시작 여부</returns>
    bool TryBeginGame ()
    {
        if ( _playSupport.Begin( GetSupportData( ) ) == false )
            return false;

        if ( _runRequest != null &&
             _runRequest.TryConsume( out string message ) == false )
        {
            Debug.LogError( $"[플레이 시작 실패] {message}", this );
            return false;
        }

#if UNITY_EDITOR
        if ( IsAiTest == false )
#endif
            _enemyScene.Begin( );

        if ( _flow.Begin( ) == false )
        {
            Debug.LogError(
                "[플레이 시작 실패] 게임 진행을 시작하지 못했습니다.", this );
            return false;
        }

        //시작 알림 중 정상 종료됐어도 반입은 사용한 것으로 확정
        _runRequest?.ConfirmStart( );

        //시작 알림 중 이미 종료된 게임은 결과 음악을 덮어쓰지 않음
        if ( _isPlayStopped == false )
        {
            //전체 준비와 게임 시작이 성공한 뒤 NPC 행동 허용
            foreach ( Npc npc in _npcs )
                npc.Begin( );

            //타이틀과 같은 곡이어도 플레이 시작 시 처음부터 재생
            _sound.FadeBgm( _playBgm, _bgmFadeDuration, restart: true );
        }

        return true;
    }


    #region ----- 플레이 종료 -----

    /// <summary>
    /// 시작 실패 시 반입 복원과 플레이 정리
    /// </summary>
    void CancelStart ()
    {
        _runRequest?.Cancel( );
        TryPrepareSceneExit( );
    }

    /// <summary>
    /// 종료 평가 전 발생 연결 정리
    /// </summary>
    void PrepareEnd ()
    {
        _pressure.Stop( );
    }

    /// <summary>
    /// 입력과 플레이 동작 중지
    /// </summary>
    void StopPlay ()
    {
        if ( _isPlayStopped == true )
            return;

        _isPlayStopped = true;
        if ( _player != null )
            _player.OnRoomChanged -= HandlePlayerRoomChanged;
        //종료 처리가 저장할 시간 배율을 메뉴 진입 전 값으로 복원
        _pause?.CloseForEnd( );

        _screenEffects?.Dispose( );

        _playInput?.Stop( );
        _playSupport?.End( );

        if ( _interact != null )
            _interact.Dispose( );

        if ( _playLog != null )
            _playLog.Stop( );
        if ( _resultEvaluation != null )
            _resultEvaluation.Stop( );

        if ( _pressure != null )
            _pressure.Stop( );
        if ( _mood != null )
            _mood.Stop( );

        //시작 실패도 결과를 발행하지 않고 관리자 연결만 종료
        if ( _mission != null )
            _mission.enabled = false;
        if ( _preparation != null )
            _preparation.enabled = false;
        if ( _flow != null )
            _flow.enabled = false;

        if ( _produceHud != null )
            _produceHud.Close( );

        //게임 종료 시 그릇별 제작 사운드 정리
        if ( _product != null )
            _product.StopSounds( );

        if ( _player != null )
        {
            _player.Move( Vector2.zero );
            _player.Run( false );
        }

        //물리와 시간 기반 동작 정지
        //재시작 시 기존 배율 복원
        if ( _timeStopped == false )
        {
            _prevTimeScale = Time.timeScale;
            _timeStopped = true;
            Time.timeScale = 0f;
        }

        //상태 종료와 포획 해제를 먼저 처리한 뒤 이동 중지
        if ( _enemyScene != null )
            _enemyScene.Stop( );
        StopBehaviours<Npc>( );
        StopBehaviours<AnomalyEventSystem>( );
        StopBehaviours<CharacterMover>( );
        StopBehaviours<Mover>( );

        if ( _player != null )
            _player.enabled = false;
        if ( _cameraFocus != null )
            _cameraFocus.SetLocked( true );

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// 플레이 종료 정리와 씬 이동 준비 요청
    /// </summary>
    /// <returns>씬 이동 가능한 정리 완료 여부</returns>
    bool TryPrepareSceneExit ()
    {
        return _exit.TryPrepareExit( );
    }

    /// <summary>
    /// 지정 컴포넌트 중지
    /// </summary>
    /// <typeparam name="T">중지할 컴포넌트 종류</typeparam>
    void StopBehaviours<T> () where T : MonoBehaviour
    {
        T [ ] targets = FindObjectsByType<T>( FindObjectsSortMode.None );
        foreach ( T target in targets )
        {
            if ( target != null && target.gameObject.scene == gameObject.scene )
                target.enabled = false;
        }
    }

    #endregion

    /// <summary>
    /// 이번 플레이의 서포트 아이템 목록 조회
    /// </summary>
    /// <returns>반입 또는 에디터 테스트 목록</returns>
    IReadOnlyList<SupportItemData> GetSupportData ()
    {
        if ( _runRequest != null )
            return _runRequest.Items;

#if UNITY_EDITOR
        return _supportTests;
#else
        return Array.Empty<SupportItemData>( );
#endif
    }

    #region ----- 디버그 -----

#if UNITY_EDITOR

    /// <summary>
    /// 촬영용 적 등장과 게임 진행 단축키 처리
    /// </summary>
    /// <param name="key">단축키 번호</param>
    void HandleDebugInput ( int key )
    {
        if ( _isPlayStopped == true || _isReturning == true ||
             Time.timeScale <= 0f ||
             ( _flow.State != GameFlowState.Playing &&
               _flow.State != GameFlowState.FinalRush ) )
            return;

        switch ( key )
        {
            case 1:
                DebugEnemy( true );
                break;
            case 2:
                DebugEnemy( false );
                break;
            case 3:
                DebugAllMissions( );
                _preparation.DebugCompletePreparation( );
                break;
            case 4:
                DebugFinalRush( );
                break;
        }
    }

    /// <summary>
    /// 등록된 여우의 납치 또는 까마귀 등장 테스트 실행
    /// </summary>
    /// <param name="kidnap">여우 납치 테스트 여부</param>
    void DebugEnemy ( bool kidnap )
    {
        if ( IsAiTest == true )
        {
            Debug.LogWarning( "[촬영 단축키] AI 실행 제외 설정을 해제해 주세요.", this );
            return;
        }

        foreach ( Enemy enemy in _enemyScene.Enemies )
        {
            bool started;		//테스트 시작 결과
            string reason;		//테스트 실행 결과

            if ( kidnap == true && enemy is EnemyKidnap fox )
                started = fox.TryDebugKidnap( null, false, out reason );
            else if ( kidnap == false && enemy is EnemySteal crow )
                started = _pressure.TryDebugStart( crow.DebugStartRoom, crow, out reason );
            else
                continue;

            if ( started == true )
                Debug.Log( $"[촬영 단축키] {reason}", enemy );
            else
                Debug.LogWarning( $"[촬영 단축키] {reason}", enemy );
            return;
        }

        Debug.LogWarning( "[촬영 단축키] 해당 종류의 등록된 적이 없습니다.", this );
    }

    /// <summary>
    /// 재시작
    /// </summary>
    void RestartPlay ()
    {
        if ( Application.isPlaying == false || _isReturning == true )
            return;

        string scenePath = gameObject.scene.path;
        if ( string.IsNullOrEmpty( scenePath ) == true )
        {
            Debug.LogWarning( "[플레이 재시작] 씬을 먼저 저장해 주세요.", this );
            return;
        }

        _isReturning = true;
        bool loaded = false;		//씬 재로드 요청 성공 여부

        try
        {
            if ( TryPrepareSceneExit( ) == false )
                return;

            //빌드 목록과 무관하게 현재 씬 재로드
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                scenePath, new LoadSceneParameters( LoadSceneMode.Single ) );
            loaded = true;
        }
        finally
        {
            if ( loaded == false )
                _isReturning = false;
        }
    }

    /// <summary>
    /// 시간 만료 검증
    /// </summary>
    public void DebugTimeExpired ()
    {
        if ( CheckDebugPlay( ) == true )
            _flow.DebugProgress( GameEndReason.TimeExpired );
    }

    /// <summary>
    /// NPC 전멸 종료 조건 주입
    /// </summary>
    public void DebugNpcDeparted ()
    {
        if ( CheckDebugPlay( ) == true )
        {
            Debug.Log( "[진행 검증] NPC 전멸 종료 조건만 주입합니다. " +
                "실제 NPC 상태와 Safety 평가는 변경하지 않습니다.", this );
            _flow.DebugProgress( GameEndReason.AllNpcDeparted );
        }
    }

    /// <summary>
    /// 마무리 구간 진입 검증
    /// </summary>
    public void DebugFinalRush ()
    {
        if ( CheckDebugPlay( ) == true )
            _flow.DebugProgress( GameEndReason.None );
    }

    /// <summary>
    /// 현재 단계 완료와 다음 단계 전환 검증
    /// </summary>
    public void DebugMissionStage ()
    {
        if ( CheckDebugPlay( ) == true )
            _flow.DebugCompleteMission( false );
    }

    /// <summary>
    /// 전체 미션 완료 후 진행 유지 검증
    /// </summary>
    public void DebugAllMissions ()
    {
        if ( CheckDebugPlay( ) == true )
            _flow.DebugCompleteMission( true );
    }

    /// <summary>
    /// 종료 후에도 플레이 재시작
    /// </summary>
    public void DebugRestart ()
    {
        RestartPlay( );
    }

    /// <summary>
    /// 디버그 실행에 필요한 진행 연결 확인
    /// </summary>
    /// <returns>디버그 실행 가능 여부</returns>
    bool CheckDebugPlay ()
    {
        if ( Application.isPlaying == true && _flow != null )
            return true;

        Debug.LogWarning(
            "[플레이 검증] 플레이 모드와 " +
            "진행 Presenter 연결을 확인해 주세요.", this );
        return false;
    }

#endif

    #endregion
}
