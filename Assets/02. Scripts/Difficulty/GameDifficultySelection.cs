using UnityEngine;

/// <summary>
/// 난이도 선택 기록 - 이번 세션의 선택을 씬 사이에 전달
/// </summary>
public static class GameDifficultySelection
{
    static GameDifficulty _selected;		//선택한 난이도
    static bool _hasSelected;		//선택 기록 여부

    /// <summary>
    /// 플레이 모드 진입 시 선택 기록 초기화
    /// </summary>
    [RuntimeInitializeOnLoadMethod (
        RuntimeInitializeLoadType.SubsystemRegistration )]
    public static void ResetSelection ( )
    {
        _selected = GameDifficulty.Normal;
        _hasSelected = false;
    }

    /// <summary>
    /// 이번 세션의 선택 난이도 기록
    /// </summary>
    /// <param name="difficulty">선택한 난이도</param>
    public static void SetSelected ( GameDifficulty difficulty )
    {
        _selected = difficulty;
        _hasSelected = true;
    }

    /// <summary>
    /// 선택 기록 조회
    /// </summary>
    /// <param name="difficulty">선택한 난이도</param>
    /// <returns>선택 기록 존재 여부</returns>
    public static bool TryGetSelected ( out GameDifficulty difficulty )
    {
        difficulty = _selected;
        return _hasSelected;
    }
}