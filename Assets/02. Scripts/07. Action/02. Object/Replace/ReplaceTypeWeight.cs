using UnityEngine;
using System;

/// <summary>
/// 대체 유형별 발생 가중치
/// </summary>
[Serializable]
public struct ReplaceTypeWeight
{
    [SerializeField] ReplaceType _type;        //선택할 유형
    [SerializeField] [Min( 0f )] float _weight;        //0이면 선택하지 않음

    /// <summary>
    /// 선택할 유형
    /// </summary>
    public ReplaceType Type => _type;

    /// <summary>
    /// 0이면 선택하지 않음
    /// </summary>
    public float Weight => _weight;
}
