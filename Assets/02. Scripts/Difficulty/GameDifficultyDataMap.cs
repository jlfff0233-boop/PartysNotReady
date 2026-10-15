using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 난이도 데이터 맵 - 난이도별 설정 등록과 조회
/// </summary>
[CreateAssetMenu ( menuName = "GameSettings/GameFlow/GameDifficultyDataMap" )]
public class GameDifficultyDataMap : ScriptableObject
{
    [SerializeField] GameDifficultySettings [ ] _data;      //난이도별 설정

    /// <summary>
    /// 난이도에 해당하는 설정 조회
    /// </summary>
    /// <param name="difficulty">조회할 난이도</param>
    /// <param name="settings">해당 난이도의 설정</param>
    /// <returns>조회 성공 여부</returns>
    public bool TryGetSettings (
        GameDifficulty difficulty , out GameDifficultySettings settings )
    {
        foreach ( GameDifficultySettings data in _data )
        {
            if ( data.Difficulty != difficulty )
                continue;

            settings = data;
            return true;
        }

        settings = null;
        return false;
    }

    /// <summary>
    /// 난이도 등록과 흐름 데이터 확인
    /// </summary>
    /// <param name="reason">설정 오류</param>
    /// <returns>설정 유효 여부</returns>
    public bool CheckSettings ( out string reason )
    {
        reason = string.Empty;

        if ( _data == null || _data.Length != 3 )
        {
            reason = "쉬움 / 보통 / 어려움 설정을 하나씩 등록해 주세요.";
            return false;
        }

        HashSet<GameDifficulty> difficulties = new ( );		//등록한 난이도

        for ( int i = 0 ; i < _data.Length ; i++ )
        {
            GameDifficultySettings settings = _data [ i ];		//검사할 설정

            if ( CheckEntry ( settings , out reason ) == false )
            {
                reason = $"{i + 1}번째 난이도 설정: {reason}";
                return false;
            }

            if ( difficulties.Add ( settings.Difficulty ) == false )
            {
                reason = $"난이도가 중복됐습니다: {settings.Difficulty}";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 난이도 항목과 흐름 데이터 확인
    /// </summary>
    /// <param name="settings">검사할 설정</param>
    /// <param name="reason">설정 오류</param>
    /// <returns>항목 유효 여부</returns>
    bool CheckEntry (
        GameDifficultySettings settings , out string reason )
    {
        reason = string.Empty;

        if ( settings == null )
        {
            reason = "설정 항목이 없습니다.";
            return false;
        }

        if ( Enum.IsDefined (
            typeof ( GameDifficulty ) , settings.Difficulty ) == false )
        {
            reason = "등록할 수 없는 난이도입니다.";
            return false;
        }

        if ( settings.Flow == null )
        {
            reason = "게임 흐름 데이터를 연결해 주세요.";
            return false;
        }

        if ( settings.Flow.Difficulty != settings.Difficulty )
        {
            reason = $"흐름 데이터의 난이도가 다릅니다: {settings.Flow.name}";
            return false;
        }

        return true;
    }
}