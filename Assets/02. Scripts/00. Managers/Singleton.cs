using UnityEngine;

/// <summary>
/// 싱글톤 - 인스턴스 등록과 씬 전환 유지
/// </summary>
public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
{
    static T _instance;		//등록된 인스턴스

    /// <summary>
    /// 등록된 인스턴스
    /// </summary>
    public static T Instance => _instance;

    /// <summary>
    /// 최초 인스턴스 등록과 중복 객체 제거
    /// </summary>
    protected virtual void Awake ()
    {
        if ( _instance != null && _instance != this )
        {
            Destroy( gameObject );
            return;
        }

        _instance = this as T;
        DontDestroyOnLoad( gameObject );
    }

    /// <summary>
    /// 자신이 등록한 인스턴스 해제
    /// </summary>
    protected virtual void OnDestroy ()
    {
        if ( _instance == this )
            _instance = null;
    }


    /// <summary>
    /// 플레이 시작 전 정적 참조 초기화
    /// </summary>
    protected static void ResetInstance ()
    {
        _instance = null;
    }


}
