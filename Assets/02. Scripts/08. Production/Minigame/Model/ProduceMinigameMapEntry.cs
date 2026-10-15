using System;
using UnityEngine;

/// <summary>
/// 제작 설정 항목 - 기본 설정에 적용할 데이터
/// </summary>
[Serializable]
public struct ProduceMinigameMapEntry
{
    [SerializeField] ProduceMinigameData _source;        //레시피의 기본 설정
    [SerializeField] ProduceMinigameData _data;        //이번 난이도의 적용 설정

    /// <summary>
    /// 레시피의 기본 설정
    /// </summary>
    public ProduceMinigameData Source => _source;

    /// <summary>
    /// 이번 난이도의 적용 설정
    /// </summary>
    public ProduceMinigameData Data => _data;
}
