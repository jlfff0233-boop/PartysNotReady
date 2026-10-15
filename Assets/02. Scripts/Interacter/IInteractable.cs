
/// <summary>
/// 월드 상호작용 대상
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// 상호작용 행동 이름
    /// </summary>
    string ActionName { get; }

    /// <summary>
    /// 현재 상호작용 가능 여부
    /// </summary>
    bool CanInteract { get; }

    /// <summary>
    /// 상호작용 실행
    /// </summary>
    void Interact ( );
}
