using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 제작 미니게임 데이터 맵 - 기본 설정과 적용 설정 연결
/// </summary>
[CreateAssetMenu ( menuName = 
    "GameSettings/Production/ProduceMinigameDataMap" )]
public class ProduceMinigameDataMap : ScriptableObject
{
    [SerializeField] ProduceMinigameMapEntry [ ] _data;       //기본 설정별 대응표


    /// <summary>
    /// 제작 설정 연결과 중복 확인
    /// </summary>
    /// <param name="reason">설정 오류</param>
    /// <returns>설정 유효 여부</returns>
    public bool CheckSettings ( out string reason )
    {
        reason = string.Empty;

        if ( _data == null || _data.Length == 0 )
        {
            reason = "제작 설정 대응표가 비어 있습니다.";
            return false;
        }

        HashSet<ProduceMinigameData> sources = new ( );		//등록한 기본 설정

        for ( int i = 0 ; i < _data.Length ; i++ )
        {
            ProduceMinigameMapEntry entry = _data [ i ];		//검사할 대응 설정

            if ( CheckEntry ( entry , out reason ) == false )
            {
                reason = $"{i + 1}번째 제작 설정: {reason}";
                return false;
            }

            if ( sources.Add ( entry.Source ) == false )
            {
                reason = $"기본 제작 설정이 중복됐습니다: {entry.Source.name}";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 대응 설정의 연결과 제작 유형 확인
    /// </summary>
    /// <param name="entry">검사할 대응 설정</param>
    /// <param name="reason">설정 오류</param>
    /// <returns>항목 유효 여부</returns>
    bool CheckEntry ( ProduceMinigameMapEntry entry , out string reason )
    {
        reason = string.Empty;

        if ( entry.Source == null || entry.Data == null )
        {
            reason = "기본 설정과 적용 설정을 연결해 주세요.";
            return false;
        }

        if ( entry.Source.MinigameType != entry.Data.MinigameType )
        {
            reason = $"제작 유형이 다릅니다: {entry.Source.name} / {entry.Data.name}";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 기본 설정에 대응하는 제작 데이터 조회
    /// </summary>
    /// <param name="source">레시피의 기본 설정</param>
    /// <param name="data">이번 난이도의 적용 설정</param>
    /// <returns>조회 성공 여부</returns>
    public bool TryGetData (
        ProduceMinigameData source , out ProduceMinigameData data )
    {
        foreach ( ProduceMinigameMapEntry entry in _data )
        {
            if ( entry.Source != source )
                continue;

            data = entry.Data;
            return true;
        }

        data = null;
        return false;
    }
}