using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 편집 객체 위치 - 저장과 재임포트 이후 계층과 컴포넌트 조회
/// </summary>
public class AnomalyPrefabObjectAddress
{
    int _rootIndex;		//편집 영역의 루트 순서
    readonly List<string> _names = new( );		//자식 이름 경로
    readonly List<int> _orders = new( );		//같은 이름인 자식의 순서
    Type _type;		//컴포넌트 형식
    int _componentIndex;		//같은 형식인 컴포넌트의 순서

    /// <summary>
    /// 편집 객체의 계층과 컴포넌트 위치 보관
    /// </summary>
    /// <param name="roots">편집 영역 루트 목록</param>
    /// <param name="value">위치를 보관할 객체</param>
    /// <returns>저장 이후 조회할 위치</returns>
    public static AnomalyPrefabObjectAddress Capture ( GameObject[] roots, Object value )
    {
        GameObject owner = value is Component component ? component.gameObject : (GameObject)value;
        Transform current = owner.transform;
        AnomalyPrefabObjectAddress address = new( );
        while ( Array.IndexOf( roots, current.gameObject ) < 0 )
        {
            int order = 0;
            for ( int i = 0; i < current.GetSiblingIndex( ); i++ )
            {
                if ( current.parent.GetChild( i ).name == current.name )
                    order++;
            }
            address._names.Insert( 0, current.name );
            address._orders.Insert( 0, order );
            current = current.parent;
        }
        address._rootIndex = Array.IndexOf( roots, current.gameObject );
        if ( value is Component selected )
        {
            address._type = selected.GetType( );
            address._componentIndex = Array.IndexOf( owner.GetComponents( address._type ), selected );
        }
        return address;
    }

    /// <summary>
    /// 보관한 위치의 현재 객체 조회
    /// </summary>
    /// <param name="roots">현재 편집 영역 루트 목록</param>
    /// <returns>일치하는 객체, 계층이 바뀌었으면 없음</returns>
    public Object Resolve ( GameObject[] roots )
    {
        if ( _rootIndex >= roots.Length )
            return null;
        Transform current = roots[_rootIndex].transform;
        for ( int i = 0; i < _names.Count; i++ )
        {
            Transform next = null;
            int order = 0;
            foreach ( Transform child in current )
            {
                if ( child.name != _names[i] )
                    continue;
                if ( order++ == _orders[i] )
                {
                    next = child;
                    break;
                }
            }
            if ( next == null )
                return null;
            current = next;
        }
        if ( _type == null )
            return current.gameObject;
        Component[] components = current.GetComponents( _type );
        return _componentIndex < components.Length ? components[_componentIndex] : null;
    }

    /// <summary>
    /// 중첩 원본의 객체 위치를 바깥 계층 위치에 연결
    /// </summary>
    /// <param name="child">중첩 원본 기준 객체 위치</param>
    /// <returns>바깥 계층에서 조회할 위치</returns>
    public AnomalyPrefabObjectAddress Append ( AnomalyPrefabObjectAddress child )
    {
        AnomalyPrefabObjectAddress address = new( )
        {
            _rootIndex = _rootIndex, _type = child._type, _componentIndex = child._componentIndex
        };
        address._names.AddRange( _names );
        address._names.AddRange( child._names );
        address._orders.AddRange( _orders );
        address._orders.AddRange( child._orders );
        return address;
    }
}
