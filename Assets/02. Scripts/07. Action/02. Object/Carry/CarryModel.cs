/// <summary>
/// 보유 방식
/// </summary>
public enum HoldType
{
    None,       //미보유
    View,       //카메라 표시 보유
    World,      //월드 표시 보유
}

/// <summary>
/// 보유 모델 - 보유 방식과 전환 규칙 관리
/// </summary>
public class CarryModel
{
    HoldType _holdType;      //현재 보유 방식

    /// <summary>
    /// 보유 여부
    /// </summary>
    public bool IsHold => _holdType != HoldType.None;

    /// <summary>
    /// 외부 밀어내기 허용 여부
    /// </summary>
    public bool CanPush => IsHold == false;

    /// <summary>
    /// 강제 이동 허용 여부
    /// </summary>
    public bool CanDisplace => IsHold == false;

    /// <summary>
    /// 현재 보유 방식
    /// </summary>
    public HoldType CurrentType => _holdType;


    /// <summary>
    /// 보유 방식 변경
    /// </summary>
    /// <param name="type">변경할 보유 방식</param>
    /// <returns>변경 성공 여부</returns>
    public bool TryChange ( HoldType type )
    {
        //전환할 수 없는 보유 방식이면 종료
        if ( CanChange( type ) == false )
            return false;

        _holdType = type;

        return true;
    }

    /// <summary>
    /// 요청한 방식으로 집을 수 있는지 확인
    /// </summary>
    /// <param name="type">요청한 보유 방식</param>
    /// <returns>집기 가능 여부</returns>
    public bool CanPickUp ( HoldType type )
    {
        return type != HoldType.None && CanChange( type );
    }

    /// <summary>
    /// 보유 방식 전환 가능 여부 확인
    /// </summary>
    /// <param name="type">변경할 보유 방식</param>
    /// <returns>전환 가능 여부</returns>
    public bool CanChange ( HoldType type )
    {
        switch ( _holdType )
        {
            //미보유 -> 카메라 또는 월드 보유
            case HoldType.None:
                return type == HoldType.View || type == HoldType.World;

            //카메라 보유는 해제 후 다른 방식으로 전환
            case HoldType.View:
                return type == HoldType.None;

            //월드 보유는 카메라 보유로 전달하거나 해제 가능
            case HoldType.World:
                return type == HoldType.View || type == HoldType.None;

            default:
                return false;
        }
    }

}
