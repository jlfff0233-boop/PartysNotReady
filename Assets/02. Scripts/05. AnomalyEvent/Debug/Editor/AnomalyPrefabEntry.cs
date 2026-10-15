using UnityEngine;

/// <summary>
/// 프리팹 목록 항목 - 원본 경로와 내부 변칙 대상, 구성된 기능 보관
/// </summary>
public class AnomalyPrefabEntry
{
    readonly GameObject _root;		//원본 루트
    readonly string _path;		//원본 에셋 경로
    readonly AnomalyTarget[] _targets;		//비활성 오브젝트를 포함한 내부 대상
    readonly string _features;		//원본에 구성된 기능 요약

    /// <summary>
    /// 원본 루트
    /// </summary>
    public GameObject Root => _root;
    /// <summary>
    /// 원본 에셋 경로
    /// </summary>
    public string Path => _path;
    /// <summary>
    /// 내부 변칙 대상
    /// </summary>
    public AnomalyTarget[] Targets => _targets;
    /// <summary>
    /// 구성된 기능 요약
    /// </summary>
    public string Features => _features;

    /// <summary>
    /// 목록 항목 초기화
    /// </summary>
    /// <param name="root">원본 루트</param>
    /// <param name="path">원본 에셋 경로</param>
    /// <param name="targets">내부 변칙 대상</param>
    /// <param name="features">구성된 기능 요약</param>
    public AnomalyPrefabEntry ( GameObject root, string path, AnomalyTarget[] targets, string features )
    {
        _root = root;
        _path = path;
        _targets = targets;
        _features = features;
    }
}
