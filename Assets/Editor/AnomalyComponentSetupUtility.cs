using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 변칙 기능 구성 종류
/// </summary>
public enum AnomalySetupKind
{
    OpenClose,      //개폐
    Rotate,         //회전
    Shake,          //흔들림
    Presence,       //추가와 제거
    Drop,           //낙하
    Displace,       //이동
    Swap,           //교체
    Material,       //색 대체
    Appearance      //외형 대체
}

/// <summary>
/// 변칙 컴포넌트 구성 - 의존 기능 추가와 명확한 참조 연결
/// </summary>
public static class AnomalyComponentSetupUtility
{
    /// <summary>
    /// 선택 계층의 존재 구성 변경 예정 조회
    /// </summary>
    [MenuItem( "Tools/MapChecks/Presence/InspectSelection" )]
    static void CheckSelectedPresence ()
    {
        ProcessSelectedPresence( false );
    }

    /// <summary>
    /// 선택 계층의 명확한 존재 구성 오류 보완
    /// </summary>
    [MenuItem( "Tools/MapChecks/Presence/RepairSelection" )]
    static void RepairSelectedPresence ()
    {
        ProcessSelectedPresence( true );
    }

    /// <summary>
    /// 선택 계층의 존재 구성을 중복 없이 처리
    /// </summary>
    /// <param name="apply">실제 보완 여부</param>
    static void ProcessSelectedPresence ( bool apply )
    {
        if ( EditorApplication.isPlayingOrWillChangePlaymode == true )
        {
            Debug.LogWarning( "존재 구성 점검과 보완은 편집 모드에서 실행하세요." );
            return;
        }
        HashSet<PresenceDisplayController> targets = new( );
        foreach ( GameObject selected in Selection.gameObjects )
        {
            if ( EditorUtility.IsPersistent( selected ) == true ||
                 selected.scene.IsValid( ) == false ||
                 PrefabUtility.IsPartOfImmutablePrefab( selected ) == true )
                continue;
            foreach ( PresenceDisplayController target in selected.GetComponentsInChildren<PresenceDisplayController>( true ) )
                targets.Add( target );
        }
        if ( targets.Count == 0 )
        {
            Debug.LogWarning( "Hierarchy에서 존재 기능이 포함된 맵 또는 객체를 선택하세요." );
            return;
        }
        int group = -1;
        if ( apply == true )
        {
            Undo.IncrementCurrentGroup( );
            group = Undo.GetCurrentGroup( );
            Undo.SetCurrentGroupName( "존재 구성 일괄 보완" );
        }
        try
        {
            foreach ( PresenceDisplayController owner in targets )
            {
                if ( PrefabUtility.IsPartOfImmutablePrefab( owner ) == true )
                    continue;
                List<string> results = new( );
                RepairPresence( owner, apply, results );
                Debug.Log( $"[존재 구성 {( apply ? "보완" : "점검" )}] {owner.name}\n{string.Join( "\n", results )}", owner );
            }
        }
        finally
        {
            if ( apply == true )
                Undo.CollapseUndoOperations( group );
        }
    }

    /// <summary>
    /// 유효한 참조를 보존하며 표시 범위와 대표 충돌체 보완
    /// </summary>
    /// <param name="owner">존재 표시 컴포넌트</param>
    /// <param name="apply">실제 보완 여부</param>
    /// <param name="results">변경 예정 또는 처리 결과</param>
    static void RepairPresence ( PresenceDisplayController owner, bool apply, List<string> results )
    {
        if ( owner.GetComponents<PresenceDisplayController>( ).Length != 1 )
        {
            results.Add( "보완 제외: 동일 객체에 존재 표시 컴포넌트가 중복되어 있습니다." );
            return;
        }
        using SerializedObject serialized = new( owner );
        SerializedProperty targetProperty = serialized.FindProperty( "_target" );
        SerializedProperty colliderProperty = serialized.FindProperty( "_collider" );
        GameObject currentTarget = targetProperty.objectReferenceValue as GameObject;
        Collider currentCollider = colliderProperty.objectReferenceValue as Collider;
        Collider restore = serialized.FindProperty( "_restoreCheckCollider" ).objectReferenceValue as Collider;
        GameObject target = currentTarget;
        Collider collider = currentCollider;
        if ( PresenceSettingsChecker.CheckDisplayConfig( owner.transform, target, collider, out _ ) == false )
        {
            //기존 표시 범위가 유효하면 누락된 충돌체부터 조회
            if ( target != null && target != owner.gameObject && target.transform.IsChildOf( owner.transform ) == true &&
                 collider == null )
                collider = AnomalyPrefabReferenceUtility.FindPresenceCollider( target, restore );
            if ( PresenceSettingsChecker.CheckDisplayConfig( owner.transform, target, collider, out _ ) == false )
            {
                Transform visual = AnomalyPrefabReferenceUtility.FindVisual( owner.gameObject );
                target = visual != null ? visual.gameObject : null;
                //전체 외형 안의 기존 대표 충돌체는 유지
                if ( collider == null || collider == restore || collider.isTrigger == true || target == null ||
                     ( collider.gameObject != target && collider.transform.IsChildOf( target.transform ) == false ) )
                    collider = AnomalyPrefabReferenceUtility.FindPresenceCollider( target, restore );
            }
        }
        if ( PresenceSettingsChecker.CheckDisplayConfig( owner.transform, target, collider, out string reason ) == false ||
             collider == restore )
        {
            results.Add( $"보완 제외: {( collider != null && collider == restore ? "대표 충돌체와 복구 조사 영역이 같습니다." : reason )}" );
            return;
        }
        if ( restore != null && PresenceSettingsChecker.CheckRestoreConfig(
            owner.transform, target, collider, restore, out reason ) == false )
        {
            results.Add( $"보완 제외: {reason}" );
            return;
        }
        AppearanceComponents appearance = target.GetComponent<AppearanceComponents>( );
        if ( appearance != null && appearance.MainCollider != collider )
        {
            results.Add( "보완 제외: 초기 외형과 존재 기능의 대표 충돌체가 다릅니다." );
            return;
        }
        bool referencesChanged = target != currentTarget || collider != currentCollider;
        bool colliderChanged = collider.enabled == false;
        if ( referencesChanged == false && colliderChanged == false )
        {
            results.Add( "정상: 기존 참조와 활성 상태 유지" );
            return;
        }
        if ( target != currentTarget )
            results.Add( $"Target: {( currentTarget != null ? currentTarget.name : "미할당" )} → {target.name}" );
        if ( collider != currentCollider )
            results.Add( $"Collider: {( currentCollider != null ? currentCollider.name : "미할당" )} → {collider.name}" );
        if ( colliderChanged == true )
            results.Add( $"Collider Enabled: {collider.name} False → True" );
        if ( apply == false )
            return;
        if ( referencesChanged == true )
        {
            targetProperty.objectReferenceValue = target;
            colliderProperty.objectReferenceValue = collider;
            serialized.ApplyModifiedProperties( );
            PrefabUtility.RecordPrefabInstancePropertyModifications( owner );
        }
        if ( colliderChanged == true )
        {
            Undo.RecordObject( collider, "존재 충돌체 활성화" );
            collider.enabled = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications( collider );
        }
        EditorSceneManager.MarkSceneDirty( owner.gameObject.scene );
    }

    /// <summary>
    /// 기능 묶음의 화면 표시 이름 조회
    /// </summary>
    /// <param name="kind">조회할 기능 묶음</param>
    /// <returns>한글 기능 이름</returns>
    public static string GetKindName ( AnomalySetupKind kind )
    {
        return kind switch
        {
            AnomalySetupKind.OpenClose => "개폐",
            AnomalySetupKind.Rotate => "회전",
            AnomalySetupKind.Shake => "흔들림",
            AnomalySetupKind.Presence => "추가 / 제거",
            AnomalySetupKind.Drop => "낙하",
            AnomalySetupKind.Displace => "이동",
            AnomalySetupKind.Swap => "교체",
            AnomalySetupKind.Material => "색 대체",
            AnomalySetupKind.Appearance => "외형 대체",
            _ => "해당 없음"
        };
    }

    /// <summary>
    /// 열거 순서의 기능 묶음 표시 이름 조회
    /// </summary>
    /// <returns>한글 기능 이름 목록</returns>
    public static string[] GetKindNames ()
    {
        return Array.ConvertAll( (AnomalySetupKind[])Enum.GetValues( typeof( AnomalySetupKind ) ), GetKindName );
    }

    /// <summary>
    /// 설정을 변경하지 않고 기능의 필수 컴포넌트 조회
    /// </summary>
    /// <param name="kind">검사할 기능</param>
    /// <returns>필수 컴포넌트 형식 목록</returns>
    public static Type[] GetRequiredTypes ( AnomalySetupKind kind )
    {
        return GetTypes( kind );
    }

    /// <summary>
    /// 기능별 필요한 컴포넌트 묶음
    /// </summary>
    /// <param name="kind">구성할 기능</param>
    /// <returns>의존 순서의 형식 목록</returns>
    static Type[] GetTypes ( AnomalySetupKind kind )
    {
        Type[] feature = kind switch
        {
            AnomalySetupKind.OpenClose => new[] { typeof( OpenClosePlacement ), typeof( OpenCloseController ) },
            AnomalySetupKind.Rotate => new[] { typeof( RotateView ), typeof( RotatePresenter ) },
            AnomalySetupKind.Shake => new[] { typeof( ShakeView ), typeof( ShakePresenter ) },
            AnomalySetupKind.Presence => new[] { typeof( PresenceDisplayController ), typeof( PresenceController ) },
            AnomalySetupKind.Drop or AnomalySetupKind.Displace or AnomalySetupKind.Swap =>
                new[] { typeof( Rigidbody ), typeof( ItemWorldController ), typeof( ItemCarry ) },
            AnomalySetupKind.Material => new[] { typeof( MaterialChangeView ), typeof( MaterialChangePresenter ), typeof( ReplaceController ) },
            AnomalySetupKind.Appearance => new[] { typeof( Rigidbody ), typeof( ItemWorldController ), typeof( AppearanceChangeController ), typeof( ReplaceController ) },
            _ => Array.Empty<Type>( )
        };
        List<Type> types = new( ) { typeof( AnomalyTarget ) };
        types.AddRange( feature );
        return types.ToArray( );
    }

    /// <summary>
    /// 같은 대상의 단일 컴포넌트 찾기
    /// </summary>
    /// <typeparam name="T">참조 형식</typeparam>
    /// <param name="target">조회 대상</param>
    /// <returns>유일한 컴포넌트 또는 없음</returns>
    static T FindSingle<T> ( GameObject target ) where T : Component
    {
        T[] values = target.GetComponents<T>( );
        return values.Length == 1 ? values [ 0 ] : null;
    }

    /// <summary>
    /// 설정이 비어 있을 때만 후보 연결 또는 결과 표시
    /// </summary>
    /// <param name="component">설정 소유자</param>
    /// <param name="field">직렬화 필드</param>
    /// <param name="candidate">명확한 단일 후보</param>
    /// <param name="apply">실제 적용 여부</param>
    /// <param name="results">결과 목록</param>
    /// <param name="useUndo">실행 취소 기록 여부</param>
    static void Connect (
        Component component, string field, UnityEngine.Object candidate,
        bool apply, List<string> results, bool useUndo )
    {
        if ( component == null )
            return;
        using SerializedObject serialized = new( component );
        SerializedProperty property = serialized.FindProperty( field );
        if ( property.objectReferenceValue != null )
        {
            results.Add( $"유지: {component.GetType( ).Name}.{field}" );
            return;
        }
        if ( candidate == null )
        {
            results.Add( $"미연결: {component.GetType( ).Name}.{field} — 직접 지정 필요" );
            return;
        }
        results.Add( $"{( apply ? "연결" : "연결 예정" )}: {component.GetType( ).Name}.{field} → {candidate.name}" );
        if ( apply == false )
            return;
        property.objectReferenceValue = candidate;
        if ( useUndo == true )
            serialized.ApplyModifiedProperties( );
        else
            serialized.ApplyModifiedPropertiesWithoutUndo( );
    }

    /// <summary>
    /// 외형과 같은 대상 참조 연결
    /// </summary>
    /// <param name="target">설정 대상</param>
    /// <param name="kind">선택 기능</param>
    /// <param name="apply">실제 적용 여부</param>
    /// <param name="results">결과 목록</param>
    /// <param name="useUndo">실행 취소 기록 여부</param>
    static void ConnectReferences (
        GameObject target, AnomalySetupKind kind, bool apply, List<string> results, bool useUndo )
    {
        Transform child = AnomalyPrefabReferenceUtility.FindVisual( target );
        switch ( kind )
        {
            case AnomalySetupKind.Rotate:
                Connect( target.GetComponent<RotateView>( ), "_rotatingTarget", child, apply, results, useUndo );
                break;
            case AnomalySetupKind.Shake:
                Connect( target.GetComponent<ShakeView>( ), "_shakingTarget", child, apply, results, useUndo );
                break;
            case AnomalySetupKind.OpenClose:
                //뚜껑은 자식이 하나여도 의미를 추정하지 않음
                Connect( target.GetComponent<OpenClosePlacement>( ), "_lidPivot", null, apply, results, useUndo );
                results.Add( "수동 설정: Floor Mask / 뚜껑 Pivot" );
                break;
            case AnomalySetupKind.Presence:
                PresenceDisplayController presence = target.GetComponent<PresenceDisplayController>( );
                Connect( presence, "_target", child != null ? child.gameObject : null, apply, results, useUndo );
                //유지된 표시 대상의 Collider를 기준으로 연결
                if ( presence != null )
                {
                    using SerializedObject serialized = new( presence );
                    GameObject visual = serialized.FindProperty( "_target" ).objectReferenceValue as GameObject;
                    if ( visual == null && apply == false && child != null )
                        visual = child.gameObject;
                    Collider restore = serialized.FindProperty( "_restoreCheckCollider" ).objectReferenceValue as Collider;
                    Connect( presence, "_collider", AnomalyPrefabReferenceUtility.FindPresenceCollider( visual, restore ), apply, results, useUndo );
                }
                Connect( presence, "_restoreCheckCollider", null, apply, results, useUndo );
                results.Add( "수동 설정: Initially Present / Obstacle Mask / 별도 복구 조사 영역" );
                break;
            case AnomalySetupKind.Material:
                Connect( target.GetComponent<MaterialChangeView>( ), "_target", FindSingle<Renderer>( target ), apply, results, useUndo );
                Connect( target.GetComponent<MaterialChangePresenter>( ), "_map", null, apply, results, useUndo );
                results.Add( "수동 설정: ReplaceController.Types / Material Index" );
                break;
            case AnomalySetupKind.Appearance:
                Connect( target.GetComponent<AppearanceChangeController>( ), "_map", null, apply, results, useUndo );
                Connect( target.GetComponent<AppearanceChangeController>( ), "_initialData", null, apply, results, useUndo );
                Connect( target.GetComponent<AppearanceChangeController>( ), "_appearanceRoot", null, apply, results, useUndo );
                results.Add( "수동 설정: ReplaceController.Types / 외형 원본의 AppearanceComponents / Obstacle Mask" );
                Connect( target.GetComponent<ItemWorldController>( ), "_collider", FindSingle<Collider>( target ), apply, results, useUndo );
                break;
            case AnomalySetupKind.Drop:
            case AnomalySetupKind.Displace:
            case AnomalySetupKind.Swap:
                Connect( target.GetComponent<ItemWorldController>( ), "_collider", FindSingle<Collider>( target ), apply, results, useUndo );
                Connect( target.GetComponent<ItemCarry>( ), "_itemData", null, apply, results, useUndo );
                results.Add( $"수동 설정: 배치 대상 / Collider 형상 / {GetKindName( kind )} 변칙 사건 관리자" );
                if ( kind == AnomalySetupKind.Swap )
                    results.Add( "ItemSwapper는 일반 클래스이므로 부착하지 않음" );
                break;
        }
    }

    /// <summary>
    /// 대상 구성 조회 또는 적용
    /// </summary>
    /// <param name="target">구성할 오브젝트</param>
    /// <param name="kind">선택 기능</param>
    /// <param name="apply">실제 적용 여부</param>
    /// <param name="results">처리 결과</param>
    /// <param name="useUndo">씬과 프리팹 편집 모드의 실행 취소 기록 여부</param>
    /// <param name="connectReferences">개별 구성의 기본 참조 연결 여부</param>
    /// <returns>대상 구성 가능 또는 적용 성공 여부</returns>
    public static bool Process (
        GameObject target, AnomalySetupKind kind, bool apply, List<string> results,
        bool useUndo = true, bool connectReferences = true )
    {
        results.Add( $"[{target.name}] {GetKindName( kind )}" );
        if ( EditorUtility.IsPersistent( target ) || PrefabUtility.IsPartOfImmutablePrefab( target ) )
        {
            results.Add( "원본 구성은 Prefab Mode에서 실행하세요. 모델 프리팹은 구성할 수 없습니다." );
            return false;
        }
        Type[] types = GetTypes( kind );
        if ( apply == false )
        {
            Transform visual = AnomalyPrefabReferenceUtility.FindVisual( target );
            string child = visual != null ? visual.name : "없음 또는 복수 후보: 구조 보완 필요";
            results.Add( $"참조 후보: 전체 외형 자식 {child}. 기존 참조가 있으면 그대로 유지" );
            results.Add( "새 컴포넌트의 SO / 외형 부모 / 뚜껑 / 조사 영역은 자동 생성하지 않습니다. 적용 후 미연결 항목을 확인하세요." );
        }
        foreach ( Type type in types )
        {
            if ( target.GetComponents( type ).Length > 1 )
            {
                results.Add( $"중복 {type.Name}: 대상 적용 제외" );
                return false;
            }
        }
        foreach ( Type type in types )
        {
            if ( target.GetComponent( type ) != null )
                results.Add( $"재사용: {type.Name}" );
            else
            {
                results.Add( $"{( apply ? "추가" : "추가 예정" )}: {type.Name}" );
                if ( apply )
                {
                    if ( useUndo == true )
                        Undo.AddComponent( target, type );
                    else
                        target.AddComponent( type );
                }
            }
        }
        if ( connectReferences == true )
            ConnectReferences( target, kind, apply, results, useUndo );
        if ( apply )
            EditorSceneManager.MarkSceneDirty( target.scene );
        return true;
    }
}
