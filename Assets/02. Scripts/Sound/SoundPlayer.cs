using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 사운드 재생 모듈 - 단발음, 반복음과 재생 상태 정리
/// </summary>
public class SoundPlayer : MonoBehaviour
{
    [Header ( "----- 컴포넌트 -----" )]
    [SerializeField] AudioSource _oneShot;		//단발음 소스, 복제용 독립 오브젝트
    [SerializeField] AudioSource _loop;		//반복음 소스
    [SerializeField] bool _ignoreListenerPause; //공통 UI 재생기의 월드 정지 예외

    /// <summary>
    /// 다음 재생 가능 시각 딕셔너리(음원 소스, 시간)
    /// </summary>
    Dictionary<SoundData , float> _nextPlayTimes = new ( );

    /// <summary>
    /// 개별 출력 그룹별 단발음 소스, 겹친 다른 음원의 출력 변경을 방지
    /// </summary>
    Dictionary<AudioMixerGroup , AudioSource> _oneShotSources = new ( );

    SoundData _loopSound;		//현재 반복음 데이터
    AudioMixerGroup _loopOutputGroup;		//반복음 기본 출력
    bool _hasLoopOutputGroup;		//비활성 객체의 초기화 전 기본 출력 보관 여부


    private void Awake ( )
    {
        GetLoopOutputGroup( );
        SetIgnoreListenerPause( _ignoreListenerPause );
        _oneShot.playOnAwake = false;
        _oneShot.loop = false;

        _loop.playOnAwake = false;
        _loop.loop = true;

        StopAll ( );
    }

    private void OnDisable ( )
    {
        StopAll ( );
    }


    #region ----- 재생 -----

    /// <summary>
    /// 단발음 재생
    /// </summary>
    /// <param name="sound">사운드 설정</param>
    public void PlayOneShot ( SoundData sound )
    {
        if ( isActiveAndEnabled == false ||
             _oneShot.isActiveAndEnabled == false )
            return;

        if ( CanPlay ( sound ) == false )
            return;

        AudioClip clip = GetClip ( sound );		//재생 음원

        if ( clip == null )
            return;

        //보정 그룹마다 소스를 분리해 이미 재생 중인 다른 효과음의 출력 유지
        AudioSource source = GetOneShotSource( sound.OutputGroup );		//이번 음원 출력
        source.pitch = GetRandomValue ( sound.PitchRange );
        source.PlayOneShot ( clip , GetRandomValue ( sound.VolumeRange ) );

        RecordPlay ( sound );
    }

    /// <summary>
    /// 반복음 시작 또는 교체
    /// </summary>
    /// <param name="sound">사운드 설정</param>
    public void StartLoop ( SoundData sound )
    {
        if ( isActiveAndEnabled == false ||
             _loop.isActiveAndEnabled == false )
            return;

        //같은 반복음은 재생 위치와 설정 유지
        if ( sound != null &&
             _loopSound == sound &&
             _loop.isPlaying == true )
            return;

        if ( CanPlay ( sound ) == false )
            return;

        AudioClip clip = GetClip ( sound );		//재생 음원

        if ( clip == null )
            return;

        //새 요청이 유효할 때 기존 반복음 교체
        StopLoop ( );

        _loopSound = sound;
        _loop.outputAudioMixerGroup = sound.OutputGroup != null
            ? sound.OutputGroup : GetLoopOutputGroup( );
        _loop.clip = clip;
        _loop.volume = GetRandomValue ( sound.VolumeRange );
        _loop.pitch = GetRandomValue ( sound.PitchRange );
        _loop.Play ( );

        RecordPlay ( sound );
    }

    /// <summary>
    /// 반복음 정지
    /// </summary>
    public void StopLoop ( )
    {
        _loop.Stop ( );
        _loop.clip = null;
        _loop.outputAudioMixerGroup = GetLoopOutputGroup( );

        _loopSound = null;
    }

    /// <summary>
    /// UI 재생기의 월드 일시정지 예외 적용
    /// </summary>
    /// <param name="ignore">일시정지 중에도 재생할지 여부</param>
    public void SetIgnoreListenerPause ( bool ignore )
    {
        _ignoreListenerPause = ignore;
        _oneShot.ignoreListenerPause = ignore;
        _loop.ignoreListenerPause = ignore;

        foreach ( AudioSource source in _oneShotSources.Values )
            source.ignoreListenerPause = ignore;
    }

    /// <summary>
    /// 전체 재생과 재생 간격 기록 초기화
    /// </summary>
    public void StopAll ( )
    {
        _oneShot.Stop ( );
        _oneShot.clip = null;

        //재사용 시 이전 보정 음원과 재생 간격이 남지 않도록 함께 정리
        foreach ( AudioSource source in _oneShotSources.Values )
        {
            source.Stop( );
            source.clip = null;
        }

        StopLoop ( );
        _nextPlayTimes.Clear ( );
    }

    #endregion

    #region ----- 검사 / 계산 -----

    /// <summary>
    /// 출력 그룹의 단발음 소스 조회, 최초 요청에서만 생성
    /// </summary>
    /// <param name="group">개별 출력 그룹, 없으면 기본 소스</param>
    /// <returns>해당 그룹 전용 소스</returns>
    AudioSource GetOneShotSource ( AudioMixerGroup group )
    {
        if ( group == null || group == _oneShot.outputAudioMixerGroup )
            return _oneShot;

        if ( _oneShotSources.TryGetValue( group, out AudioSource source ) == true )
            return source;

        //독립 소스를 복제해 인스펙터의 음량, 공간 설정과 곡선 유지
        //같은 부모 아래 배치해 이전 보정 소스가 다음 복제에 포함되지 않도록 함
        source = Instantiate( _oneShot, _oneShot.transform.parent, false );
        source.gameObject.name = $"OneShot_{group.name}";
        source.outputAudioMixerGroup = group;
        source.Stop( );
        source.clip = null;

        _oneShotSources.Add( group, source );
        return source;
    }

    /// <summary>
    /// Awake 전 정리 요청에서도 반복음 기본 출력 보관
    /// </summary>
    /// <returns>반복음 기본 출력</returns>
    AudioMixerGroup GetLoopOutputGroup ()
    {
        if ( _hasLoopOutputGroup == false )
        {
            _loopOutputGroup = _loop.outputAudioMixerGroup;
            _hasLoopOutputGroup = true;
        }

        return _loopOutputGroup;
    }

    /// <summary>
    /// 사운드 재생 가능 여부 확인
    /// </summary>
    /// <param name="sound">사운드 설정</param>
    /// <returns>재생 가능 여부</returns>
    bool CanPlay ( SoundData sound )
    {
        //월드 정지 중 재생 요청으로 기존 반복음 위치를 초기화하지 않음
        if ( AudioListener.pause == true && _oneShot.ignoreListenerPause == false )
            return false;
        if ( sound == null )
            return false;

        if ( _nextPlayTimes.TryGetValue ( sound , out float nextPlayTime ) == false )
            return true;

        return Time.unscaledTime >= nextPlayTime;
    }

    /// <summary>
    /// 다음 재생 가능 시각 기록
    /// </summary>
    /// <param name="sound">재생한 사운드 설정</param>
    void RecordPlay ( SoundData sound )
    {
        _nextPlayTimes [ sound ] =
            Time.unscaledTime + Mathf.Max ( 0f , sound.Cooldown );
    }

    /// <summary>
    /// 유효한 음원 중 하나 선택
    /// </summary>
    /// <param name="sound">사운드 설정</param>
    /// <returns>선택한 음원, 없으면 null</returns>
    AudioClip GetClip ( SoundData sound )
    {
        AudioClip [ ] clips = sound.Clips;		//음원 후보

        if ( clips == null || clips.Length == 0 )
            return null;

        int validCount = 0;		//유효 음원 수

        for ( int i = 0 ; i < clips.Length ; i++ )
        {
            if ( clips [ i ] != null )
                validCount++;
        }

        if ( validCount == 0 )
            return null;

        int selectedIndex = Random.Range ( 0 , validCount );		//유효 후보 순번

        for ( int i = 0 ; i < clips.Length ; i++ )
        {
            if ( clips [ i ] == null )
                continue;

            if ( selectedIndex == 0 )
                return clips [ i ];

            selectedIndex--;
        }

        return null;
    }

    /// <summary>
    /// 범위 안의 무작위 값 계산
    /// </summary>
    /// <param name="range">최솟값과 최댓값</param>
    /// <returns>선택한 값</returns>
    float GetRandomValue ( Vector2 range )
    {
        return Random.Range ( range.x , range.y );
    }

    #endregion
}
