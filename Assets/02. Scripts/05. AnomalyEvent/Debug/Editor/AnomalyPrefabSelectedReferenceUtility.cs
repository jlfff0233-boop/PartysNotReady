using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 지정 참조 적용 - 원본 선택을 임시 편집 객체로 연결하고 최종 구성 확인
/// </summary>
public static class AnomalyPrefabSelectedReferenceUtility
{
    public const string ItemKey = "ItemWorldController._collider";		//아이템 대표 충돌체 항목
    public const string PresenceKey = "PresenceDisplayController._collider";		//존재 대표 충돌체 항목
    public const string RestoreKey = "PresenceDisplayController._restoreCheckCollider";		//조사 영역 항목
    public const string LidKey = "OpenClosePlacement._lidPivot";		//개폐 축 항목
    public const string ColorKey = "MaterialChangeView._target";		//색 변경 대상 항목

    /// <summary>
    /// 원본 컴포넌트의 식별자 조회
    /// </summary>
    /// <param name="value">선택한 원본 컴포넌트</param>
    /// <returns>원본 식별자 또는 빈 문자열</returns>
    public static string GetId ( Component value )
    {
        return value != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier( value, out string guid, out long id )
            ? guid + ":" + id : string.Empty;
    }

    /// <summary>
    /// 같은 이름과 같은 종류의 컴포넌트도 구별할 수 있는 경로 표시
    /// </summary>
    /// <param name="value">표시할 컴포넌트</param>
    /// <param name="root">원본 관리 루트</param>
    /// <returns>자식 경로와 컴포넌트 순번</returns>
    public static string Describe ( Component value, Transform root )
    {
        if ( value == null )
            return "미연결";
        Stack<string> names = new( );
        Transform current = value.transform;
        while ( current != root && current != null )
        {
            names.Push( current.name + "[" + current.GetSiblingIndex( ) + "]" );
            current = current.parent;
        }
        string path = current == root ? root.name : "원본 밖";
        if ( names.Count > 0 )
            path += "/" + string.Join( "/", names );
        int index = Array.IndexOf( value.GetComponents( value.GetType( ) ), value );
        return $"{path} / {value.GetType( ).Name}[{index}]";
    }

    /// <summary>
    /// 원본 선택의 식별자를 확인하고 동일한 임시 컴포넌트 조회
    /// </summary>
    /// <param name="asset">원본 루트</param>
    /// <param name="root">구성 전 임시 루트</param>
    /// <param name="entry">원본별 선택</param>
    /// <param name="key">참조 항목</param>
    /// <returns>대응하는 임시 컴포넌트 또는 없음</returns>
    static Component Resolve ( GameObject asset, GameObject root, AnomalyPrefabReferenceEntry entry, string key )
    {
        Component source = entry.Values[key];
        if ( source == null || entry.Ids.TryGetValue( key, out string id ) == false || GetId( source ) != id ||
            ( source.transform != asset.transform && source.transform.IsChildOf( asset.transform ) == false ) )
            return null;
        //이름 대신 형제 순서와 컴포넌트 순서로 원본 내부의 동일 객체를 찾음
        Stack<int> indices = new( );
        Transform current = source.transform;
        while ( current != asset.transform )
        {
            indices.Push( current.GetSiblingIndex( ) );
            current = current.parent;
        }
        Transform target = root.transform;
        foreach ( int index in indices )
        {
            if ( index >= target.childCount )
                return null;
            target = target.GetChild( index );
        }
        Type type = source.GetType( );
        int componentIndex = Array.IndexOf( source.GetComponents( type ), source );
        Component[] candidates = target.GetComponents( type );
        return componentIndex >= 0 && componentIndex < candidates.Length ? candidates[componentIndex] : null;
    }

    /// <summary>
    /// 직접 지정할 단일 참조 적용
    /// </summary>
    /// <param name="asset">원본 루트</param>
    /// <param name="root">임시 관리 루트</param>
    /// <param name="entry">선택 정보</param>
    /// <param name="key">참조 항목</param>
    /// <param name="ownerType">참조 소유 컴포넌트 형식</param>
    /// <param name="field">직렬화 필드</param>
    /// <param name="valueType">허용할 참조 형식</param>
    /// <param name="results">변경과 제외 사유</param>
    /// <returns>선택이 없거나 지정 성공 여부</returns>
    static bool Apply (
        GameObject asset, GameObject root, AnomalyPrefabReferenceEntry entry, string key,
        Type ownerType, string field, Type valueType, List<string> results )
    {
        if ( entry.Values.ContainsKey( key ) == false )
            return true;
        Component owner = root.GetComponent( ownerType );
        Component value = Resolve( asset, root, entry, key );
        if ( owner == null || value == null || valueType.IsInstanceOfType( value ) == false )
        {
            results.Add( "제외: 직접 지정 항목의 기능 또는 원본 후보가 변경됐습니다. 다시 선택하세요. " + key );
            return false;
        }
        using SerializedObject serialized = new( owner );
        SerializedProperty property = serialized.FindProperty( field );
        Component previous = property.objectReferenceValue as Component;
        results.Add( $"직접 지정: {key} / {Describe( previous, root.transform )} → {Describe( value, root.transform )}" );
        property.objectReferenceValue = value;
        if ( key == ColorKey )
        {
            SerializedProperty slot = serialized.FindProperty( "_materialIndex" );
            results.Add( $"직접 지정: 머티리얼 슬롯 {slot.intValue} → {entry.MaterialIndex}" );
            slot.intValue = entry.MaterialIndex;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo( );
        return true;
    }

    /// <summary>
    /// 외형 구성 전에 원본별 직접 지정 참조 적용
    /// </summary>
    /// <param name="path">원본 경로</param>
    /// <param name="root">구성 전 임시 루트</param>
    /// <param name="entry">원본별 선택, 없으면 자동 구성 사용</param>
    /// <param name="results">적용 결과</param>
    /// <returns>직접 지정 적용 성공 여부</returns>
    public static bool ApplySelected ( string path, GameObject root, AnomalyPrefabReferenceEntry entry, List<string> results )
    {
        if ( entry == null || entry.HasSelection == false )
            return true;
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>( path );
        if ( root.GetComponent<AnomalyTarget>( ) == null )
        {
            results.Add( "제외: 직접 지정은 원본 루트의 변칙 기능에 적용합니다." );
            return false;
        }
        return Apply( asset, root, entry, ItemKey, typeof( ItemWorldController ), "_collider", typeof( Collider ), results ) &&
            Apply( asset, root, entry, PresenceKey, typeof( PresenceDisplayController ), "_collider", typeof( Collider ), results ) &&
            Apply( asset, root, entry, RestoreKey, typeof( PresenceDisplayController ), "_restoreCheckCollider", typeof( Collider ), results ) &&
            Apply( asset, root, entry, LidKey, typeof( OpenClosePlacement ), "_lidPivot", typeof( Transform ), results ) &&
            Apply( asset, root, entry, ColorKey, typeof( MaterialChangeView ), "_target", typeof( Renderer ), results );
    }


    /// <summary>
    /// 존재하는 기능의 미연결 참조 수집
    /// </summary>
    /// <param name="owner">참조 소유 컴포넌트</param>
    /// <param name="field">직렬화 필드</param>
    /// <param name="label">누락 항목 이름</param>
    /// <param name="pending">누락 항목 목록</param>
    static void AddPending ( Component owner, string field, string label, List<string> pending )
    {
        if ( owner == null )
            return;
        using SerializedObject serialized = new( owner );
        if ( serialized.FindProperty( field ).objectReferenceValue == null )
            pending.Add( label );
    }

    /// <summary>
    /// 원본 저장 가능 여부와 별도로 남은 참조 설정 표시
    /// </summary>
    /// <param name="root">최종 구성된 기능 관리 루트</param>
    /// <param name="results">누락 안내 목록</param>
    public static void ReportPending ( GameObject root, List<string> results )
    {
        List<string> pending = new( );
        PresenceDisplayController presence = root.GetComponent<PresenceDisplayController>( );
        AddPending( root.GetComponent<ItemWorldController>( ), "_collider", "아이템 대표 충돌체", pending );
        AddPending( root.GetComponent<ItemCarry>( ), "_itemData", "아이템 데이터", pending );
        AddPending( root.GetComponent<RotateView>( ), "_rotatingTarget", "회전 대상", pending );
        AddPending( root.GetComponent<ShakeView>( ), "_shakingTarget", "흔들림 대상", pending );
        AddPending( presence, "_target", "존재 표시 대상", pending );
        AddPending( presence, "_collider", "존재 대표 충돌체", pending );
        AddPending( presence, "_restoreCheckCollider", "빈자리 조사 영역", pending );
        AddPending( root.GetComponent<OpenClosePlacement>( ), "_lidPivot", "개폐 축", pending );
        AddPending( root.GetComponent<MaterialChangeView>( ), "_target", "색 변경 대상", pending );
        AddPending( root.GetComponent<MaterialChangePresenter>( ), "_map", "색 대체 목록", pending );
        AddPending( root.GetComponent<AppearanceChangeController>( ), "_map", "외형 대체 목록", pending );
        results.Add( pending.Count == 0
            ? $"{root.name}: 확인 범위의 참조 누락 없음 / 판정값과 실제 동작은 별도 확인"
            : $"{root.name}: 저장 가능하지만 참조 설정 미완료 {pending.Count}개 / {string.Join( ", ", pending )}" );
    }

    /// <summary>
    /// 최종 구성에서 지정한 참조의 사용 조건 확인
    /// </summary>
    /// <param name="root">구성이 끝난 임시 원본</param>
    /// <param name="entry">직접 지정 항목</param>
    /// <param name="results">제외 사유</param>
    /// <returns>직접 지정한 참조의 사용 가능 여부</returns>
    public static bool CheckSelected ( GameObject root, AnomalyPrefabReferenceEntry entry, List<string> results )
    {
        if ( entry == null || entry.HasSelection == false )
            return true;
        if ( entry.Values.ContainsKey( ItemKey ) )
        {
            using SerializedObject item = new( root.GetComponent<ItemWorldController>( ) );
            Collider collider = item.FindProperty( "_collider" ).objectReferenceValue as Collider;
            if ( collider == null || collider.isTrigger == true ||
                ( collider.transform != root.transform && collider.transform.IsChildOf( root.transform ) == false ) )
            {
                results.Add( "제외: 아이템 대표 충돌체는 원본 내부의 비트리거 충돌체여야 합니다." );
                return false;
            }
        }
        if ( entry.Values.ContainsKey( PresenceKey ) || entry.Values.ContainsKey( RestoreKey ) )
        {
            using SerializedObject presence = new( root.GetComponent<PresenceDisplayController>( ) );
            GameObject target = presence.FindProperty( "_target" ).objectReferenceValue as GameObject;
            Collider main = presence.FindProperty( "_collider" ).objectReferenceValue as Collider;
            Collider restore = presence.FindProperty( "_restoreCheckCollider" ).objectReferenceValue as Collider;
            if ( PresenceSettingsChecker.CheckDisplayConfig( root.transform, target, main, out string reason ) == false ||
                ( entry.Values.ContainsKey( RestoreKey ) && PresenceSettingsChecker.CheckRestoreConfig( root.transform, target, main, restore, out reason ) == false ) )
            {
                results.Add( "제외: " + reason );
                return false;
            }
        }
        if ( entry.Values.ContainsKey( LidKey ) )
        {
            using SerializedObject lid = new( root.GetComponent<OpenClosePlacement>( ) );
            Transform pivot = lid.FindProperty( "_lidPivot" ).objectReferenceValue as Transform;
            if ( pivot == null || pivot == root.transform || pivot.IsChildOf( root.transform ) == false )
            {
                results.Add( "제외: 개폐 축은 관리 루트 자신이 아닌 내부 자식이어야 합니다." );
                return false;
            }
        }
        if ( entry.Values.ContainsKey( ColorKey ) )
        {
            using SerializedObject color = new( root.GetComponent<MaterialChangeView>( ) );
            Renderer renderer = color.FindProperty( "_target" ).objectReferenceValue as Renderer;
            int slot = color.FindProperty( "_materialIndex" ).intValue;
            if ( renderer == null || slot < 0 || slot >= renderer.sharedMaterials.Length || renderer.sharedMaterials[slot] == null )
            {
                results.Add( "제외: 색 변경 대상에 유효한 머티리얼 슬롯을 선택하세요." );
                return false;
            }
        }
        return true;
    }

}
