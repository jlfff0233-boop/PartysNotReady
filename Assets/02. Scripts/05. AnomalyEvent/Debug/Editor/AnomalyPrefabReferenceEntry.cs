using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 원본 참조 선택 - 직접 지정할 컴포넌트와 원본 식별 정보 보관
/// </summary>
public class AnomalyPrefabReferenceEntry
{
    /// <summary>
    /// 참조 항목별 직접 지정 컴포넌트, 항목이 없으면 자동 연결 사용
    /// </summary>
    public readonly Dictionary<string, Component> Values = new( );
    /// <summary>
    /// 참조 항목별 선택 당시 원본 식별자
    /// </summary>
    public readonly Dictionary<string, string> Ids = new( );
    public int MaterialIndex;		//직접 지정할 머티리얼 슬롯

    /// <summary>
    /// 직접 지정 항목 유무
    /// </summary>
    public bool HasSelection => Values.Count > 0;
}
