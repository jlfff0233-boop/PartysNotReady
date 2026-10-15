using System;
using UnityEngine;

/// <summary>
/// 사건 설정 맵 - 사건 종류별 설정 SO 조회
/// </summary>
[CreateAssetMenu ( menuName = "GameSettings/Anomaly/EventSettingsDataMap" )]
public class EventSettingsDataMap : ScriptableObject
{
    [SerializeField] EventSettingsMapEntry [ ] _entries = 
        Array.Empty<EventSettingsMapEntry> ( );     //사건 종류별 설정 목록

    /// <summary>
    /// 사건 종류에 등록된 설정 SO 조회
    /// </summary>
    /// <param name="type">조회할 사건 종류</param>
    /// <param name="settings">해당 사건의 설정 SO</param>
    /// <param name="reason">조회 또는 설정 실패 사유</param>
    /// <returns>등록된 설정 조회 여부</returns>
    public bool TryGetSettings (
        AnomalyEventType type ,
        out AnomalyEventSettings settings , 
        out string reason )
    {
        settings = null;
        reason = string.Empty;

        bool found = false;
        AnomalyEventSettings foundSettings = null;

        if ( _entries != null )
        {
            foreach ( EventSettingsMapEntry entry in _entries )
            {
                if ( entry.Type != type )
                    continue;

                //먼저 찾은 항목을 바로 반환하지 않고
                //같은 종류의 중복 등록 확인
                if ( found == true )
                {
                    reason = $"{type} 설정이 중복 등록되어 있습니다.";
                    return false;
                }

                found = true;
                foundSettings = entry.Settings;
            }
        }

        if ( found == false )
        {
            reason = $"{type} 설정이 등록되어 있지 않습니다.";
            return false;
        }

        if ( foundSettings == null )
        {
            reason = $"{type}에 연결된 설정 SO가 없습니다.";
            return false;
        }

        //여기서는 연결만 확인하고 시간 값 검사는 설정을 읽을 때 수행
        settings = foundSettings;
        return true;
    }
}
