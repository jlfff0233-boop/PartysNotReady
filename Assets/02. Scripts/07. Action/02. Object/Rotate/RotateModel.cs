
/// <summary>
/// 회전 모델 - 현재 회전 각도 관리
/// </summary>
public class RotateModel
{
    float _angle;     //초기 회전값 기준 현재 회전 각도, 0 이상 360 미만

    /// <summary>
    /// 초기 회전값 기준 현재 회전 각도
    /// </summary>
    public float Angle => _angle;

    /// <summary>
    /// 현재 회전 각도 변경
    /// </summary>
    /// <param name="angle">변경할 각도, 0 이상 360 미만</param>
    /// <returns>실제 변경 여부</returns>
    public bool TrySetAngle ( float angle )
    {
        if ( float.IsNaN( angle ) == true ||
             float.IsInfinity( angle ) == true ||
             angle < 0f || angle >= 360f ||
             _angle == angle )
            return false;

        _angle = angle;
        return true;
    }

    /// <summary>
    /// 한 바퀴를 넘지 않는 다음 회전 각도 계산
    /// </summary>
    /// <param name="amount">이번에 회전할 양</param>
    /// <returns>다음 각도, 초기 회전으로 복귀 시 0</returns>
    public float GetNextAngle ( float amount )
    {
        float remainingAngle = 360f - _angle;

        //한 바퀴에 도달하거나 넘으면 초과 각도를 버리고 초기 회전인 0도로 복귀
        if ( amount >= remainingAngle )
            return 0f;

        return _angle + amount;
    }
}
