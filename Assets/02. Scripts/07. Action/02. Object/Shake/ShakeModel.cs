
/// <summary>
/// 흔들림 모델 - 동작 상태와 흔들림 대상 관리
/// </summary>
public class ShakeModel
{
    bool _isShaking;     //흔들림 여부
    int _shakeId;     //현재 흔들림 ID


    /// <summary>
    /// 현재 흔들림 여부
    /// </summary>
    public bool IsShaking => _isShaking;
    /// <summary>
    /// 현재 흔들림 ID
    /// </summary>
    public int ShakeId => _shakeId;


    /// <summary>
    /// 흔들림 상태를 변경하고 시작할 때 새 ID 부여
    /// </summary>
    /// <param name="isShaking">변경할 흔들림 여부</param>
    /// <returns>변경 여부</returns>
    public bool TrySetShaking ( bool isShaking )
    {
        if ( _isShaking == isShaking )
            return false;

        _isShaking = isShaking;

        //정지 후 다시 시작한 흔들림을 이전 흔들림과 구분
        if ( isShaking == true )
        {
            unchecked
            {
                _shakeId++;
            }
        }

        return true;
    }
}
