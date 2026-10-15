using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 배치 표면 계층 목록 - 원본 구조 표시와 묶음 선택 및 이름 검색
/// </summary>
public class PlacementSurfaceTree
{
    readonly List<PlacementSurfaceNode> _roots = new( );     //조회한 원본과 씬 루트
    readonly Dictionary<GameObject, PlacementSurfaceNode> _nodes = new( );     //계층 객체별 항목
    readonly HashSet<GameObject> _expanded = new( );     //펼친 계층
    PlacementSurfaceEntry _active;     //오른쪽에 표시할 개별 후보

    /// <summary>
    /// 오른쪽에 표시할 개별 후보
    /// </summary>
    public PlacementSurfaceEntry Active => _active;


    /// <summary>
    /// 계층 연결 - 충돌체 없는 상위 묶음도 원본 부모 구조대로 구성
    /// </summary>
    /// <param name="owner">연결할 계층 객체</param>
    /// <returns>해당 객체의 계층 항목</returns>
    PlacementSurfaceNode GetNode ( GameObject owner )
    {
        if ( _nodes.TryGetValue( owner, out PlacementSurfaceNode node ) )
            return node;
        node = new PlacementSurfaceNode( owner );
        _nodes.Add( owner, node );
        if ( owner.transform.parent == null )
        {
            _roots.Add( node );
            _expanded.Add( owner );
        }
        else
            GetNode( owner.transform.parent.gameObject ).Children.Add( node );
        return node;
    }

    /// <summary>
    /// 계층 정렬 - 원본 하이어라키의 형제 순서 유지
    /// </summary>
    /// <param name="node">정렬할 묶음</param>
    void Sort ( PlacementSurfaceNode node )
    {
        node.Children.Sort( ( left, right ) => left.Owner.transform.GetSiblingIndex( ).CompareTo( right.Owner.transform.GetSiblingIndex( ) ) );
        foreach ( PlacementSurfaceNode child in node.Children )
            Sort( child );
    }

    /// <summary>
    /// 목록 구성 - 후보들을 원본 계층에 연결하고 기본 펼침 상태 설정
    /// </summary>
    /// <param name="entries">현재 조회한 후보</param>
    public void Build ( List<PlacementSurfaceEntry> entries )
    {
        _roots.Clear( );
        _nodes.Clear( );
        _expanded.Clear( );
        _active = null;
        foreach ( PlacementSurfaceEntry entry in entries )
            GetNode( entry.Owner ).Entry = entry;
        foreach ( PlacementSurfaceNode root in _roots )
        {
            Sort( root );
            foreach ( PlacementSurfaceNode child in root.Children )
                _expanded.Add( child.Owner );
        }
    }

    /// <summary>
    /// 후보 집계 - 검색 결과와 선택 수를 자식부터 합산
    /// </summary>
    /// <param name="node">집계할 계층</param>
    /// <param name="search">검색어</param>
    /// <param name="usePath">전체 경로 검색 여부</param>
    void Count ( PlacementSurfaceNode node, string search, bool usePath )
    {
        bool visible = node.Entry != null && Matches( node.Entry, search, usePath );
        node.Visible = visible ? 1 : 0;
        node.Selected = visible && node.Entry.Selected ? 1 : 0;
        foreach ( PlacementSurfaceNode child in node.Children )
        {
            Count( child, search, usePath );
            node.Visible += child.Visible;
            node.Selected += child.Selected;
        }
    }

    /// <summary>
    /// 묶음 선택 - 검색에 해당하는 자신과 자식 후보의 적용 선택 변경
    /// </summary>
    /// <param name="node">선택할 묶음</param>
    /// <param name="selected">적용 선택 여부</param>
    /// <param name="search">현재 검색어</param>
    /// <param name="usePath">전체 경로 검색 여부</param>
    void Select ( PlacementSurfaceNode node, bool selected, string search, bool usePath )
    {
        if ( node.Entry != null && Matches( node.Entry, search, usePath ) )
            node.Entry.Selected = selected;
        foreach ( PlacementSurfaceNode child in node.Children )
            Select( child, selected, search, usePath );
    }

    /// <summary>
    /// 계층 표시 - 접기와 펼치기 및 묶음 선택, 개별 후보 상세 연결
    /// </summary>
    /// <param name="node">표시할 계층</param>
    /// <param name="search">검색어</param>
    /// <param name="usePath">전체 경로 검색 여부</param>
    /// <param name="depth">루트부터 내려온 계층 깊이</param>
    /// <returns>적용 선택 변경 여부</returns>
    bool DrawNode ( PlacementSurfaceNode node, string search, bool usePath, int depth )
    {
        if ( node.Visible == 0 || node.Owner == null )
            return false;
        bool changed = false;
        bool open = _expanded.Contains( node.Owner );
        EditorGUILayout.BeginHorizontal( );
        GUILayout.Space( depth * 14f );
        bool mixed = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = node.Selected > 0 && node.Selected < node.Visible;
        EditorGUI.BeginChangeCheck( );
        bool selected = GUILayout.Toggle( node.Selected == node.Visible, GUIContent.none, GUILayout.Width( 18f ) );
        if ( EditorGUI.EndChangeCheck( ) )
        {
            Select( node, selected, search, usePath );
            Count( node, search, usePath );
            changed = true;
        }
        EditorGUI.showMixedValue = mixed;
        string label = node.Owner.name + $" ({node.Selected}/{node.Visible})";
        if ( node.Children.Count > 0 )
        {
            bool expanded = EditorGUILayout.Foldout( open, label, true );
            if ( expanded != open )
            {
                open = expanded;
                if ( open )
                    _expanded.Add( node.Owner );
                else
                    _expanded.Remove( node.Owner );
            }
        }
        else if ( GUILayout.Button( label, EditorStyles.label ) )
            _active = node.Entry;
        if ( node.Entry != null && Matches( node.Entry, search, usePath ) && GUILayout.Button( "상세", GUILayout.Width( 42f ) ) )
            _active = node.Entry;
        EditorGUILayout.EndHorizontal( );
        if ( open )
        {
            foreach ( PlacementSurfaceNode child in node.Children )
                changed |= DrawNode( child, search, usePath, depth + 1 );
        }
        return changed;
    }

    /// <summary>
    /// 검색 경로 펼침 - 검색어 변경 시 일치하는 후보의 상위 묶음 표시
    /// </summary>
    /// <param name="search">검색어</param>
    /// <param name="usePath">전체 경로 검색 여부</param>
    public void ExpandMatches ( string search, bool usePath )
    {
        foreach ( PlacementSurfaceNode root in _roots )
            Count( root, search, usePath );
        foreach ( PlacementSurfaceNode node in _nodes.Values )
        {
            if ( node.Visible > 0 && node.Children.Count > 0 )
                _expanded.Add( node.Owner );
        }
    }

    /// <summary>
    /// 목록 표시 - 검색 결과가 있는 원본 계층만 표시
    /// </summary>
    /// <param name="search">검색어</param>
    /// <param name="usePath">전체 경로 검색 여부</param>
    /// <returns>적용 선택 변경 여부</returns>
    public bool Draw ( string search, bool usePath )
    {
        bool changed = false;
        foreach ( PlacementSurfaceNode root in _roots )
        {
            Count( root, search, usePath );
            changed |= DrawNode( root, search, usePath, 0 );
        }
        return changed;
    }

    /// <summary>
    /// 검색 일치 확인 - 기본은 객체 이름, 선택 시 전체 경로 비교
    /// </summary>
    /// <param name="entry">검색할 후보</param>
    /// <param name="search">검색어</param>
    /// <param name="usePath">전체 경로 검색 여부</param>
    /// <returns>검색 일치 여부</returns>
    public static bool Matches ( PlacementSurfaceEntry entry, string search, bool usePath )
    {
        if ( entry.Owner == null )
            return false;
        string text = usePath ? entry.Path : entry.Owner.name;
        return search.Length == 0 || text.IndexOf( search, StringComparison.OrdinalIgnoreCase ) >= 0;
    }

}
