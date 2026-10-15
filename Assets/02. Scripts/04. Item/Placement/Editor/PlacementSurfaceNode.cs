using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 배치 표면 계층 항목 - 원본 객체와 자식 후보 및 선택 집계 보관
/// </summary>
public class PlacementSurfaceNode
{
    GameObject _owner;     //원본 계층 객체
    readonly List<PlacementSurfaceNode> _children = new( );     //후보가 있는 자식 계층
    PlacementSurfaceEntry _entry;     //자신의 표면 후보, 묶음 객체는 없음
    int _visible;     //검색에 해당하는 자신과 자식 후보 수
    int _selected;     //검색에 해당하는 선택 후보 수

    /// <summary>
    /// 원본 계층 객체
    /// </summary>
    public GameObject Owner => _owner;
    /// <summary>
    /// 후보가 있는 자식 계층
    /// </summary>
    public List<PlacementSurfaceNode> Children => _children;
    /// <summary>
    /// 자신의 표면 후보
    /// </summary>
    public PlacementSurfaceEntry Entry { get => _entry; set => _entry = value; }
    /// <summary>
    /// 검색에 해당하는 후보 수
    /// </summary>
    public int Visible { get => _visible; set => _visible = value; }
    /// <summary>
    /// 검색에 해당하는 선택 후보 수
    /// </summary>
    public int Selected { get => _selected; set => _selected = value; }

    /// <summary>
    /// 계층 항목 구성 - 원본 객체 연결
    /// </summary>
    /// <param name="owner">이 항목의 계층 객체</param>
    public PlacementSurfaceNode ( GameObject owner )
    {
        _owner = owner;
    }
}
