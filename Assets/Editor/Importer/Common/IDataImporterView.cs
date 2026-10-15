using System;
using UnityEngine;

/// <summary>
/// 임포터 검사 / 적용 상태
/// </summary>
public enum DataImportState
{
    Unchecked,      //미검사
    Ready,          //검사 통과
    Invalid,        //검사 오류
    Applied,        //적용 완료
    Failed          //적용 실패
}

/// <summary>
/// 개별 임포터의 화면과 적용 결과를 통합 창에 제공
/// </summary>
public interface IDataImporterView
{
    /// <summary>
    /// 목록에 표시할 이름
    /// </summary>
    string ImporterName { get; }
    /// <summary>
    /// 현재 원본 파일
    /// </summary>
    string SourcePath { get; }
    /// <summary>
    /// 검사 / 적용 상태
    /// </summary>
    DataImportState ImportState { get; }
    /// <summary>
    /// 성공한 적용 결과 Map
    /// </summary>
    ScriptableObject ResultMap { get; }
    /// <summary>
    /// 선행 항목에서 연결할 참조 Map 종류
    /// </summary>
    Type ReferenceMapType { get; }
    /// <summary>
    /// 검사 / 적용 상태 변경 알림
    /// </summary>
    event Action<IDataImporterView> OnStateChanged;

    /// <summary>
    /// 입력 / 검사 / 적용 화면 표시
    /// </summary>
    void DrawGUI ();

    /// <summary>
    /// 이전 검사와 적용 완료 표시 해제
    /// </summary>
    void InvalidatePreview ();

    /// <summary>
    /// 선행 항목의 참조 Map 연결
    /// </summary>
    /// <param name="map">참조 Map 또는 연결 해제 null</param>
    /// <param name="locked">선행 연결 중 수동 변경 차단 여부</param>
    void SetReferenceMap ( ScriptableObject map, bool locked = false );

    /// <summary>
    /// 대상 Map의 저장 경로 조회
    /// </summary>
    /// <param name="path">유효한 Map 경로</param>
    /// <returns>경로 설정 유효 여부</returns>
    bool TryGetTargetMapPath ( out string path );
}
