using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 데이터맵 원본과 미션 입력 연결 검사
/// </summary>
public static class PlayDataSetupChecker
{
    /// <summary>
    /// 원본 데이터와 순수 설정 계약 검사
    /// </summary>
    /// <param name="context">검사 문맥</param>
    /// <param name="owner">데이터 소유자</param>
    public static void Check ( PlayReadinessContext context, Object owner )
    {
        if ( owner is ScriptableObject == false )
            return;
        using SerializedObject serialized = new( owner );
        CheckArrays( context, owner, serialized );
        bool valid;
        string reason;
        switch ( owner )
        {
            case GameFlowData flow:
                valid = flow.CheckSettings( out reason );
                ReportSettings( context, owner, "_stages", valid, reason );
                float duration = serialized.FindProperty( "_duration" ).floatValue;
                float rush = serialized.FindProperty( "_finalRushDuration" ).floatValue;
                bool finiteTime = Finite( flow.Duration ) == true && Finite( flow.FinalRushDuration ) == true;
                context.Report( owner, "데이터", "settings", "_duration / _finalRushDuration",
                    finiteTime == false ? PlayReadinessStatus.Error : duration != flow.Duration || rush != flow.FinalRushDuration
                        ? PlayReadinessStatus.Warning : PlayReadinessStatus.Passed,
                    $"저장 시간 {duration} / {rush} · 실제 적용 시간 {flow.Duration} / {flow.FinalRushDuration}. 런타임 보정되는 값은 경고로 구분합니다." );
                for ( int i = 0; i < flow.StageCount; i++ )
                {
                    MissionStageSettings stage = flow.GetStageSettings( i );
                    if ( stage == null || stage.MissionMap == null )
                        continue;
                    context.Report( owner, "데이터", "effective-stage", $"_stages.Array.data[{i}]",
                        PlayReadinessStatus.Passed,
                        $"후보 맵 {stage.MissionMap.name} · 후보 {stage.MissionMap.MissionCount}개 · 선정 {stage.GoalCountRange} · 기본 수량 {stage.RequiredCountRange}", stage.MissionMap );
                    for ( int j = 0; j < stage.MissionMap.MissionCount; j++ )
                    {
                        MissionData mission = stage.MissionMap.GetMission( j );
                        if ( mission == null )
                            continue;
                        context.Report( owner, "데이터", "effective-count", $"_stages.Array.data[{i}].candidate[{j}]",
                            PlayReadinessStatus.Passed,
                            $"{mission.name} ({mission.Id}) · 실제 필요 수량 범위 " +
                            $"{( mission.OverrideRequiredCount == true ? mission.RequiredCountRange : stage.RequiredCountRange )} · " +
                            ( mission.OverrideRequiredCount == true ? "미션 개별 설정 사용" : "단계 기본값 사용; 미션의 저장된 고정 수량은 적용되지 않음" ), mission );
                    }
                }
                break;
            case GameDifficultyDataMap difficulty:
                //직렬화 배열의 빈 항목은 별도 검사하고 기존 순수 검사를 호출합니다.
                valid = difficulty.CheckSettings( out reason );
                ReportSettings( context, owner, "_data", valid, reason );
                CheckDifficultyReferences( context, owner, serialized );
                break;
            case AnomalySpawnData spawn:
                valid = spawn.CheckSettings( out reason );
                ReportSettings( context, owner, "발생 설정", valid, reason );
                break;
            case AnomalySpawnDataMap spawnMap:
                valid = spawnMap.CheckSettings( out reason );
                ReportSettings( context, owner, "_data", valid, reason );
                break;
            case ProduceMinigameData minigame:
                valid = ProduceMinigameModel.CheckSettings(
                    minigame.AttemptDuration, minigame.HandleTravelDuration,
                    minigame.SuccessRangeStart, minigame.SuccessRangeEnd,
                    minigame.HitPointSpawnInterval, minigame.HitPointLifetime, out reason );
                ReportSettings( context, owner, "미니게임 설정", valid, reason );
                ReportSettings( context, owner, "_requiredSuccessCount / _failureLossCount",
                    System.Enum.IsDefined( typeof( ProduceMinigameType ), minigame.MinigameType ) == true,
                    "유효한 미니게임 종류가 필요합니다." );
                CheckMinigameCorrections( context, minigame, serialized );
                break;
            case ProduceMinigameDataMap:
                CheckMinigameMap( context, owner, serialized );
                break;
            case EventSettingsDataMap:
                CheckEventMap( context, owner, serialized );
                break;
            case AnomalyEventSettings settings:
                valid = settings.TryGetTimes( out _, out reason );
                ReportSettings( context, owner, "사건 시간", valid, reason );
                break;
            case PreparationGoalData goal:
                ReportSettings( context, owner, "준비 목표", goal.IsValid,
                    "GoalId, Category, ItemData, 필요 수량과 비음식 목표의 Locations를 확인해 주세요." );
                context.Require( owner, "데이터", "_category", "_itemData" );
                break;
            case FoodPlacementData food:
                context.Require( owner, "데이터", "_recipe", "_placementMission" );
                ReportSettings( context, owner, "_locations", food.HasLocation, "음식 배치 인정 장소를 지정해 주세요." );
                break;
            case ItemPlacementMissionData item:
                context.Require( owner, "데이터", "_itemData", "_placementMission" );
                ReportSettings( context, owner, "_locations", item.HasLocation, "일반 아이템 배치 인정 장소를 지정해 주세요." );
                ReportSettings( context, owner, "_itemData 종류",
                    ( item.ItemData is FoodData prepared && prepared.Category == FoodCategory.Prepared ) == false,
                    "완성 음식은 일반 아이템 배치맵이 아닌 음식 배치맵에서 처리해야 합니다." );
                break;
            case CookGoalData:
                context.Require( owner, "데이터", "_recipe", "_mission" );
                break;
            case SupportItemData support:
                ReportSettings( context, owner, "지원 아이템", support.IsValid,
                    "지원 아이템의 식별자, 이름, 종류별 설정을 확인해 주세요." );
                context.Optional( owner, "데이터", "_icon" );
                break;
        }
        foreach ( string field in new[] { "_id", "_recipeId", "_foodId", "_goalId" } )
        {
            SerializedProperty id = serialized.FindProperty( field );
            if ( id != null && id.propertyType == SerializedPropertyType.String )
                ReportSettings( context, owner, field, string.IsNullOrWhiteSpace( id.stringValue ) == false,
                    "빈 식별자입니다. 데이터 ID를 설정해 주세요." );
        }
    }

    /// <summary>
    /// 맵 종류에 명시된 등록 배열의 빈 항목과 중복 검사
    /// </summary>
    /// <param name="context">검사 문맥</param>
    /// <param name="owner">데이터 소유자</param>
    /// <param name="serialized">현재 직렬화 값</param>
    static void CheckArrays ( PlayReadinessContext context, Object owner, SerializedObject serialized )
    {
        string field = PlayReadinessContext.GetMapField( owner );
        if ( field.Length == 0 )
        {
            if ( owner.GetType( ).Name.EndsWith( "DataMap" ) == true )
                context.Report( owner, "데이터", "map-schema", "등록 목록", PlayReadinessStatus.Blocked,
                    "지원되지 않은 데이터맵 종류입니다. 등록 배열을 추측하거나 빈 맵으로 판정하지 않습니다." );
            return;
        }
        SerializedProperty array = serialized.FindProperty( field );
        if ( array == null || array.isArray == false || array.propertyType == SerializedPropertyType.String )
        {
            context.Report( owner, "데이터", "map-schema", field, PlayReadinessStatus.Blocked,
                "맵 등록 배열과 검사 규칙이 다릅니다. 등록 개수를 판정할 수 없습니다." );
            return;
        }
        //문자열 내부 배열과 항목의 하위 배열은 맵 등록 개수에 포함하지 않습니다.
        context.Report( owner, "데이터", "map-inventory", field,
            array.arraySize == 0 ? PlayReadinessStatus.Warning : PlayReadinessStatus.Passed,
            array.arraySize == 0 ? $"등록 배열 {field}에 항목이 0개입니다. 이 맵을 사용하는 기능의 필요 여부를 확인해 주세요."
                : $"등록 배열 {field}: {array.arraySize}개 · 항목 유효성과 씬 연결은 별도 검사합니다." );
        HashSet<Object> references = new( );
        HashSet<string> keys = new( );
        for ( int i = 0; i < array.arraySize; i++ )
        {
            SerializedProperty element = array.GetArrayElementAtIndex( i );
            if ( element.propertyType != SerializedPropertyType.ObjectReference )
                continue;
            Object value = element.objectReferenceValue;
            string key = GetIdentifier( value );
            if ( string.IsNullOrEmpty( key ) == false && keys.Add( key ) == false )
                context.Report( owner, "데이터", "map-key", element.propertyPath,
                    owner is FoodDataMap ? PlayReadinessStatus.Warning : PlayReadinessStatus.Error,
                    $"같은 데이터 키가 중복 등록되어 있습니다: {key}" +
                    ( owner is FoodDataMap ? " · FoodDataMap은 뒤 항목으로 덮어씁니다." : string.Empty ), value );
            if ( value == null || references.Add( value ) == false )
                context.Report( owner, "데이터", "array-entry", element.propertyPath,
                    value == null ? PlayReadinessStatus.Error : PlayReadinessStatus.Warning,
                    value == null ? "등록 항목이 비어 있습니다. 해당 데이터를 연결하거나 불필요한 항목을 제거해 주세요."
                        : "동일 자산이 중복 등록되어 있습니다.", value );
        }
    }

    /// <summary>
    /// 난이도별 하위 설정 연결 검사
    /// </summary>
    /// <param name="context">검사 문맥</param>
    /// <param name="owner">난이도맵</param>
    /// <param name="serialized">원본 설정</param>
    static void CheckDifficultyReferences ( PlayReadinessContext context, Object owner, SerializedObject serialized )
    {
        SerializedProperty array = serialized.FindProperty( "_data" );
        for ( int i = 0; i < array.arraySize; i++ )
        {
            foreach ( string field in new[] { "_flow", "_pressure", "_spawnMap", "_minigameMap", "_mood", "_score" } )
                context.Require( owner, "데이터", $"_data.Array.data[{i}].{field}" );
        }
    }

    /// <summary>
    /// 빈 직렬화 항목에서도 중단 없이 미니게임 대응맵 검사
    /// </summary>
    /// <param name="context">검사 문맥</param>
    /// <param name="owner">미니게임 대응맵</param>
    /// <param name="serialized">원본 설정</param>
    static void CheckMinigameMap ( PlayReadinessContext context, Object owner, SerializedObject serialized )
    {
        SerializedProperty array = serialized.FindProperty( "_data" );
        HashSet<Object> sources = new( );
        for ( int i = 0; i < array.arraySize; i++ )
        {
            string path = $"_data.Array.data[{i}]";
            context.Require( owner, "데이터", path + "._source", path + "._data" );
            SerializedProperty entry = array.GetArrayElementAtIndex( i );
            ProduceMinigameData source = entry.FindPropertyRelative( "_source" )?.objectReferenceValue as ProduceMinigameData;
            ProduceMinigameData data = entry.FindPropertyRelative( "_data" )?.objectReferenceValue as ProduceMinigameData;
            if ( source == null || data == null )
                continue;
            bool valid = sources.Add( source ) == true && source.MinigameType == data.MinigameType;
            ReportSettings( context, owner, path, valid, "기본 설정 중복 또는 대응 설정의 미니게임 종류 불일치입니다." );
        }
    }

    /// <summary>
    /// 실제 미니게임 계산에 앞서 보정되는 저장 값을 경고로 표시
    /// </summary>
    /// <param name="context">검사 문맥</param>
    /// <param name="data">미니게임 설정</param>
    /// <param name="serialized">원본 설정</param>
    static void CheckMinigameCorrections ( PlayReadinessContext context, ProduceMinigameData data, SerializedObject serialized )
    {
        string[] fields = { "_actionDuration", "_handleTravelDuration", "_successRangeStart", "_successRangeEnd", "_hitPointSpawnInterval", "_hitPointLifetime" };
        float[] applied = { data.AttemptDuration, data.HandleTravelDuration, data.SuccessRangeStart, data.SuccessRangeEnd, data.HitPointSpawnInterval, data.HitPointLifetime };
        for ( int i = 0; i < fields.Length; i++ )
        {
            float raw = serialized.FindProperty( fields [ i ] ).floatValue;
            if ( raw != applied [ i ] )
                context.Report( data, "데이터", "runtime-correction", fields [ i ], PlayReadinessStatus.Warning,
                    $"저장 값 {raw} → 실제 적용 값 {applied[i]}. 런타임 범위 보정을 사용하는 설정입니다." );
        }
        foreach ( string field in new[] { "_requiredSuccessCount", "_failureLossCount" } )
        {
            int raw = serialized.FindProperty( field ).intValue;
            if ( raw < 1 )
                context.Report( data, "데이터", "runtime-correction", field, PlayReadinessStatus.Warning,
                    $"저장 수량 {raw}은 런타임에서 1로 보정됩니다." );
        }
    }

    /// <summary>
    /// 사건 종류별 설정의 누락과 중복 검사
    /// </summary>
    /// <param name="context">검사 문맥</param>
    /// <param name="owner">사건 설정맵</param>
    /// <param name="serialized">원본 설정</param>
    static void CheckEventMap ( PlayReadinessContext context, Object owner, SerializedObject serialized )
    {
        SerializedProperty array = serialized.FindProperty( "_entries" );
        HashSet<int> types = new( );
        for ( int i = 0; i < array.arraySize; i++ )
        {
            string path = $"_entries.Array.data[{i}]";
            SerializedProperty entry = array.GetArrayElementAtIndex( i );
            int type = entry.FindPropertyRelative( "_type" ).intValue;
            context.Require( owner, "데이터", path + "._settings" );
            ReportSettings( context, owner, path + "._type",
                System.Enum.IsDefined( typeof( AnomalyEventType ), type ) == true && types.Add( type ) == true,
                "유효하지 않은 사건 종류 또는 같은 종류의 설정이 중복 등록되어 있습니다." );
        }
    }

    /// <summary>
    /// 설정 판정과 실패 원인 보고
    /// </summary>
    /// <param name="context">검사 문맥</param>
    /// <param name="owner">데이터 소유자</param>
    /// <param name="field">검사 필드</param>
    /// <param name="valid">유효 여부</param>
    /// <param name="reason">실패 원인</param>
    static void ReportSettings ( PlayReadinessContext context, Object owner, string field, bool valid, string reason )
    {
        context.Report( owner, "데이터", "settings", field,
            valid == true ? PlayReadinessStatus.Passed : PlayReadinessStatus.Error,
            valid == true ? "정적 설정 계약 통과" : reason );
    }

    /// <summary>
    /// 수치의 유한 여부 확인
    /// </summary>
    /// <param name="value">검사 수치</param>
    /// <returns>유한 여부</returns>
    public static bool Finite ( float value )
    {
        return float.IsNaN( value ) == false && float.IsInfinity( value ) == false;
    }

    /// <summary>
    /// 데이터맵에서 실제 사용하는 식별 키 조회
    /// </summary>
    /// <param name="value">등록 데이터</param>
    /// <returns>등록 키 또는 빈 문자열</returns>
    static string GetIdentifier ( Object value )
    {
        return value switch
        {
            MissionData mission => "미션/" + mission.Id,
            RecipeData recipe => "레시피/" + recipe.RecipeId,
            SupportItemData support => "지원/" + support.Id,
            FoodData food => "음식/" + food.FoodId,
            PreparationGoalData goal => "준비/" + goal.GoalId,
            CookGoalData cook when cook.Recipe != null => "제작 입력/" + cook.Recipe.GetInstanceID( ),
            FoodPlacementData food when food.Recipe != null => "음식 배치/" + food.Recipe.GetInstanceID( ),
            ItemPlacementMissionData item when item.ItemData != null => "아이템 배치/" + item.ItemData.GetInstanceID( ),
            _ => string.Empty
        };
    }

    /// <summary>
    /// 각 진행 중재자에 연결된 맵으로 미션 입력과 ID 중복 검사
    /// </summary>
    /// <param name="context">검사 문맥</param>
    public static void CheckRelations ( PlayReadinessContext context )
    {
        Dictionary<string, Object> ids = new( );
        foreach ( Object owner in context.Objects )
        {
            string id = GetIdentifier( owner );
            if ( string.IsNullOrEmpty( id ) == false && ( owner is MissionData || owner is RecipeData ||
                 owner is SupportItemData || owner is PreparationGoalData || owner is FoodData ) )
            {
                if ( ids.TryGetValue( id, out Object previous ) == true && previous != owner )
                    context.Report( owner, "데이터", "duplicate-id", "데이터 ID", PlayReadinessStatus.Warning,
                        $"같은 ID {id}를 다른 자산 {previous.name}도 사용합니다. 현재 맵의 자산·의도를 대조해 주세요.", previous );
                else
                    ids [ id ] = owner;
            }
            if ( owner is PlayScene scene )
                CheckMissionInputs( context, scene );
        }
    }

    /// <summary>
    /// 후보 미션 ID가 실제 제작 또는 배치 입력에 등록되어 있는지 확인
    /// </summary>
    /// <param name="context">검사 문맥</param>
    /// <param name="scene">플레이 씬</param>
    static void CheckMissionInputs ( PlayReadinessContext context, PlayScene scene )
    {
        if ( context.CanCheckScene( scene ) == false )
        {
            context.Report( scene, "데이터", "relation-scope", "씬 입력 관계", PlayReadinessStatus.Blocked,
                "프리팹·부분 선택 범위에서는 씬 전체의 공급·배치·발생 관계를 단정하지 않습니다. 해당 씬 전체 범위에서 새로고침해 주세요." );
            return;
        }
        using SerializedObject root = new( scene );
        Object flow = root.FindProperty( "_flow" ).objectReferenceValue;
        Object recorder = root.FindProperty( "_mission" ).objectReferenceValue;
        Object preparation = root.FindProperty( "_preparation" ).objectReferenceValue;
        if ( flow == null || recorder == null || preparation == null )
        {
            context.Report( scene, "데이터", "input-parent", "미션 입력", PlayReadinessStatus.Blocked,
                "Flow, MissionResultRecorder, PreparationSystem 연결 후 미션 입력 대조를 진행할 수 있습니다." );
            return;
        }
        using SerializedObject flowSettings = new( flow );
        using SerializedObject inputs = new( recorder );
        using SerializedObject placement = new( preparation );
        Object difficulty = flowSettings.FindProperty( "_difficultyMap" ).objectReferenceValue;
        if ( difficulty == null )
            return;
        CheckInputContent( context, scene, recorder, preparation );
        HashSet<string> inputIds = new( );
        foreach ( Object entry in context.ReadMap( inputs.FindProperty( "_cookGoalMap" ).objectReferenceValue ) )
        {
            if ( entry is CookGoalData cook && cook.Mission != null )
                inputIds.Add( cook.Mission.Id );
        }
        foreach ( Object entry in context.ReadMap( placement.FindProperty( "_foodMap" ).objectReferenceValue ) )
        {
            if ( entry is FoodPlacementData food && food.PlacementMission != null )
                inputIds.Add( food.PlacementMission.Id );
        }
        foreach ( Object entry in context.ReadMap( placement.FindProperty( "_itemMap" ).objectReferenceValue ) )
        {
            if ( entry is ItemPlacementMissionData item && item.PlacementMission != null )
                inputIds.Add( item.PlacementMission.Id );
        }
        using SerializedObject map = new( difficulty );
        SerializedProperty settings = map.FindProperty( "_data" );
        for ( int d = 0; d < settings.arraySize; d++ )
        {
            SerializedProperty difficultyEntry = settings.GetArrayElementAtIndex( d );
            CheckSpawnContent( context, scene, difficultyEntry.FindPropertyRelative( "_spawnMap" )?.objectReferenceValue );
            CheckMinigameCoverage( context, scene, difficultyEntry.FindPropertyRelative( "_minigameMap" )?.objectReferenceValue );
            SerializedProperty entry = settings.GetArrayElementAtIndex( d ).FindPropertyRelative( "_flow" );
            if ( entry == null || entry.objectReferenceValue is GameFlowData data == false )
                continue;
            for ( int s = 0; s < data.StageCount; s++ )
            {
                MissionDataMap candidates = data.GetMissionMap( s );
                if ( candidates == null )
                    continue;
                for ( int m = 0; m < candidates.MissionCount; m++ )
                {
                    MissionData mission = candidates.GetMission( m );
                    if ( mission == null )
                        continue;
                    bool covered = inputIds.Contains( mission.Id );
                    context.Report( data, "데이터", "mission-input/" + scene.GetInstanceID( ), $"_stages.Array.data[{s}].input[{m}]",
                        covered == true ? PlayReadinessStatus.Passed : PlayReadinessStatus.Error,
                        covered == true ? $"{mission.Id}: 연결된 제작/음식/아이템 배치 입력맵에 등록됨"
                            : $"{mission.name} ({mission.Id}): 현재 씬의 CookGoalMap, FoodMap, ItemMap 어디에도 입력이 없습니다. 해당 미션을 처리하는 데이터를 연결된 맵에 등록해 주세요.", mission );
                }
            }
        }
    }

    /// <summary>
    /// 입력 데이터의 레시피 등록과 실제 배치 표면 검사
    /// </summary>
    /// <param name="context">검사 문맥</param>
    /// <param name="scene">플레이 씬</param>
    /// <param name="recorder">제작 미션 입력</param>
    /// <param name="preparation">배치와 평가 입력</param>
    static void CheckInputContent ( PlayReadinessContext context, PlayScene scene, Object recorder, Object preparation )
    {
        using SerializedObject root = new( scene );
        Object production = root.FindProperty( "_product" ).objectReferenceValue;
        if ( production == null )
            return;
        using SerializedObject products = new( production );
        List<Object> recipes = context.ReadMap( products.FindProperty( "_recipeMap" ).objectReferenceValue );
        using SerializedObject cookSettings = new( recorder );
        using SerializedObject placement = new( preparation );
        List<Object> entries = context.ReadMap( cookSettings.FindProperty( "_cookGoalMap" ).objectReferenceValue );
        List<Object> foodSetups = context.ReadMap( placement.FindProperty( "_foodMap" ).objectReferenceValue );
        entries.AddRange( foodSetups );
        entries.AddRange( context.ReadMap( placement.FindProperty( "_itemMap" ).objectReferenceValue ) );
        entries.AddRange( context.ReadMap( placement.FindProperty( "_goalMap" ).objectReferenceValue ) );
        HashSet<ItemData> placedItems = new( );
        foreach ( Object candidate in context.Objects )
        {
            if ( candidate is ItemCarry carry && carry.gameObject.scene == scene.gameObject.scene && carry.ItemData != null )
                placedItems.Add( carry.ItemData );
        }
        foreach ( Object entry in entries )
        {
            if ( entry == null )
                continue;
            RecipeData recipe = entry is CookGoalData cook ? cook.Recipe : entry is FoodPlacementData food ? food.Recipe : null;
            if ( recipe != null )
                context.Report( entry, "데이터", "input-recipe/" + scene.GetInstanceID( ), "_recipe",
                    recipes.Contains( recipe ) == true ? PlayReadinessStatus.Passed : PlayReadinessStatus.Error,
                    $"입력 레시피 {recipe.name}: 현재 ProductionManager._recipeMap 등록 " +
                    ( recipes.Contains( recipe ) == true ? "확인" : "누락; 연결된 RecipeDataMap에 같은 자산을 등록해 주세요." ), recipe );
            if ( entry is ItemPlacementMissionData item && item.ItemData != null )
                context.Report( entry, "데이터", "item-content/" + scene.GetInstanceID( ), "_itemData",
                    placedItems.Contains( item.ItemData ) == true ? PlayReadinessStatus.Passed : PlayReadinessStatus.Warning,
                    "일반 아이템의 실제 씬 배치 여부를 확인합니다. 별도 생성 방식이면 플레이에서 공급을 확인해 주세요.", item.ItemData );
            if ( entry is CookGoalData )
                continue;
            bool hasSurface = false;
            foreach ( Object candidate in context.Objects )
            {
                if ( candidate is PlacementTarget target == false || target.gameObject.scene != scene.gameObject.scene ||
                     target.isActiveAndEnabled == false )
                    continue;
                hasSurface = entry is FoodPlacementData foodEntry && foodEntry.ContainsTarget( target ) == true ||
                    entry is ItemPlacementMissionData itemEntry && itemEntry.ContainsTarget( target ) == true ||
                    entry is PreparationGoalData goal && ( goal.IsFoodGoal == true
                        ? foodSetups.Exists( setup => setup is FoodPlacementData foodSetup && foodSetup.Recipe != null &&
                            foodSetup.Recipe.ResultItem == goal.ItemData && foodSetup.ContainsTarget( target ) == true )
                        : goal.ContainsLocation( target.PreparationLocation ) == true );
                if ( hasSurface == true )
                    break;
            }
            context.Report( entry, "데이터", "placement-surface/" + scene.GetInstanceID( ), "_locations",
                hasSurface == true ? PlayReadinessStatus.Passed : PlayReadinessStatus.Warning,
                hasSurface == true ? "인정 장소와 일치하는 활성 씬 PlacementTarget 존재 · 물리 배치 가능성은 플레이 확인 필요"
                    : "인정 장소와 일치하는 활성 PlacementTarget을 찾지 못했습니다. 씬 표면의 PreparationLocation을 확인해 주세요." );
        }
    }

    /// <summary>
    /// 연결된 발생맵에 대응하는 활성 적과 사건 종류 확인
    /// </summary>
    /// <param name="context">검사 문맥</param>
    /// <param name="scene">검사 씬</param>
    /// <param name="map">난이도에서 연결된 발생맵</param>
    static void CheckSpawnContent ( PlayReadinessContext context, PlayScene scene, Object map )
    {
        HashSet<Object> registered = new( );
        using SerializedObject root = new( scene );
        Object enemyScene = root.FindProperty( "_enemyScene" ).objectReferenceValue;
        if ( enemyScene != null )
        {
            using SerializedObject settings = new( enemyScene );
            foreach ( SerializedProperty property in PlayReadinessContext.EnumerateProperties( settings ) )
            {
                if ( property.name == "_enemy" && property.propertyType == SerializedPropertyType.ObjectReference &&
                     property.objectReferenceValue != null )
                    registered.Add( property.objectReferenceValue );
            }
        }
        foreach ( Object entry in context.ReadMap( map ) )
        {
            if ( entry is AnomalySpawnData data == false )
                continue;
            int enemies = 0;
            int events = 0;
            foreach ( Object candidate in context.Objects )
            {
                if ( candidate is Component component == false || component.gameObject.scene != scene.gameObject.scene )
                    continue;
                if ( candidate is Enemy enemy && registered.Contains( enemy ) == true &&
                     enemy.isActiveAndEnabled == true && data.Contains( enemy.SpawnType ) == true )
                    enemies++;
                if ( candidate is AnomalyEventSystem presenter && presenter.isActiveAndEnabled == true && data.Contains( presenter.EventType ) == true )
                    events++;
            }
            bool valid = ( data.EnemyType == EnemySpawnType.None || enemies > 0 ) &&
                ( data.HasEventOwner == false && data.EnemyType != EnemySpawnType.None || events > 0 );
            context.Report( scene, "데이터", "spawn-content", $"{map.name}/{data.name}/{data.GetInstanceID( )}",
                valid == true ? PlayReadinessStatus.Passed : PlayReadinessStatus.Error,
                $"{data.EnemyType} · 등록된 활성 적 {enemies}개 / 사건 중재자 {events}개. EnemySceneController 등록과 발생맵의 적·사건 종류에 맞는 씬 구성 필요", data );
        }
    }

    /// <summary>
    /// 사용 레시피마다 난이도 미니게임 대응 설정이 있는지 확인
    /// </summary>
    /// <param name="context">검사 문맥</param>
    /// <param name="scene">플레이 씬</param>
    /// <param name="map">난이도 미니게임 대응맵</param>
    static void CheckMinigameCoverage ( PlayReadinessContext context, PlayScene scene, Object map )
    {
        if ( map == null )
            return;
        using SerializedObject root = new( scene );
        Object production = root.FindProperty( "_product" ).objectReferenceValue;
        if ( production == null )
            return;
        using SerializedObject products = new( production );
        using SerializedObject minigames = new( map );
        SerializedProperty array = minigames.FindProperty( "_data" );
        HashSet<Object> sources = new( );
        for ( int i = 0; i < array.arraySize; i++ )
        {
            SerializedProperty entry = array.GetArrayElementAtIndex( i );
            Object source = entry.FindPropertyRelative( "_source" )?.objectReferenceValue;
            Object target = entry.FindPropertyRelative( "_data" )?.objectReferenceValue;
            if ( source != null && target != null )
                sources.Add( source );
        }
        foreach ( Object value in context.ReadMap( products.FindProperty( "_recipeMap" ).objectReferenceValue ) )
        {
            if ( value is RecipeData recipe == false || recipe.ProduceMinigame == null )
                continue;
            bool mapped = sources.Contains( recipe.ProduceMinigame );
            context.Report( map, "데이터", "minigame-coverage/" + scene.GetInstanceID( ), $"레시피/{recipe.RecipeId}/{recipe.GetInstanceID( )}",
                mapped == true ? PlayReadinessStatus.Passed : PlayReadinessStatus.Error,
                mapped == true ? $"{recipe.name}: 기본 미니게임에 대한 난이도 대응 항목 존재"
                    : $"{recipe.name}: {recipe.ProduceMinigame.name}의 대응 항목이 없습니다. 이 맵의 Source→Data를 등록해 주세요.", recipe );
        }
    }
}
