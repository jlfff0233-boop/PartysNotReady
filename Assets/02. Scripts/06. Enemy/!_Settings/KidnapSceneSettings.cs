using System;
using UnityEngine;

/// <summary>
/// 납치 적 배치 설정 - 적과 시작 방 연결
/// </summary>
[Serializable]
public class KidnapSceneSettings
{
    [SerializeField] EnemyKidnap _enemy;		//초기화할 적
    [SerializeField] Room _startRoom;		//최초 발생 승인 방
    [Tooltip( "기존 설정 보존용입니다. 납치 도주는 경로 그래프의 노드를 자동 선정한 뒤 EnemySceneController의 공용 입구로 이동합니다." )]
    [SerializeField] Transform _escapePoint;		//기존 개별 완료 지점 보존

    /// <summary>
    /// 초기화할 적
    /// </summary>
    public EnemyKidnap Enemy => _enemy;

    /// <summary>
    /// 최초 발생 승인 방
    /// </summary>
    public Room StartRoom => _startRoom;

    /// <summary>
    /// 기존 개별 완료 지점 - 현재 납치 도주에서는 사용하지 않음
    /// </summary>
    public Transform EscapePoint => _escapePoint;
}

