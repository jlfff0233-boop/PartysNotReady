using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// 저장하지 않는 임시 데이터로 준비 점검의 배열 오탐 회귀 검사
/// </summary>
public static class PlayReadinessRegressionChecks
{
    /// <summary>
    /// 등록 배열, 문자열과 맵 종류별 회귀 검사 실행
    /// </summary>
    [MenuItem( "Tools/Play/플레이 준비 점검 회귀 검사" )]
    public static void Run ()
    {
        List<Object> temporary = new( );		//검사 종료 시 해제할 임시 객체
        int checks = 0;		//완료한 시나리오 개수
        try
        {
            Type[] types = { typeof( MissionDataMap ), typeof( ItemDataMap ), typeof( FoodDataMap ),
                typeof( EventSettingsDataMap ), typeof( AppearanceMap ), typeof( MaterialMap ),
                typeof( RecipeDataMap ), typeof( CookGoalDataMap ), typeof( PreparationGoalDataMap ),
                typeof( FoodPlacementDataMap ), typeof( ItemPlacementMissionDataMap ), typeof( AnomalySpawnDataMap ),
                typeof( GameDifficultyDataMap ), typeof( ProduceMinigameDataMap ), typeof( ShopDataMap ) };
            foreach ( Type type in types )
            {
                ScriptableObject map = ScriptableObject.CreateInstance( type );
                temporary.Add( map );
                using SerializedObject serialized = new( map );
                string field = PlayReadinessContext.GetMapField( map );
                SerializedProperty array = serialized.FindProperty( field );
                Assert( array != null && array.isArray == true, type.Name + " 등록 필드 불일치" );
                array.arraySize = 1;
                serialized.ApplyModifiedPropertiesWithoutUndo( );
                CheckInventory( map, field, PlayReadinessStatus.Passed );
                checks++;
            }

            MissionData mission = ScriptableObject.CreateInstance<MissionData>( );
            MissionDataMap missions = ScriptableObject.CreateInstance<MissionDataMap>( );
            temporary.Add( mission );
            temporary.Add( missions );
            using ( SerializedObject serialized = new( missions ) )
            {
                serialized.FindProperty( "_name" ).stringValue = string.Empty;
                SerializedProperty array = serialized.FindProperty( "_missions" );
                array.arraySize = 1;
                array.GetArrayElementAtIndex( 0 ).objectReferenceValue = mission;
                serialized.ApplyModifiedPropertiesWithoutUndo( );
            }
            CheckInventory( missions, "_missions", PlayReadinessStatus.Passed );
            Assert( new PlayReadinessContext( ).ReadMap( missions ).Count == 1, "직렬화 등록 참조 누락" );
            checks++;

            using ( SerializedObject serialized = new( missions ) )
            {
                serialized.FindProperty( "_name" ).stringValue = "내용이 있는 이름";
                serialized.FindProperty( "_missions" ).arraySize = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo( );
            }
            CheckInventory( missions, "_missions", PlayReadinessStatus.Warning );
            using ( SerializedObject serialized = new( missions ) )
            {
                foreach ( SerializedProperty property in PlayReadinessContext.EnumerateProperties( serialized ) )
                    Assert( property.propertyPath.StartsWith( "_name.", StringComparison.Ordinal ) == false,
                        "문자열 내부 속성을 설정 배열로 순회했습니다." );
            }
            checks++;

            using ( SerializedObject serialized = new( missions ) )
            {
                SerializedProperty array = serialized.FindProperty( "_missions" );
                array.arraySize = 2;
                array.GetArrayElementAtIndex( 0 ).objectReferenceValue = mission;
                array.GetArrayElementAtIndex( 1 ).objectReferenceValue = mission;
                serialized.ApplyModifiedPropertiesWithoutUndo( );
            }
            PlayReadinessContext duplicates = new( );
            PlayDataSetupChecker.Check( duplicates, missions );
            Assert( duplicates.Results.Exists( result => result.Rule == "array-entry" &&
                result.Field == "_missions.Array.data[1]" && result.Status == PlayReadinessStatus.Warning ) == true,
                "중복 참조를 놓쳤습니다." );
            checks++;

            VolumeProfile unsupported = ScriptableObject.CreateInstance<VolumeProfile>( );
            temporary.Add( unsupported );
            PlayReadinessContext unknown = new( );
            unknown.ReadMap( unsupported );
            Assert( unknown.Results.Exists( result => result.Status == PlayReadinessStatus.Blocked ) == true,
                "지원하지 않는 맵을 빈 맵으로 읽었습니다." );
            checks++;
            Debug.Log( $"[플레이 준비 점검 회귀 검사] {checks}개 시나리오 통과. 자산과 씬을 저장하지 않았습니다." );
        }
        finally
        {
            //생성한 비영속 객체만 해제하고 기존 자산에는 접근하지 않습니다.
            foreach ( Object value in temporary )
                Object.DestroyImmediate( value );
        }
    }

    /// <summary>
    /// 등록 배열 한 곳만 개수 판정을 받는지 확인
    /// </summary>
    /// <param name="map">임시 데이터맵</param>
    /// <param name="field">기대하는 등록 필드</param>
    /// <param name="status">기대하는 개수 판정</param>
    static void CheckInventory ( Object map, string field, PlayReadinessStatus status )
    {
        PlayReadinessContext context = new( );
        PlayDataSetupChecker.Check( context, map );
        List<PlayReadinessResult> inventory = context.Results.FindAll( result => result.Rule == "map-inventory" );
        Assert( inventory.Count == 1 && inventory [ 0 ].Field == field && inventory [ 0 ].Status == status,
            map.GetType( ).Name + ": 등록 배열 외의 속성을 맵으로 판정했습니다." );
        Assert( context.Results.Exists( result => result.Rule == "empty-map" ) == false,
            map.GetType( ).Name + ": 재귀 배열 기반 빈 맵 판정이 남아 있습니다." );
    }

    /// <summary>
    /// 회귀 실패를 성공 로그와 구분하여 중단
    /// </summary>
    /// <param name="condition">통과 조건</param>
    /// <param name="message">실패 설명</param>
    static void Assert ( bool condition, string message )
    {
        if ( condition == false )
            throw new InvalidOperationException( "[플레이 준비 점검 회귀 검사 실패] " + message );
    }
}
