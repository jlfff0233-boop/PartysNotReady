using UnityEngine;

using UnityEngine.Audio;

/// <summary>
/// 사운드 설정 데이터 - 음원, 음량, 피치와 재생 간격
/// </summary>
[CreateAssetMenu ( menuName = "GameSettings/Sound/SoundData" )]
public class SoundData : ScriptableObject
{
    [Header ( "----- 음원 -----" )]
    [SerializeField] AudioClip [ ] _clips;		//재생 후보 음원

    [Header ( "----- 재생 -----" )]
    [SerializeField] Vector2 _volumeRange = Vector2.one;		//음량 범위
    [SerializeField] Vector2 _pitchRange = Vector2.one;		//피치 범위
    [SerializeField, Min ( 0f )] float _cooldown;		//최소 재생 간격

    [Header ( "----- 출력 -----" )]
    [SerializeField] AudioMixerGroup _outputGroup;		//개별 보정 그룹, 없으면 재생기 기본 출력


    /// <summary>
    /// 재생 후보 음원
    /// </summary>
    public AudioClip [ ] Clips => _clips;

    /// <summary>
    /// 음량 범위
    /// </summary>
    public Vector2 VolumeRange => _volumeRange;

    /// <summary>
    /// 피치 범위
    /// </summary>
    public Vector2 PitchRange => _pitchRange;

    /// <summary>
    /// 최소 재생 간격
    /// </summary>
    public float Cooldown => _cooldown;

    /// <summary>
    /// 개별 음량 보정에 사용할 출력 그룹
    /// </summary>
    public AudioMixerGroup OutputGroup => _outputGroup;


    private void OnValidate ( )
    {
        _volumeRange.x = Mathf.Clamp01 ( _volumeRange.x );
        _volumeRange.y = Mathf.Clamp ( _volumeRange.y , _volumeRange.x , 1f );

        _pitchRange.x = Mathf.Clamp ( _pitchRange.x , 0.01f , 3f );
        _pitchRange.y = Mathf.Clamp ( _pitchRange.y , _pitchRange.x , 3f );

        _cooldown = Mathf.Max ( 0f , _cooldown );
    }
}
