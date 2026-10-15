using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 변칙 대상 설정 조회 - 대상 수집, 편집 항목과 적용 범위 확인
/// </summary>
public static class AnomalyTargetSettingsUtility
{

    #region ----- 대상 수집 -----

    /// <summary>
    /// 열린 씬의 변칙 대상 수집, 비활성 대상도 포함
    /// </summary>
    /// <param name="targets">조회 결과를 받을 목록</param>
    public static void CollectSceneTargets ( List<AnomalyTarget> targets )
    {
        targets.Clear( );
        foreach ( AnomalyTarget target in Resources.FindObjectsOfTypeAll<AnomalyTarget>( ) )
        {
            //비활성 대상은 포함하되 에셋과 프리팹 편집용 미리보기 씬은 제외
            if ( EditorUtility.IsPersistent( target ) ||
                 target.gameObject.scene.IsValid( ) == false ||
                 target.gameObject.scene.isLoaded == false ||
                 EditorSceneManager.IsPreviewScene( target.gameObject.scene ) )
                continue;

            targets.Add( target );
        }
    }

    /// <summary>
    /// 지정 폴더의 일반 프리팹과 내부 변칙 대상 수집
    /// </summary>
    /// <param name="folder">Assets 내부 검색 폴더</param>
    /// <param name="prefabs">미설정 원본도 포함할 목록</param>
    /// <param name="targets">조회 결과를 받을 목록</param>
    public static void CollectPrefabTargets (
        string folder, List<AnomalyPrefabEntry> prefabs, List<AnomalyTarget> targets )
    {
        prefabs.Clear( );
        targets.Clear( );
        if ( AssetDatabase.IsValidFolder( folder ) == false ||
             ( folder != "Assets" && folder.StartsWith( "Assets/", StringComparison.Ordinal ) == false ) )
            return;

        //프로젝트 전체를 매 프레임 검색하지 않고 명시적 새로고침 때만 조회
        foreach ( string guid in AssetDatabase.FindAssets( "t:Prefab", new[] { folder } ) )
        {
            string path = AssetDatabase.GUIDToAssetPath( guid );
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>( path );
            //직접 설정을 편집할 수 없는 모델 프리팹은 제외
            if ( root == null || PrefabUtility.GetPrefabAssetType( root ) == PrefabAssetType.Model )
                continue;

            AnomalyTarget[] children = root.GetComponentsInChildren<AnomalyTarget>( true );
            List<string> features = new( );
            foreach ( MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>( true ) )
            {
                string feature = GetFeatureName( component );
                if ( feature != null && features.Contains( feature ) == false )
                    features.Add( feature );
            }
            prefabs.Add( new AnomalyPrefabEntry( root, path, children,
                features.Count > 0 ? string.Join( " / ", features ) : "없음" ) );
            targets.AddRange( children );
        }
        prefabs.Sort( ( a, b ) => string.Compare( a.Path, b.Path, StringComparison.OrdinalIgnoreCase ) );
    }

    /// <summary>
    /// 구성 컴포넌트의 기능 표시 이름 조회
    /// </summary>
    /// <param name="component">조회할 컴포넌트</param>
    /// <returns>기능 이름 또는 해당 없음</returns>
    static string GetFeatureName ( MonoBehaviour component )
    {
        return component switch
        {
            ItemCarry => "운반·낙하·이동·교체",
            OpenCloseController => "개폐",
            RotatePresenter => "회전",
            ShakePresenter => "흔들림",
            PresenceController => "추가·제거",
            MaterialChangePresenter => "색 대체",
            AppearanceChangeController => "외형 대체",
            _ => null
        };
    }

    /// <summary>
    /// 대상에 직접 구성된 기능 요약
    /// </summary>
    /// <param name="target">조회할 대상 오브젝트</param>
    /// <returns>구성된 기능 이름</returns>
    public static string GetFeatures ( GameObject target )
    {
        List<string> features = new( );
        foreach ( MonoBehaviour component in target.GetComponents<MonoBehaviour>( ) )
        {
            string feature = GetFeatureName( component );
            if ( feature != null )
                features.Add( feature );
        }
        return features.Count > 0 ? string.Join( " / ", features ) : "없음";
    }

    /// <summary>
    /// 같은 이름의 대상을 구분할 씬 또는 에셋 경로와 계층 경로 조회
    /// </summary>
    /// <param name="target">조회할 대상</param>
    public static string GetTargetPath ( AnomalyTarget target )
    {
        string hierarchy = target.name;
        Transform parent = target.transform.parent;
        while ( parent != null )
        {
            hierarchy = parent.name + "/" + hierarchy;
            parent = parent.parent;
        }

        string source = EditorUtility.IsPersistent( target )
            ? AssetDatabase.GetAssetPath( target ) : target.gameObject.scene.name;
        return source + " / " + hierarchy;
    }

    #endregion

    #region ----- 설정 항목 -----

    /// <summary>
    /// 대상에 실제로 붙어 있는 스크립트 형식 수집
    /// </summary>
    /// <param name="targets">조회할 대상 목록</param>
    /// <param name="types">중복 없이 정렬한 형식을 받을 목록</param>
    public static void CollectTypes ( List<AnomalyTarget> targets, List<Type> types )
    {
        types.Clear( );
        foreach ( AnomalyTarget target in targets )
        {
            if ( target == null )
                continue;
            foreach ( MonoBehaviour component in target.GetComponents<MonoBehaviour>( ) )
            {
                if ( component != null && types.Contains( component.GetType( ) ) == false )
                    types.Add( component.GetType( ) );
            }
        }
        types.Sort( ( a, b ) => string.Compare( a.Name, b.Name, StringComparison.Ordinal ) );
    }

    /// <summary>
    /// 컴포넌트의 사용자 직렬화 항목 이름 수집
    /// </summary>
    /// <param name="component">설정을 조회할 컴포넌트</param>
    /// <param name="paths">항목 경로를 받을 목록</param>
    /// <param name="labels">표시 이름을 받을 목록</param>
    public static void CollectProperties (
        Component component, List<string> paths, List<string> labels )
    {
        paths.Clear( );
        labels.Clear( );
        if ( component == null )
            return;

        using ( SerializedObject serialized = new( component ) )
        {
            SerializedProperty property = serialized.GetIterator( );
            bool enterChildren = true;
            while ( property.NextVisible( enterChildren ) )
            {
                //최상위 항목만 선택 목록에 넣고 배열 / 구조체 내부는 상세 편집에서 처리
                enterChildren = false;
                if ( property.name.StartsWith( "m_", StringComparison.Ordinal ) )
                    continue;
                paths.Add( property.propertyPath );
                labels.Add( property.displayName );
            }
        }
    }


    /// <summary>
    /// 직렬화 값 하나를 비교용 문자열로 변환
    /// </summary>
    /// <param name="property">표시할 직렬화 값</param>
    static string GetValueText ( SerializedProperty property )
    {
        return property.propertyType switch
        {
            SerializedPropertyType.Boolean => property.boolValue.ToString( ),
            SerializedPropertyType.Integer => property.longValue.ToString( ),
            SerializedPropertyType.Float => property.doubleValue.ToString( "0.###" ),
            SerializedPropertyType.String => property.stringValue,
            SerializedPropertyType.Enum => property.enumValueIndex >= 0 && property.enumValueIndex < property.enumDisplayNames.Length
                ? property.enumDisplayNames[property.enumValueIndex] : property.intValue.ToString( ),
            SerializedPropertyType.ObjectReference => property.objectReferenceValue != null ? property.objectReferenceValue.name : "미할당",
            SerializedPropertyType.Vector3 => property.vector3Value.ToString( "F2" ),
            SerializedPropertyType.Vector2 => property.vector2Value.ToString( "F2" ),
            SerializedPropertyType.Color => property.colorValue.ToString( ),
            _ => property.displayName
        };
    }

    /// <summary>
    /// 배열과 복합 설정의 실제 값을 목록 비교용으로 요약
    /// </summary>
    /// <param name="property">요약할 직렬화 항목</param>
    public static string GetSummary ( SerializedProperty property )
    {
        if ( property.propertyType == SerializedPropertyType.String )
            return property.stringValue;
        List<string> values = new( );
        SerializedProperty cursor = property.Copy( );
        int depth = property.depth;
        while ( cursor.NextVisible( true ) && cursor.depth > depth )
        {
            if ( cursor.propertyType == SerializedPropertyType.Generic )
                continue;
            //목록 행이 지나치게 길어지지 않도록 실제 값 12개까지만 요약
            if ( values.Count == 12 )
            {
                values.Add( "… 상세에서 나머지 확인" );
                break;
            }
            values.Add( $"{cursor.displayName}: {GetValueText( cursor )}" );
        }
        return values.Count > 0 ? string.Join( " / ", values ) : "빈 설정";
    }

    #endregion

    #region ----- 편집 조건 검사 -----

    /// <summary>
    /// 목록에서 직접 편집할 수 있는 단일 값인지 확인
    /// </summary>
    /// <param name="property">검사할 직렬화 항목</param>
    public static bool IsSimple ( SerializedProperty property )
    {
        return property.propertyType != SerializedPropertyType.Generic &&
               ( property.isArray == false || property.propertyType == SerializedPropertyType.String );
    }

    /// <summary>
    /// 현재 모드에서 선택한 설정을 편집할 수 있는지 확인
    /// </summary>
    /// <param name="component">설정을 소유한 컴포넌트</param>
    /// <param name="path">편집할 항목 경로</param>
    public static bool CanEdit ( Component component, string path )
    {
        if ( component == null )
            return false;
        if ( EditorApplication.isPlayingOrWillChangePlaymode == false )
            return true;

        //대체 가중치는 매 요청마다 읽으므로 다음 발생 요청부터 반영
        //초기화 시 모델에 복사하는 값은 재초기화 API 없이 임시 수정하지 않음
        return Application.isPlaying && EditorUtility.IsPersistent( component ) == false &&
               component is ReplaceController && path == "_types";
    }

    #endregion

}
