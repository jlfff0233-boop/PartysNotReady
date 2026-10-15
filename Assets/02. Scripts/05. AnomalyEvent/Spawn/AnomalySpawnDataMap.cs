using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 발생 종류 데이터 목록
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/Anomaly/SpawnDataMap" )]
public class AnomalySpawnDataMap : ScriptableObject
{
    [Header( "----- 발생 종류 -----" )]
    [SerializeField] AnomalySpawnData [ ] _data;		//발생 종류 목록

    /// <summary>
    /// 발생 종류 데이터
    /// </summary>
    public IReadOnlyList<AnomalySpawnData> Data => _data;

    /// <summary>
    /// 발생 설정과 종류 중복 검사
    /// </summary>
    /// <param name="reason">설정 오류</param>
    /// <returns>설정 유효 여부</returns>
    public bool CheckSettings ( out string reason )
    {
        reason = string.Empty;

        if ( _data == null || _data.Length == 0 )
        {
            reason = "발생 종류 데이터맵이 비어 있습니다.";
            return false;
        }

        HashSet<AnomalySpawnData> settings = new( );		//등록한 설정
        HashSet<EnemySpawnType> enemies = new( );
        HashSet<AnomalyEventType> events = new( );
        double totalWeight = 0d;

        for ( int i = 0; i < _data.Length; i++ )
        {
            AnomalySpawnData data = _data [ i ];

            if ( data == null )
            {
                reason = $"발생 종류 데이터가 없습니다: {i}";
                return false;
            }

            if ( settings.Add( data ) == false )
            {
                reason = $"발생 설정이 중복됐습니다: {data.name}";
                return false;
            }

            if ( data.CheckSettings( out reason ) == false )
            {
                reason = $"{data.name}: {reason}";
                return false;
            }

            foreach ( EnemySpawnType type in Enum.GetValues( typeof( EnemySpawnType ) ) )
            {
                if ( data.Contains( type ) == true && enemies.Add( type ) == false )
                {
                    reason = $"Enemy 종류가 중복됐습니다: {type}";
                    return false;
                }
            }

            foreach ( AnomalyEventType type in Enum.GetValues( typeof( AnomalyEventType ) ) )
            {
                if ( data.EnemyType == EnemySpawnType.None &&
                     data.Contains( type ) == true && events.Add( type ) == false )
                {
                    reason = $"독립 사건 종류가 중복됐습니다: {type}";
                    return false;
                }
            }

            totalWeight += data.Weight;
        }

        if ( totalWeight > float.MaxValue )
        {
            reason = "전체 발생 가중치가 허용 범위를 초과했습니다.";
            return false;
        }

        return true;
    }
}
