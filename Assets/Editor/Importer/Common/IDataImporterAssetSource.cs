using System;
using System.Collections.Generic;

/// <summary>
/// Map 없이 ID별 적용 에셋을 제공하는 임포터
/// </summary>
public interface IDataImporterAssetSource
{
    /// <summary>
    /// 결과 에셋 종류
    /// </summary>
    Type ResultAssetType { get; }
    /// <summary>
    /// 현재 검사한 생성 / 갱신 대상
    /// </summary>
    IReadOnlyList<DataImportAssetChange> TargetAssets { get; }
    /// <summary>
    /// 마지막 적용에서 저장을 시도한 ID와 실제 SO
    /// </summary>
    IReadOnlyList<DataImportReference> ResultAssets { get; }
}

/// <summary>
/// 선행 항목의 ID별 에셋을 참조하는 임포터
/// </summary>
public interface IDataImporterAssetReference
{
    /// <summary>
    /// 연결할 에셋 종류
    /// </summary>
    Type ReferenceAssetType { get; }

    /// <summary>
    /// 선행 적용 결과 연결 또는 해제
    /// </summary>
    /// <param name="assets">ID별 SO 또는 선행 미적용 null</param>
    /// <param name="locked">선행 연결 사용 여부</param>
    void SetReferenceAssets ( IReadOnlyList<DataImportReference> assets, bool locked );
}
