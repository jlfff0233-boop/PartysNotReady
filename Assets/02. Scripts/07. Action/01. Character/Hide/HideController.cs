
/// <summary>
/// 잠복 제어 - 대기 시간, 숨김과 이동 복구 연결
/// </summary>
public class HideController
{
    HideActivationController _view;             //잠복 표시 뷰
    HideTimer _model = new( ); //잠복 시간 모델

    /// <summary>
    /// 잠복 시간 종료 여부
    /// </summary>
    public bool IsReady => _model.IsReady;

#if UNITY_EDITOR
    /// <summary>
    /// 남은 잠복 시간
    /// </summary>
    public float DebugRemainingTime => _model.DebugRemainingTime;
#endif

    /// <summary>
    /// 잠복 뷰 연결
    /// </summary>
    /// <param name="view">잠복 표시 뷰</param>
    public HideController ( HideActivationController view )
    {
        _view = view;
    }

    /// <summary>
    /// 숨김과 잠복 대기 시작
    /// </summary>
    /// <param name="duration">대기 시간(초)</param>
    public void Begin ( float duration )
    {
        _model.Begin( duration );
        _view.Hide( );
    }

    /// <summary>
    /// 잠복 시간 갱신
    /// </summary>
    /// <param name="deltaTime">경과 시간(초)</param>
    public void Update ( float deltaTime )
    {
        _model.Update( deltaTime );
    }

    /// <summary>
    /// 표시와 충돌 복구
    /// </summary>
    public void Show ()
    {
        _view.Show( );
    }

    /// <summary>
    /// 숨김을 유지하며 이동 기능 복구
    /// </summary>
    public void RestoreMovement ( )
    {
        _view.RestoreMovement ( );
    }

}
