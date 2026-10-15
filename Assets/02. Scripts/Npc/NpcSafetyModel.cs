
/// <summary>
/// NPC 안전 모델 - 안전 상태와 전환 판정
/// </summary>
public class NpcSafetyModel
{
    NpcSafeStatusType _status = NpcSafeStatusType.Normal;       //현재 상태

    /// <summary>
    /// 현재 안전 상태
    /// </summary>
    public NpcSafeStatusType Status => _status;

    /// <summary>
    /// 게임에 남아 있는지 여부
    /// </summary>
    public bool IsRemaining => _status != NpcSafeStatusType.Departed;

    /// <summary>
    /// 포획 가능한 상태인지 여부
    /// </summary>
    public bool CanCapture => _status == NpcSafeStatusType.Normal;

    /// <summary>
    /// 정상 상태에서 포획 상태로 전환
    /// </summary>
    /// <returns>전환 성공 여부</returns>
    public bool TryCapture ( )
    {
        if ( CanCapture == false )
            return false;

        _status = NpcSafeStatusType.Captured;
        return true;
    }

    /// <summary>
    /// 포획을 해제하고 정상 상태로 전환
    /// </summary>
    /// <returns>전환 성공 여부</returns>
    public bool TryRelease ( )
    {
        if ( _status != NpcSafeStatusType.Captured )
            return false;

        _status = NpcSafeStatusType.Normal;
        return true;
    }

    /// <summary>
    /// 포획 중인 NPC의 납치 완료
    /// </summary>
    /// <returns>게임 이탈 전환 성공 여부</returns>
    public bool TryCompleteKidnap ( )
    {
        if ( _status != NpcSafeStatusType.Captured )
            return false;

        _status = NpcSafeStatusType.Departed;
        return true;
    }
}
