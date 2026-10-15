using System;
using UnityEngine;

/// <summary>
/// 사건 설정 항목 - 사건 종류와 시간 설정 연결
/// </summary>
[Serializable]
public struct EventSettingsMapEntry
{
    [SerializeField] AnomalyEventType _type;        //사건 종류
    [SerializeField] AnomalyEventSettings _settings;        //시간 설정

    /// <summary>
    /// 사건 종류
    /// </summary>
    public AnomalyEventType Type => _type;

    /// <summary>
    /// 시간 설정
    /// </summary>
    public AnomalyEventSettings Settings => _settings;
}
