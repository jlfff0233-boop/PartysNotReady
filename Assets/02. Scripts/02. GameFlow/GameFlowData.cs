using System;
using UnityEngine;

/// <summary>
/// 게임 진행 데이터 - 난이도, 제한시간과 미션 맵
/// </summary>
[CreateAssetMenu ( menuName = "GameSettings/GameFlow/GameFlowData" )]
public class GameFlowData : ScriptableObject
{
    [SerializeField, Min ( 1f )] float _duration = 600f;     //제한시간
    [SerializeField, Min ( 0f )] float _finalRushDuration = 30f;     //종료 전 마무리 구간
    [SerializeField] GameDifficulty _difficulty = GameDifficulty.Normal;     //난이도
    [SerializeField] MissionStageSettings [ ] _stages;		//단계별 미션 선정 설정

    /// <summary>
    /// 제한시간
    /// </summary>
    public float Duration => Mathf.Max ( 1f , _duration );
    /// <summary>
    /// 종료 전 마무리 구간
    /// </summary>
    public float FinalRushDuration =>
        Mathf.Clamp ( _finalRushDuration , 0f , Duration );
    /// <summary>
    /// 난이도
    /// </summary>
    public GameDifficulty Difficulty => _difficulty;

    /// <summary>
    /// 미션 단계 수
    /// </summary>
    public int StageCount => _stages == null ? 0 : _stages.Length;

    /// <summary>
    /// 단계별 미션 설정 조회
    /// </summary>
    /// <param name="index">단계 번호</param>
    /// <returns>단계 설정, 범위 밖이면 null</returns>
    public MissionStageSettings GetStageSettings ( int index )
    {
        if ( index < 0 || index >= StageCount )
            return null;

        return _stages [ index ];
    }

    /// <summary>
    /// 단계별 미션 후보 맵 조회
    /// </summary>
    /// <param name="index">단계 번호</param>
    /// <returns>미션 맵, 설정이 없으면 null</returns>
    public MissionDataMap GetMissionMap ( int index )
    {
        MissionStageSettings settings = GetStageSettings ( index );		//단계 설정
        return settings == null ? null : settings.MissionMap;
    }

    /// <summary>
    /// 전체 미션 단계 설정 검사
    /// </summary>
    /// <param name="reason">설정 오류</param>
    /// <returns>설정 유효 여부</returns>
    public bool CheckSettings ( out string reason )
    {
        reason = string.Empty;

        if ( StageCount == 0 )
        {
            reason = $"{name}: 미션 단계를 설정해 주세요.";
            return false;
        }

        for ( int i = 0 ; i < StageCount ; i++ )
        {
            MissionStageSettings settings = _stages [ i ];		//검사할 단계

            if ( settings == null )
            {
                reason = $"{name} / 미션 {i + 1}단계: 단계 설정이 없습니다.";
                return false;
            }

            if ( settings.CheckSettings ( out reason ) == false )
            {
                reason = $"{name} / 미션 {i + 1}단계: {reason}";
                return false;
            }
        }

        return true;
    }
}
