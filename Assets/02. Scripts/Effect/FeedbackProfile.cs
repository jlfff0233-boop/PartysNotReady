using UnityEngine;

/// <summary>
/// 공통 연출 설정 - 행동별 사운드와 지속 여부
/// </summary>
[CreateAssetMenu( menuName = "GameSettings/Feedback/FeedbackProfile" )]
public class FeedbackProfile : ScriptableObject
{
    /// <summary>
    /// 행동 하나의 사운드 설정
    /// </summary>
    [System.Serializable]
    public struct Entry
    {
        public FeedbackCue Cue;		//행동 종류
        public SoundData Sound;		//사운드, 파티클만 사용하면 미지정
        public bool IsContinuous;		//지속 연출 여부
    }

    [SerializeField] Entry [ ] _entries = System.Array.Empty<Entry>( );		//사용할 행동 설정


    /// <summary>
    /// 행동 설정 조회
    /// </summary>
    /// <param name="cue">행동 종류</param>
    /// <param name="entry">조회한 설정</param>
    /// <returns>설정 존재 여부</returns>
    public bool TryGetEntry ( FeedbackCue cue, out Entry entry )
    {
        for ( int i = 0; i < _entries.Length; i++ )
        {
            if ( _entries [ i ].Cue != cue )
                continue;

            entry = _entries [ i ];
            return true;
        }

        entry = default;
        return false;
    }

    void OnValidate ()
    {
        //빈 행동 키와 중복 설정은 편집 시 확인
        for ( int i = 0; i < _entries.Length; i++ )
        {
            FeedbackCue cue = _entries [ i ].Cue;		//검사할 행동 종류

            if ( cue == FeedbackCue.None )
                Debug.LogError( "연출 설정에 None을 사용할 수 없습니다.", this );

            for ( int j = 0; j < i; j++ )
            {
                if ( _entries [ j ].Cue == cue )
                    Debug.LogError( $"연출 설정이 중복되었습니다: {cue}", this );
            }
        }
    }
}
