using System;
using UnityEngine;

/// <summary>
/// 사운드 설정 항목 - 데이터 참조와 적용할 음량, 음높이 보관
/// </summary>
[Serializable]
public class SoundSetupEntry
{
    [SerializeField] string _name;		//자동 조회할 에셋 이름
    [SerializeField] SoundData _sound;		//설정할 데이터
    [SerializeField] Vector2 _volume;		//적용할 음량 범위
    [SerializeField] Vector2 _pitch;		//적용할 음높이 범위

    /// <summary>
    /// 자동 조회할 에셋 이름
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// 설정할 데이터
    /// </summary>
    public SoundData Sound { get => _sound; set => _sound = value; }

    /// <summary>
    /// 적용할 음량 범위
    /// </summary>
    public Vector2 Volume { get => _volume; set => _volume = value; }

    /// <summary>
    /// 적용할 음높이 범위
    /// </summary>
    public Vector2 Pitch { get => _pitch; set => _pitch = value; }


    /// <summary>
    /// 에셋 이름과 초기 설정값 보관
    /// </summary>
    /// <param name="name">에셋 이름</param>
    /// <param name="volume">음량 범위</param>
    /// <param name="pitch">음높이 범위</param>
    public SoundSetupEntry ( string name, Vector2 volume, Vector2 pitch )
    {
        _name = name;
        _volume = volume;
        _pitch = pitch;
    }
}
