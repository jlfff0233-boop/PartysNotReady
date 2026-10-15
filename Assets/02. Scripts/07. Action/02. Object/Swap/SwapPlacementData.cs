
/// <summary>
/// 교체 배치 정보 - 두 아이템에 적용할 목적지 보관
/// </summary>
public struct SwapPlacementData
{
    PlacementData _destinationA;        //첫 번째 아이템을 두 번째 아이템의 자리로 옮길 배치 정보
    PlacementData _destinationB;        //두 번째 아이템을 첫 번째 아이템의 자리로 옮길 배치 정보

    /// <summary>
    /// 첫 번째 아이템을 두 번째 아이템의 자리로 옮길 배치 정보
    /// </summary>
    public PlacementData DestinationA => _destinationA;

    /// <summary>
    /// 두 번째 아이템을 첫 번째 아이템의 자리로 옮길 배치 정보
    /// </summary>
    public PlacementData DestinationB => _destinationB;

    /// <summary>
    /// 검사를 통과한 두 아이템의 배치 정보 저장
    /// </summary>
    /// <param name="destinationA">첫 번째 아이템의 목적지</param>
    /// <param name="destinationB">두 번째 아이템의 목적지</param>
    public SwapPlacementData (
        PlacementData destinationA, PlacementData destinationB )
    {
        _destinationA = destinationA;
        _destinationB = destinationB;
    }
}
