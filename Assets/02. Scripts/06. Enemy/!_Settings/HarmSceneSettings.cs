using System;
using UnityEngine;

/// <summary>
/// 공격 적 배치 설정 - 시작 방과 도주 위치
/// </summary>
[Serializable]
public class HarmSceneSettings
{
    [SerializeField] EnemyHarm _enemy;		//초기화할 적
    [SerializeField] Room _startRoom;		//최초 발생 승인 방
    [Tooltip( "비워 두면 최소 이동 거리를 확보한 경로로 공용 입구까지 도주합니다." )]
    [SerializeField] Transform _escapePoint;		//선택 도주 위치

    /// <summary>
    /// 초기화할 적
    /// </summary>
    public EnemyHarm Enemy => _enemy;

    /// <summary>
    /// 최초 발생 승인 방
    /// </summary>
    public Room StartRoom => _startRoom;

    /// <summary>
    /// 도주 위치
    /// </summary>
    public Transform EscapePoint => _escapePoint;
}

