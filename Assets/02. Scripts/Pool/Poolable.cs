using UnityEngine;

/// <summary>
/// 풀 객체 - 소속 풀 연결과 반환 요청
/// </summary>
[DisallowMultipleComponent]
public class Poolable : MonoBehaviour
{
    Pool _pool;		//자신을 생성한 풀
    IPoolReturnHandler _returnHandler;		//선택적 반환 준비 기능
    bool _isReturning;		//반환 처리 중 여부

    /// <summary>
    /// 소속 풀과 반환 준비 기능 연결
    /// </summary>
    /// <param name="pool">자신을 생성한 풀</param>
    public void Init ( Pool pool )
    {
        _pool = pool;
        _returnHandler = GetComponent<IPoolReturnHandler>( );
    }

    /// <summary>
    /// 외부 파괴 시 남은 대여 기록 해제
    /// </summary>
    void OnDestroy ()
    {
        _pool?.Clear( this );
        ClearPool( );
    }


    /// <summary>
    /// 기능별 반환 준비 실행
    /// </summary>
    /// <param name="reason">반환 준비 실패 사유</param>
    /// <returns>반환 준비 성공 여부</returns>
    public bool TryPrepareReturn ( out string reason )
    {
        reason = string.Empty;

        if ( _returnHandler == null )
            return true;

        return _returnHandler.TryPreparePoolReturn( out reason );
    }

    /// <summary>
    /// 파괴할 객체의 풀 연결 해제
    /// </summary>
    public void ClearPool ()
    {
        _pool = null;
        _returnHandler = null;
    }

    /// <summary>
    /// 자신을 생성한 풀에 반환 요청
    /// </summary>
    /// <param name="reason">반환 실패 사유</param>
    /// <returns>반환 성공 여부</returns>
    public bool TryReturnToPool ( out string reason )
    {
        reason = string.Empty;

        if ( _pool == null )
        {
            reason = "연결된 풀이 없습니다.";
            return false;
        }

        if ( _isReturning == true )
        {
            reason = "이미 반환을 처리하고 있습니다.";
            return false;
        }

        _isReturning = true;

        try
        {
            return _pool.TryReturn( this, out reason );
        }
        finally
        {
            _isReturning = false;
        }
    }


}
