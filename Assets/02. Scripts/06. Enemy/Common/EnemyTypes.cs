/// <summary>
/// 적 상태 종류
/// </summary>
public enum EnemyStateType
{
    Normal,     //기본 대기, 배회, 주변 확인
    Chase,      //대상에게 접근
    Mad,        //분노
    Hide,       //잠복
    Attack,     //공격
    Escape,     //퇴장
    Fetch,      //물건 가져오기
    Return,     //가져온 물건 전달
    Enter,      //등장
    Eat,        //섭취
    Collect,        //복수 아이템 수집

    Count,
}


/// <summary>
/// 자동 발생 대상 적 종류
/// </summary>
public enum EnemySpawnType
{
    None,
    Harm,
    Kidnap,
    Demand,
    Bothersome,
    Steal
}
