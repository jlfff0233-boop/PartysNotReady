using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 확장 메서드 - 게임오브젝트와 목록의 공통 유틸리티 호출
/// </summary>
public static class Extension
{
    /// <summary>
    /// 컴포넌트를 찾아 반환하고 없으면 추가
    /// </summary>
    /// <typeparam name="T">확보할 컴포넌트 형식</typeparam>
    /// <param name="go">컴포넌트를 확보할 게임오브젝트</param>
    /// <returns>기존 또는 추가한 컴포넌트</returns>
    public static T GetOrAddComponent<T> ( this GameObject go ) where T : Component
    {
        return Util.GetOrAddComponent<T>( go );
    }

    /// <summary>
    /// 풀 객체는 반환하고 일반 객체는 파괴
    /// </summary>
    /// <param name="go">사용을 종료할 게임오브젝트</param>
    /// <returns>반환 또는 파괴 요청 성공 여부, 이미 없는 객체는 성공</returns>
    public static bool DestroyOrReturnToPool ( this GameObject go )
    {
        return Util.DestroyOrReturnToPool( go );
    }

    /// <summary>
    /// 목록에서 무작위 원소 하나 선택
    /// </summary>
    /// <typeparam name="T">원소 형식</typeparam>
    /// <param name="list">선택할 원소 목록</param>
    /// <returns>선택한 원소, 목록이 없거나 비어 있으면 기본값</returns>
    public static T ChooseRandom<T> ( this IReadOnlyList<T> list )
    {
        return Util.ChooseRandom( list );
    }
}