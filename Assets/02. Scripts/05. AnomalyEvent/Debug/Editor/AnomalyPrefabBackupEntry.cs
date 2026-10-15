using System;

/// <summary>
/// 원본 복구 기록 - 원본 식별 정보와 적용 전후 상태 보관
/// </summary>
[Serializable]
public class AnomalyPrefabBackupEntry
{
    public string Guid;		//원본 식별자
    public string Path;		//적용 시 원본 경로
    public string ClonePath;		//수정 전 프리팹 복제본 경로
    public string Package;		//복구 패키지 이름
    public string PackageHash;		//복구 자료 무결성 확인값
    public string BeforeHash;		//적용 전 원본 내용 확인값
    public string AfterHash;		//저장 시도 이후 원본 내용 확인값
    public bool Restored;		//복구 완료 여부
}
