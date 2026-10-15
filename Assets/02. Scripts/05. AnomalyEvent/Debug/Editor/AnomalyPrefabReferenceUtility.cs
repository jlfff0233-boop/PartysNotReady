using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 변칙 참조 구성 - 전체 외형과 명확한 대표 컴포넌트 연결
/// </summary>
public static class AnomalyPrefabReferenceUtility
{
    /// <summary>
    /// 모든 메시 렌더러를 포함한 단일 직계 자식 조회
    /// </summary>
    /// <param name="root">기능 관리 루트</param>
    /// <returns>전체 외형 자식, 구조 보완이 필요하면 없음</returns>
    public static Transform FindVisual ( GameObject root )
    {
        Transform visual = null;
        foreach ( Renderer renderer in root.GetComponentsInChildren<Renderer>( true ) )
        {
            if ( renderer is MeshRenderer == false && renderer is SkinnedMeshRenderer == false )
                continue;
            Transform branch = renderer.transform;
            if ( branch == root.transform )
                return null;
            while ( branch.parent != root.transform )
                branch = branch.parent;
            if ( visual != null && visual != branch )
                return null;
            visual = branch;
        }
        return visual;
    }

    /// <summary>
    /// 범위 안의 유일한 비트리거 충돌체 조회
    /// </summary>
    /// <param name="root">충돌체 검색 범위</param>
    /// <param name="excluded">빈자리 조사 충돌체 등 제외할 대상</param>
    /// <returns>유일한 활성 충돌체 또는 없음</returns>
    static Collider FindCollider ( GameObject root, Collider excluded )
    {
        if ( root == null )
            return null;
        Collider found = null;
        foreach ( Collider collider in root.GetComponentsInChildren<Collider>( true ) )
        {
            if ( collider == excluded || collider.isTrigger == true )
                continue;
            if ( found != null || collider.enabled == false )
                return null;
            found = collider;
        }
        return found;
    }

    /// <summary>
    /// 활성 상태와 무관한 유일한 표시 충돌체 조회
    /// </summary>
    /// <param name="target">표시 범위</param>
    /// <param name="excluded">복구 조사 충돌체</param>
    /// <returns>유일한 비트리거 충돌체 또는 없음</returns>
    public static Collider FindPresenceCollider ( GameObject target, Collider excluded )
    {
        if ( target == null )
            return null;
        Collider found = null;
        foreach ( Collider collider in target.GetComponentsInChildren<Collider>( true ) )
        {
            if ( collider == excluded || collider.isTrigger == true )
                continue;
            if ( found != null )
                return null;
            found = collider;
        }
        return found;
    }

    /// <summary>
    /// 비어 있거나 다시 지정할 참조 연결
    /// </summary>
    /// <param name="owner">참조를 저장할 컴포넌트</param>
    /// <param name="field">직렬화 필드</param>
    /// <param name="candidate">명확한 후보</param>
    /// <param name="replace">기존 참조 교체 여부</param>
    /// <param name="results">설정 결과</param>
    static void Connect ( Component owner, string field, Object candidate, bool replace, List<string> results )
    {
        if ( owner == null )
            return;
        using SerializedObject serialized = new( owner );
        SerializedProperty property = serialized.FindProperty( field );
        Object current = property.objectReferenceValue;
        if ( current != null && replace == false )
        {
            results.Add( $"유지: {owner.GetType( ).Name}.{field} → {current.name}" );
            return;
        }
        if ( candidate == null )
        {
            results.Add( $"직접 지정 필요: {owner.GetType( ).Name}.{field} — 명확한 후보 없음, 기존 값 유지" );
            return;
        }
        if ( current == candidate )
            return;
        property.objectReferenceValue = candidate;
        serialized.ApplyModifiedPropertiesWithoutUndo( );
        results.Add( $"연결: {owner.GetType( ).Name}.{field} → {candidate.name}" );
    }

    /// <summary>
    /// 현재 존재 기능의 표시 대상과 조사 영역 연결
    /// </summary>
    /// <param name="root">기능 관리 루트</param>
    /// <param name="visual">전체 외형 후보</param>
    /// <param name="replace">기존 참조 교체 여부</param>
    /// <param name="results">설정 결과</param>
    /// <param name="entry">원본별 직접 지정 정보</param>
    static void ConnectPresence ( GameObject root, Transform visual, bool replace, List<string> results, AnomalyPrefabReferenceEntry entry )
    {
        PresenceDisplayController owner = root.GetComponent<PresenceDisplayController>( );
        if ( owner == null )
            return;
        Connect( owner, "_target", visual != null ? visual.gameObject : null, replace, results );
        using SerializedObject serialized = new( owner );
        GameObject target = serialized.FindProperty( "_target" ).objectReferenceValue as GameObject;
        Collider restore = serialized.FindProperty( "_restoreCheckCollider" ).objectReferenceValue as Collider;
        //루트 자체는 표시 대상으로 연결하지 않음
        Collider main = target != null && target != root && target.transform.IsChildOf( root.transform ) == true
            ? FindPresenceCollider( target, restore ) : null;
        if ( IsSelected( entry, AnomalyPrefabSelectedReferenceUtility.PresenceKey ) == false )
            Connect( owner, "_collider", main, replace, results );
        serialized.Update( );
        main = serialized.FindProperty( "_collider" ).objectReferenceValue as Collider;
        if ( IsSelected( entry, AnomalyPrefabSelectedReferenceUtility.RestoreKey ) == true )
            return;
        Collider restoreCandidate = null;
        if ( target != null && target != root )
        {
            foreach ( Collider collider in root.GetComponentsInChildren<Collider>( true ) )
            {
                if ( PresenceSettingsChecker.CheckRestoreConfig(
                    root.transform, target, main, collider, out _ ) == false )
                    continue;
                if ( restoreCandidate != null )
                {
                    restoreCandidate = null;
                    break;
                }
                restoreCandidate = collider;
            }
        }
        Connect( owner, "_restoreCheckCollider", restoreCandidate, replace, results );
    }


    /// <summary>
    /// 기존 기능의 명확한 참조 연결
    /// </summary>
    /// <param name="root">기능 관리 루트</param>
    /// <param name="replace">기존 참조 교체 여부</param>
    /// <param name="results">설정 결과</param>
    /// <param name="entry">원본별 직접 지정 정보, 없으면 기존 자동 연결 사용</param>
    public static void Process ( GameObject root, bool replace, List<string> results, AnomalyPrefabReferenceEntry entry = null )
    {
        Transform visual = FindVisual( root );
        if ( visual == null && ( root.GetComponent<RotateView>( ) != null ||
            root.GetComponent<ShakeView>( ) != null || root.GetComponent<PresenceDisplayController>( ) != null ) )
            results.Add( "구조 보완 필요: 물건 전체 외형을 포함한 별도 자식이 없습니다." );
        Connect( root.GetComponent<RotateView>( ), "_rotatingTarget", visual, replace, results );
        Connect( root.GetComponent<ShakeView>( ), "_shakingTarget", visual, replace, results );
        ConnectPresence( root, visual, replace, results, entry );
        PresenceDisplayController presence = root.GetComponent<PresenceDisplayController>( );
        Collider restore = null;
        if ( presence != null )
        {
            using SerializedObject serialized = new( presence );
            restore = serialized.FindProperty( "_restoreCheckCollider" ).objectReferenceValue as Collider;
        }
        if ( IsSelected( entry, AnomalyPrefabSelectedReferenceUtility.ItemKey ) == false )
            Connect( root.GetComponent<ItemWorldController>( ), "_collider", FindCollider( root, restore ), replace, results );
        Renderer single = null;
        foreach ( Renderer renderer in root.GetComponentsInChildren<Renderer>( true ) )
        {
            if ( renderer is MeshRenderer == false && renderer is SkinnedMeshRenderer == false )
                continue;
            if ( single != null )
            {
                single = null;
                break;
            }
            single = renderer;
        }
        //복수 머티리얼 슬롯은 색 변경 부분을 임의로 선택하지 않음
        MaterialChangeView color = root.GetComponent<MaterialChangeView>( );
        if ( IsSelected( entry, AnomalyPrefabSelectedReferenceUtility.ColorKey ) == false )
            Connect( color, "_target",
                single != null && single.sharedMaterials.Length == 1 ? single : null, replace, results );
        if ( IsSelected( entry, AnomalyPrefabSelectedReferenceUtility.ColorKey ) == false &&
            color != null && single != null && single.sharedMaterials.Length == 1 )
        {
            using SerializedObject serialized = new( color );
            if ( serialized.FindProperty( "_target" ).objectReferenceValue == single )
            {
                serialized.FindProperty( "_materialIndex" ).intValue = 0;
                if ( serialized.ApplyModifiedPropertiesWithoutUndo( ) == true )
                    results.Add( "설정: 색 변경의 유일한 머티리얼 슬롯 0" );
            }
        }
        OpenClosePlacement lid = root.GetComponent<OpenClosePlacement>( );
        if ( lid != null )
        {
            using SerializedObject serialized = new( lid );
            if ( serialized.FindProperty( "_lidPivot" ).objectReferenceValue == null )
                results.Add( "직접 지정 필요: 개폐 뚜껑 축" );
        }
    }

    /// <summary>
    /// 직접 지정하여 자동 연결에서 유지할 항목 확인
    /// </summary>
    /// <param name="entry">원본별 직접 지정 정보</param>
    /// <param name="key">참조 항목</param>
    /// <returns>직접 지정 여부</returns>
    static bool IsSelected ( AnomalyPrefabReferenceEntry entry, string key )
    {
        return entry != null && entry.Values.ContainsKey( key );
    }

}
