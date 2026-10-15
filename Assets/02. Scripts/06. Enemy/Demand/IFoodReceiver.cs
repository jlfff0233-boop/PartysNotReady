
/// <summary>
/// 음식 수락 계약 - 수락 가능 여부와 전달 결과
/// </summary>
public interface IFoodReceiver
{
    /// <summary>
    /// 음식 수락 가능 여부 확인
    /// </summary>
    /// <param name="data">음식 데이터</param>
    /// <returns>수락 가능 여부</returns>
    bool CanReceive ( FoodData data );

    /// <summary>
    /// 전달한 음식 수락
    /// </summary>
    /// <param name="data">음식 데이터</param>
    /// <returns>수락 성공 여부, 섭취 완료와는 별개</returns>
    bool TryReceive ( FoodData data );
}
