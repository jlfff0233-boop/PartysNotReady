using System;
using UnityEngine;

/// <summary>
/// 난이도 설정 - 게임 흐름과 시스템별 데이터 연결
/// </summary>
[Serializable]
public class GameDifficultySettings
{
    [Header ( "----- 게임 흐름 -----" )]
    [SerializeField] GameDifficulty _difficulty;        //난이도
    [SerializeField] GameFlowData _flow;        //시간과 미션 단계

    [Header ( "----- 변칙 발생 -----" )]
    [SerializeField] AnomalyPressureData _pressure;     //발생 압력
    [SerializeField] AnomalySpawnDataMap _spawnMap;     //발생 종류

    [Header ( "----- 제작 -----" )]
    [SerializeField] ProduceMinigameDataMap _minigameMap;       //제작 미니게임 데이터맵

    [Header ( "----- 분위기 / 평가 -----" )]
    [SerializeField] MoodData _mood;        //분위기 설정
    [SerializeField] ResultScoreData _score;        //평가 설정

    [Header ( "----- 선택 안내 -----" )]
    [SerializeField, TextArea ( 5 , 10 )] string _prodDescText;       //제작 난이도 안내
    [SerializeField, TextArea ( 5 , 10 )] string _anomalyDescText;      //변칙 발생 안내


    /// <summary>
    /// 난이도
    /// </summary>
    public GameDifficulty Difficulty => _difficulty;

    /// <summary>
    /// 시간과 미션 단계
    /// </summary>
    public GameFlowData Flow => _flow;

    /// <summary>
    /// 발생 압력 설정
    /// </summary>
    public AnomalyPressureData Pressure => _pressure;

    /// <summary>
    /// 발생 종류 목록
    /// </summary>
    public AnomalySpawnDataMap SpawnMap => _spawnMap;

    /// <summary>
    /// 제작 설정 대응표
    /// </summary>
    public ProduceMinigameDataMap MinigameMap => _minigameMap;

    /// <summary>
    /// 분위기 설정
    /// </summary>
    public MoodData Mood => _mood;

    /// <summary>
    /// 평가 설정
    /// </summary>
    public ResultScoreData Score => _score;

    /// <summary>
    /// 제작 난이도 안내
    /// </summary>
    public string ProductionDescription => _prodDescText;

    /// <summary>
    /// 변칙 발생 안내
    /// </summary>
    public string AnomalyDescription => _anomalyDescText;
}