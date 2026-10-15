using UnityEngine;

/// <summary>
/// 결과 평가 가중치와 등급 기준
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/Result/ScoreData" )]
public class ResultScoreData : ScriptableObject
{
    [Header("----- 가중치 -----")]
    [SerializeField, Min( 0f )] float _preparationWeight;		//준비 가중치
    [SerializeField, Min( 0f )] float _safetyWeight;		//안전 가중치
    [SerializeField, Min( 0f )] float _moodWeight;		//분위기 가중치
    [SerializeField, Min( 0f )] float _predatorWeight;		//대응 가중치
    [SerializeField, Range( 0f, 1f )] float _moodAverageRatio;		//분위기 평균 반영 비율

    [Header("----- 점수 -----")]
    [SerializeField, Range( 0, 100 )] int _gradeA;		//A등급 최소 점수
    [SerializeField, Range( 0, 100 )] int _gradeB;		//B등급 최소 점수
    [SerializeField, Range( 0, 100 )] int _gradeC;		//C등급 최소 점수

    /// <summary>
    /// 분위기 평균 반영 비율
    /// </summary>
    public float MoodAverageRatio => _moodAverageRatio;

    /// <summary>
    /// 평가 설정 확인
    /// </summary>
    public bool IsValid => CheckValid( );

    /// <summary>
    /// 평가축 가중치 조회
    /// </summary>
    /// <param name="index">평가축 번호</param>
    /// <returns>해당 평가축 가중치</returns>
    public float GetWeight ( int index )
    {
        return index switch
        {
            0 => _preparationWeight,
            1 => _safetyWeight,
            2 => _moodWeight,
            3 => _predatorWeight,
            _ => 0f
        };
    }

    /// <summary>
    /// 총점의 등급 조회
    /// </summary>
    /// <param name="score">정수 총점</param>
    /// <returns>총점 등급</returns>
    public string GetGrade ( int score )
    {
        if ( score >= _gradeA ) return "A";
        if ( score >= _gradeB ) return "B";
        if ( score >= _gradeC ) return "C";
        return "D";
    }


    /// <summary>
    /// 가중치와 등급 경계 확인
    /// </summary>
    /// <returns>설정 유효 여부</returns>
    bool CheckValid ()
    {
        return float.IsInfinity( _preparationWeight ) == false &&
        float.IsInfinity( _safetyWeight ) == false &&
        float.IsInfinity( _moodWeight ) == false &&
        float.IsInfinity( _predatorWeight ) == false &&
        float.IsNaN( _moodAverageRatio ) == false &&
        _moodAverageRatio >= 0f && _moodAverageRatio <= 1f &&
        _preparationWeight >= 0f &&
        _safetyWeight >= 0f &&
        _moodWeight >= 0f &&
        _predatorWeight >= 0f &&
        _preparationWeight + _safetyWeight +
        _moodWeight + _predatorWeight > 0f &&
        _gradeA > _gradeB &&
        _gradeB > _gradeC;
    }
}
