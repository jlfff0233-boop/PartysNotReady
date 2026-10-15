using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 발생 가능한 방 중 압력 임계치와 확률로 요청 대상 선정
/// </summary>
class AnomalyRoomSelector
{
    /// <summary>
    /// 임계치 또는 확률을 통과한 방 목록
    /// </summary>
    List<Room> _selectedRooms = new ( );

    /// <summary>
    /// 이전 선정 후보 참조 해제
    /// </summary>
    public void Clear ( )
    {
        _selectedRooms.Clear ( );
    }

    /// <summary>
    /// 유효 후보의 순서를 유지하여 발생 요청 방 선정
    /// </summary>
    /// <param name="eligibleRooms">활성 여부와 발생 자격을 통과한 방</param>
    /// <param name="pressures">방별 압력 상태</param>
    /// <param name="data">압력과 확률 설정</param>
    /// <param name="selectedRoom">선정된 방</param>
    /// <returns>방 선정 여부</returns>
    public bool TrySelect (
        IReadOnlyList<Room> eligibleRooms ,
        IReadOnlyDictionary<Room , AnomalyPressureModel> pressures ,
        AnomalyPressureData data , out Room selectedRoom )
    {
        selectedRoom = null;
        Clear ( );

		//임계치에 도달한 방 중 가장 오래 기다린 방 우선
        float earliest = float.PositiveInfinity;

        foreach ( Room room in eligibleRooms )
        {
            float reachedAt =
                pressures [ room ].ThresholdReachedAt;

            if ( reachedAt < 0f )
                continue;

            if ( reachedAt < earliest )
            {
                earliest = reachedAt;
                _selectedRooms.Clear ( );
            }

            if ( reachedAt == earliest )
                _selectedRooms.Add ( room );
        }

        if ( _selectedRooms.Count == 0 )
        {
		//임계치 도달 방이 없으면 방마다 현재 압력으로 확률 검사
            foreach ( Room room in eligibleRooms )
            {
                float chance =
                    pressures [ room ].Pressure /
                    data.MaxPressure *
                    data.PressureChanceScale;

                if ( Random.value < chance )
                    _selectedRooms.Add ( room );
            }
        }

        if ( _selectedRooms.Count == 0 )
            return false;

		//통과한 방 중 하나만 선택하고 시작 실패 시 재추첨하지 않음
        selectedRoom = _selectedRooms [
            Random.Range ( 0 , _selectedRooms.Count ) ];
        return true;
    }
}
