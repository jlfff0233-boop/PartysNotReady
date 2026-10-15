using UnityEngine;

/// <summary>
/// 미션 맵 - 단계 이름과 미션 후보 목록
/// </summary>
[CreateAssetMenu ( menuName = "GameSettings/GameFlow/MissionDataMap" )]
public class MissionDataMap : ScriptableObject
{
    [SerializeField] string _name;     //단계 이름
    [SerializeField] MissionData [ ] _missions;		//단계에서 선정할 미션 후보

    /// <summary>
    /// 단계 이름
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// 미션 수
    /// </summary>
    public int MissionCount => _missions == null ? 0 : _missions.Length;

    /// <summary>
    /// 미션 조회
    /// </summary>
    /// <param name="index">미션 번호</param>
    /// <returns>미션 설정, 범위 밖이면 null</returns>
    public MissionData GetMission ( int index )
    {
        if ( index < 0 || index >= MissionCount )
            return null;

        return _missions [ index ];
    }
}
