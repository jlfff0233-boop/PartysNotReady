using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static AnomalyPrefabSelectedReferenceUtility;

/// <summary>
/// 원본 참조 선택 화면 - 내부 후보 조회와 원본별 직접 지정
/// </summary>
public class AnomalyPrefabReferencePanel
{
    /// <summary>
    /// 원본 경로별 직접 지정 항목
    /// </summary>
    readonly Dictionary<string, AnomalyPrefabReferenceEntry> _entries = new( );

    /// <summary>
    /// 직접 지정한 원본 유무
    /// </summary>
    public bool HasSelection
    {
        get
        {
            foreach ( AnomalyPrefabReferenceEntry entry in _entries.Values )
            {
                if ( entry.HasSelection == true )
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// 모든 원본의 직접 지정 항목 해제
    /// </summary>
    public void Clear ()
    {
        _entries.Clear( );
    }

    /// <summary>
    /// 선택 해제된 원본의 지정 항목 정리
    /// </summary>
    /// <param name="paths">현재 선택된 원본 경로</param>
    public void Retain ( HashSet<string> paths )
    {
        List<string> removed = new( );
        foreach ( string path in _entries.Keys )
        {
            if ( paths.Contains( path ) == false )
                removed.Add( path );
        }
        foreach ( string path in removed )
            _entries.Remove( path );
    }

    /// <summary>
    /// 원본별 직접 지정 정보 조회
    /// </summary>
    /// <param name="path">원본 경로</param>
    /// <returns>지정 정보 또는 없음</returns>
    public AnomalyPrefabReferenceEntry Get ( string path )
    {
        return _entries.TryGetValue( path, out AnomalyPrefabReferenceEntry entry ) ? entry : null;
    }

    /// <summary>
    /// 현재 원본의 단일 참조 조회
    /// </summary>
    /// <param name="owner">참조 소유 컴포넌트</param>
    /// <param name="field">직렬화 필드</param>
    /// <returns>현재 연결된 컴포넌트 또는 없음</returns>
    static Component Read ( Component owner, string field )
    {
        if ( owner == null )
            return null;
        using SerializedObject serialized = new( owner );
        return serialized.FindProperty( field ).objectReferenceValue as Component;
    }

    /// <summary>
    /// 참조 후보 선택값과 원본 식별자 보관
    /// </summary>
    /// <param name="entry">원본별 선택 정보</param>
    /// <param name="key">참조 항목</param>
    /// <param name="value">선택한 원본 컴포넌트</param>
    static void Select ( AnomalyPrefabReferenceEntry entry, string key, Component value )
    {
        entry.Values[key] = value;
        entry.Ids[key] = GetId( value );
    }

    /// <summary>
    /// 원본 내부 후보와 직접 지정 여부 표시
    /// </summary>
    /// <param name="asset">원본 루트</param>
    /// <param name="entry">원본별 선택</param>
    /// <param name="key">참조 항목</param>
    /// <param name="label">화면 표시 이름</param>
    /// <param name="ownerType">참조 소유 형식</param>
    /// <param name="field">직렬화 필드</param>
    /// <param name="valueType">선택할 컴포넌트 형식</param>
    /// <returns>직접 지정 변경 여부</returns>
    static bool DrawReference (
        GameObject asset, AnomalyPrefabReferenceEntry entry, string key, string label,
        Type ownerType, string field, Type valueType )
    {
        Component owner = asset.GetComponent( ownerType );
        Component current = Read( owner, field );
        bool enabled = entry.Values.ContainsKey( key );
        bool selected = EditorGUILayout.ToggleLeft( label + " 직접 지정", enabled );
        bool changed = selected != enabled;
        if ( changed == true )
        {
            if ( selected == true )
            {
                Select( entry, key, current );
                if ( key == ColorKey && owner != null )
                {
                    using SerializedObject color = new( owner );
                    entry.MaterialIndex = color.FindProperty( "_materialIndex" ).intValue;
                }
            }
            else
            {
                entry.Values.Remove( key );
                entry.Ids.Remove( key );
            }
        }
        EditorGUILayout.LabelField( "현재", Describe( current, asset.transform ), EditorStyles.wordWrappedLabel );
        if ( selected == false )
            return changed;
        List<Component> candidates = new( );
        List<string> names = new( ) { "후보를 선택하세요" };
        foreach ( Component candidate in asset.GetComponentsInChildren( valueType, true ) )
        {
            if ( candidate is Collider collider && collider.isTrigger == true ||
                key == LidKey && candidate.transform == asset.transform )
                continue;
            candidates.Add( candidate );
            names.Add( Describe( candidate, asset.transform ) );
        }
        Component value = entry.Values[key];
        int index = candidates.IndexOf( value ) + 1;
        int next = EditorGUILayout.Popup( "지정 후보", index, names.ToArray( ) );
        if ( next != index )
        {
            value = next == 0 ? null : candidates[next - 1];
            Select( entry, key, value );
            changed = true;
        }
        if ( index == 0 || value == null || entry.Ids[key] != GetId( value ) )
            EditorGUILayout.HelpBox( "유효한 원본 후보를 다시 선택하세요. 이 상태에서는 해당 원본을 적용하지 않습니다.", MessageType.Warning );
        if ( key == ColorKey && value is Renderer renderer )
        {
            Material[] materials = renderer.sharedMaterials;
            string[] slots = new string[materials.Length];
            for ( int i = 0; i < slots.Length; i++ )
                slots[i] = $"{i}: {( materials[i] != null ? materials[i].name : "미연결" )}";
            if ( slots.Length == 0 )
                EditorGUILayout.HelpBox( "선택한 렌더러에 머티리얼 슬롯이 없습니다.", MessageType.Warning );
            else
            {
                int slot = entry.MaterialIndex;
                int displayedSlot = slot >= 0 && slot < slots.Length ? slot : -1;
                int nextSlot = EditorGUILayout.Popup( "머티리얼 슬롯", displayedSlot, slots );
                if ( nextSlot != displayedSlot )
                {
                    entry.MaterialIndex = nextSlot;
                    changed = true;
                }
            }
        }
        return changed;
    }

    /// <summary>
    /// 선택 원본별 필요한 참조 항목 표시
    /// </summary>
    /// <param name="paths">현재 선택된 원본 경로</param>
    /// <param name="kinds">부착할 기능 묶음</param>
    /// <param name="focusedPath">오른쪽에서 설정할 원본 경로</param>
    /// <returns>직접 지정 변경 여부</returns>
    public bool Draw ( HashSet<string> paths, AnomalySetupKind[] kinds, string focusedPath )
    {
        if ( paths.Count == 0 )
            return false;
        EditorGUILayout.Space( );
        EditorGUILayout.LabelField( "원본별 참조 직접 지정", EditorStyles.boldLabel );
        if ( string.IsNullOrEmpty( focusedPath ) || paths.Contains( focusedPath ) == false )
        {
            EditorGUILayout.HelpBox( "왼쪽 목록에서 설정할 원본 이름을 누르고 적용 대상 체크를 켜세요.", MessageType.Info );
            return false;
        }
        EditorGUILayout.HelpBox( "현재 원본의 필요한 항목만 직접 지정하세요. 다른 원본으로 이동해도 지정값은 유지됩니다. 직접 지정한 항목은 자동 연결로 덮어쓰지 않습니다.", MessageType.Info );
        List<string> ordered = new( ) { focusedPath };
        bool changed = false;
        foreach ( string path in ordered )
        {
            if ( _entries.TryGetValue( path, out AnomalyPrefabReferenceEntry entry ) == false )
            {
                entry = new( );
                _entries.Add( path, entry );
            }
            EditorGUILayout.LabelField( Path.GetFileNameWithoutExtension( path ), EditorStyles.boldLabel );
            EditorGUILayout.LabelField( path, EditorStyles.wordWrappedLabel );
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>( path );
            if ( asset == null )
                continue;
            bool item = entry.Values.ContainsKey( ItemKey ) || asset.GetComponent<ItemWorldController>( ) != null ||
                Array.Exists( kinds, kind => kind == AnomalySetupKind.Drop || kind == AnomalySetupKind.Displace ||
                    kind == AnomalySetupKind.Swap || kind == AnomalySetupKind.Appearance );
            bool presence = entry.Values.ContainsKey( PresenceKey ) || entry.Values.ContainsKey( RestoreKey ) ||
                asset.GetComponent<PresenceDisplayController>( ) != null || Array.IndexOf( kinds, AnomalySetupKind.Presence ) >= 0;
            if ( item == true )
                changed |= DrawReference( asset, entry, ItemKey, "낙하 / 이동 / 교체 / 외형 대체 변칙 - 아이템 대표 충돌체", typeof( ItemWorldController ), "_collider", typeof( Collider ) );
            if ( presence == true )
            {
                changed |= DrawReference( asset, entry, PresenceKey, "추가 / 제거 변칙 - 대표 충돌체", typeof( PresenceDisplayController ), "_collider", typeof( Collider ) );
                changed |= DrawReference( asset, entry, RestoreKey, "추가 / 제거 변칙 - 기존 조사 영역", typeof( PresenceDisplayController ), "_restoreCheckCollider", typeof( Collider ) );
            }
            if ( entry.Values.ContainsKey( LidKey ) || asset.GetComponent<OpenClosePlacement>( ) != null || Array.IndexOf( kinds, AnomalySetupKind.OpenClose ) >= 0 )
                changed |= DrawReference( asset, entry, LidKey, "개폐 변칙 - 개폐 축", typeof( OpenClosePlacement ), "_lidPivot", typeof( Transform ) );
            if ( entry.Values.ContainsKey( ColorKey ) || asset.GetComponent<MaterialChangeView>( ) != null || Array.IndexOf( kinds, AnomalySetupKind.Material ) >= 0 )
                changed |= DrawReference( asset, entry, ColorKey, "색 대체 변칙 - 색 변경 대상", typeof( MaterialChangeView ), "_target", typeof( Renderer ) );
        }
        return changed;
    }
}
