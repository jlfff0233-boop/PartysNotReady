using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 리소스 관리 - 프리팹 경로 로드와 캐시
/// </summary>
public class ResourceManager : MonoBehaviour
{
    /// <summary>
    /// 프리팹 딕셔너리(경로, 프리팹)
    /// </summary>
    Dictionary<string, GameObject> _prefabs = new( );


    /// <summary>
    /// 캐시된 프리팹을 조회하고 없으면 Resources에서 로드
    /// </summary>
    /// <param name="path">Resources 기준 확장자를 제외한 프리팹 경로</param>
    /// <returns>로드한 프리팹, 찾지 못하면 null</returns>
    public GameObject GetPrefab ( string path )
    {
        if ( string.IsNullOrWhiteSpace( path ) == true )
        {
            Debug.LogError( "프리팹 경로가 비어 있습니다." );
            return null;

        }

        if ( _prefabs.TryGetValue( path, out GameObject prefab ) == true )
            return prefab;

        prefab = Resources.Load<GameObject>( path );

        if ( prefab == null )
        {
            Debug.LogError( $"[프리팹 로드 실패] Resources/{path}", this );
            return null;
        }

        _prefabs.Add( path, prefab );
        return prefab;
    }

    /// <summary>
    /// 로드한 프리팹의 캐시 참조 해제
    /// </summary>
    public void ClearCache ()
    {
        //실제 에셋 언로드와 캐시 참조 해제는 구분
        _prefabs.Clear( );
    }
}