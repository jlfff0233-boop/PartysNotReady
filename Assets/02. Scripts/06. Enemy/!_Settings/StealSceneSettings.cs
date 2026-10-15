using System;
using UnityEngine;

/// <summary>
/// 절도 적 배치 설정 - 시작 방과 비행 경유점
/// </summary>
[Serializable]
public class StealSceneSettings
{
    [SerializeField] EnemySteal _enemy;		//초기화할 적
    [SerializeField] Room _startRoom;		//최초 발생 승인 방
    [SerializeField] Transform _spawnPoint;		//사건 시작 시 등장 위치와 방향
    [SerializeField] Transform [ ] _enterPoints;		//침입 경유점
    [SerializeField] Transform [ ] _escapePoints;		//탈출 경유점

    /// <summary>
    /// 초기화할 적
    /// </summary>
    public EnemySteal Enemy => _enemy;

    /// <summary>
    /// 최초 발생 승인 방
    /// </summary>
    public Room StartRoom => _startRoom;

    /// <summary>
    /// 사건 시작 시 등장 지점
    /// </summary>
    public Transform SpawnPoint => _spawnPoint;

    /// <summary>
    /// 침입 경유점
    /// </summary>
    public Transform [ ] EnterPoints => _enterPoints;

    /// <summary>
    /// 탈출 경유점
    /// </summary>
    public Transform [ ] EscapePoints => _escapePoints;
}

