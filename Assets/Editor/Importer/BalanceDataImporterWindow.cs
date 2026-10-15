using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 밸런스 데이터와 난이도 및 곰 취향 연결의 입력 종류
/// </summary>
public enum BalanceImportKind
{
    Pressure,       //발생 압박
    Mood,           //분위기
    Score,          //결과 평가
    FoodMap,        //음식 목록
    Difficulty,     //세 난이도 연결
    Bear            //곰 취향 연결
}

/// <summary>
/// CSV와 JSON으로 밸런스 데이터를 생성하고 지정한 기존 자산에 연결
/// </summary>
public class BalanceDataImporterWindow : DataImporterWindow, IDataImporterAssetSource
{
    [SerializeField] BalanceImportKind _kind;        //입력 종류
    [SerializeField] ScriptableObject _target;        //연결을 갱신할 기존 자산
    bool _fixedKind;        //통합 창에서 종류 변경 차단
    string[] _names = { "발생 압박", "분위기", "결과 평가", "음식맵", "난이도 연결", "곰 취향 연결" };        //종류 표시

    /// <summary>
    /// 종류별 입력 형식
    /// </summary>
    protected override string DataType => _kind switch
    {
        BalanceImportKind.Pressure => "AnomalyPressureData",
        BalanceImportKind.Mood => "MoodData",
        BalanceImportKind.Score => "ResultScoreData",
        BalanceImportKind.FoodMap => "FoodDataMap",
        BalanceImportKind.Difficulty => "GameDifficultyDataMap",
        _ => "EnemyDemandLinks"
    };
    /// <summary>
    /// 현재 임포터 이름
    /// </summary>
    protected override string DisplayName => _names[(int)_kind] + " 임포터";
    /// <summary>
    /// 신규 데이터 저장 폴더
    /// </summary>
    protected override string DefaultOutputFolder => _kind switch
    {
        BalanceImportKind.Pressure => "Assets/03. Configs/!_GameFlow/Pressure",
        BalanceImportKind.Mood => "Assets/03. Configs/!_GameFlow/Mood",
        BalanceImportKind.Score => "Assets/03. Configs/!_GameFlow/Result",
        BalanceImportKind.FoodMap => "Assets/03. Configs/Enemy/FoodMaps",
        _ => "Assets/03. Configs/!_GameFlow"
    };
    /// <summary>
    /// 개별 자산으로 처리하므로 공통 목록맵은 사용하지 않음
    /// </summary>
    protected override Type MapType => null;
    /// <summary>
    /// 공통 목록맵 이름은 사용하지 않음
    /// </summary>
    protected override string DefaultMapName => null;
    /// <summary>
    /// 난이도 및 곰 연결 입력의 기존 대상
    /// </summary>
    protected override ScriptableObject PresenceTarget =>
        _kind == BalanceImportKind.Difficulty || _kind == BalanceImportKind.Bear ? _target : null;
    /// <summary>
    /// 생성 및 갱신 결과의 자산 종류
    /// </summary>
    public Type ResultAssetType => BalanceDataAssetImporter.GetAssetType( _kind );
    /// <summary>
    /// 현재 검사한 생성 및 갱신 대상
    /// </summary>
    public IReadOnlyList<DataImportAssetChange> TargetAssets => PreparedAssets;
    /// <summary>
    /// 마지막 적용의 ID별 자산
    /// </summary>
    public IReadOnlyList<DataImportReference> ResultAssets => AppliedAssets;

    /// <summary>
    /// 통합 창에 맞춰 입력 종류 고정
    /// </summary>
    /// <param name="kind">입력 종류</param>
    public void Configure ( BalanceImportKind kind )
    {
        _kind = kind;
        _fixedKind = true;
        OutputFolder = DefaultOutputFolder;
        InvalidatePreview( );
    }

    /// <summary>
    /// 종류와 기존 연결 대상을 표시
    /// </summary>
    protected override void DrawOptions ()
    {
        if ( _fixedKind == false )
        {
            BalanceImportKind selected = (BalanceImportKind)EditorGUILayout.Popup( "입력 종류", (int)_kind, _names );
            if ( selected != _kind )
            {
                _kind = selected;
                _target = null;
                OutputFolder = DefaultOutputFolder;
                InvalidatePreview( );
            }
        }
        if ( _kind == BalanceImportKind.Difficulty || _kind == BalanceImportKind.Bear )
        {
            _target = (ScriptableObject)EditorGUILayout.ObjectField( "갱신할 기존 자산", _target, ResultAssetType, false );
            EditorGUILayout.HelpBox( "대상을 지정하면 기존 자산을 갱신합니다. 난이도는 3종의 데이터 연결만, 곰은 취향맵 연결만 변경합니다.", MessageType.Info );
        }
        EditorGUILayout.HelpBox( "권장 순서: 압박 → 분위기 → 평가 → 음식맵 → 난이도 연결 → 곰 취향 연결. 연결 입력은 앞선 데이터 적용 후 검사합니다.", MessageType.Info );
    }

    /// <summary>
    /// 입력 검사와 적용 계획 구성
    /// </summary>
    /// <param name="rows">입력 행</param>
    /// <param name="report">검사 내역</param>
    /// <returns>개별 자산 적용 계획</returns>
    protected override DataImportPlan Prepare ( List<DataImportRow> rows, DataImportReport report )
    {
        return BalanceDataAssetImporter.Prepare( _kind, rows, OutputFolder, Bindings, _target, report );
    }

    /// <summary>
    /// 개별 밸런스 임포터 열기
    /// </summary>
    [MenuItem( "Tools/Importer/Balance and Difficulty" )]
    public static void Open ()
    {
        GetWindow<BalanceDataImporterWindow>( "밸런스 / 난이도 임포터" ).minSize = new Vector2( 600f, 420f );
    }
}
