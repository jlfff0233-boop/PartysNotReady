using System;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 발생 종류별 선택 설정
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/Anomaly/SpawnData" )]
public class AnomalySpawnData : ScriptableObject
{
    [Header( "----- 대상 -----" )]
    [SerializeField] EnemySpawnType _enemyType;		//발생할 적 종류
    [SerializeField] AnomalyEventType [ ] _eventTypes;		//발생할 사건 종류

    [Header( "----- 발생 설정 -----" )]
    [SerializeField, Min( 0f )] float _weight;		//선택 가중치
    [SerializeField, Min( 0f )] float _repeatDelay;		//재발생 대기시간

    /// <summary>
    /// 발생할 적 종류
    /// </summary>
    public EnemySpawnType EnemyType => _enemyType;

    /// <summary>
    /// 적에게 사건 결과를 연결하는 설정 여부
    /// </summary>
    public bool HasEventOwner =>
        _enemyType == EnemySpawnType.Harm || _enemyType == EnemySpawnType.Kidnap;

    /// <summary>
    /// 선택 가중치
    /// </summary>
    public float Weight => _weight;

    /// <summary>
    /// 재발생 대기시간
    /// </summary>
    public float RepeatDelay => _repeatDelay;

    /// <summary>
    /// 발생 설정 유효 여부
    /// </summary>
    public bool IsValid => CheckSettings( out _ );

    /// <summary>
    /// 발생 종류와 선택 설정 검사
    /// </summary>
    /// <param name="reason">설정 오류</param>
    /// <returns>설정 유효 여부</returns>
    public bool CheckSettings ( out string reason )
    {
        reason = string.Empty;
        int eventCount = _eventTypes == null ? 0 : _eventTypes.Length;		//사건 후보 수

        if ( Enum.IsDefined( typeof( EnemySpawnType ), _enemyType ) == false )
        {
            reason = $"유효하지 않은 Enemy 종류: {_enemyType}";
        }
        else if ( ( HasEventOwner == true ||
            _enemyType == EnemySpawnType.None ) && eventCount == 0 )
        {
            reason = $"{_enemyType}: 사건 후보를 설정해 주세요.";
        }
        else if ( HasEventOwner == false &&
            _enemyType != EnemySpawnType.None && eventCount > 0 )
        {
            reason = $"{_enemyType}: " +
                $"사건을 사용하지 않는 적의 Event 목록은 비워 주세요.";
        }
        else if ( float.IsNaN( _weight ) == true ||
            float.IsInfinity( _weight ) == true || _weight <= 0f )
        {
            reason = "발생 가중치는 유한한 양수여야 합니다.";
        }
        else if ( float.IsNaN( _repeatDelay ) == true ||
            float.IsInfinity( _repeatDelay ) == true || _repeatDelay < 0f )
        {
            reason = "그룹 재발생 대기시간은 유한한 0 이상 값이어야 합니다.";

        }

        if ( reason.Length > 0 )
            return false;

        HashSet<AnomalyEventType> types = new( );		//등록한 사건 종류

        for ( int i = 0; i < eventCount; i++ )
        {
            AnomalyEventType type = _eventTypes [ i ];		//검사할 사건 종류

            if ( Enum.IsDefined( typeof( AnomalyEventType ), type ) == false )
            {
                reason = $"_eventTypes[{i}]: 유효하지 않은 Event 종류: {type}";
            }
            else if ( types.Add( type ) == false )
            {
                reason = $"_eventTypes[{i}]: Event 종류 중복: {type}";
            }

            if ( reason.Length > 0 )
                return false;
        }

        return true;
    }

    /// <summary>
    /// 적 종류 포함 여부
    /// </summary>
    /// <param name="type">적 종류</param>
    /// <returns>포함 여부</returns>
    public bool Contains ( EnemySpawnType type )
    {
        return type != EnemySpawnType.None && _enemyType == type;
    }

    /// <summary>
    /// 사건 종류 포함 여부
    /// </summary>
    /// <param name="type">사건 종류</param>
    /// <returns>포함 여부</returns>
    public bool Contains ( AnomalyEventType type )
    {
        if ( _eventTypes == null )
            return false;

        foreach ( AnomalyEventType candidate in _eventTypes )
        {
            if ( candidate == type )
                return true;
        }

        return false;
    }
}
