using System;
using UnityEngine;

/// <summary>
/// 미션 데이터 - 아이디, 이름과 필요 수량
/// </summary>
[CreateAssetMenu ( menuName = "GameSettings/GameFlow/MissionData" )]
public class MissionData : ScriptableObject
{
    [SerializeField] string _id;     //같은 단계 안에서 고유한 아이디
    [SerializeField] string _name;     //HUD 표시 이름
    [SerializeField, Min ( 1 )] int _requiredCount = 1;		//개별 고정 필요 수량
    [SerializeField] bool _overrideReqCount;		//단계 기본 수량 재정의
    [SerializeField] bool _randomReqCount;		//개별 수량 범위 사용
    [SerializeField] Vector2Int _requiredCountRange = Vector2Int.one;		//개별 필요 수량 범위

    /// <summary>
    /// 목표 아이디
    /// </summary>
    public string Id => _id;
    /// <summary>
    /// 미션 이름
    /// </summary>
    public string Name => _name;
    /// <summary>
    /// 필요 수량
    /// </summary>
    public int RequiredCount => Mathf.Max ( 1 , _requiredCount );

    /// <summary>
    /// 단계 기본 수량 재정의 여부
    /// </summary>
    public bool OverrideRequiredCount => _overrideReqCount;

    /// <summary>
    /// 개별 필요 수량 범위
    /// </summary>
    public Vector2Int RequiredCountRange =>
        _randomReqCount == true
            ? _requiredCountRange
            : new Vector2Int ( _requiredCount , _requiredCount );
}
