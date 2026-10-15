using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 변칙 공통 설정 적용 - 선택한 판정값과 데이터 참조 저장
/// </summary>
public static class AnomalyPrefabSettingsUtility
{
    /// <summary>
    /// 공통 설정에서 사용하는 기능 형식
    /// </summary>
    static readonly Type[] _types =
    {
        typeof( AnomalyTarget ), typeof( RotateView ), typeof( ShakeView ),
        typeof( PresenceDisplayController ), typeof( PresenceController ), typeof( ItemWorldController ),
        typeof( MaterialChangeView ), typeof( MaterialChangePresenter ),
        typeof( OpenClosePlacement ), typeof( AppearanceChangeController )
    };


    /// <summary>
    /// 지정 컴포넌트의 단일 설정 변경
    /// </summary>
    /// <param name="owner">설정 소유자, 기능이 없으면 건너뜀</param>
    /// <param name="field">직렬화 필드</param>
    /// <param name="value">적용할 설정값</param>
    /// <param name="label">화면 표시 이름</param>
    /// <param name="results">변경 결과</param>
    static void Set ( Component owner, string field, object value, string label, List<string> results )
    {
        if ( owner == null )
        {
            results.Add( $"건너뜀: {label} / 해당 기능 없음" );
            return;
        }
        using SerializedObject serialized = new( owner );
        SerializedProperty property = serialized.FindProperty( field );
        switch ( value )
        {
            case int number: property.intValue = number; break;
            case bool flag: property.boolValue = flag; break;
            case float seconds: property.floatValue = seconds; break;
            case Vector3 direction: property.vector3Value = direction; break;
            case Object reference: property.objectReferenceValue = reference; break;
        }
        if ( serialized.ApplyModifiedPropertiesWithoutUndo( ) == true )
            results.Add( $"설정: {owner.name} / {label} → {value}" );
    }


    /// <summary>
    /// 관리 루트의 기존 기능에 선택한 공통값 적용
    /// </summary>
    /// <param name="root">기능 관리 루트</param>
    /// <param name="options">일괄 적용할 설정</param>
    /// <param name="results">변경 결과</param>
    /// <param name="entry">원본별 직접 지정 정보</param>
    public static void Process ( GameObject root, AnomalyPrefabOptions options, List<string> results, AnomalyPrefabReferenceEntry entry = null )
    {
        if ( options.ConnectReferences == true )
            AnomalyPrefabReferenceUtility.Process( root, options.ReplaceReferences, results, entry );
        PresenceController presence = root.GetComponent<PresenceController>( );
        AppearanceChangeController appearance = root.GetComponent<AppearanceChangeController>( );
        if ( options.ChangePresenceMask == true )
            Set( presence, "_obstacleMask", options.PresenceMask, "존재 장애물 마스크", results );
        if ( options.ChangeAppearanceMask == true )
            Set( appearance, "_obstacleMask", options.AppearanceMask, "외형 장애물 마스크", results );
        if ( options.ChangeFloorMask == true )
            Set( root.GetComponent<OpenClosePlacement>( ), "_floorMask", options.FloorMask, "개폐 바닥 마스크", results );
        if ( options.ChangePresence == true )
            Set( presence, "_initiallyPresent", options.InitiallyPresent, "초기 존재 여부", results );
        if ( options.ChangeRestoreTime == true )
        {
            Set( presence, "_refillDuration", options.RefillDuration, "복구 대기 시간", results );
            Set( presence, "_restoreCheckInterval", options.CheckInterval, "공간 검사 간격", results );
        }
        if ( options.ChangeAxis == true )
            Set( root.GetComponent<RotateView>( ), "_axis", options.Axis, "로컬 회전 축", results );
        if ( options.ChangeItemData == true )
            Set( root.GetComponent<ItemCarry>( ), "_itemData", options.ItemData, "아이템 데이터", results );
        if ( options.ChangeMaterialMap == true )
            Set( root.GetComponent<MaterialChangePresenter>( ), "_map", options.MaterialMap, "색 대체 목록", results );
        if ( options.ChangeAppearanceMap == true )
            Set( appearance, "_map", options.AppearanceMap, "외형 대체 목록", results );
    }

    /// <summary>
    /// 설정할 기능의 컴포넌트 중복 확인
    /// </summary>
    /// <param name="root">기능 관리 루트</param>
    /// <param name="results">제외 사유</param>
    /// <returns>설정할 컴포넌트가 중복되지 않았는지 여부</returns>
    public static bool CheckTarget ( GameObject root, List<string> results )
    {
        foreach ( Type type in _types )
        {
            if ( root.GetComponents( type ).Length > 1 )
            {
                results.Add( $"제외: {root.name} / {type.Name} 컴포넌트 중복" );
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 대상에 존재하는 기능의 선택한 설정값 확인
    /// </summary>
    /// <param name="root">설정할 기능 관리 루트</param>
    /// <param name="options">선택한 설정</param>
    /// <param name="results">문제 안내</param>
    /// <returns>선택값 사용 가능 여부</returns>
    public static bool CheckTargetOptions ( GameObject root, AnomalyPrefabOptions options, List<string> results )
    {
        bool presence = root.GetComponent<PresenceController>( ) != null;
        bool appearance = root.GetComponent<AppearanceChangeController>( ) != null;
        if ( ( presence == true && options.ChangePresenceMask == true && options.PresenceMask == 0 ) ||
            ( appearance == true && options.ChangeAppearanceMask == true && options.AppearanceMask == 0 ) ||
            ( root.GetComponent<OpenClosePlacement>( ) != null && options.ChangeFloorMask == true && options.FloorMask == 0 ) )
        {
            results.Add( $"제외: {root.name} / 해당 기능의 선택한 판정 마스크에 레이어가 없습니다." );
            return false;
        }
        if ( root.GetComponent<ItemCarry>( ) != null && options.ChangeItemData == true && options.ItemData == null )
        {
            results.Add( $"제외: {root.name} / 아이템 기능에 적용할 아이템 데이터가 없습니다." );
            return false;
        }
        if ( root.GetComponent<MaterialChangePresenter>( ) != null && options.ChangeMaterialMap == true && options.MaterialMap == null )
        {
            results.Add( $"제외: {root.name} / 색 대체 기능에 적용할 대체 목록이 없습니다." );
            return false;
        }
        if ( appearance == true && options.ChangeAppearanceMap == true && options.AppearanceMap == null )
        {
            results.Add( $"제외: {root.name} / 외형 대체 기능에 적용할 대체 목록이 없습니다." );
            return false;
        }
        if ( root.GetComponent<RotateView>( ) != null && options.ChangeAxis == true &&
            ( options.Axis.sqrMagnitude < 0.000001f || float.IsNaN( options.Axis.sqrMagnitude ) ||
                float.IsInfinity( options.Axis.sqrMagnitude ) ) )
        {
            results.Add( $"제외: {root.name} / 회전 축에 유효한 방향이 필요합니다." );
            return false;
        }
        if ( presence == true && options.ChangeRestoreTime == true &&
            ( float.IsNaN( options.RefillDuration ) || float.IsInfinity( options.RefillDuration ) ||
                float.IsNaN( options.CheckInterval ) || float.IsInfinity( options.CheckInterval ) ) )
        {
            results.Add( $"제외: {root.name} / 복구 시간에는 유한한 값이 필요합니다." );
            return false;
        }
        return true;
    }

}
