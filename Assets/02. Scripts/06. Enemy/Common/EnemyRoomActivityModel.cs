using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 방 활동 모델 - 방별 저지 기록, 보복/이동 선택
/// </summary>
public class EnemyRoomActivityModel
{
    int _requiredCount;     //저지 횟수
    float _retaliationChance;     //보복 확률

    /// <summary>
    /// 방별 누적 저지 횟수 딕셔너리(방, 저지 횟수)
    /// </summary>
    Dictionary<Room, int> _counts = new( );
    /// <summary>
    /// 방별 분기에 반영한 횟수 딕셔너리(방, 저지 횟수)
    /// </summary>
    Dictionary<Room, int> _handledCounts = new( );

    Room _currentRoom;     //현재 활동 중인 방
    EnemyRoomActionType _selectedAction;     //이번 활동에서 선택한 행동

    /// <summary>
    /// 현재 활동 중인 방
    /// </summary>
    public Room CurrentRoom => _currentRoom;

    /// <summary>
    /// 보복 분기에 필요한 추가 저지 횟수
    /// </summary>
    public int RequiredCount => _requiredCount;

    /// <summary>
    /// 이번 활동에서 선택한 행동
    /// </summary>
    public EnemyRoomActionType SelectedAction => _selectedAction;


    /// <summary>
    /// 저지 기준과 보복 확률 설정
    /// </summary>
    /// <param name="requiredCount">분기에 필요한 추가 저지 횟수</param>
    /// <param name="retaliationChance">보복 선택 확률</param>
    public EnemyRoomActivityModel (
        int requiredCount, float retaliationChance )
    {
        _requiredCount = Mathf.Max( 1, requiredCount );
        _retaliationChance = Mathf.Clamp01( retaliationChance );
    }


    #region ----- 저지 -----


    /// <summary>
    /// 방에서 발생한 저지 한 번 기록
    /// </summary>
    /// <param name="room">저지가 발생한 방</param>
    public void RecordInterruption ( Room room )
    {
        if ( room == null )
            return;

        int count = GetCount( room );

        //장시간 진행해도 누적값이 음수로 넘치지 않도록 제한
        if ( count == int.MaxValue )
            return;

        _counts [ room ] = count + 1;
    }

    /// <summary>
    /// 분기 기준에 도달한 저지 횟수를 처리한 것으로 기록
    /// </summary>
    void MarkHandled ()
    {
        int count = GetCount( _currentRoom );

        //기준 미만의 나머지는 다음 분기에 유지
        //예: 기준 3회, 누적 4회이면 3회만 처리
        _handledCounts [ _currentRoom ] =
            count - count % _requiredCount;
    }

    /// <summary>
    /// 방의 누적 저지 횟수 조회
    /// </summary>
    /// <param name="room">조회할 방</param>
    /// <returns>누적 저지 횟수</returns>
    public int GetCount ( Room room )
    {
        if ( room == null )
            return 0;

        return _counts.TryGetValue( room, out int count )
            ? count : 0;
    }

    #endregion

    #region ----- 행동 -----

    /// <summary>
    /// 지정한 방에서 새 활동 시작
    /// </summary>
    /// <param name="room">활동할 방</param>
    /// <returns>활동 시작 여부</returns>
    public bool BeginActivity ( Room room )
    {
        _currentRoom = room;
        _selectedAction = EnemyRoomActionType.None;

        //방을 다시 방문해도 누적 기록과 처리한 기준은 유지
        return _currentRoom != null;
    }


    /// <summary>
    /// 행동 선택(보복, 방 이동)
    /// </summary>
    /// <param name="canRetaliate">보복 가능 여부</param>
    /// <param name="randomValue">0부터 1 사이의 난수</param>
    /// <param name="action">이번 활동에서 선택한 행동</param>
    /// <returns>선택 결과 제공 여부</returns>
    public bool TrySelectAction (
        bool canRetaliate, float randomValue,
        out EnemyRoomActionType action )
    {
        action = EnemyRoomActionType.None;

        if ( _currentRoom == null )
            return false;

        //같은 활동에서는 다시 호출해도 재추첨하지 않음
        if ( _selectedAction != EnemyRoomActionType.None )
        {
            action = _selectedAction;
            return true;
        }

        _selectedAction =
            canRetaliate == true && ShouldRetaliate( randomValue )
                ? EnemyRoomActionType.Retaliate
                : EnemyRoomActionType.MoveRoom;

        //방 이동을 선택해도 이번 저지 기준은 처리한 것으로 기록
        MarkHandled( );

        action = _selectedAction;
        return true;
    }

    /// <summary>
    /// 현재 활동의 방과 선택 결과 정리
    /// </summary>
    public void EndActivity ()
    {
        _currentRoom = null;
        _selectedAction = EnemyRoomActionType.None;

        //도주나 방 이동으로 저지 기록을 초기화하지 않음
    }

    /// <summary>
    /// 게임 초기화 시 모든 방의 저지 기록 정리
    /// </summary>
    public void Clear ()
    {
        EndActivity( );
        _counts.Clear( );
        _handledCounts.Clear( );
    }


    #endregion

    #region ----- 상태 검사 -----

    /// <summary>
    /// 아직 처리하지 않은 저지가 기준 횟수에 도달했는지 확인
    /// </summary>
    /// <param name="room">검사할 방</param>
    /// <returns>새 보복 분기 가능 여부</returns>
    public bool HasReachedThreshold ( Room room )
    {
        if ( room == null )
            return false;

        int count = GetCount( room );
        _handledCounts.TryGetValue( room, out int handledCount );

        return count - handledCount >= _requiredCount;
    }

    /// <summary>
    /// 보복 확률에 해당하는지 확인
    /// </summary>
    /// <param name="randomValue">0부터 1 사이의 난수</param>
    /// <returns>보복 선택 여부</returns>
    bool ShouldRetaliate ( float randomValue )
    {
        if ( _retaliationChance <= 0f )
            return false;

        //난수가 1이어도 확률 100%를 보장
        if ( _retaliationChance >= 1f )
            return true;

        return randomValue < _retaliationChance;
    }

    #endregion
#if UNITY_EDITOR
    /// <summary>
    /// 지정 방에서 독립된 반복 테스트를 위한 저지 기록 정리
    /// </summary>
    /// <param name="room">테스트 방</param>
    public void ResetDebugCounts ( Room room )
    {
        _counts.Remove( room );
        _handledCounts.Remove( room );
    }
#endif

}
