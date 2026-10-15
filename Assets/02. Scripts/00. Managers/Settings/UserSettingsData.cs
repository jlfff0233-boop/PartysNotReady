using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 사용자 설정 - 화면, 음량, 언어와 키보드
/// </summary>
[Serializable]
public class UserSettingsData
{
    [Header( "----- 화면 -----" )]
    [SerializeField] int _width;		//가로 해상도, 0은 최초 실행 크기
    [SerializeField] int _height;		//세로 해상도, 0은 최초 실행 크기
    [SerializeField] int _windowWidth;		//창 가로 크기, 0은 기존 해상도 사용
    [SerializeField] int _windowHeight;		//창 세로 크기, 0은 기존 해상도 사용
    [SerializeField] FullScreenMode _screenMode = FullScreenMode.FullScreenWindow;		//화면 모드
    [SerializeField] bool _hasScreenMode;		//기존 전체 화면 bool 변환 완료 여부

    [Header( "----- 소리 -----" )]
    [SerializeField, Range( 0f, 1f )] float _masterVolume = 1f;		//전체 음량
    [SerializeField, Range( 0f, 1f )] float _bgmVolume = 1f;		//배경음 음량
    [SerializeField, Range( 0f, 1f )] float _sfxVolume = 1f;		//효과음 음량
    [SerializeField] bool _masterMuted;		//전체 음소거
    [SerializeField] bool _bgmMuted;		//배경음 음소거
    [SerializeField] bool _sfxMuted;		//효과음 음소거

    [Header( "----- 언어 -----" )]
    [SerializeField] SystemLanguage _language = SystemLanguage.Unknown;		//Unknown은 자동 선택

    [Header( "----- 키보드 -----" )]
    [SerializeField]
    KeyboardBindingData [ ] _keyboardBindings =
        Array.Empty<KeyboardBindingData>( );		//빈 목록은 기본 키 사용
    [SerializeField] bool _fullScreen = true;		//전체 화면 여부

    /// <summary>
    /// 가로 해상도
    /// </summary>
    public int Width => _width;
    /// <summary>
    /// 세로 해상도
    /// </summary>
    public int Height => _height;
    /// <summary>
    /// 전체 화면 여부
    /// </summary>
    public bool FullScreen => ScreenMode != FullScreenMode.Windowed;
    /// <summary>
    /// 기존 저장의 전체 화면 여부를 반영한 화면 모드
    /// </summary>
    public FullScreenMode ScreenMode => _hasScreenMode == true ? _screenMode
        : _fullScreen == true ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
    /// <summary>
    /// 창 가로 크기
    /// </summary>
    public int WindowWidth => _windowWidth > 0 ? _windowWidth : _width;
    /// <summary>
    /// 창 세로 크기
    /// </summary>
    public int WindowHeight => _windowHeight > 0 ? _windowHeight : _height;
    /// <summary>
    /// 전체 음량
    /// </summary>
    public float MasterVolume => _masterVolume;
    /// <summary>
    /// 배경음 음량
    /// </summary>
    public float BgmVolume => _bgmVolume;
    /// <summary>
    /// 효과음 음량
    /// </summary>
    public float SfxVolume => _sfxVolume;
    /// <summary>
    /// 전체 음소거 여부
    /// </summary>
    public bool MasterMuted => _masterMuted;
    /// <summary>
    /// 배경음 음소거 여부
    /// </summary>
    public bool BgmMuted => _bgmMuted;
    /// <summary>
    /// 효과음 음소거 여부
    /// </summary>
    public bool SfxMuted => _sfxMuted;
    /// <summary>
    /// 선택 언어
    /// </summary>
    public SystemLanguage Language => _language;
    /// <summary>
    /// 읽기 전용 키 설정 목록
    /// </summary>
    public IReadOnlyList<KeyboardBindingData> KeyboardBindings =>
        Array.AsReadOnly( _keyboardBindings );


    /// <summary>
    /// 화면 크기와 모드 설정
    /// </summary>
    /// <param name="width">가로 해상도</param>
    /// <param name="height">세로 해상도</param>
    /// <param name="fullScreen">전체 화면 여부</param>
    public void SetScreen ( int width, int height, bool fullScreen )
    {
        _width = width;
        _height = height;
        _fullScreen = fullScreen;
        SetScreenMode( fullScreen == true
            ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed );
    }

    /// <summary>
    /// 출력 해상도 설정
    /// </summary>
    /// <param name="width">가로 해상도</param>
    /// <param name="height">세로 해상도</param>
    public void SetResolution ( int width, int height )
    {
        _width = width;
        _height = height;
    }

    /// <summary>
    /// 창 크기 설정
    /// </summary>
    /// <param name="width">창 가로 크기</param>
    /// <param name="height">창 세로 크기</param>
    public void SetWindowSize ( int width, int height )
    {
        _windowWidth = width;
        _windowHeight = height;
    }

    /// <summary>
    /// 화면 모드 설정과 기존 저장 호환 값 갱신
    /// </summary>
    /// <param name="mode">적용할 화면 모드</param>
    public void SetScreenMode ( FullScreenMode mode )
    {
        _screenMode = mode;
        _hasScreenMode = true;
        _fullScreen = mode != FullScreenMode.Windowed;
    }

    /// <summary>
    /// 전체, 배경음과 효과음 음량 설정
    /// </summary>
    /// <param name="master">전체 음량</param>
    /// <param name="bgm">배경음 음량</param>
    /// <param name="sfx">효과음 음량</param>
    public void SetVolumes ( float master, float bgm, float sfx )
    {
        _masterVolume = master;
        _bgmVolume = bgm;
        _sfxVolume = sfx;
    }

    /// <summary>
    /// 저장된 음량과 별도로 음소거 상태 설정
    /// </summary>
    /// <param name="master">전체 음소거 여부</param>
    /// <param name="bgm">배경음 음소거 여부</param>
    /// <param name="sfx">효과음 음소거 여부</param>
    public void SetMutes ( bool master, bool bgm, bool sfx )
    {
        _masterMuted = master;
        _bgmMuted = bgm;
        _sfxMuted = sfx;
    }

    /// <summary>
    /// 선택 언어 설정
    /// </summary>
    /// <param name="language">선택 언어</param>
    public void SetLanguage ( SystemLanguage language )
    {
        _language = language;
    }

    /// <summary>
    /// 키 설정 목록 복사
    /// </summary>
    /// <param name="bindings">적용할 키 목록</param>
    public void SetKeyboardBindings ( IReadOnlyList<KeyboardBindingData> bindings )
    {
        _keyboardBindings = new KeyboardBindingData [ bindings.Count ];

        for ( int i = 0; i < bindings.Count; i++ )
            _keyboardBindings [ i ] = bindings [ i ];
    }

    /// <summary>
    /// 현재 설정과 키 목록을 독립된 데이터로 복사
    /// </summary>
    /// <returns>복사한 설정</returns>
    public UserSettingsData Copy ()
    {
        return new UserSettingsData
        {
            _masterVolume = _masterVolume,
            _bgmVolume = _bgmVolume,
            _sfxVolume = _sfxVolume,
            _masterMuted = _masterMuted,
            _bgmMuted = _bgmMuted,
            _sfxMuted = _sfxMuted,
            _fullScreen = _fullScreen,
            _width = _width,
            _height = _height,
            _windowWidth = _windowWidth,
            _windowHeight = _windowHeight,
            _screenMode = _screenMode,
            _hasScreenMode = _hasScreenMode,
            _language = _language,
            _keyboardBindings =
                ( KeyboardBindingData [ ] ) _keyboardBindings.Clone( )
        };
    }


    /// <summary>
    /// 음량과 화면 크기 및 키 설정 구성 확인
    /// </summary>
    /// <returns>설정 유효 여부</returns>
    public bool CheckSettings ()
    {
        return MasterVolume >= 0f && MasterVolume <= 1f &&
            BgmVolume >= 0f && BgmVolume <= 1f &&
            SfxVolume >= 0f && SfxVolume <= 1f &&
            Width > 0 && Height > 0 &&
            WindowWidth > 0 && WindowHeight > 0 &&
            ( ScreenMode == FullScreenMode.Windowed ||
              ScreenMode == FullScreenMode.FullScreenWindow ||
              ScreenMode == FullScreenMode.ExclusiveFullScreen ) &&
            _keyboardBindings != null;
    }

}
