using System;
using UnityEngine;

/// <summary>
/// 요구 적 배치 설정 - 시작 방과 행동 위치
/// </summary>
[Serializable]
public class DemandSceneSettings
{
    [SerializeField] EnemyDemand _enemy;		//초기화할 적
    [SerializeField] Room _startRoom;		//최초 발생 승인 방
    [SerializeField] Transform _spawnPoint;		//사건 시작 시 등장 위치와 방향
    [Tooltip( "비워 두면 등장 방 안에서 이동 가능한 대기 위치를 선택합니다." )]
    [SerializeField] Transform _waitPoint;		//선택 음식 요구 대기 위치
    [Tooltip( "비워 두면 EnemySceneController의 공용 퇴장 지점을 사용합니다." )]
    [SerializeField] Transform _exitPoint;		//선택 퇴장 위치
    [Tooltip( "비워 두면 등장 방의 아이템 주변에서 난동 접근 위치를 선택합니다." )]
    [SerializeField] Transform _atkStandPoint;		//선택 난동 공격 위치

    /// <summary>
    /// 초기화할 적
    /// </summary>
    public EnemyDemand Enemy => _enemy;

    /// <summary>
    /// 최초 발생 승인 방
    /// </summary>
    public Room StartRoom => _startRoom;

    /// <summary>
    /// 사건 시작 시 등장 지점
    /// </summary>
    public Transform SpawnPoint => _spawnPoint;

    /// <summary>
    /// 음식 요구 대기 위치
    /// </summary>
    public Transform WaitPoint => _waitPoint;

    /// <summary>
    /// 퇴장 위치
    /// </summary>
    public Transform ExitPoint => _exitPoint;

    /// <summary>
    /// 난동 공격 위치
    /// </summary>
    public Transform AtkStandPoint => _atkStandPoint;
}

