using UnityEngine;

/// <summary>
/// 이동 속도 증가 효과
/// </summary>
public class MoveSpeedUpEffect : ISupportItemEffect
{
    MoveSpeedUpData _data;		//효과 데이터
    Mover _target;      //적용 대상
    float _remainingTime;      //남은 시간

    /// <summary>
    /// 남은 효과 시간
    /// </summary>
    public float RemainingTime => _remainingTime;

    /// <summary>
    /// 효과 연결
    /// </summary>
    /// <param name="data">효과 데이터</param>
    /// <param name="target">적용 대상</param>
    public MoveSpeedUpEffect (
        MoveSpeedUpData data, Mover target )
    {
        _data = data;
        _target = target;
    }

    /// <summary>
    /// 효과 적용
    /// </summary>
    /// <returns>실행 결과</returns>
    public SupportItemUseResult Use ()
    {
        //적용 가능 여부 확인 후 효과 적용
        if ( _data.IsValid == false ||
            _remainingTime > 0f ||
             _target == null ||
             _target.TryApplySupportSpeed( this, _data.Multiplier ) == false )
            return SupportItemUseResult.Rejected;

        //남은 시간 설정
        _remainingTime = _data.Duration;

        //적용 결과 반환
        return SupportItemUseResult.Applied;
    }

    /// <summary>
    /// 적용값 복원
    /// </summary>
    public void End ()
    {
        _remainingTime = 0f;

        if ( _target != null )
            _target.ClearSupportSpeed( this );
    }

    /// <summary>
    /// 효과 시간 진행
    /// </summary>
    /// <param name="deltaTime">경과시간</param>
    public void Tick ( float deltaTime )
    {
        if ( _remainingTime <= 0f || deltaTime <= 0f ||
             float.IsNaN( deltaTime ) == true ||
             float.IsInfinity( deltaTime ) == true )
            return;

        //남은 시간 설정
        _remainingTime = Mathf.Max( 0f, _remainingTime - deltaTime );

        if ( _remainingTime == 0f )
            End( );
    }
}
