using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// 당근 수프 검증용 설정 에셋 생성
/// </summary>
public static class CarrotSoupTestSetup
{
    /// <summary>
    /// 에셋을 만들고 기존 에셋은 재사용
    /// </summary>
    /// <param name="path">에셋 경로</param>
    /// <param name="created">새로 생성했는지 여부</param>
    /// <param name="createdAssets">이번에 생성한 에셋</param>
    /// <param name="report">생성 또는 보존 내역</param>
    /// <returns>설정 에셋</returns>
    static T GetOrCreate<T> ( string path, out bool created,
        List<ScriptableObject> createdAssets, StringBuilder report ) where T : ScriptableObject
    {
        T asset = CheckExisting<T>( path );
        created = asset == null;

        if ( created == false )
        {
            report.AppendLine( $"기존 설정 보존: {path}" );
            return asset;
        }

        asset = ScriptableObject.CreateInstance<T>( );
        report.AppendLine( $"생성 요청: {path}" );
        AssetDatabase.CreateAsset( asset, path );
        if ( AssetDatabase.GetAssetPath( asset ) != path )
            throw new IOException( $"'{path}' 생성에 실패했습니다." );
        createdAssets.Add( asset );
        report.AppendLine( $"생성: {path}" );
        return asset;
    }

    /// <summary>
    /// 생성 경로와 기존 에셋 타입 검사
    /// </summary>
    /// <param name="path">에셋 경로</param>
    /// <returns>기존 에셋, 없으면 null</returns>
    static T CheckExisting<T> ( string path ) where T : ScriptableObject
    {
        if ( AssetDatabase.IsValidFolder( Path.GetDirectoryName( path ).Replace( '\\', '/' ) ) == false )
            throw new InvalidOperationException( $"'{path}'의 상위 폴더가 없습니다." );
        UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath( path );
        if ( existing != null && existing is not T || Directory.Exists( path ) ||
             ( existing == null && File.Exists( path ) ) )
            throw new InvalidOperationException( $"'{path}'에 {typeof( T ).Name}이 아닌 파일 또는 에셋이 있습니다." );
        return existing as T;
    }

    /// <summary>
    /// 기존 연결이 이번 설정 대상과 일치하는지 검사
    /// </summary>
    /// <param name="asset">기존 에셋</param>
    /// <param name="field">참조 필드</param>
    /// <param name="expected">필요한 참조</param>
    static void CheckReference ( ScriptableObject asset, string field, UnityEngine.Object expected )
    {
        if ( asset == null )
            return;
        SerializedObject serialized = new( asset );
        if ( expected == null || serialized.FindProperty( field ).objectReferenceValue != expected )
            throw new InvalidOperationException( $"'{AssetDatabase.GetAssetPath( asset )}'의 {field} 연결을 확인해 주세요. 기존 값은 변경하지 않습니다." );
    }

    /// <summary>
    /// 기존 목록의 빈 참조와 필수 대상 검사
    /// </summary>
    /// <param name="asset">기존 목록 에셋</param>
    /// <param name="expected">포함할 대상</param>
    static void CheckMap ( ScriptableObject asset, ScriptableObject expected )
    {
        if ( asset == null )
            return;
        SerializedObject serialized = new( asset );
        SerializedProperty values = serialized.FindProperty( "_data" );
        bool found = false;
        for ( int i = 0; i < values.arraySize; i++ )
        {
            UnityEngine.Object value = values.GetArrayElementAtIndex( i ).objectReferenceValue;
            if ( value == null )
                throw new InvalidOperationException( $"'{AssetDatabase.GetAssetPath( asset )}'의 {i}번 참조가 비어 있습니다." );
            found |= value == expected;
        }
        if ( found == false )
            throw new InvalidOperationException( $"'{AssetDatabase.GetAssetPath( asset )}'에 필요한 대상이 없습니다. 기존 목록은 변경하지 않습니다." );
    }

    /// <summary>
    /// 에셋 참조 설정
    /// </summary>
    /// <param name="asset">설정 대상</param>
    /// <param name="field">필드 이름</param>
    /// <param name="value">연결할 에셋</param>
    static void SetReference ( UnityEngine.Object asset, string field, UnityEngine.Object value )
    {
        SerializedObject serialized = new( asset );
        serialized.FindProperty( field ).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo( );
    }

    /// <summary>
    /// 에셋 참조 배열 설정
    /// </summary>
    /// <param name="asset">설정 대상</param>
    /// <param name="field">필드 이름</param>
    /// <param name="value">첫 번째 에셋</param>
    static void SetSingleReference ( UnityEngine.Object asset, string field, UnityEngine.Object value )
    {
        SerializedObject serialized = new( asset );
        SerializedProperty array = serialized.FindProperty( field );
        array.arraySize = 1;
        array.GetArrayElementAtIndex( 0 ).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo( );
    }

    /// <summary>
    /// 기존 첫 단계에 배치 미션 추가
    /// </summary>
    /// <param name="stage">첫 단계 설정</param>
    /// <param name="mission">배치 미션</param>
    /// <returns>기존 목록 변경 여부</returns>
    static bool AddPlacementMission ( MissionDataMap stage, MissionData mission )
    {
        SerializedObject serialized = new( stage );
        SerializedProperty missions = serialized.FindProperty( "_missions" );

        for ( int i = 0; i < missions.arraySize; i++ )
        {
            if ( missions.GetArrayElementAtIndex( i ).objectReferenceValue == mission )
                return false;
        }

        missions.arraySize++;
        missions.GetArrayElementAtIndex( missions.arraySize - 1 ).objectReferenceValue = mission;
        return serialized.ApplyModifiedProperties( );
    }

    /// <summary>
    /// 기존 미션 구성 검사
    /// </summary>
    /// <param name="mission">검사할 미션</param>
    static void CheckMission ( MissionData mission )
    {
        if ( mission == null )
            throw new InvalidOperationException( "미션 참조가 비어 있습니다." );
        SerializedObject serialized = new( mission );
        if ( string.IsNullOrWhiteSpace( mission.Id ) || string.IsNullOrWhiteSpace( mission.Name ) ||
             serialized.FindProperty( "_requiredCount" ).intValue <= 0 )
            throw new InvalidOperationException( $"'{AssetDatabase.GetAssetPath( mission )}'의 식별자, 이름 또는 수량을 확인해 주세요." );
    }

    /// <summary>
    /// 당근 수프 검증용 SO 입력
    /// </summary>
    [MenuItem( "Tools/Utilities/Test Setup/Apply Carrot Soup Test SO Setup" )]
    public static void Apply ( )
    {
        if ( EditorApplication.isPlayingOrWillChangePlaymode )
        {
            Debug.LogWarning( "[SO 설정 중단] 플레이 중이거나 전환 중에는 실행할 수 없습니다." );
            return;
        }

        List<ScriptableObject> createdAssets = new( );
        StringBuilder report = new( );
        try
        {
            ApplySetup( createdAssets, report );
            Debug.Log( "[SO 설정 완료]\n" + report );
        }
        catch ( Exception exception )
        {
            Debug.LogError( $"[SO 설정 실패] {exception.Message}\n{report}\n" +
                "앞선 생성과 변경은 자동 복원되지 않습니다. 재실행 전에 실패 항목을 확인해 주세요." );
        }
    }

    /// <summary>
    /// 기존 설정 검사 후 부족한 시험 에셋 생성
    /// </summary>
    /// <param name="createdAssets">이번에 생성한 에셋</param>
    /// <param name="report">실제 적용 내역</param>
    static void ApplySetup ( List<ScriptableObject> createdAssets, StringBuilder report )
    {
        const string goalFolder = "Assets/03. Configs/!_Settings/!_GameFlow/Goal/";
        const string missionFolder = "Assets/03. Configs/!_Settings/!_GameFlow/Mission/";

        RecipeData recipe = CheckExisting<RecipeData>(
            "Assets/03. Configs/Recipe/Test_0_Recipe_CarrotSoup.asset" );
        MissionData cookMission = CheckExisting<MissionData>(
            missionFolder + "Mission_0_Cook_CarrotSoup.asset" );
        MissionDataMap firstStage = CheckExisting<MissionDataMap>(
            missionFolder + "!_Mission_1_Map.asset" );
        PreparationGoalData goal = CheckExisting<PreparationGoalData>(
            goalFolder + "Test_0_Preparation_GoalData.asset" );
        PreparationCategoryData category = CheckExisting<PreparationCategoryData>(
            goalFolder + "Test_0_Preparation_CatecoryData.asset" );

        if ( recipe == null || cookMission == null || firstStage == null ||
             goal == null || category == null )
            throw new InvalidOperationException( "기존 당근 수프 레시피, 미션, 첫 단계 또는 준비 목표 에셋이 없습니다." );

        //전체 생성 경로와 기존 구성을 쓰기 전에 확인
        CookGoalData existingCook = CheckExisting<CookGoalData>( missionFolder + "Test_0_CookGoal_CarrotSoup.asset" );
        CookGoalDataMap existingCookMap = CheckExisting<CookGoalDataMap>( missionFolder + "!_Test_CookGoalDataMap.asset" );
        MissionData existingMission = CheckExisting<MissionData>( missionFolder + "Mission_0_Place_CarrotSoup.asset" );
        FoodPlacementData existingPlacement = CheckExisting<FoodPlacementData>( goalFolder + "Test_0_FoodPlacement_CarrotSoup.asset" );
        FoodPlacementDataMap existingFoodMap = CheckExisting<FoodPlacementDataMap>( goalFolder + "!_Test_FoodPlacementDataMap.asset" );
        PreparationGoalDataMap existingGoalMap = CheckExisting<PreparationGoalDataMap>( goalFolder + "!_Test_PreparationGoalDataMap.asset" );

        if ( string.IsNullOrWhiteSpace( recipe.RecipeId ) || recipe.ResultItem == null ||
             recipe.ProduceMinigame == null || recipe.ResultPrefab == null || recipe.IngredientCount == 0 )
            throw new InvalidOperationException( "기존 Recipe의 식별자, 결과, 미니게임, Prefab 또는 재료 설정을 확인해 주세요." );
        HashSet<ItemData> ingredients = new( );
        SerializedObject recipeData = new( recipe );
        SerializedProperty requirements = recipeData.FindProperty( "_ingredients" );
        for ( int i = 0; i < recipe.IngredientCount; i++ )
        {
            IngredientRequirement ingredient = recipe.GetIngredient( i );
            if ( ingredient == null || ingredient.Item == null ||
                 ingredients.Add( ingredient.Item ) == false ||
                 requirements.GetArrayElementAtIndex( i ).FindPropertyRelative( "_requiredCount" ).intValue <= 0 )
                throw new InvalidOperationException( $"기존 Recipe의 {i}번 재료 설정을 확인해 주세요." );
        }
        if ( goal.IsValid == false || category.IsValid == false ||
             goal.Category != category || goal.ItemData != recipe.ResultItem )
            throw new InvalidOperationException( "기존 준비 목표의 수량, 카테고리 또는 결과 연결을 확인해 주세요." );
        CheckMission( cookMission );
        if ( existingMission != null )
            CheckMission( existingMission );

        HashSet<string> missionIds = new( StringComparer.Ordinal );
        bool hasCookMission = false;
        for ( int i = 0; i < firstStage.MissionCount; i++ )
        {
            MissionData mission = firstStage.GetMission( i );
            CheckMission( mission );
            hasCookMission |= mission == cookMission;
            string placementId = existingMission != null ? existingMission.Id : "Mission_0_Place_CarrotSoup";
            if ( missionIds.Add( mission.Id ) == false ||
                 ( mission.Id == placementId && mission != existingMission ) )
                throw new InvalidOperationException( $"첫 단계 미션 ID '{mission.Id}'가 중복됩니다." );
        }
        if ( hasCookMission == false )
            throw new InvalidOperationException( "첫 단계에 기존 제작 미션이 연결되어 있지 않습니다." );

        CheckReference( existingCook, "_recipe", recipe );
        CheckReference( existingCook, "_mission", cookMission );
        CheckReference( existingPlacement, "_recipe", recipe );
        CheckReference( existingPlacement, "_placementMission", existingMission );
        if ( existingPlacement != null && existingPlacement.HasLocation == false )
            throw new InvalidOperationException( "기존 음식 배치 설정의 허용 표면 종류를 확인해 주세요. 빈 목록과 None은 사용할 수 없습니다." );
        CheckMap( existingCookMap, existingCook );
        CheckMap( existingFoodMap, existingPlacement );
        CheckMap( existingGoalMap, goal );
        report.AppendLine( "기존 Recipe / 제작 미션 / 준비 목표 / 카테고리 설정 보존" );
        report.AppendLine( "Undo: 기존 첫 단계 목록 변경만 지원. 에셋 생성 / 저장의 전체 복원은 지원하지 않습니다." );

        CookGoalData cookGoal = GetOrCreate<CookGoalData>(
            missionFolder + "Test_0_CookGoal_CarrotSoup.asset", out bool cookCreated, createdAssets, report );
        if ( cookCreated )
        {
            SetReference( cookGoal, "_recipe", recipe );
            SetReference( cookGoal, "_mission", cookMission );
        }

        CookGoalDataMap cookMap = GetOrCreate<CookGoalDataMap>(
            missionFolder + "!_Test_CookGoalDataMap.asset", out bool cookMapCreated, createdAssets, report );
        if ( cookMapCreated )
            SetSingleReference( cookMap, "_data", cookGoal );

        MissionData placementMission = GetOrCreate<MissionData>(
            missionFolder + "Mission_0_Place_CarrotSoup.asset", out bool missionCreated, createdAssets, report );
        if ( missionCreated )
        {
            SerializedObject missionData = new( placementMission );
            missionData.FindProperty( "_id" ).stringValue = "Mission_0_Place_CarrotSoup";
            missionData.FindProperty( "_name" ).stringValue = "당근 수프 배치하기";
            missionData.FindProperty( "_requiredCount" ).intValue = 1;
            missionData.ApplyModifiedPropertiesWithoutUndo( );
        }

        FoodPlacementData foodPlacement = GetOrCreate<FoodPlacementData>(
            goalFolder + "Test_0_FoodPlacement_CarrotSoup.asset", out bool placementCreated, createdAssets, report );
        if ( placementCreated )
        {
            SetReference( foodPlacement, "_recipe", recipe );
            SetReference( foodPlacement, "_placementMission", placementMission );
            SerializedObject placementData = new( foodPlacement );
            SerializedProperty locations = placementData.FindProperty( "_locations" );
            locations.arraySize = 1;
            locations.GetArrayElementAtIndex( 0 ).intValue = ( int ) PreparationLocationType.PartyFood;
            placementData.ApplyModifiedPropertiesWithoutUndo( );
        }

        FoodPlacementDataMap foodMap = GetOrCreate<FoodPlacementDataMap>(
            goalFolder + "!_Test_FoodPlacementDataMap.asset", out bool foodMapCreated, createdAssets, report );
        if ( foodMapCreated )
            SetSingleReference( foodMap, "_data", foodPlacement );

        PreparationGoalDataMap goalMap = GetOrCreate<PreparationGoalDataMap>(
            goalFolder + "!_Test_PreparationGoalDataMap.asset", out bool goalMapCreated, createdAssets, report );
        if ( goalMapCreated )
            SetSingleReference( goalMap, "_data", goal );

        bool stageChanged = AddPlacementMission( firstStage, placementMission );
        report.AppendLine( stageChanged ? "기존 첫 단계 수정: 배치 미션 추가" : "기존 첫 단계 목록 보존" );
        foreach ( ScriptableObject asset in createdAssets )
        {
            report.AppendLine( $"신규 설정 반영 / 저장 요청: {AssetDatabase.GetAssetPath( asset )}" );
            EditorUtility.SetDirty( asset );
            AssetDatabase.SaveAssetIfDirty( asset );
            if ( EditorUtility.IsDirty( asset ) )
                throw new IOException( $"'{AssetDatabase.GetAssetPath( asset )}' 저장에 실패했습니다." );
            report.AppendLine( $"저장 완료: {AssetDatabase.GetAssetPath( asset )}" );
        }
        if ( stageChanged )
        {
            report.AppendLine( $"기존 첫 단계 저장 요청: {AssetDatabase.GetAssetPath( firstStage )}" );
            AssetDatabase.SaveAssetIfDirty( firstStage );
            if ( EditorUtility.IsDirty( firstStage ) )
                throw new IOException( "첫 단계 미션 목록 저장에 실패했습니다." );
            report.AppendLine( "기존 첫 단계 저장 완료" );
        }
    }
}
