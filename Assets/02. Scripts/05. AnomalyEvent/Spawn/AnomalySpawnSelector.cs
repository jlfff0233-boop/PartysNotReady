using System.Collections.Generic;

/// <summary>
/// 변칙 발생 선정 - 그룹별 가중치 선택과 재발생 시간 관리
/// </summary>
public class AnomalySpawnSelector
{
    float [ ] _nextAllowedTime;		//다음 발생 가능 시간

    /// <summary>
    /// 발생 그룹 수에 맞춰 대기 기록 생성
    /// </summary>
    /// <param name="kindCount">설정한 발생 그룹 수</param>
    public AnomalySpawnSelector ( int kindCount )
    {
        _nextAllowedTime = new float [ kindCount ];
    }


    /// <summary>
    /// 가능한 그룹 중 가중치에 따라 하나 선정
    /// </summary>
    /// <param name="elapsedTime">경과 시간</param>
    /// <param name="weights">그룹별 가중치</param>
    /// <param name="available">시작 가능 여부</param>
    /// <param name="randomValue">0~1 난수</param>
    /// <param name="kindIndex">선정한 그룹 번호</param>
    /// <returns>종류 선정 여부</returns>
    public bool TrySelect (
        float elapsedTime,
        IReadOnlyList<float> weights,
        IReadOnlyList<bool> available,
        float randomValue,
        out int kindIndex )
    {
        kindIndex = -1;

        if ( weights == null || available == null ||
             weights.Count != _nextAllowedTime.Length ||
             available.Count != _nextAllowedTime.Length ||
             float.IsNaN( randomValue ) || randomValue < 0f || randomValue > 1f )
            return false;

        double total = 0d;
        int lastAvailable = -1;

        for ( int i = 0; i < weights.Count; i++ )
        {
            if ( available [ i ] == false ||
                 elapsedTime < _nextAllowedTime [ i ] )
                continue;

            total += weights [ i ];
            lastAvailable = i;
        }

        if ( total <= 0f )
            return false;

        double selected = randomValue * total;
        double current = 0d;

        for ( int i = 0; i < weights.Count; i++ )
        {
            if ( available [ i ] == false ||
                 elapsedTime < _nextAllowedTime [ i ] )
                continue;

            current += weights [ i ];

            if ( selected < current )
            {
                kindIndex = i;
                return true;
            }
        }

        //유니티 난수의 1과 누적 반올림 경계는 마지막 유효 후보에 포함
        kindIndex = lastAvailable;
        return true;
    }

    /// <summary>
    /// 반복 대기 시작
    /// </summary>
    /// <param name="kindIndex">발생 그룹 번호</param>
    /// <param name="elapsedTime">경과 시간</param>
    /// <param name="repeatDelay">그룹별 재발생 대기시간</param>
    public void RecordSuccess (
        int kindIndex, float elapsedTime, float repeatDelay )
    {
        if ( kindIndex < 0 || kindIndex >= _nextAllowedTime.Length )
            return;

        _nextAllowedTime [ kindIndex ] =
            elapsedTime + repeatDelay;
    }
#if UNITY_EDITOR
    /// <summary>
    /// 그룹별 다음 발생 가능 시간 조회
    /// </summary>
    /// <param name="index">그룹 번호</param>
    /// <returns>게임 경과 시간 기준 시각</returns>
    public float GetDebugAllowedTime ( int index )
    {
        return _nextAllowedTime [ index ];
    }
#endif

}
