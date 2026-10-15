
/// <summary>
/// 제작 진행 상태
/// </summary>
public enum ProductionState
{
    Preparing,      //준비 중
    Processing,     //진행 중
    Completed       //완료
}

/// <summary>
/// 제작 시작 결과
/// </summary>
public enum ProductionStartResult
{
    Started,        //시작
    MissingIngredient,      //재료 부족
    InvalidIngredient,      //잘못된 재료
    Unavailable     //제작 불가능
}

/// <summary>
/// 제작 진행 결과
/// </summary>
public enum ProductionProgressResult
{
    Progressed,     //진행 중
    Completed,      //완료
    Unavailable     //불가능
}

/// <summary>
/// 제작 미니게임 판정 결과
/// </summary>
public enum ProduceMinigameResult
{
    InProgress,     //진행 중
    Succeeded,      //성공
    Failed,     //실패
    Unavailable     //판정 불가능
}
