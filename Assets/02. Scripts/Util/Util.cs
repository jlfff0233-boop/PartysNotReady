using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// 공통 유틸리티 - 컴포넌트 확보, 객체 반환과 파괴, 무작위 선택
/// </summary>
public static class Util
{
    /// <summary>
    /// 작은 값 비교에 사용할 기본 허용 오차
    /// </summary>
    public const float Epsilon = 0.01f;


    /// <summary>
    /// 컴포넌트를 찾아 반환하고 없으면 추가
    /// </summary>
    /// <typeparam name="T">확보할 컴포넌트 형식</typeparam>
    /// <param name="go">컴포넌트를 확보할 게임오브젝트</param>
    /// <returns>기존 또는 추가한 컴포넌트</returns>
    public static T GetOrAddComponent<T> ( GameObject go ) where T : Component
    {
        if ( go.TryGetComponent<T>( out T component ) == false )
            component = go.AddComponent<T>( );

        return component;
    }

    /// <summary>
    /// 풀 객체는 반환하고 일반 객체는 파괴
    /// </summary>
    /// <param name="go">사용을 종료할 게임오브젝트</param>
    /// <returns>반환 또는 파괴 요청 성공 여부, 이미 없는 객체는 성공</returns>
    public static bool DestroyOrReturnToPool ( GameObject go )
    {
        if ( go == null )
            return true;

        //반환 여부를 확인할 때는 컴포넌트를 새로 추가하지 않음
        if ( go.TryGetComponent<Poolable>( out Poolable poolable ) == true )
        {
            if ( poolable.TryReturnToPool( out string reason ) == true )
                return true;

            //반환 준비 실패를 파괴로 대체하지 않음
            Debug.LogWarning( $"[풀 반환 실패] {go.name}: {reason}", go );
            return false;
        }

        Object.Destroy( go );
        return true;
    }

    /// <summary>
    /// 목록에서 무작위 원소 하나 선택
    /// </summary>
    /// <typeparam name="T">원소 형식</typeparam>
    /// <param name="list">선택할 원소 목록</param>
    /// <returns>선택한 원소, 목록이 없거나 비어 있으면 기본값</returns>
    public static T ChooseRandom<T> ( IReadOnlyList<T> list )
    {
        if ( list == null || list.Count == 0 )
        {
            Debug.LogWarning( "[무작위 선택 실패] 목록이 없거나 비어 있습니다." );
            return default;
        }

        int index = Random.Range( 0, list.Count );
        return list [ index ];
    }
}