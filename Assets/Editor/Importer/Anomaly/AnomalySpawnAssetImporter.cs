using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 발생 설정과 발생 종류 Map의 임포트 준비
/// </summary>
public static class AnomalySpawnAssetImporter
{
    /// <summary>
    /// 단일 적 / 사건 후보 / 수치와 Map 중복 검사
    /// </summary>
    /// <param name="rows">원본 행</param>
    /// <param name="folder">신규 에셋 폴더</param>
    /// <param name="dataMap">갱신할 발생 Map</param>
    /// <param name="bindings">직접 지정한 기존 에셋</param>
    /// <param name="report">검사 결과</param>
    /// <param name="mapName">신규 Map 파일명</param>
    /// <returns>발생 설정과 Map 반영 계획</returns>
    public static DataImportPlan Prepare ( List<DataImportRow> rows, string folder,
        AnomalySpawnDataMap dataMap, List<DataImportReference> bindings, DataImportReport report, string mapName = "Anomaly_Spawn_Map" )
    {
        DataImportPlan plan = DataImportAssetUtility.PrepareMap( folder, dataMap, mapName );
        List<AnomalySpawnData> existing = DataImportAssetUtility.GetEntries<AnomalySpawnData>( plan.DataMap, "_data" );
        HashSet<string> ids = new( StringComparer.Ordinal );
        HashSet<EnemySpawnType> enemies = new( );
        HashSet<AnomalyEventType> independent = new( );
        double totalWeight = 0d;
        foreach ( DataImportRow row in rows )
        {
            try
            {
                row.CheckFields( "id", "enemyType", "enemyTypes", "eventTypes", "weight", "repeatDelay" );
                string id = DataImportAssetUtility.ReadId( row, ids );
                EnemySpawnType enemy = ReadEnemy( row );
                AnomalyEventType [ ] events = row.Kinds<AnomalyEventType>( "eventTypes" );
                float weight = row.Number( "weight" );
                float delay = row.Number( "repeatDelay" );

                //임시 SO 생성 없이 현재 발생 계약을 원본 값으로 확인
                bool owner = enemy == EnemySpawnType.Harm || enemy == EnemySpawnType.Kidnap;
                if ( ( owner == true || enemy == EnemySpawnType.None ) && events.Length == 0 )
                    throw row.Error( "eventTypes", $"{enemy}에는 사건 후보가 필요합니다." );
                if ( owner == false && enemy != EnemySpawnType.None && events.Length > 0 )
                    throw row.Error( "eventTypes", $"{enemy}는 사건 목록을 사용하지 않습니다." );
                if ( weight <= 0f || delay < 0f )
                    throw row.Error( "weight / repeatDelay", "가중치는 양수, 재발생 시간은 0 이상이어야 합니다." );
                if ( enemy != EnemySpawnType.None && enemies.Add( enemy ) == false )
                    throw row.Error( "enemyType", $"Map에서 적 종류가 중복됐습니다: {enemy}" );
                if ( enemy == EnemySpawnType.None )
                {
                    foreach ( AnomalyEventType type in events )
                    {
                        if ( independent.Add( type ) == false )
                            throw row.Error( "eventTypes", $"독립 사건이 다른 그룹과 중복됐습니다: {type}" );
                    }
                }
                totalWeight += weight;
                AnomalySpawnData asset = FindExisting( existing, enemy, events );
                if ( bindings.Exists( binding => binding.Id == id ) == true )
                    asset = DataImportAssetUtility.Resolve<AnomalySpawnData>( id, bindings,
                        Array.Empty<AnomalySpawnData>( ), value => value.name );
                plan.Changes.Add( DataImportAssetUtility.Change( plan, id, asset, serialized =>
                {
                    serialized.FindProperty( "_enemyType" ).intValue = ( int )enemy;
                    DataImportAssetUtility.ApplyKinds( serialized, "_eventTypes", events );
                    serialized.FindProperty( "_weight" ).floatValue = weight;
                    serialized.FindProperty( "_repeatDelay" ).floatValue = delay;
                } ) );
            }
            catch ( Exception exception ) when ( exception is FormatException || exception is ArgumentException || exception is InvalidOperationException )
            {
                report.Errors.Add( $"{row.Location}: {exception.Message}" );
            }
        }
        if ( totalWeight > float.MaxValue )
            report.Errors.Add( "전체 발생 가중치가 허용 범위를 초과했습니다." );
        plan.ApplyMap = ( serialized, changes ) => DataImportAssetUtility.ApplyList( serialized, "_data", changes );
        if ( report.CanImport == true )
            DataImportAssetUtility.Preflight( plan, "_data", null, report );
        return plan;
    }

    /// <summary>
    /// 단일 적 종류와 이전 원본의 적 배열 읽기
    /// </summary>
    /// <param name="row">원본 행</param>
    /// <returns>발생할 적 종류</returns>
    static EnemySpawnType ReadEnemy ( DataImportRow row )
    {
        if ( row.Has( "enemyType" ) == true )
        {
            if ( row.Has( "enemyTypes" ) == true )
                throw row.Error( "enemyType / enemyTypes", "두 필드를 함께 지정하지 마세요." );
            return row.Kind<EnemySpawnType>( "enemyType" );
        }
        EnemySpawnType [ ] values = row.Kinds<EnemySpawnType>( "enemyTypes" );
        if ( values.Length > 1 )
            throw row.Error( "enemyTypes", "현재 발생 설정은 단일 적만 지원합니다." );
        return values.Length == 0 ? EnemySpawnType.None : values [ 0 ];
    }

    /// <summary>
    /// 기존 Map에서 같은 적 또는 독립 사건 묶음 조회
    /// </summary>
    /// <param name="existing">기존 발생 설정</param>
    /// <param name="enemy">발생할 적</param>
    /// <param name="events">독립 사건 후보</param>
    /// <returns>기존 설정, 없으면 null</returns>
    static AnomalySpawnData FindExisting ( List<AnomalySpawnData> existing,
        EnemySpawnType enemy, AnomalyEventType [ ] events )
    {
        AnomalySpawnData found = null;
        foreach ( AnomalySpawnData asset in existing )
        {
            if ( asset == null || asset.EnemyType != enemy )
                continue;
            if ( enemy == EnemySpawnType.None )
            {
                AnomalyEventType [ ] current = Enum.GetValues( typeof( AnomalyEventType ) ).Cast<AnomalyEventType>( )
                    .Where( asset.Contains ).ToArray( );
                if ( new HashSet<AnomalyEventType>( current ).SetEquals( events ) == false )
                    continue;
            }
            if ( found != null )
                throw new InvalidOperationException( $"기존 Map에서 발생 설정이 중복됐습니다: {enemy}" );
            found = asset;
        }
        return found;
    }
}
