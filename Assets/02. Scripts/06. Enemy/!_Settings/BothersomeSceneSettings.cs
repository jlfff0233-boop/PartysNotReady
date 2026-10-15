using System;
using UnityEngine;

/// <summary>
/// 방해 적 배치 설정 - 시작 방, 놀이 공과 퇴장 위치
/// </summary>
[Serializable]
public class BothersomeSceneSettings
{
    [SerializeField] EnemyBothersome _enemy;		//초기화할 적
    [SerializeField] Room _startRoom;		//최초 발생 승인 방
    [SerializeField] Transform _spawnPoint;		//사건 시작 시 등장 위치와 방향
    [SerializeField] ItemThrow _ball;		//놀이 공
    [Tooltip( "비워 두면 EnemySceneController의 공용 퇴장 지점을 사용합니다." )]
    [SerializeField] Transform _exitPoint;		//선택 퇴장 위치

    /// <summary>
    /// 초기화할 적
    /// </summary>
    public EnemyBothersome Enemy => _enemy;

    /// <summary>
    /// 최초 발생 승인 방
    /// </summary>
    public Room StartRoom => _startRoom;

    /// <summary>
    /// 사건 시작 시 등장 지점
    /// </summary>
    public Transform SpawnPoint => _spawnPoint;

    /// <summary>
    /// 놀이 공
    /// </summary>
    public ItemThrow Ball => _ball;

    /// <summary>
    /// 퇴장 위치
    /// </summary>
    public Transform ExitPoint => _exitPoint;
}

