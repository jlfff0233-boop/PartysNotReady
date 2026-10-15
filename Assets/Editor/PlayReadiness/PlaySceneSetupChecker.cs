using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 플레이 진입에 필요한 시스템 참조와 테스트 설정 검사
/// </summary>
public static class PlaySceneSetupChecker
{
    /// <summary>
    /// 명시된 시스템 계약 검사
    /// </summary>
    /// <param name="context">검사 문맥</param>
    /// <param name="owner">설정 소유자</param>
    public static void Check ( PlayReadinessContext context, Object owner )
    {
        string[] fields = owner.GetType( ).Name switch
        {
            "PlayScene" => new[] { "_eventSettingsDataMap", "_player", "_inputHandler", "_cameraFocus", "_camera",
                "_playerHud", "_produceHud", "_canvasHud", "_screenEffect", "_cameraEffect", "_stunEffect",
                "_captureEffect", "_routeGraph", "_npcPartyRoom", "_flow", "_ingredientStock", "_enemyScene", "_pressure",
                "_mood", "_resultEvaluation", "_product", "_mission", "_preparation" },
            "GameFlowController" => new[] { "_difficultyMap", "_view", "_resultView" },
            "ProductionManager" => new[] { "_recipeMap", "_hud" },
            "MissionResultRecorder" => new[] { "_preparation" },
            "PreparationSystem" => new[] { "_goalMap" },
            "Npc" => new[] { "_data", "_mover", "_animatorHandler", "_captureEffect" },
            _ => System.Array.Empty<string>( )
        };
        if ( fields.Length > 0 )
            context.Require( owner, "씬", fields );
        if ( owner is PlaySceneTestSettings )
        {
            using SerializedObject serialized = new( owner );
            foreach ( string field in new[] { "_allowMissingItemContent", "_disableAi" } )
            {
                bool enabled = serialized.FindProperty( field ).boolValue;
                context.Report( owner, "씬", "test-mode", field,
                    enabled == true ? PlayReadinessStatus.Warning : PlayReadinessStatus.Passed,
                    enabled == true ? "테스트 우회가 켜져 있습니다. 정상 콘텐츠·AI 플레이 검증 전에 해제해 주세요."
                        : "테스트 우회 해제됨" );
            }
        }
        if ( owner is EnemySceneController )
            CheckEnemyEntries( context, owner );
        if ( owner is PreparationSystem )
            CheckPreparationDependencies( context, owner );
        if ( owner is MissionResultRecorder )
        {
            using SerializedObject serialized = new( owner );
            if ( context.ReadMap( serialized.FindProperty( "_cookGoalMap" ).objectReferenceValue ).Count > 0 )
                context.Require( owner, "씬", "_productPresenter" );
        }
    }

    /// <summary>
    /// 음식 기능을 사용하는 구성에서만 제작과 음식 배치 연결 요구
    /// </summary>
    /// <param name="context">검사 문맥</param>
    /// <param name="owner">준비 평가 중재자</param>
    static void CheckPreparationDependencies ( PlayReadinessContext context, Object owner )
    {
        using SerializedObject serialized = new( owner );
        System.Collections.Generic.List<Object> goals = context.ReadMap( serialized.FindProperty( "_goalMap" ).objectReferenceValue );
        System.Collections.Generic.List<Object> food = context.ReadMap( serialized.FindProperty( "_foodMap" ).objectReferenceValue );
        bool requiresFood = food.Count > 0 || goals.Exists( value => value is PreparationGoalData goal && goal.IsFoodGoal == true );
        if ( goals.Count == 0 && serialized.FindProperty( "_goalMap" ).objectReferenceValue != null )
            context.Report( owner, "씬", "required-goals", "_goalMap", PlayReadinessStatus.Error,
                "PreparationSystem은 하나 이상의 준비 목표가 필요합니다. 연결된 GoalMap에 목표를 등록해 주세요." );
        if ( requiresFood == true )
        {
            context.Require( owner, "씬", "_foodMap", "_productPresenter" );
            if ( food.Count == 0 && serialized.FindProperty( "_foodMap" ).objectReferenceValue != null )
                context.Report( owner, "씬", "required-food", "_foodMap", PlayReadinessStatus.Error,
                    "음식 준비 목표가 있지만 연결된 FoodMap에 배치 설정이 없습니다." );
        }
        foreach ( Object value in goals )
        {
            if ( value is PreparationGoalData goal == false || goal.IsFoodGoal == false )
                continue;
            bool hasInput = food.Exists( entry => entry is FoodPlacementData setup &&
                setup.Recipe != null && setup.Recipe.ResultItem == goal.ItemData );
            context.Report( owner, "씬", "food-goal-input", "_goalMap/" + goal.GoalId,
                hasInput == true ? PlayReadinessStatus.Passed : PlayReadinessStatus.Error,
                hasInput == true ? $"음식 목표 {goal.GoalId}: 결과 ItemData에 대응하는 음식 배치 입력 존재"
                    : $"음식 목표 {goal.GoalId}: FoodMap에 같은 결과 ItemData의 레시피 배치 설정이 없습니다.", goal );
        }
        Object itemMap = serialized.FindProperty( "_itemMap" ).objectReferenceValue;
        if ( itemMap != null && context.ReadMap( itemMap ).Count == 0 )
            context.Report( owner, "씬", "configured-item-map", "_itemMap", PlayReadinessStatus.Error,
                "일반 아이템 기능을 사용하지 않으면 ItemMap 연결을 비워도 됩니다. 연결한 경우에는 하나 이상의 배치 설정이 필요합니다.", itemMap );
    }

    /// <summary>
    /// 적 배치 항목의 필수 객체와 목적지 검사
    /// </summary>
    /// <param name="context">검사 문맥</param>
    /// <param name="owner">적 씬 중재자</param>
    static void CheckEnemyEntries ( PlayReadinessContext context, Object owner )
    {
        using SerializedObject serialized = new( owner );
        foreach ( SerializedProperty property in PlayReadinessContext.EnumerateProperties( serialized ) )
        {
            if ( property.isArray == true && property.arraySize == 0 &&
                 ( property.name == "_enterPoints" || property.name == "_escapePoints" ) )
                context.Report( owner, "씬", "enemy-waypoints", property.propertyPath,
                    PlayReadinessStatus.Error, "침입·탈출 경유점에는 하나 이상의 Transform을 지정해 주세요." );
            if ( property.propertyType != SerializedPropertyType.ObjectReference )
                continue;
            context.Require( owner, "씬", property.propertyPath );
        }
    }
}
