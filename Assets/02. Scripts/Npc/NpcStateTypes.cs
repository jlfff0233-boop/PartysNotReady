
/// <summary>
/// NPC 안전 상태
/// </summary>
public enum NpcSafeStatusType
{
    Normal,       //정상
    Captured,     //포획 중
    Departed      //납치 완료로 게임에서 이탈
}

/// <summary>
/// NPC 행동 상태
/// </summary>
public enum NpcStateType
{
    Normal,      //대기와 일반 이동
    Excited,     //음악 반응
    Captured     //포획 중 행동
}

/// <summary>
/// NPC 일반 행동의 진행 구간
/// </summary>
public enum NpcNormalActionType
{
    Waiting,     //대기
    LocalMove,   //같은 방 안 이동
    RoomMove     //다른 방 이동
}
