using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 사건 시간 설정과 종류별 Map의 임포트 준비
/// </summary>
public static class EventSettingsAssetImporter
{
    /// <summary>
    /// 사건 종류 / 시간 / 기존 참조와 출력 경로 검사
    /// </summary>
    /// <param name="rows">원본 행</param>
    /// <param name="folder">신규 에셋 폴더</param>
    /// <param name="dataMap">갱신할 사건 설정 Map</param>
    /// <param name="bindings">직접 지정한 기존 에셋</param>
    /// <param name="report">검사 결과</param>
    /// <param name="mapName">신규 Map 파일명</param>
    /// <returns>시간 설정과 Map 반영 계획</returns>
    public static DataImportPlan Prepare ( List<DataImportRow> rows, string folder,
        EventSettingsDataMap dataMap, List<DataImportReference> bindings, DataImportReport report, string mapName = "Event_Settings_Map" )
    {
        DataImportPlan plan = DataImportAssetUtility.PrepareMap( folder, dataMap, mapName );
        Dictionary<AnomalyEventType, AnomalyEventSettings> existing = ReadMap( plan.DataMap );
        List<AnomalyEventType> types = new( );
        HashSet<string> ids = new( StringComparer.Ordinal );
        HashSet<AnomalyEventType> registered = new( );
        foreach ( DataImportRow row in rows )
        {
            try
            {
                row.CheckFields( "id", "eventType", "timeLimit", "recoveryTimeLimit" );
                string id = DataImportAssetUtility.ReadId( row, ids );
                AnomalyEventType type = ReadType( row, id, report );
                if ( registered.Add( type ) == false )
                    throw row.Error( "eventType", $"사건 종류가 중복됐습니다: {type}" );
                float time = row.Number( "timeLimit" );
                float recovery = row.Number( "recoveryTimeLimit" );
                if ( new AnomalyEventTimes( time, recovery ).CheckValid( out string reason ) == false )
                    throw row.Error( "timeLimit / recoveryTimeLimit", reason );
                existing.TryGetValue( type, out AnomalyEventSettings asset );
                if ( bindings.Exists( binding => binding.Id == id ) == true )
                    asset = DataImportAssetUtility.Resolve<AnomalyEventSettings>( id, bindings,
                        Array.Empty<AnomalyEventSettings>( ), value => value.name );
                if ( asset != null )
                    report.Warnings.Add( $"{id}: 기존 SO를 공유하는 다른 Map도 시간 변경의 영향을 받습니다: {AssetDatabase.GetAssetPath( asset )}" );
                plan.Changes.Add( DataImportAssetUtility.Change( plan, id, asset, serialized =>
                {
                    serialized.FindProperty( "_timeLimit" ).floatValue = time;
                    serialized.FindProperty( "_recoveryTimeLimit" ).floatValue = recovery;
                } ) );
                types.Add( type );
            }
            catch ( Exception exception ) when ( exception is FormatException || exception is ArgumentException || exception is InvalidOperationException )
            {
                report.Errors.Add( $"{row.Location}: {exception.Message}" );
            }
        }
        plan.ApplyMap = ( serialized, changes ) =>
        {
            SerializedProperty entries = serialized.FindProperty( "_entries" );
            entries.arraySize = changes.Count;
            for ( int i = 0; i < changes.Count; i++ )
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex( i );
                entry.FindPropertyRelative( "_type" ).intValue = ( int )types [ i ];
                entry.FindPropertyRelative( "_settings" ).objectReferenceValue = changes [ i ].Asset;
            }
        };
        if ( report.CanImport == true )
            DataImportAssetUtility.Preflight( plan, "_entries", "_settings", report );
        return plan;
    }

    /// <summary>
    /// 명시한 사건 종류 또는 이전 원본 ID의 종류 읽기
    /// </summary>
    /// <param name="row">원본 행</param>
    /// <param name="id">원본 식별자</param>
    /// <param name="report">호환 변환 안내</param>
    /// <returns>사건 종류</returns>
    static AnomalyEventType ReadType ( DataImportRow row, string id, DataImportReport report )
    {
        if ( row.Has( "eventType" ) == true )
            return row.Kind<AnomalyEventType>( "eventType" );
        Match match = Regex.Match( id, "^AnomalyEventSettings_[0-9]+_([A-Za-z]+)$" );
        if ( match.Success == false || Array.IndexOf( Enum.GetNames( typeof( AnomalyEventType ) ), match.Groups [ 1 ].Value ) < 0 )
            throw row.Error( "eventType", "종류를 명시하거나 AnomalyEventSettings_번호_종류 ID를 사용해 주세요." );
        report.Details.Add( $"이전 원본 ID의 사건 종류 사용: {id} → {match.Groups [ 1 ].Value}" );
        return ( AnomalyEventType )Enum.Parse( typeof( AnomalyEventType ), match.Groups [ 1 ].Value );
    }

    /// <summary>
    /// 기존 Map의 종류 중복을 확인하고 설정 참조 조회
    /// </summary>
    /// <param name="map">기존 사건 설정 Map</param>
    /// <returns>사건 종류별 설정</returns>
    static Dictionary<AnomalyEventType, AnomalyEventSettings> ReadMap ( ScriptableObject map )
    {
        Dictionary<AnomalyEventType, AnomalyEventSettings> result = new( );
        if ( map == null )
            return result;
        SerializedProperty entries = new SerializedObject( map ).FindProperty( "_entries" );
        for ( int i = 0; i < entries.arraySize; i++ )
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex( i );
            AnomalyEventType type = ( AnomalyEventType )entry.FindPropertyRelative( "_type" ).intValue;
            if ( Enum.IsDefined( typeof( AnomalyEventType ), type ) == false || result.ContainsKey( type ) == true )
                throw new InvalidOperationException( $"기존 Map의 사건 종류가 잘못됐거나 중복됐습니다: {type}" );
            result.Add( type, entry.FindPropertyRelative( "_settings" ).objectReferenceValue as AnomalyEventSettings );
        }
        return result;
    }
}
