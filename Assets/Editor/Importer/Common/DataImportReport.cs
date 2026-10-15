using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 데이터 임포트 검사와 예정 변경 내역
/// </summary>
public class DataImportReport
{
    /// <summary>
    /// 적용을 막는 입력 오류
    /// </summary>
    public List<string> Errors = new( );
    /// <summary>
    /// 적용 전에 확인할 사항
    /// </summary>
    public List<string> Warnings = new( );
    /// <summary>
    /// 생성 / 수정 / 보존 / 목록 제외 예정 내역
    /// </summary>
    public List<string> Details = new( );

    /// <summary>
    /// 입력 오류 없이 적용할 수 있는지 여부
    /// </summary>
    public bool CanImport => Errors.Count == 0;
}

/// <summary>
/// 데이터 에셋과 Map의 실제 적용 결과
/// </summary>
public class DataImportResult
{
    public int CreatedCount;		//생성 수
    public int UpdatedCount;		//갱신 수
    public ScriptableObject DataMap;		//적용한 Map
    public bool MapApplied;		//목록 반영 여부
    public string Error;		//실패 단계와 사유
    /// <summary>
    /// 실제 변경과 저장 내역
    /// </summary>
    public List<string> Changes = new( );
}

/// <summary>
/// 원본 식별자에 직접 연결할 기존 에셋
/// </summary>
public class DataImportReference
{
    public string Id;		//원본 식별자
    public Object Asset;		//기존 에셋
}
