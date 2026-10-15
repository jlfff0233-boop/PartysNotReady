using UnityEngine;

/// <summary>
/// 결과 점수 계산 - 평가축 가중 점수와 등급 산출
/// </summary>
public class ResultScoreCalculator
{
    /// <summary>
    /// 점수 및 등급 계산
    /// </summary>
    /// <param name="data">평가 설정</param>
    /// <param name="values">준비, 안전, 분위기, 포식자 대응 점수</param>
    /// <param name="included">축별 평가 포함 여부</param>
    /// <param name="scores">정수 점수 항목</param>
    /// <param name="grade">총점 등급</param>
    /// <returns>계산 성공 여부</returns>
    public bool TryCalculate (
        ResultScoreData data,
        float [ ] values, bool [ ] included,
        out GameScoreResult [ ] scores,
        out string grade )
    {
        scores = null;
        grade = null;

        if ( data == null || data.IsValid == false ||
             values == null || values.Length != 4 ||
             included == null || included.Length != 4 )
            return false;

        float weightSum = 0f;
        for ( int i = 0; i < 4; i++ )
        {
            if ( included [ i ] == false )
                continue;

            if ( float.IsNaN( values [ i ] ) ||
                 float.IsInfinity( values [ i ] ) )
                return false;

            weightSum += data.GetWeight( i );
        }

        if ( weightSum <= 0f )
            return false;

        int [ ] points = new int [ 4 ];
        float [ ] fractions = new float [ 4 ];
        float total = 0f;
        int assigned = 0;

        //제외 축의 가중치를 남은 축에 재배분
        for ( int i = 0; i < 4; i++ )
        {
            if ( included [ i ] == false )
                continue;

            float weighted = Mathf.Clamp( values [ i ], 0f, 100f ) *
                data.GetWeight( i ) / weightSum;
            points [ i ] = Mathf.FloorToInt( weighted );
            fractions [ i ] = weighted - points [ i ];
            total += weighted;
            assigned += points [ i ];
        }

        //반올림 차이를 소수 부분이 큰 축부터 배분
        int roundedTotal = Mathf.RoundToInt( total );
        for ( int remaining = roundedTotal - assigned; remaining > 0; remaining-- )
        {
            int selected = -1;
            for ( int i = 0; i < 4; i++ )
            {
                if ( included [ i ] == false )
                    continue;

                if ( selected < 0 || fractions [ i ] > fractions [ selected ] )
                    selected = i;
            }

            points [ selected ]++;
            fractions [ selected ] = -1f;
        }

        string [ ] names =
        {
            "Preparation", "Safety", "Mood", "Predator Control"
        };

        scores = new GameScoreResult [ 4 ];
        for ( int i = 0; i < scores.Length; i++ )
            scores [ i ] = new GameScoreResult(
                names [ i ], points [ i ], included [ i ] == false );

        grade = data.GetGrade( roundedTotal );
        return true;
    }
}
