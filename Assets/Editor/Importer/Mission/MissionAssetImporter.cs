using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 단계 후보 미션과 미션 Map의 임포트 준비
/// </summary>
public static class MissionAssetImporter
{
    /// <summary>
    /// 대상 Map 안의 미션 ID와 수량 설정 검사
    /// </summary>
    /// <param name="rows">원본 행</param>
    /// <param name="folder">신규 에셋 폴더</param>
    /// <param name="dataMap">갱신할 미션 후보 Map</param>
    /// <param name="stageName">단계 이름, 비어 있으면 기존 이름 보존</param>
    /// <param name="bindings">직접 지정한 기존 미션</param>
    /// <param name="report">검사 결과</param>
    /// <param name="mapName">신규 Map 파일명</param>
    /// <returns>미션과 후보 Map 반영 계획</returns>
    public static DataImportPlan Prepare ( List<DataImportRow> rows, string folder,
        MissionDataMap dataMap, string stageName, List<DataImportReference> bindings, DataImportReport report, string mapName = "Mission_Data_Map" )
    {
        DataImportPlan plan = DataImportAssetUtility.PrepareMap( folder, dataMap, mapName );
        //같은 폴더에 있는 다른 단계의 동일 ID를 자동으로 공유하지 않음
        plan.OutputFolder = $"{plan.OutputFolder}/{Path.GetFileNameWithoutExtension( plan.MapPath )}";
        FoodImportValidator.ValidateOutputFolder( plan.OutputFolder );
        List<MissionData> candidates = DataImportAssetUtility.GetEntries<MissionData>( plan.DataMap, "_missions" );
        Dictionary<string, MissionData> existing = ReadMissions( candidates );
        HashSet<string> ids = new( StringComparer.Ordinal );
        foreach ( DataImportRow row in rows )
        {
            try
            {
                row.CheckFields( "id", "name", "requiredCount", "overrideRequiredCount", "randomRequiredCount",
                    "requiredCountMin", "requiredCountMax" );
                string id = DataImportAssetUtility.ReadId( row, ids );
                string name = row.Text( "name" );
                int count = row.Integer( "requiredCount" );
                if ( count < 1 || count == int.MaxValue )
                    throw row.Error( "requiredCount", "1 이상, int 최대값 미만이어야 합니다." );
                bool hasSettings = row.Has( "overrideRequiredCount" ) == true || row.Has( "randomRequiredCount" ) == true ||
                    row.Has( "requiredCountMin" ) == true || row.Has( "requiredCountMax" ) == true;
                bool overrideCount = true;
                bool randomCount = false;
                Vector2Int range = new( count, count );
                if ( hasSettings == true )
                {
                    overrideCount = row.Flag( "overrideRequiredCount" );
                    randomCount = row.Flag( "randomRequiredCount" );
                    range = new Vector2Int( row.Integer( "requiredCountMin" ), row.Integer( "requiredCountMax" ) );
                    if ( range.x < 1 || range.y < range.x || range.y == int.MaxValue )
                        throw row.Error( "requiredCountMin / requiredCountMax", "양수이며 최대값이 최소값 이상, int 최대값 미만이어야 합니다." );
                }
                existing.TryGetValue( id, out MissionData asset );
                if ( bindings.Exists( binding => binding.Id == id ) == true )
                    asset = DataImportAssetUtility.Resolve( id, bindings, candidates, value => value.Id );
                DataImportAssetChange change = DataImportAssetUtility.Change( plan, id, asset, serialized =>
                {
                    serialized.FindProperty( "_id" ).stringValue = id;
                    serialized.FindProperty( "_name" ).stringValue = name;
                    serialized.FindProperty( "_requiredCount" ).intValue = count;
                } );
                bool applySettings = hasSettings == true || change.Asset == null;
                Action<SerializedObject> applyValues = change.Apply;
                change.Apply = serialized =>
                {
                    applyValues( serialized );
                    if ( applySettings == true )
                    {
                        serialized.FindProperty( "_overrideReqCount" ).boolValue = overrideCount;
                        serialized.FindProperty( "_randomReqCount" ).boolValue = randomCount;
                        serialized.FindProperty( "_requiredCountRange" ).vector2IntValue = range;
                    }
                };
                plan.Changes.Add( change );
                report.Details.Add( $"{id}: {( applySettings == true ? "개별 수량 설정 반영" : "기존 단계 수량 재정의 / 랜덤 범위 보존" )}" );
            }
            catch ( Exception exception ) when ( exception is FormatException || exception is ArgumentException || exception is InvalidOperationException )
            {
                report.Errors.Add( $"{row.Location}: {exception.Message}" );
            }
        }
        bool applyName = string.IsNullOrWhiteSpace( stageName ) == false;
        if ( plan.DataMap == null && applyName == false )
            report.Errors.Add( "새 미션 Map의 단계 이름을 입력해 주세요." );
        plan.ApplyMap = ( serialized, changes ) =>
        {
            if ( applyName == true )
                serialized.FindProperty( "_name" ).stringValue = stageName.Trim( );
            DataImportAssetUtility.ApplyList( serialized, "_missions", changes );
        };
        report.Warnings.Add( "입력 전체가 하나의 후보 Map에 들어갑니다. 단계별 후보 분리는 원본 파일 / 대상 Map을 나누어 적용해 주세요. GameFlowData의 단계 / 추첨 범위는 변경하지 않습니다." );
        if ( report.CanImport == true )
            DataImportAssetUtility.Preflight( plan, "_missions", null, report );
        return plan;
    }

    /// <summary>
    /// 같은 후보 Map의 미션 ID 중복과 기존 참조 확인
    /// </summary>
    /// <param name="missions">기존 미션 후보</param>
    /// <returns>Map 안의 ID별 미션</returns>
    static Dictionary<string, MissionData> ReadMissions ( List<MissionData> missions )
    {
        Dictionary<string, MissionData> result = new( StringComparer.Ordinal );
        foreach ( MissionData mission in missions )
        {
            if ( mission == null )
                continue;
            if ( string.IsNullOrWhiteSpace( mission.Id ) == true || result.ContainsKey( mission.Id ) == true )
                throw new InvalidOperationException( $"기존 Map의 미션 ID가 비었거나 중복됐습니다: {mission.name}" );
            result.Add( mission.Id, mission );
        }
        return result;
    }
}
