using System;
using System.Collections.Generic;

/// <summary>
/// 일괄 적용 복구 묶음 - 작업 시각과 원본별 복구 기록 보관
/// </summary>
[Serializable]
public class AnomalyPrefabBackupBatch
{
    public string Id;		//복구 묶음 식별자
    public string Time;		//작업 시작 시각
    public bool LinkedAssets;		//원본과 연관 에셋을 함께 복구할지 여부
    /// <summary>
    /// 적용 전 보관한 원본별 복구 기록
    /// </summary>
    public List<AnomalyPrefabBackupEntry> Entries = new( );
}
