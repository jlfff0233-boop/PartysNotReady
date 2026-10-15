using System;
using UnityEngine;

/// <summary>
/// 일시정지 중재 - 메뉴 전환, 플레이 입력과 시간 및 오디오 수명 관리
/// </summary>
public class PlayPauseController : IDisposable
{
    enum Screen
    {
        Closed, //플레이
        Menu, //메뉴
        Settings, //설정
        Exit //나가기
    }

    PausePanel _panel; //메뉴
    ExitPanel _exit; //나가기 팝업
    SettingPanel _settings; //공통 설정
    PlayInputController _input; //로컬 입력
    ProduceHud _produce; //제작 화면
    bool _stopWorld; //싱글 월드 정지 여부
    Screen _screen; //현재 화면
    float _previousTimeScale; //정지 전 시간 배율
    bool _previousAudioPause; //정지 전 오디오 상태
    bool _ownsWorldPause; //이 메뉴가 적용한 월드 정지
    bool _isEnding; //종료 후 재개 차단

    /// <summary>
    /// 타이틀 복귀 요청
    /// </summary>
    public event Action OnTitleRequested;
    /// <summary>
    /// 게임 종료 요청
    /// </summary>
    public event Action OnQuitRequested;

    /// <summary>
    /// 진행을 유지한 채 입력과 월드 일시정지
    /// </summary>
    public void Open ()
    {
        if ( _screen != Screen.Closed )
            return;
        _screen = Screen.Menu;
        _input.SetPaused( true );
        _produce.SetInputBlocked( true );
        if ( _stopWorld == true )
        {
            _previousTimeScale = Time.timeScale;
            _previousAudioPause = AudioListener.pause;
            _ownsWorldPause = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }
        _panel.SetMenuVisible( true );
        _panel.SetVisible( true );
    }

    /// <summary>
    /// 시간과 오디오를 복원하고 메뉴 정리
    /// </summary>
    /// <param name="resume">플레이 입력도 재개할지 여부</param>
    void Close ( bool resume )
    {
        if ( _screen == Screen.Closed )
            return;
        _screen = Screen.Closed;
        //설정 닫기 알림에서 메뉴를 다시 열지 않도록 먼저 상태 변경
        if ( _settings != null )
            _settings.CancelEdit( );
        if ( _exit != null )
            _exit.SetVisible( false, true );
        if ( _panel != null )
            _panel.SetVisible( false, true );
        if ( _ownsWorldPause == true )
        {
            Time.timeScale = _previousTimeScale;
            AudioListener.pause = _previousAudioPause;
            _ownsWorldPause = false;
        }
        if ( _produce != null )
            _produce.SetInputBlocked( false );
        if ( resume == true )
            _input.SetPaused( false );
    }

    /// <summary>
    /// 플레이 재개
    /// </summary>
    public void Resume () => Close( true );

    /// <summary>
    /// 종료 화면으로 전환하며 일시정지 소유 상태 회수
    /// </summary>
    public void CloseForEnd () => Close( false );

    /// <summary>
    /// 설정 편집 종료 후 메뉴 복귀
    /// </summary>
    void ShowMenu ()
    {
        if ( _screen == Screen.Closed )
            return;
        _screen = Screen.Menu;
        _exit.SetVisible( false, true );
        _panel.SetMenuVisible( true );
    }

    /// <summary>
    /// 공통 설정 패널 열기
    /// </summary>
    void OpenSettings ()
    {
        _screen = Screen.Settings;
        _panel.SetMenuVisible( false );
        _settings.Open( );
    }

    /// <summary>
    /// 나가기 팝업 열기
    /// </summary>
    void OpenExit ()
    {
        _screen = Screen.Exit;
        _panel.SetMenuVisible( false );
        _exit.SetInteractable( true );
        _exit.SetVisible( true );
    }

    /// <summary>
    /// 타이틀 복귀 요청과 중복 클릭 차단
    /// </summary>
    void RequestTitle ()
    {
        _exit.SetInteractable( false );
        OnTitleRequested?.Invoke( );
    }

    /// <summary>
    /// 게임 종료 요청과 중복 클릭 차단
    /// </summary>
    void RequestQuit ()
    {
        _exit.SetInteractable( false );
        OnQuitRequested?.Invoke( );
    }

    /// <summary>
    /// 현재 화면에 따라 ESC 입력 처리
    /// </summary>
    public void HandleEscape ()
    {
        if ( _isEnding == true )
            return;
        switch ( _screen )
        {
            case Screen.Closed: Open( ); break;
            case Screen.Menu: Resume( ); break;
            case Screen.Settings: _settings.HandleCancelInput( ); break;
            case Screen.Exit: ShowMenu( ); break;
        }
    }

    /// <summary>
    /// 패널과 입력을 연결하고 초기 화면 숨김
    /// </summary>
    /// <param name="panel">일시정지 메뉴</param>
    /// <param name="exit">나가기 팝업</param>
    /// <param name="settings">공통 설정 패널</param>
    /// <param name="input">로컬 플레이 입력</param>
    /// <param name="produce">제작 화면</param>
    /// <param name="stopWorld">싱글 월드 정지 여부</param>
    public PlayPauseController (
        PausePanel panel, ExitPanel exit, SettingPanel settings,
        PlayInputController input, ProduceHud produce, bool stopWorld )
    {
        _panel = panel;
        _exit = exit;
        _settings = settings;
        _input = input;
        _produce = produce;
        _stopWorld = stopWorld;
        _panel.OnResumeRequested += Resume;
        _panel.OnSettingsRequested += OpenSettings;
        _panel.OnExitRequested += OpenExit;
        _exit.OnCloseRequested += ShowMenu;
        _exit.OnTitleRequested += RequestTitle;
        _exit.OnQuitRequested += RequestQuit;
        _settings.OnClosed += ShowMenu;
        _exit.SetVisible( false, true );
        _panel.SetVisible( false, true );
    }

    /// <summary>
    /// 정리 실패 후 나가기 선택 다시 허용
    /// </summary>
    public void AllowExitRetry ()
    {
        //플레이 종료 정리가 실패해도 나가기 재시도 화면은 유지
        _isEnding = true;
        _screen = Screen.Exit;
        _panel.SetMenuVisible( false );
        _panel.SetVisible( true, true );
        _exit.SetVisible( true, true );
        _exit.AllowExitRetry( );
    }

    /// <summary>
    /// 화면 상태와 알림 구독 정리
    /// </summary>
    public void Dispose ()
    {
        CloseForEnd( );
        _panel.OnResumeRequested -= Resume;
        _panel.OnSettingsRequested -= OpenSettings;
        _panel.OnExitRequested -= OpenExit;
        _exit.OnCloseRequested -= ShowMenu;
        _exit.OnTitleRequested -= RequestTitle;
        _exit.OnQuitRequested -= RequestQuit;
        _settings.OnClosed -= ShowMenu;
        OnTitleRequested = null;
        OnQuitRequested = null;
    }
}
