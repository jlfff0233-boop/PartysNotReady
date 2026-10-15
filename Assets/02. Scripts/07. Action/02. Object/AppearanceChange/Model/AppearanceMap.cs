using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 외형 맵 - 변경 후보 설정 목록 보관
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/Action/AppearanceMap" )]
public class AppearanceMap : ScriptableObject
{
    [SerializeField] AppearanceData [ ] _appearances;     //변경 후보 목록

    /// <summary>
    /// 변경 후보 외형 목록
    /// </summary>
    public IReadOnlyList<AppearanceData> Appearances => _appearances;
}
