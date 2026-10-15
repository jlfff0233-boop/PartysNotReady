using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// 이동 키 버튼에 연결할 방향
/// </summary>
public enum MoveDirection
{
    Up,     //위
    Down,   //아래
    Left,   //왼쪽
    Right   //오른쪽
}


/// <summary>
/// 설정 패널 - 화면, 음량, 이동 키와 언어 편집, 저장과 취소
/// </summary>
public class SettingPanel : MonoBehaviour
{
    /// <summary>
    /// 설정 콘텐츠 탭 종류
    /// </summary>
    public enum SettingTab
    {
        Window, //화면
        Sound, //소리
        Control, //조작
        Language //언어
    }


    [Header ( "----- 탭 -----" )]
    [SerializeField]
    SettingTabEntry [ ] _tabs =
    {
        new( SettingTab.Window ),
        new( SettingTab.Sound ),
        new( SettingTab.Control ),
        new( SettingTab.Language )
    };		//탭별 버튼과 콘텐츠

    [Header ( "----- 화면 -----" )]
    [SerializeField] TMP_Dropdown _screenModeDropdown;		//화면 모드 선택
    [SerializeField] TMP_Dropdown _windowSizeDropdown;		//창 크기 선택
    [SerializeField] TMP_Dropdown _resolutionDropdown;		//해상도 선택

    [Header ( "----- 음량 -----" )]
    [SerializeField] Slider _masterSlider;		//전체 음량
    [SerializeField] Slider _bgmSlider;		//배경음 음량
    [SerializeField] Slider _sfxSlider;		//효과음 음량
    [SerializeField] TMP_Text _masterText;		//전체 음량 표시
    [SerializeField] TMP_Text _bgmText;		//배경음 음량 표시
    [SerializeField] TMP_Text _sfxText;		//효과음 음량 표시
    [SerializeField] Toggle _masterMuteToggle;		//전체 음소거 선택
    [SerializeField] Toggle _bgmMuteToggle;		//배경음 음소거 선택
    [SerializeField] Toggle _sfxMuteToggle;		//효과음 음소거 선택


    [Header ( "----- 조작 -----" )]
    [SerializeField]
    MoveKeyEntry [ ] _moveKeys =
    {
        new( MoveDirection.Up ),
        new( MoveDirection.Down ),
        new( MoveDirection.Left ),
        new( MoveDirection.Right )
    };		//방향별 버튼과 키 텍스트

    [Header ( "----- 언어 -----" )]
    [SerializeField] TMP_Dropdown _languageDropdown;		//언어 선택

    [Header ( "----- 버튼 -----" )]
    [SerializeField] Button _confirmButton;		//저장 후 닫기
    [SerializeField] Button _cancelButton;		//변경 취소
    [FormerlySerializedAs ( "_defaultButton" )]
    [SerializeField] Button _applyButton;		//현재 탭 적용 후 화면 유지

    [Header ( "----- 안내 -----" )]
    [SerializeField] TMP_Text _messageText;		//적용 실패 안내

    SettingManager _settings;		//설정 적용과 저장
    UserSettingsData _draft;		//탭별 미적용 편집값
    SettingTab _currentTab;		//현재 표시 탭
    bool _isOpen;		//설정 편집 중 여부
    MoveKeyEntry _capturing;		//현재 키 입력 대기 항목
    KeyboardRebindController _keyCapture = new ( );		//키 감지와 입력 차단, 복원

    /// <summary>
    /// 창 크기 선택 순서별 가로와 세로 크기
    /// </summary>
    List<Vector2Int> _windowSizes = new ( );

    FullScreenMode [ ] _screenModes =
    {
        FullScreenMode.Windowed,
        FullScreenMode.FullScreenWindow,
        FullScreenMode.ExclusiveFullScreen
    };		//화면 모드 드롭다운 순서

    /// <summary>
    /// 해상도 선택 순서별 화면 크기
    /// </summary>
    List<Vector2Int> _resolutions = new ( );

    /// <summary>
    /// 언어 선택 순서별 언어
    /// </summary>
    List<SystemLanguage> _languages = new ( );

    /// <summary>
    /// 저장 또는 취소 후 패널 닫힘 알림
    /// </summary>
    public event Action OnClosed;


    /// <summary>
    /// 설정 관리자 연결과 패널 준비
    /// </summary>
    /// <param name="settings">사용자 설정 관리자</param>
    public void Init ( SettingManager settings )
    {
        _settings = settings;

        foreach ( MoveKeyEntry entry in _moveKeys )
        {
            entry.BindingId = _settings.GetMoveBindingId ( entry.Direction.ToString ( ) );
        }

        InitVolumeSlider ( _masterSlider );
        InitVolumeSlider ( _bgmSlider );
        InitVolumeSlider ( _sfxSlider );

        foreach ( SettingTabEntry entry in _tabs )
            entry.Content.SetActive ( false );

        PanelEffect.SetVisible ( gameObject , false , true );
    }

    /// <summary>
    /// 음량 슬라이더 범위 설정
    /// </summary>
    /// <param name="slider">설정할 슬라이더</param>
    void InitVolumeSlider ( Slider slider )
    {
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
    }


    /// <summary>
    /// 패널 입력과 키 지정 결과 알림 연결
    /// </summary>
    void OnEnable ( )
    {
        _keyCapture.OnKeyCaptured += HandleKeyCaptured;
        _keyCapture.OnCaptureCanceled += HandleKeyCaptureCanceled;

        _screenModeDropdown.onValueChanged.AddListener ( HandleScreenModeChanged );
        _windowSizeDropdown.onValueChanged.AddListener ( HandleWindowSizeChanged );
        _resolutionDropdown.onValueChanged.AddListener ( HandleResolutionChanged );

        _masterSlider.onValueChanged.AddListener ( HandleVolumeChanged );
        _bgmSlider.onValueChanged.AddListener ( HandleVolumeChanged );
        _sfxSlider.onValueChanged.AddListener ( HandleVolumeChanged );
        _masterMuteToggle.onValueChanged.AddListener ( HandleMuteChanged );
        _bgmMuteToggle.onValueChanged.AddListener ( HandleMuteChanged );
        _sfxMuteToggle.onValueChanged.AddListener ( HandleMuteChanged );

        _languageDropdown.onValueChanged.AddListener ( HandleLanguageChanged );

        _confirmButton.onClick.AddListener ( HandleConfirmClicked );
        _cancelButton.onClick.AddListener ( HandleCancelClicked );
        _applyButton.onClick.AddListener ( HandleApplyClicked );

        foreach ( SettingTabEntry entry in _tabs )
        {
            entry.Click ??= ( ) => ShowTab ( entry.Tab );
            entry.Button.onClick.AddListener ( entry.Click );
        }

        foreach ( MoveKeyEntry entry in _moveKeys )
        {
            //초기 Init 이전 활성화에도 같은 콜백으로 구독과 해제를 유지
            entry.Click ??= ( ) => BeginKeyCapture ( entry );
            entry.Button.onClick.AddListener ( entry.Click );
        }
    }

    /// <summary>
    /// 패널 입력 해제와 미적용 편집값 정리
    /// </summary>
    void OnDisable ( )
    {
        _screenModeDropdown.onValueChanged.RemoveListener ( HandleScreenModeChanged );
        _windowSizeDropdown.onValueChanged.RemoveListener ( HandleWindowSizeChanged );
        _resolutionDropdown.onValueChanged.RemoveListener ( HandleResolutionChanged );

        _masterSlider.onValueChanged.RemoveListener ( HandleVolumeChanged );
        _bgmSlider.onValueChanged.RemoveListener ( HandleVolumeChanged );
        _sfxSlider.onValueChanged.RemoveListener ( HandleVolumeChanged );
        _masterMuteToggle.onValueChanged.RemoveListener ( HandleMuteChanged );
        _bgmMuteToggle.onValueChanged.RemoveListener ( HandleMuteChanged );
        _sfxMuteToggle.onValueChanged.RemoveListener ( HandleMuteChanged );

        _languageDropdown.onValueChanged.RemoveListener ( HandleLanguageChanged );

        _confirmButton.onClick.RemoveListener ( HandleConfirmClicked );
        _cancelButton.onClick.RemoveListener ( HandleCancelClicked );
        _applyButton.onClick.RemoveListener ( HandleApplyClicked );

        foreach ( SettingTabEntry entry in _tabs )
            entry.Button.onClick.RemoveListener ( entry.Click );

        foreach ( MoveKeyEntry entry in _moveKeys )
            entry.Button.onClick.RemoveListener ( entry.Click );

        _capturing = null;
        _keyCapture.Dispose ( );
        _keyCapture.OnKeyCaptured -= HandleKeyCaptured;
        _keyCapture.OnCaptureCanceled -= HandleKeyCaptureCanceled;

        if ( _isOpen == false )
            return;

        _isOpen = false;

        //미적용 값은 실제 설정을 변경하지 않았으므로 버리기만 함
        _draft = null;
    }

    /// <summary>
    /// 키 지정 입력 진행
    /// </summary>
    void Update ( )
    {
        _keyCapture.Tick ( );
    }


    #region ----- 핸들 -----

    /// <summary>
    /// 화면 모드 선택 적용
    /// </summary>
    /// <param name="index">화면 모드 목록 위치</param>
    void HandleScreenModeChanged ( int index )
    {
        if ( _isOpen == false )
            return;

        UserSettingsData data = _draft.Copy ( );
        data.SetScreenMode ( _screenModes [ index ] );

        ApplySettings ( data );
    }

    /// <summary>
    /// 선택한 창 크기 적용
    /// </summary>
    /// <param name="index">창 크기 목록 위치</param>
    void HandleWindowSizeChanged ( int index )
    {
        if ( _isOpen == false )
            return;

        Vector2Int size = _windowSizes [ index ];		//선택한 창 크기
        UserSettingsData data = _draft.Copy ( );
        data.SetWindowSize ( size.x , size.y );
        ApplySettings ( data );
    }

    /// <summary>
    /// 선택한 해상도 적용
    /// </summary>
    /// <param name="index">해상도 목록 위치</param>
    void HandleResolutionChanged ( int index )
    {
        if ( _isOpen == false )
            return;

        Vector2Int size = _resolutions [ index ];
        UserSettingsData data = _draft.Copy ( );
        data.SetResolution ( size.x , size.y );

        ApplySettings ( data );
    }

    /// <summary>
    /// 음량 슬라이더 변경 적용
    /// </summary>
    /// <param name="value">변경된 슬라이더 값</param>
    void HandleVolumeChanged ( float value )
    {
        if ( _isOpen == false )
            return;

        UserSettingsData data = _draft.Copy ( );

        data.SetVolumes (
            _masterSlider.value ,
            _bgmSlider.value ,
            _sfxSlider.value );

        ApplySettings ( data );
    }

    /// <summary>
    /// 음소거 토글 변경 적용
    /// </summary>
    /// <param name="value">변경된 토글 값</param>
    void HandleMuteChanged ( bool value )
    {
        if ( _isOpen == false )
            return;

        UserSettingsData data = _draft.Copy ( );
        data.SetMutes ( _masterMuteToggle.isOn , _bgmMuteToggle.isOn , _sfxMuteToggle.isOn );
        ApplySettings ( data );
    }

    /// <summary>
    /// 선택 언어 적용
    /// </summary>
    /// <param name="index">언어 목록 위치</param>
    void HandleLanguageChanged ( int index )
    {
        if ( _isOpen == false )
            return;

        UserSettingsData data = _draft.Copy ( );
        data.SetLanguage ( _languages [ index ] );

        ApplySettings ( data );
    }

    /// <summary>
    /// 현재 설정 저장 후 닫기
    /// </summary>
    void HandleConfirmClicked ( )
    {
        if ( _isOpen == false )
            return;

        CancelKeyCapture ( );
        if ( ApplyCurrentTab ( ) == true )
            Close ( );
    }

    /// <summary>
    /// 미적용 편집값을 버리고 닫기
    /// </summary>
    void HandleCancelClicked ( )
    {
        if ( _isOpen == false )
            return;

        CancelKeyCapture ( );

        Close ( );
    }

    /// <summary>
    /// 미적용 설정 편집 취소와 닫기
    /// </summary>
    public void CancelEdit ( )
    {
        HandleCancelClicked ( );
    }

    /// <summary>
    /// 키 지정 취소를 우선하고 설정 화면 닫기
    /// </summary>
    public void HandleCancelInput ( )
    {
        if ( _keyCapture.TryCancelInput ( ) == true )
            return;

        CancelEdit ( );
    }

    /// <summary>
    /// 현재 탭 적용 후 화면 유지
    /// </summary>
    void HandleApplyClicked ( )
    {
        if ( _isOpen == false )
            return;

        CancelKeyCapture ( );
        ApplyCurrentTab ( );
        RefreshValues ( );
    }

    /// <summary>
    /// 감지한 키의 중복 검사 후 편집 목록에 반영
    /// </summary>
    /// <param name="path">입력한 키의 바인딩 경로</param>
    void HandleKeyCaptured ( string path )
    {
        if ( _capturing == null || _draft == null )
            return;

        string bindingId = _capturing.BindingId;
        _capturing = null;

        //자신의 기존 키는 허용하고 다른 동작의 키는 거절
        foreach ( KeyboardBindingData binding in _draft.KeyboardBindings )
        {
            if ( binding.Id == bindingId )
                continue;

            if ( string.Equals (
                binding.Path , path , StringComparison.OrdinalIgnoreCase ) == false )
                continue;

            string actionName =
                _settings.GetKeyboardBindingName ( binding.Id );

            RefreshValues ( );
            _messageText.text =
                $"이미 {actionName}에 할당된 키입니다. 다른 키를 선택해 주세요.";
            return;
        }

        KeyboardBindingData [ ] bindings =
            new KeyboardBindingData [ _draft.KeyboardBindings.Count ];

        bool found = false;

        for ( int i = 0 ; i < bindings.Length ; i++ )
        {
            KeyboardBindingData binding = _draft.KeyboardBindings [ i ];

            if ( binding.Id == bindingId )
            {
                bindings [ i ] = new KeyboardBindingData ( bindingId , path );
                found = true;
            }
            else
            {
                bindings [ i ] = binding;
            }
        }

        if ( found == false )
        {
            RefreshValues ( );
            _messageText.text = "변경할 키 항목을 찾지 못했습니다.";
            return;
        }

        UserSettingsData data = _draft.Copy ( );
        data.SetKeyboardBindings ( bindings );
        ApplySettings ( data );
    }

    /// <summary>
    /// 키 지정 취소에 따른 안내와 키 표시 갱신
    /// </summary>
    /// <param name="keyboardMissing">키보드 연결 필요 여부</param>
    void HandleKeyCaptureCanceled ( bool keyboardMissing )
    {
        _capturing = null;
        _messageText.text = keyboardMissing == true
            ? "키보드를 연결해 주세요." : string.Empty;
        RefreshValues ( );
    }

    #endregion

    #region ----- 편집/표시 -----

    /// <summary>
    /// 선택 탭만 표시하고 키 입력 대기 정리
    /// </summary>
    /// <param name="tab">표시할 탭</param>
    void ShowTab ( SettingTab tab )
    {
        CancelKeyCapture ( );
        _currentTab = tab;

        foreach ( SettingTabEntry entry in _tabs )
        {
            bool selected = entry.Tab == tab;		//현재 탭 여부
            entry.Content.SetActive ( selected );
            entry.Button.interactable = selected == false;
        }

        _messageText.text = string.Empty;
        RefreshValues ( );
    }

    /// <summary>
    /// 현재 탭의 편집값만 실제 설정에 적용하고 저장
    /// </summary>
    /// <returns>변경 없음 또는 적용 성공 여부</returns>
    bool ApplyCurrentTab ( )
    {
        UserSettingsData current = _settings.GetSettings ( );		//현재 적용된 전체 설정
        //화면 탭은 값이 같아도 실제 화면 상태를 재검사
        if ( _currentTab != SettingTab.Window &&
             HasTabChanges ( current ) == false )
        {
            _messageText.text = string.Empty;
            return true;
        }

        UserSettingsData data = current.Copy ( );		//현재 탭만 합칠 적용값

        switch ( _currentTab )
        {
            case SettingTab.Window:
                data.SetScreenMode ( _draft.ScreenMode );
                data.SetWindowSize ( _draft.WindowWidth , _draft.WindowHeight );
                data.SetResolution ( _draft.Width , _draft.Height );
                break;
            case SettingTab.Sound:
                data.SetVolumes ( _draft.MasterVolume , _draft.BgmVolume , _draft.SfxVolume );
                data.SetMutes ( _draft.MasterMuted , _draft.BgmMuted , _draft.SfxMuted );
                break;
            case SettingTab.Control:
                data.SetKeyboardBindings ( _draft.KeyboardBindings );
                break;
            case SettingTab.Language:
                data.SetLanguage ( _draft.Language );
                break;
        }

        if ( _settings.ApplySettings ( data ) == false )
        {
            _messageText.text = "현재 탭의 설정을 적용하지 못했습니다.";
            return false;
        }

        _settings.Save ( );
        _messageText.text = string.Empty;
        return true;
    }

    /// <summary>
    /// 키 입력 대기와 입력 차단 정리
    /// </summary>
    void CancelKeyCapture ( )
    {
        _capturing = null;
        _keyCapture.CancelCapture ( );
    }

    /// <summary>
    /// 방향별 키 입력 대기 시작
    /// </summary>
    /// <param name="entry">변경할 이동 키 항목</param>
    void BeginKeyCapture ( MoveKeyEntry entry )
    {
        CancelKeyCapture ( );
        _capturing = entry;
        _keyCapture.BeginCapture ( );
        _messageText.text = "변경할 키를 누르세요. Esc: 취소";
        RefreshValues ( );
    }

    /// <summary>
    /// 크기 드롭다운 옵션 구성
    /// </summary>
    /// <param name="dropdown">옵션을 표시할 드롭다운</param>
    /// <param name="sizes">가로와 세로 크기 목록</param>
    void SetSizeOptions ( TMP_Dropdown dropdown , List<Vector2Int> sizes )
    {
        List<string> names = new ( );		//크기 표시 이름

        foreach ( Vector2Int size in sizes )
            names.Add ( $"{size.x} × {size.y}" );

        dropdown.ClearOptions ( );
        dropdown.AddOptions ( names );
    }

    /// <summary>
    /// 목록에 없는 현재 크기를 포함하여 입력 알림 없이 표시
    /// </summary>
    /// <param name="dropdown">표시할 드롭다운</param>
    /// <param name="sizes">크기 목록</param>
    /// <param name="size">현재 설정 크기</param>
    void RefreshSizeValue ( TMP_Dropdown dropdown , List<Vector2Int> sizes , Vector2Int size )
    {
        int index = sizes.IndexOf ( size );		//현재 크기의 선택 위치

        if ( index < 0 )
        {
            index = sizes.Count;
            sizes.Add ( size );
            dropdown.AddOptions ( new List<string> { $"{size.x} × {size.y}" } );
        }

        dropdown.SetValueWithoutNotify ( index );
    }

    /// <summary>
    /// 화면 모드, 창 크기, 해상도와 지원 언어 목록 구성
    /// </summary>
    void RefreshOptions ( )
    {
        _resolutions = _settings.GetResolutionOptions ( );
        _windowSizes = _settings.GetWindowSizeOptions ( );
        SetSizeOptions ( _resolutionDropdown , _resolutions );
        SetSizeOptions ( _windowSizeDropdown , _windowSizes );

        _screenModeDropdown.ClearOptions ( );
        _screenModeDropdown.AddOptions ( new List<string>
        {
            "창 모드", "테두리 없는 전체 화면", "전체 화면"
        } );

        _languages.Clear ( );
        List<string> languageNames = new ( );

        foreach ( SystemLanguage language in _settings.Languages )
        {
            _languages.Add ( language );
            languageNames.Add ( language.ToString ( ) );
        }

        //지원 언어가 아직 없으면 선택 기능만 비활성화
        if ( _languages.Count == 0 )
        {
            _languages.Add ( SystemLanguage.Unknown );
            languageNames.Add ( "기본 언어" );
        }

        _languageDropdown.ClearOptions ( );
        _languageDropdown.AddOptions ( languageNames );
        _languageDropdown.interactable = _settings.Languages.Count > 0;
    }

    /// <summary>
    /// 적용된 설정을 입력 알림 없이 표시
    /// </summary>
    void RefreshValues ( )
    {
        UserSettingsData data = _draft.Copy ( );
        RefreshSizeValue ( _resolutionDropdown , _resolutions , new Vector2Int ( data.Width , data.Height ) );
        RefreshSizeValue ( _windowSizeDropdown , _windowSizes , new Vector2Int ( data.WindowWidth , data.WindowHeight ) );
        _screenModeDropdown.SetValueWithoutNotify ( Array.IndexOf ( _screenModes , data.ScreenMode ) );

        //현재 모드에서 실제 적용되는 크기 항목만 편집 허용
        _windowSizeDropdown.interactable = data.ScreenMode == FullScreenMode.Windowed;
        _resolutionDropdown.interactable = data.ScreenMode == FullScreenMode.ExclusiveFullScreen;

        foreach ( MoveKeyEntry entry in _moveKeys )
        {
            entry.Button.interactable = string.IsNullOrEmpty ( entry.BindingId ) == false;
            entry.Text.text = entry == _capturing ? "키 입력 대기"
                : GetDraftKeyName ( entry.BindingId );
        }

        _masterSlider.SetValueWithoutNotify ( data.MasterVolume );
        _bgmSlider.SetValueWithoutNotify ( data.BgmVolume );
        _sfxSlider.SetValueWithoutNotify ( data.SfxVolume );
        _masterMuteToggle.SetIsOnWithoutNotify ( data.MasterMuted );
        _bgmMuteToggle.SetIsOnWithoutNotify ( data.BgmMuted );
        _sfxMuteToggle.SetIsOnWithoutNotify ( data.SfxMuted );

        _masterText.text = $"{Mathf.RoundToInt ( data.MasterVolume * 100f )}%";
        _bgmText.text = $"{Mathf.RoundToInt ( data.BgmVolume * 100f )}%";
        _sfxText.text = $"{Mathf.RoundToInt ( data.SfxVolume * 100f )}%";

        _languageDropdown.SetValueWithoutNotify (
            _languages.IndexOf ( data.Language ) );
    }

    /// <summary>
    /// 편집값 보관과 표시 갱신
    /// </summary>
    /// <param name="data">변경한 설정</param>
    void ApplySettings ( UserSettingsData data )
    {
        _draft = data;
        _messageText.text = string.Empty;
        RefreshValues ( );
    }

    /// <summary>
    /// 설정 편집 종료와 패널 닫기
    /// </summary>
    void Close ( )
    {
        CancelKeyCapture ( );
        //OnDisable에서 변경 취소를 다시 실행하지 않도록 먼저 종료
        _isOpen = false;
        _draft = null;

        PanelEffect.SetVisible ( gameObject , false );
        OnClosed?.Invoke ( );
    }

    /// <summary>
    /// 기존 설정을 보관하고 패널 열기
    /// </summary>
    public void Open ( )
    {
        if ( _isOpen == true )
            return;

        _draft = _settings.GetSettings ( );
        _draft.SetKeyboardBindings ( _settings.GetKeyboardBindings ( ) );

        RefreshOptions ( );
        RefreshValues ( );
        _messageText.text = string.Empty;

        _isOpen = true;
        ShowTab ( SettingTab.Window );
        PanelEffect.SetVisible ( gameObject , true );
    }

    #endregion

    /// <summary>
    /// 편집 중인 키의 표시 이름 조회
    /// </summary>
    /// <param name="id">표시할 바인딩 ID</param>
    /// <returns>키 표시 이름</returns>
    string GetDraftKeyName ( string id )
    {
        foreach ( KeyboardBindingData binding in _draft.KeyboardBindings )
        {
            if ( binding.Id == id )
                return InputControlPath.ToHumanReadableString ( binding.Path ,
                    InputControlPath.HumanReadableStringOptions.OmitDevice );
        }

        return string.Empty;
    }

    /// <summary>
    /// 현재 탭에 미적용 변경이 있는지 확인
    /// </summary>
    /// <param name="current">실제 적용된 설정</param>
    /// <returns>현재 탭 변경 여부</returns>
    bool HasTabChanges ( UserSettingsData current )
    {
        switch ( _currentTab )
        {
            case SettingTab.Window:
                return current.ScreenMode != _draft.ScreenMode ||
                    current.WindowWidth != _draft.WindowWidth || current.WindowHeight != _draft.WindowHeight ||
                    current.Width != _draft.Width || current.Height != _draft.Height;
            case SettingTab.Sound:
                return current.MasterVolume != _draft.MasterVolume || current.BgmVolume != _draft.BgmVolume ||
                    current.SfxVolume != _draft.SfxVolume || current.MasterMuted != _draft.MasterMuted ||
                    current.BgmMuted != _draft.BgmMuted || current.SfxMuted != _draft.SfxMuted;
            case SettingTab.Language:
                return current.Language != _draft.Language;
            case SettingTab.Control:
                //저장 여부와 무관하게 실제 적용 중인 키 목록과 비교
                KeyboardBindingData [ ] bindings = _settings.GetKeyboardBindings ( );
                for ( int i = 0 ; i < bindings.Length ; i++ )
                {
                    if ( bindings [ i ].Path != _draft.KeyboardBindings [ i ].Path )
                        return true;
                }
                return false;
            default:
                return false;
        }
    }
}
