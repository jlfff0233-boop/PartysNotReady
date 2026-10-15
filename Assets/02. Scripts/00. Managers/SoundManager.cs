using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 사운드 관리 - 배경음, 효과음과 음량 적용
/// </summary>
public class SoundManager : MonoBehaviour, IGameService
{
    const string MasterVolume = "MasterVolume";		//전체 음량 파라미터
    const string BgmVolume = "BgmVolume";		//배경음 음량 파라미터
    const string SfxVolume = "SfxVolume";		//효과음 음량 파라미터
    const float MinVolume = -80f;		//음소거 시 Mixer 음량

    [Header( "----- 오디오 -----" )]
    [SerializeField] AudioMixer _mixer;		//음량 조절 Mixer
    [SerializeField] AudioSource _bgm;		//배경음 재생
    [SerializeField] AudioSource _sfx;		//효과음 재생

    AudioClip _nextBgm;		//전환할 배경음
    bool _nextBgmLoop;		//전환할 배경음 반복 여부
    bool _isBgmFading;		//배경음 전환 중 여부
    bool _isFadingOut;		//기존 곡 감소 단계 여부
    float _bgmFadeDuration;		//각 감소/증가 단계 시간

    /// <summary>
    /// 재생 상태와 공통 소스 설정 준비
    /// </summary>
    /// <param name="game">공통 매니저</param>
    /// <returns>초기화 성공 여부</returns>
    public bool Init ( GameManager game )
    {
        //재생 시작은 각 씬에서 명시적으로 요청
        StopBgm( );
        _bgm.ignoreListenerPause = true;
        _sfx.ignoreListenerPause = true;
        _bgm.playOnAwake = false;
        _bgm.spatialBlend = 0f;
        _bgm.volume = 1f;

        _sfx.Stop( );
        _sfx.clip = null;
        _sfx.playOnAwake = false;
        _sfx.spatialBlend = 0f;
        _sfx.loop = false;
        _sfx.volume = 1f;

        return true;
    }

    /// <summary>
    /// 실제 시간에 따른 배경음 전환 진행
    /// </summary>
    void Update ()
    {
        if ( _isBgmFading == false )
            return;

        float step = Time.unscaledDeltaTime / _bgmFadeDuration;		//이번 프레임 음량 변화

        if ( _isFadingOut == true )
        {
            //재생 중인 곡이 없으면 바로 교체 단계로 진행
            if ( _bgm.isPlaying == true )
            {
                _bgm.volume = Mathf.MoveTowards( _bgm.volume, 0f, step );

                if ( _bgm.volume > 0f )
                    return;
            }

            _bgm.Stop( );
            _bgm.clip = _nextBgm;

            if ( _nextBgm == null )
            {
                StopBgm( );
                return;
            }

            _bgm.loop = _nextBgmLoop;
            _bgm.volume = 0f;
            _bgm.Play( );
            _isFadingOut = false;
            return;
        }

        _bgm.volume = Mathf.MoveTowards( _bgm.volume, 1f, step );

        if ( _bgm.volume >= 1f )
            ClearBgmFade( );
    }

    /// <summary>
    /// 비활성화 시 전체 재생과 전환 정리
    /// </summary>
    void OnDisable ()
    {
        Shutdown( );
    }


    /// <summary>
    /// 선형 음량을 Mixer 음량으로 적용
    /// </summary>
    /// <param name="parameter">노출된 음량 파라미터</param>
    /// <param name="volume">0부터 1 사이의 음량</param>
    /// <returns>적용 성공 여부</returns>
    bool SetVolume ( string parameter, float volume )
    {
        //0은 음소거, 나머지는 데시벨로 변환
        float decibel = volume <= 0f
            ? MinVolume
            : Mathf.Max( MinVolume, Mathf.Log10( volume ) * 20f );

        if ( _mixer.SetFloat( parameter, decibel ) == true )
            return true;

        Debug.LogError(
            $"[음량 적용 실패] Mixer 파라미터 확인: {parameter}", this );
        return false;
    }

    /// <summary>
    /// 전체, 배경음, 효과음 음량 적용
    /// </summary>
    /// <param name="master">전체 음량</param>
    /// <param name="bgm">배경음 음량</param>
    /// <param name="sfx">효과음 음량</param>
    /// <returns>전체 적용 성공 여부</returns>
    public bool SetVolumes ( float master, float bgm, float sfx )
    {
        bool masterApplied = SetVolume( MasterVolume, master );
        bool bgmApplied = SetVolume( BgmVolume, bgm );
        bool sfxApplied = SetVolume( SfxVolume, sfx );

        return masterApplied && bgmApplied && sfxApplied;
    }

    /// <summary>
    /// 배경음 전환 기록 정리
    /// </summary>
    void ClearBgmFade ()
    {
        _isBgmFading = false;
        _isFadingOut = false;
        _nextBgm = null;
    }

    /// <summary>
    /// 배경음 재생 또는 즉시 교체
    /// </summary>
    /// <param name="clip">재생할 배경음</param>
    /// <param name="loop">반복 재생 여부</param>
    /// <param name="restart">같은 곡도 처음부터 재생할지 여부</param>
    public void PlayBgm (
        AudioClip clip, bool loop = true, bool restart = false )
    {
        ClearBgmFade( );
        _bgm.volume = 1f;

        if ( clip == null )
        {
            StopBgm( );
            return;
        }

        _bgm.loop = loop;

        //일반 중복 요청은 현재 재생 위치 유지
        if ( restart == false &&
            _bgm.clip == clip && _bgm.isPlaying == true )
            return;

        if ( restart == true )
            _bgm.Stop( );

        _bgm.clip = clip;
        _bgm.Play( );
    }

    /// <summary>
    /// 배경음 정지와 전환 기록 정리
    /// </summary>
    public void StopBgm ()
    {
        ClearBgmFade( );
        _bgm.Stop( );
        _bgm.clip = null;
        _bgm.volume = 1f;
    }

    /// <summary>
    /// 배경음 감소 후 곡 전환 또는 재시작
    /// </summary>
    /// <param name="clip">전환할 배경음, 없으면 정지</param>
    /// <param name="duration">각 감소 / 증가 단계 시간</param>
    /// <param name="loop">반복 재생 여부</param>
    /// <param name="restart">같은 곡도 처음부터 재생할지 여부</param>
    public void FadeBgm (
        AudioClip clip, float duration, bool loop = true, bool restart = false )
    {
        if ( duration <= 0f )
        {
            PlayBgm( clip, loop, restart );
            return;
        }

        //재시작 요청이 아니면 진행 중인 동일 전환 유지
        if ( restart == false &&
            _isBgmFading == true &&
            _nextBgm == clip && _nextBgmLoop == loop )
            return;

        _nextBgm = clip;
        _nextBgmLoop = loop;
        _bgmFadeDuration = duration;

        //재시작 요청이 아니면 같은 곡의 재생 위치 유지
        if ( restart == false &&
            clip != null && _bgm.clip == clip && _bgm.isPlaying == true )
        {
            //기존 전환을 취소하고 현재 곡의 음량만 복구
            _bgm.loop = loop;
            _isFadingOut = false;
            _isBgmFading = _bgm.volume < 1f;

            if ( _isBgmFading == false )
                ClearBgmFade( );

            return;
        }

        //기존 Update의 Stop → Play 순서로 곡을 처음부터 재생
        _isFadingOut = true;
        _isBgmFading = true;
    }


    /// <summary>
    /// 효과음 중첩 재생
    /// </summary>
    /// <param name="clip">재생할 효과음</param>
    /// <param name="volumeScale">효과음 개별 음량</param>
    public void PlaySfx ( AudioClip clip, float volumeScale = 1f )
    {
        _sfx.PlayOneShot( clip, Mathf.Clamp01( volumeScale ) );
    }

    /// <summary>
    /// 재생 중인 공통 효과음 정지
    /// </summary>
    public void StopSfx ()
    {
        _sfx.Stop( );
    }

    /// <summary>
    /// 전체 재생 종료
    /// </summary>
    public void Shutdown ()
    {
        StopBgm( );
        StopSfx( );
    }
}
