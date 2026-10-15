/// <summary>
/// 이동 적용 결과 - 수평 이동 거리와 이동 상태
/// </summary>
public struct MoveResult
{
    float _distance;        //수평 이동 거리
    bool _isGrounded;        //접지 여부
    bool _canMove;        //정상 이동 가능 여부
    bool _isRunning;        //달리기 여부

    /// <summary>
    /// 수평 이동 거리
    /// </summary>
    public float Distance => _distance;
    /// <summary>
    /// 접지 여부
    /// </summary>
    public bool IsGrounded => _isGrounded;
    /// <summary>
    /// 정상 이동 가능 여부
    /// </summary>
    public bool CanMove => _canMove;
    /// <summary>
    /// 달리기 여부
    /// </summary>
    public bool IsRunning => _isRunning;


    /// <summary>
    /// 이동 적용 결과 생성
    /// </summary>
    /// <param name="distance">수평 이동 거리</param>
    /// <param name="isGrounded">접지 여부</param>
    /// <param name="canMove">정상 이동 가능 여부</param>
    /// <param name="isRunning">달리기 여부</param>
    public MoveResult ( float distance , bool isGrounded , bool canMove , bool isRunning )
    {
        _distance = distance;
        _isGrounded = isGrounded;
        _canMove = canMove;
        _isRunning = isRunning;
    }
}
