using System;

/// <summary>
/// 플레이 화면 연출 제어 - 포획과 스턴 상태 연결, 효과 우선순위와 복원
/// </summary>
public class PlayScreenEffectController : IDisposable
{
    Player _player;		//상태 알림을 제공할 플레이어
    ScreenEffect _screenEffect;		//포획과 스턴 화면 연출
    CameraEffect _cameraEffect;		//스턴 카메라 연출
    StunEffect _stunEffect;		//스턴 넘어짐 연출
    CaptureEffect _captureEffect;		//납치 자세 연출

    /// <summary>
    /// 플레이어와 화면 및 자세 연출 연결
    /// </summary>
    /// <param name="player">플레이어</param>
    /// <param name="screenEffect">화면 연출</param>
    /// <param name="cameraEffect">카메라 연출</param>
    /// <param name="stunEffect">넘어짐 연출</param>
    /// <param name="captureEffect">납치 자세 연출</param>
    public PlayScreenEffectController (
        Player player, ScreenEffect screenEffect, CameraEffect cameraEffect,
        StunEffect stunEffect, CaptureEffect captureEffect )
    {
        _player = player;
        _screenEffect = screenEffect;
        _cameraEffect = cameraEffect;
        _stunEffect = stunEffect;
        _captureEffect = captureEffect;
    }

    /// <summary>
    /// 연출 초기화와 플레이어 상태 알림 연결
    /// </summary>
    /// <returns>연출 준비 성공 여부</returns>
    public bool Init ()
    {
        if ( _screenEffect.Init( ) == false )
            return false;

        _cameraEffect.Init( );
        _stunEffect.Init( );
        _captureEffect.Init( );

        Unsubscribe( );
        _player.OnCaptureChanged += HandleCaptureChanged;
        _player.OnStunChanged += HandleStunChanged;
        Refresh( );
        return true;
    }

    /// <summary>
    /// 상태 알림 연결 해제
    /// </summary>
    void Unsubscribe ()
    {
        //외부 씬 해제 시 플레이어가 먼저 파괴될 수 있음
        if ( _player == null )
            return;

        _player.OnCaptureChanged -= HandleCaptureChanged;
        _player.OnStunChanged -= HandleStunChanged;
    }

    /// <summary>
    /// 상태 알림을 해제하고 화면 및 자세 복원
    /// </summary>
    public void Dispose ()
    {
        //종료 중 상태 변경이 연출을 다시 켜지 않도록 먼저 해제
        Unsubscribe( );
        Reset( );
    }

    /// <summary>
    /// 포획 변경에 따른 연출 갱신
    /// </summary>
    /// <param name="isCaptured">포획 여부</param>
    void HandleCaptureChanged ( bool isCaptured )
    {
        Refresh( );
    }

    /// <summary>
    /// 스턴 변경에 따른 연출 갱신
    /// </summary>
    /// <param name="isStunned">스턴 여부</param>
    void HandleStunChanged ( bool isStunned )
    {
        Refresh( );
    }

    /// <summary>
    /// 현재 플레이어 상태를 화면, 카메라와 자세 연출에 반영
    /// </summary>
    public void Refresh ()
    {
        bool isCaptured = _player.IsCaptured;		//현재 포획 상태
        bool isStunned = _player.IsStunned;		//현재 스턴 상태

        _screenEffect.SetState( isCaptured, isStunned );
        _cameraEffect.SetStunned( isStunned );

        //포획 중에는 넘어짐을 즉시 정리하고 납치 자세 표시
        if ( isCaptured == true )
            _stunEffect.ResetEffect( );

        _captureEffect.SetCaptured( isCaptured );

        //포획 해제 후에도 스턴이 남아 있으면 넘어짐 적용
        if ( isCaptured == false )
            _stunEffect.SetStunned( isStunned );
    }

    /// <summary>
    /// 초기화 실패 또는 씬 해제 중에도 남아 있는 연출 복원
    /// </summary>
    public void Reset ()
    {
        if ( _screenEffect != null )
            _screenEffect.ResetEffect( );
        if ( _cameraEffect != null )
            _cameraEffect.ResetEffect( );
        if ( _stunEffect != null )
            _stunEffect.ResetEffect( );
        if ( _captureEffect != null )
            _captureEffect.ResetEffect( );
    }
}
