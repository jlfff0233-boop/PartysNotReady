/// <summary>
/// 개폐 모델 - 개폐 상태와 변경 판단
/// </summary>
public class OpenCloseModel
{
    bool _isOpen;     //열림 여부

    /// <summary>
    /// 열림 여부
    /// </summary>
    public bool IsOpen => _isOpen;

    /// <summary>
    /// 초기 개폐 상태 설정
    /// </summary>
    /// <param name="isOpen">초기 열림 여부</param>
    public OpenCloseModel ( bool isOpen )
    {
        _isOpen = isOpen;
    }

    /// <summary>
    /// 요청한 개폐 상태로 변경
    /// </summary>
    /// <param name="isOpen">변경할 열림 여부</param>
    /// <returns>실제 상태 변경 여부</returns>
    public bool TrySetOpen ( bool isOpen )
    {
        //같은 상태 요청은 변경으로 처리하지 않음
        if ( _isOpen == isOpen )
            return false;

        _isOpen = isOpen;
        return true;
    }
}
