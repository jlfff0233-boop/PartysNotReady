#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이 디버그 기록 - 상태 변화와 테스트 결과 보관
/// </summary>
public static class PlayDebugHistory
{
    const int Capacity = 300;		//최대 기록 수

    /// <summary>
    /// 시간과 대상 이름을 복사한 최근 기록
    /// </summary>
    static readonly List<string> _records = new( Capacity );

    /// <summary>
    /// 일반 기록과 보관 수량을 공유하지 않는 풀 기록
    /// </summary>
    static List<string> _poolRecords = new( Capacity );

    /// <summary>
    /// 콘솔에도 기록할지 여부
    /// </summary>
    public static bool ConsoleEnabled { get; set; }

    /// <summary>
    /// 콘솔 출력 대상 ID, 0이면 전체
    /// </summary>
    public static int ConsoleSourceId { get; set; }

    /// <summary>
    /// 최근 기록
    /// </summary>
    public static IReadOnlyList<string> Records => _records;

    /// <summary>
    /// 최근 풀 생성, 대여, 반환과 제거 기록
    /// </summary>
    public static IReadOnlyList<string> PoolRecords => _poolRecords;

    /// <summary>
    /// 풀 기록 비우기
    /// </summary>
    public static void ClearPool ()
    {
        _poolRecords.Clear( );
    }

    /// <summary>
    /// 기록 비우기
    /// </summary>
    public static void Clear ()
    {
        _records.Clear( );
    }

    /// <summary>
    /// 플레이 시작 시 이전 기록 초기화
    /// </summary>
    [RuntimeInitializeOnLoadMethod( RuntimeInitializeLoadType.SubsystemRegistration )]
    static void Reset ()
    {
        Clear( );
        ClearPool( );
        ConsoleEnabled = false;
        ConsoleSourceId = 0;
    }

    /// <summary>
    /// 현재 상태와 요청 결과 기록
    /// </summary>
    /// <param name="category">기록 분류</param>
    /// <param name="source">발생 대상</param>
    /// <param name="message">변경 내용</param>
    public static void Record ( string category, Object source, string message )
    {
        AddRecord( _records, category, source, message );
    }

    /// <summary>
    /// 풀 상태 변화를 별도 목록에 기록
    /// </summary>
    /// <param name="category">풀 기록 분류</param>
    /// <param name="source">원본 프리팹</param>
    /// <param name="message">변경 내용</param>
    public static void RecordPool ( string category, Object source, string message )
    {
        //풀 생성이 많아도 일반 사건과 실패 기록을 밀어내지 않음
        AddRecord( _poolRecords, category, source, message );
    }

    /// <summary>
    /// 지정 목록의 보관 수량을 제한하고 콘솔 출력 적용
    /// </summary>
    /// <param name="records">추가할 기록 목록</param>
    /// <param name="category">기록 분류</param>
    /// <param name="source">발생 대상</param>
    /// <param name="message">변경 내용</param>
    static void AddRecord ( List<string> records, string category, Object source, string message )
    {
        string name = source != null ? source.name : "없음";		//기록 당시 대상
        string entry = $"{Time.time:F2} [{category}] {name} / {message}";		//보관할 내용
        if ( records.Count == Capacity )
            records.RemoveAt( 0 );
        records.Add( entry );
        if ( ConsoleEnabled == true && ( ConsoleSourceId == 0 ||
             ( source != null && source.GetInstanceID( ) == ConsoleSourceId ) ) )
            Debug.Log( entry, source );
    }
}
#endif
