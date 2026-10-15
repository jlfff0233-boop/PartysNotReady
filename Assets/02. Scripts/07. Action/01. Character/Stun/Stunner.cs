using System;

/// <summary>
/// 스턴 중재 - 시간 갱신과 상태 변경 알림
/// </summary>
public class Stunner
{
    StunTimer _model = new( );      //스턴 시간 모델

    /// <summary>
    /// 스턴 적용 여부
    /// </summary>
    public bool IsStunned => _model.IsStunned;

    /// <summary>
    /// 스턴 시작과 해제 알림(스턴 여부)
    /// </summary>
    public event Action<bool> OnChanged;

    /// <summary>
    /// 스턴 적용과 시작 알림
    /// </summary>
    /// <param name="duration">스턴 지속 시간(초)</param>
    public void Apply ( float duration )
    {
        bool previous = IsStunned;
        _model.Apply( duration );

        //남은 시간만 연장된 경우에는 시작 알림을 반복하지 않음
        if ( previous != IsStunned )
            OnChanged?.Invoke( IsStunned );
    }

    /// <summary>
    /// 스턴 시간 갱신과 해제 알림
    /// </summary>
    /// <param name="deltaTime">경과 시간(초)</param>
    public void Update ( float deltaTime )
    {
        if ( IsStunned == false )
            return;

        _model.Update( deltaTime );

        //이번 갱신으로 스턴이 끝났을 때만 해제 알림
        if ( IsStunned == false )
            OnChanged?.Invoke( false );
    }

    /// <summary>
    /// 스턴 해제와 상태 변경 알림
    /// </summary>
    public void Clear ()
    {
        //이미 해제된 상태에서는 알림을 반복하지 않음
        if ( IsStunned == false )
            return;

        _model.Clear( );
        OnChanged?.Invoke( false );
    }
}
