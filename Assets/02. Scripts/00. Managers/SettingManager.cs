using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 사용자 설정 관리 - 음량과 화면 적용, 저장
/// </summary>
public class SettingManager : MonoBehaviour, IGameService
{
    [Header( "----- 기본 설정 -----" )]
    [SerializeField] UserSettingsData _defaults = new( );       //최초 실행과 초기화 기준

    [Header( "----- 지원 언어 -----" )]
    [SerializeField] SystemLanguage [ ] _languages = Array.Empty<SystemLanguage>( );		//선택 가능한 언어, 첫 항목은 기본 언어

    SaveManager _save;		//설정 저장
    SoundManager _sound;		//음량 적용
    UserSettingsData _current;      //현재 적용된 설정
    KeyboardSettings _keyboard;     //키 설정 검사와 적용

    /// <summary>
    /// 선택 가능한 언어 목록
    /// </summary>
    public IReadOnlyList<SystemLanguage> Languages => _languages;

    /// <summary>
    /// 현재 적용된 언어
    /// </summary>
    public SystemLanguage Language => _current.Language;

    /// <summary>
    /// 언어 변경 알림
    /// </summary>
    public event Action<SystemLanguage> OnLanguageChanged;

    /// <summary>
    /// 사용자 설정 변경 알림
    /// </summary>
    public event Action OnSettingsChanged;


    /// <summary>
    /// 입력 연결과 저장된 사용자 설정 준비
    /// </summary>
    /// <param name="game">공통 매니저</param>
    /// <returns>초기화 성공 여부</returns>
    public bool Init ( GameManager game )
    {
        bool completed = false;     //초기화 완료 여부

        _save = game.Save;
        _sound = game.Sound;

        try
        {
            InputActionMap map =
                InputSystem.actions?.FindActionMap ( "Player" , false );

            if ( map == null )
            {
                Debug.LogError (
                    "[설정 초기화 실패] Player 입력 맵을 확인해 주세요." , this );
                return false;
            }

            //Unknown은 실제 지원 언어 항목으로 등록하지 않음
            if ( Array.IndexOf ( _languages , SystemLanguage.Unknown ) >= 0 )
            {
                Debug.LogError (
                    "[설정 초기화 실패] 지원 언어 목록에서 Unknown을 제외해 주세요." , this );
                return false;
            }

            _keyboard = new KeyboardSettings ( map );

            UserSettingsData data;

            try
            {
                data = _save.LoadSettings ( );
            }
            catch ( Exception exception ) when (
                exception is ArgumentException ||
                exception is InvalidOperationException )
            {
                Debug.LogError (
                    $"[설정 초기화 실패] {exception.Message}" , this );
                return false;
            }

            UserSettingsData initial =
                PrepareInitialSettings ( data ?? _defaults );

            completed = ApplySettings ( initial );
            return completed;
        }
        finally
        {
            //실패 반환과 예외 모두 키 설정과 부분 연결 정리
            if ( completed == false )
                Shutdown ( );
        }
    }

    /// <summary>
    /// 현재 적용된 설정 복사
    /// </summary>
    /// <returns>독립된 설정 데이터</returns>
    public UserSettingsData GetSettings ()
    {
        return _current.Copy( );
    }

    /// <summary>
    /// 지원 언어 여부 확인
    /// </summary>
    /// <param name="language">확인할 언어</param>
    /// <returns>지원 여부</returns>
    bool SupportsLanguage ( SystemLanguage language )
    {
        //번역 콘텐츠 준비 전에는 언어 미선택 상태 허용
        if ( _languages.Length == 0 )
            return language == SystemLanguage.Unknown;

        return Array.IndexOf( _languages, language ) >= 0;
    }

    /// <summary>
    /// 출력 해상도 변경과 무관한 모니터 크기 조회
    /// </summary>
    /// <returns>모니터의 가로와 세로 크기</returns>
    Vector2Int GetDisplaySize ()
    {
        //전체 화면 해상도를 낮춘 뒤에도 원래 크기로 복원할 수 있도록 시스템 크기 사용
        return new Vector2Int( Display.main.systemWidth, Display.main.systemHeight );
    }

    /// <summary>
    /// 최초 해상도와 자동 선택 언어 준비
    /// </summary>
    /// <param name="source">저장 또는 기본 설정</param>
    /// <returns>초기 적용할 설정</returns>
    UserSettingsData PrepareInitialSettings ( UserSettingsData source )
    {
        UserSettingsData data = source.Copy( );
        Vector2Int display = GetDisplaySize( );

        //기존 저장에 해상도가 없으면 현재 창 크기로 시작
        if ( data.Width == 0 && data.Height == 0 )
        {
            data.SetResolution( Screen.width, Screen.height );
        }

        //더 작은 모니터로 변경된 경우 현재 화면 크기까지 조정
        data.SetWindowSize(
            Mathf.Min( data.WindowWidth, display.x ),
            Mathf.Min( data.WindowHeight, display.y ) );
        data.SetScreenMode( data.ScreenMode );
        data.SetResolution(
            Mathf.Min( data.Width, display.x ),
            Mathf.Min( data.Height, display.y ) );

        if ( data.Language == SystemLanguage.Unknown &&
            _languages.Length > 0 )
        {
            SystemLanguage language = SupportsLanguage( Application.systemLanguage ) == true
                ? Application.systemLanguage
                : _languages [ 0 ];
            data.SetLanguage( language );
        }

        return data;
    }

    /// <summary>
    /// 중복 해상도를 제외한 화면 크기 목록 조회
    /// </summary>
    /// <returns>선택 가능한 가로와 세로 크기</returns>
    public List<Vector2Int> GetResolutionOptions ()
    {
        List<Vector2Int> options = new( );
        Vector2Int display = GetDisplaySize( );

        foreach ( Resolution resolution in Screen.resolutions )
        {
            Vector2Int size = new( resolution.width, resolution.height );

            if ( size.x > display.x || size.y > display.y )
                continue;

            //주사율만 다른 같은 크기는 한 번만 표시
            if ( options.Contains( size ) == false )
                options.Add( size );
        }

        Vector2Int current = new( _current.Width, _current.Height );

        if ( options.Contains( current ) == false )
            options.Add( current );

        return options;
    }

    /// <summary>
    /// 창 크기 선택 목록 조회
    /// </summary>
    /// <returns>출력 해상도 목록과 현재 창 크기</returns>
    public List<Vector2Int> GetWindowSizeOptions ()
    {
        List<Vector2Int> options = GetResolutionOptions( );		//선택 가능한 화면 크기
        Vector2Int current = new( _current.WindowWidth, _current.WindowHeight );		//저장된 창 크기

        if ( options.Contains( current ) == false )
            options.Add( current );

        return options;
    }

    /// <summary>
    /// 이동 방향의 주 키보드 바인딩 ID 조회
    /// </summary>
    /// <param name="direction">이동 Composite 방향 이름</param>
    /// <returns>바인딩 ID</returns>
    public string GetMoveBindingId ( string direction )
    {
        return _keyboard.GetMoveBindingId( direction );
    }

    /// <summary>
    /// 현재 키보드 바인딩의 표시 이름 조회
    /// </summary>
    /// <param name="id">바인딩 ID</param>
    /// <returns>키 표시 이름</returns>
    public string GetKeyboardKeyName ( string id )
    {
        return _keyboard.GetKeyName( id );
    }

    /// <summary>
    /// 현재 키보드 바인딩 목록 조회
    /// </summary>
    /// <returns>독립된 키 설정 목록</returns>
    public KeyboardBindingData [ ] GetKeyboardBindings ()
    {
        return _keyboard.GetBindings( );
    }

    /// <summary>
    /// 키보드 바인딩의 행동 이름 조회
    /// </summary>
    /// <param name="id">바인딩 ID</param>
    /// <returns>행동과 방향 이름</returns>
    public string GetKeyboardBindingName ( string id )
    {
        return _keyboard.GetBindingName( id );
    }

    /// <summary>
    /// 화면, 음량, 키보드와 언어 설정 적용
    /// </summary>
    /// <param name="data">적용할 설정</param>
    /// <returns>적용 성공 여부</returns>
    public bool ApplySettings ( UserSettingsData data )
    {
        if ( data.CheckSettings( ) == false )
        {
            Debug.LogError(
                "[설정 적용 실패] 음량, 화면 크기와 키 목록을 확인해 주세요.", this );
            return false;
        }

        Vector2Int display = GetDisplaySize( );

        if ( data.Width > display.x || data.Height > display.y ||
             data.WindowWidth > display.x || data.WindowHeight > display.y )
        {
            Debug.LogError(
                "[설정 적용 실패] 현재 화면보다 큰 해상도입니다.", this );
            return false;
        }

        if ( SupportsLanguage( data.Language ) == false )
        {
            Debug.LogError(
                $"[설정 적용 실패] 지원하지 않는 언어입니다: {data.Language}", this );
            return false;
        }

        //검사 단계에서는 실제 바인딩을 변경하지 않음
        if ( _keyboard.TryPrepare( data.KeyboardBindings,
            out string [ ] paths,
            out string reason ) == false )
        {
            Debug.LogError( $"[설정 적용 실패] {reason}", this );
            return false;
        }

        //음소거는 출력에만 반영하고 저장된 슬라이더 음량은 유지
        if ( _sound.SetVolumes(
            data.MasterMuted == true ? 0f : data.MasterVolume,
            data.BgmMuted == true ? 0f : data.BgmVolume,
            data.SfxMuted == true ? 0f : data.SfxVolume ) == false )
            return false;

        _keyboard.Apply( paths );

        //음량만 변경했을 때 해상도를 다시 요청하지 않음
        Vector2Int size = GetScreenSize( data );		//현재 모드에서 사용할 크기
        bool screenChanged = _current == null ||
            GetScreenSize( _current ) != size ||
            _current.ScreenMode != data.ScreenMode;

#if !UNITY_EDITOR
        //저장값과 같아도 실제 Player 화면이 다르면 재적용
        screenChanged = screenChanged ||
            Screen.fullScreenMode != data.ScreenMode ||
            Screen.width != size.x ||
            Screen.height != size.y;
#endif

        if ( screenChanged == true )
        {
            Screen.SetResolution( size.x, size.y, data.ScreenMode );
        }

        bool languageChanged = _current == null ||
            _current.Language != data.Language;

        _current = data.Copy( );

        if ( languageChanged == true )
            OnLanguageChanged?.Invoke( _current.Language );

        OnSettingsChanged?.Invoke( );
        return true;
    }

    /// <summary>
    /// 화면 모드에 따른 적용 크기 조회
    /// </summary>
    /// <param name="data">조회할 설정</param>
    /// <returns>적용할 가로와 세로 크기</returns>
    Vector2Int GetScreenSize ( UserSettingsData data )
    {
        if ( data.ScreenMode == FullScreenMode.Windowed )
            return new Vector2Int( data.WindowWidth, data.WindowHeight );

        if ( data.ScreenMode == FullScreenMode.FullScreenWindow )
            return GetDisplaySize( );

        return new Vector2Int( data.Width, data.Height );
    }

    /// <summary>
    /// 현재 적용된 설정 저장
    /// </summary>
    public void Save ()
    {
        _save.SaveSettings( _current );
    }

    /// <summary>
    /// 기본 화면, 음량, 키보드와 언어 적용
    /// </summary>
    /// <returns>기본값 적용 성공 여부</returns>
    public bool ResetDefaults ()
    {
        UserSettingsData data = PrepareInitialSettings( _defaults );

        //키 기본값은 Input Action 원본 설정 사용
        data.SetKeyboardBindings( Array.Empty<KeyboardBindingData>( ) );

        return ApplySettings( data );
    }

    /// <summary>
    /// 키보드 설정 복원과 외부 연결 정리
    /// </summary>
    public void Shutdown ()
    {
        //Domain Reload 비활성 실행에도 이전 재지정이 남지 않도록 복원
        if ( _keyboard != null )
            _keyboard.Restore( );

        OnLanguageChanged = null;
        OnSettingsChanged = null;

        _keyboard = null;
        _current = null;
        _sound = null;
        _save = null;
    }

}
