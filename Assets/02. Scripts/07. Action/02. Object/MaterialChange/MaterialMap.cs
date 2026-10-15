using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 매트리얼 맵 - 색 변경 후보 목록 보관
/// </summary>
[CreateAssetMenu ( menuName = "GameSettings/Action/MaterialMap" )]
public class MaterialMap : ScriptableObject
{
    [SerializeField] Material [ ] _materials;       //색 변경 후보 매트리얼 목록

    /// <summary>
    /// 색 변경 후보 목록
    /// </summary>
    public IReadOnlyList<Material> Materials => _materials;
}
