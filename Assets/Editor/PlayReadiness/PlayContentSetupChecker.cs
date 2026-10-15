using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 제작 공급원, 아이템과 변칙 컴포넌트 구성 검사
/// </summary>
public static class PlayContentSetupChecker
{
    /// <summary>
    /// 콘텐츠별 필수 참조와 컴포넌트 검사
    /// </summary>
    /// <param name="context">검사 문맥</param>
    /// <param name="owner">콘텐츠 소유자</param>
    public static void Check ( PlayReadinessContext context, Object owner )
    {
        string[] fields = owner.GetType( ).Name switch
        {
            "IngredientSource" => new[] { "_data", "_itemPrefab" },
            "ItemCarry" => new[] { "_itemData" },
            "CookController" => new[] { "_guidePoint", "_highlight", "_ingredientTarget", "_ingredientRoot", "_contentRoot" },
            "RecipeData" => new[] { "_resultItem", "_resultPrefab", "_produceMinigame" },
            "PlacementTarget" => new[] { "_surfaceCollider" },
            "RotateView" => new[] { "_rotatingTarget" },
            "ShakeView" => new[] { "_shakingTarget" },
            "OpenClosePlacement" => new[] { "_lidPivot" },
            "PresenceDisplayController" => new[] { "_target", "_collider", "_restoreCheckCollider" },
            "MaterialChangeView" => new[] { "_target" },
            "MaterialChangePresenter" => new[] { "_map" },
            "AppearanceChangeController" => new[] { "_map", "_initialData", "_appearanceRoot" },
            "EnemyDemandData" => new[] { "_likedFoods", "_dislikedFoods" },
            _ => Array.Empty<string>( )
        };
        if ( fields.Length > 0 )
            context.Require( owner, "콘텐츠", fields );
        if ( owner is ItemWorldController world )
        {
            bool valid = world.CheckConfiguration( out string reason );
            context.Report( owner, "콘텐츠", "item-physics", "Collider / Rigidbody",
                valid == true ? PlayReadinessStatus.Passed : PlayReadinessStatus.Error,
                valid == true ? "실제 조회 경로의 Collider와 Rigidbody 구성 통과" : reason );
        }
        if ( owner is IngredientSource source )
        {
            using SerializedObject serialized = new( source );
            Object hud = serialized.FindProperty( "_refillHud" ).objectReferenceValue;
            context.Report( source, "콘텐츠", "refill-hud", "_refillHud",
                hud == null ? PlayReadinessStatus.Warning : PlayReadinessStatus.Passed,
                hud == null ? "선택 재보충 게이지가 비어 있습니다. 재고 동작은 가능하지만 게이지가 표시되지 않습니다."
                    : "재보충 게이지 연결됨", hud );
            ItemCarry prefab = serialized.FindProperty( "_itemPrefab" ).objectReferenceValue as ItemCarry;
            if ( prefab != null )
            {
                CheckComponent( context, prefab, typeof( ItemPoolHandler ) );
                CheckComponent( context, prefab, typeof( ItemWorldController ) );
            }
        }
        if ( owner is RecipeData recipe )
        {
            bool valid = ProductionModel.CheckSettings( recipe, 1, 1, out string reason );
            context.Report( recipe, "콘텐츠", "recipe-contract", "레시피 조합",
                valid == true ? PlayReadinessStatus.Passed : PlayReadinessStatus.Error,
                valid == true ? "레시피 ID·결과·재료 참조와 중복 검사 통과; 미니게임 수량은 데이터 검사에서 확인" : reason );
            context.Report( owner, "콘텐츠", "recipe-ingredients", "_ingredients",
                recipe.IngredientCount > 0 ? PlayReadinessStatus.Passed : PlayReadinessStatus.Error,
                $"필요 재료 {recipe.IngredientCount}종 · 재료별 Item과 양수 수량을 사용해 주세요." );
            for ( int i = 0; i < recipe.IngredientCount; i++ )
            {
                context.Require( owner, "콘텐츠", $"_ingredients.Array.data[{i}]._item" );
                using SerializedObject serialized = new( owner );
                int count = serialized.FindProperty( $"_ingredients.Array.data[{i}]._requiredCount" ).intValue;
                context.Report( owner, "콘텐츠", "ingredient-count", $"_ingredients.Array.data[{i}]._requiredCount",
                    count > 0 ? PlayReadinessStatus.Passed : PlayReadinessStatus.Warning,
                    $"저장 필요 수량: {count} · 실제 적용: {Mathf.Max( 1, count )} (최소 1로 보정)" );
            }
        }
        if ( owner is Component component )
        {
            AnomalySetupKind? kind = component.GetType( ).Name switch
            {
                "OpenCloseController" or "OpenClosePlacement" => AnomalySetupKind.OpenClose,
                "RotatePresenter" or "RotateView" => AnomalySetupKind.Rotate,
                "ShakePresenter" or "ShakeView" => AnomalySetupKind.Shake,
                "PresenceController" or "PresenceDisplayController" => AnomalySetupKind.Presence,
                "MaterialChangePresenter" or "MaterialChangeView" => AnomalySetupKind.Material,
                "AppearanceChangeController" => AnomalySetupKind.Appearance,
                _ => null
            };
            if ( kind.HasValue == true )
            {
                foreach ( Type type in AnomalyComponentSetupUtility.GetRequiredTypes( kind.Value ) )
                    CheckComponent( context, component, type );
            }
        }
    }

    /// <summary>
    /// 같은 오브젝트의 필수 컴포넌트 존재와 중복 검사
    /// </summary>
    /// <param name="context">검사 문맥</param>
    /// <param name="owner">기능 소유자</param>
    /// <param name="type">필수 컴포넌트 형식</param>
    static void CheckComponent ( PlayReadinessContext context, Component owner, Type type )
    {
        Component[] components = owner.GetComponents( type );
        context.Report( owner.gameObject, "콘텐츠", "component", type.Name,
            components.Length == 1 ? PlayReadinessStatus.Passed : PlayReadinessStatus.Error,
            $"{type.Name}: {components.Length}개 · 기능 구성에는 같은 오브젝트에 1개가 필요합니다." );
    }

    /// <summary>
    /// 등록된 공급원과 레시피의 실제 공급 관계 검사
    /// </summary>
    /// <param name="context">검사 문맥</param>
    public static void CheckRelations ( PlayReadinessContext context )
    {
        foreach ( Object owner in context.Objects )
        {
            if ( owner is PlayScene scene == false )
                continue;
            if ( context.CanCheckScene( scene ) == false )
                continue;
            using SerializedObject serialized = new( scene );
            Object stock = serialized.FindProperty( "_ingredientStock" ).objectReferenceValue;
            Object production = serialized.FindProperty( "_product" ).objectReferenceValue;
            if ( stock == null || production == null )
            {
                context.Report( scene, "콘텐츠", "supply-parent", "재료 공급", PlayReadinessStatus.Blocked,
                    "IngredientStock와 ProductionManager 연결 후 공급 관계를 검사할 수 있습니다." );
                continue;
            }
            using SerializedObject sources = new( stock );
            SerializedProperty array = sources.FindProperty( "_sources" );
            HashSet<ItemData> supplied = new( );
            HashSet<IngredientSource> registered = new( );
            for ( int i = 0; i < array.arraySize; i++ )
            {
                IngredientSource source = array.GetArrayElementAtIndex( i ).objectReferenceValue as IngredientSource;
                bool valid = source != null && registered.Add( source ) == true &&
                    source.gameObject.scene == scene.gameObject.scene && source.isActiveAndEnabled == true;
                context.Report( stock, "콘텐츠", "registered-source", $"_sources.Array.data[{i}]",
                    valid == true ? PlayReadinessStatus.Passed : PlayReadinessStatus.Error,
                    valid == true ? $"등록된 활성 씬 공급원: {source.name}" : "빈 항목, 중복, 비활성 또는 다른 씬/프리팹 공급원입니다.", source );
                if ( valid == false )
                    continue;
                using SerializedObject sourceSettings = new( source );
                ItemCarry prefab = sourceSettings.FindProperty( "_itemPrefab" ).objectReferenceValue as ItemCarry;
                if ( prefab != null && prefab.ItemData != null &&
                     sourceSettings.FindProperty( "_data" ).objectReferenceValue != null &&
                     prefab.GetComponent<ItemPoolHandler>( ) != null && prefab.GetComponent<ItemWorldController>( ) != null &&
                     prefab.GetComponent<ItemWorldController>( ).CheckConfiguration( out _ ) == true )
                    supplied.Add( prefab.ItemData );
            }
            foreach ( Object candidate in context.Objects )
            {
                if ( candidate is IngredientSource source && source.gameObject.scene == scene.gameObject.scene &&
                     EditorUtility.IsPersistent( source ) == false && registered.Contains( source ) == false )
                    context.Report( source, "콘텐츠", "unregistered-source", "IngredientStockManager._sources",
                        PlayReadinessStatus.Warning, "씬에 배치했지만 현재 재고 중재자에 등록되지 않은 공급원입니다.", stock );
            }
            using SerializedObject recipes = new( production );
            foreach ( Object value in context.ReadMap( recipes.FindProperty( "_recipeMap" ).objectReferenceValue ) )
            {
                if ( value is RecipeData recipe == false )
                    continue;
                for ( int i = 0; i < recipe.IngredientCount; i++ )
                {
                    IngredientRequirement requirement = recipe.GetIngredient( i );
                    if ( requirement == null || requirement.Item == null )
                        continue;
                    bool available = supplied.Contains( requirement.Item );
                    context.Report( recipe, "콘텐츠", "ingredient-supply/" + scene.GetInstanceID( ), $"_ingredients.Array.data[{i}]",
                        available == true ? PlayReadinessStatus.Passed : PlayReadinessStatus.Warning,
                        available == true ? $"{requirement.Item.name}: 현재 등록된 공급원에서 공급 가능"
                            : $"{requirement.Item.name}: 유효한 등록 공급원을 찾지 못했습니다. _sources→_itemPrefab→ItemCarry._itemData 연결을 확인해 주세요. 별도 공급 방식이 있다면 플레이로 확인해 주세요.", requirement.Item );
                }
            }
        }
    }
}
