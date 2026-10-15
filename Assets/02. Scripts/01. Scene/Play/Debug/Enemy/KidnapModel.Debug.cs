#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// 납치 판정 디버그 - 테스트 표적 종류 지정
/// </summary>
public partial class KidnapModel
{
    /// <summary>
    /// 테스트 표적 종류 지정
    /// </summary>
    /// <param name="isPlayer">플레이어 대상 여부</param>
    public void SetDebugTargetType ( bool isPlayer )
    {
        _isPlayerTarget = isPlayer;
        _isPlayerAttempt = isPlayer;
    }
}
#endif
