using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 밸런스 원본 검사와 기존 자산을 보존하는 생성 및 연결 계획
/// </summary>
public static class BalanceDataAssetImporter
{
    /// <summary>
    /// 종류별 결과 자산 타입 조회
    /// </summary>
    /// <param name="kind">입력 종류</param>
    /// <returns>결과 타입</returns>
    public static Type GetAssetType ( BalanceImportKind kind )
    {
        return kind switch
        {
            BalanceImportKind.Pressure => typeof( AnomalyPressureData ),
            BalanceImportKind.Mood => typeof( MoodData ),
            BalanceImportKind.Score => typeof( ResultScoreData ),
            BalanceImportKind.FoodMap => typeof( FoodDataMap ),
            BalanceImportKind.Difficulty => typeof( GameDifficultyDataMap ),
            BalanceImportKind.Bear => typeof( EnemyDemandData ),
            _ => throw new ArgumentOutOfRangeException( nameof( kind ) )
        };
    }

    /// <summary>
    /// 직접 연결 또는 고유한 자산 이름으로 갱신 대상 조회
    /// </summary>
    /// <param name="id">입력 ID</param>
    /// <param name="bindings">직접 연결</param>
    /// <returns>기존 자산 또는 신규 생성 null</returns>
    static T FindExisting<T> ( string id, List<DataImportReference> bindings ) where T : ScriptableObject
    {
        List<T> assets = DataImportAssetUtility.FindAssets<T>( );
        if ( bindings.Exists( binding => binding.Id == id ) == true || assets.Exists( asset => asset != null && asset.name == id ) == true )
            return DataImportAssetUtility.Resolve( id, bindings, assets, asset => asset.name );
        return null;
    }

    /// <summary>
    /// 필수 참조를 기존 자산에서 조회
    /// </summary>
    /// <param name="row">입력 행</param>
    /// <param name="field">참조 필드</param>
    /// <param name="bindings">직접 연결</param>
    /// <returns>연결할 자산</returns>
    static T Resolve<T> ( DataImportRow row, string field, List<DataImportReference> bindings ) where T : ScriptableObject
    {
        return DataImportAssetUtility.Resolve( row.Text( field ), bindings, DataImportAssetUtility.FindAssets<T>( ), asset => asset.name );
    }

    /// <summary>
    /// 미리보기에서 필드 존재와 현재 값 및 적용 값을 확인
    /// </summary>
    /// <param name="change">변경 대상</param>
    /// <param name="field">직렬화 필드</param>
    /// <param name="value">적용 값</param>
    /// <param name="report">검사 결과</param>
    static void Describe ( DataImportAssetChange change, string field, object value, DataImportReport report )
    {
        string previous = "신규";
        if ( change.Asset != null )
        {
            using SerializedObject serialized = new( change.Asset );
            SerializedProperty property = serialized.FindProperty( field );
            if ( property == null )
                throw new InvalidOperationException( $"직렬화 필드가 없습니다: {change.Asset.name}.{field}" );
            previous = property.propertyType switch
            {
                SerializedPropertyType.ObjectReference => property.objectReferenceValue == null ? "없음" : property.objectReferenceValue.name,
                SerializedPropertyType.Float => property.floatValue.ToString( System.Globalization.CultureInfo.InvariantCulture ),
                SerializedPropertyType.Integer => property.intValue.ToString( ),
                _ => property.isArray == true ? property.arraySize + "개" : property.stringValue
            };
        }
        report.Details.Add( $"{change.Id}.{field}: {previous} → {value}" );
    }

    /// <summary>
    /// 수치 입력과 런타임 범위 계약 검사
    /// </summary>
    /// <param name="kind">수치 종류</param>
    /// <param name="row">입력 행</param>
    /// <returns>원본 열별 수치</returns>
    static Dictionary<string, float> ReadNumbers ( BalanceImportKind kind, DataImportRow row )
    {
        string[] fields = kind switch
        {
            BalanceImportKind.Pressure => new[] { "maxPressure", "pressureChanceScale", "threshold", "checkInterval", "decreaseOnSuccess", "increase", "unvisitedBonus", "roomCooldown", "globalCooldown" },
            BalanceImportKind.Mood => new[] { "initialMood", "missionRecovery", "resolvedRecovery", "rescueRecovery", "failedDecrease", "departureDecrease", "decrease", "decreaseDelay" },
            _ => new[] { "preparationWeight", "safetyWeight", "moodWeight", "predatorWeight", "moodAverageRatio", "gradeA", "gradeB", "gradeC" }
        };
        row.CheckFields( new[] { "id" }.Concat( fields ).ToArray( ) );
        Dictionary<string, float> values = new( StringComparer.Ordinal );
        foreach ( string field in fields )
        {
            float value = row.Number( field );
            if ( value < 0f )
                throw row.Error( field, "0 이상이어야 합니다." );
            if ( field.StartsWith( "grade", StringComparison.Ordinal ) == true )
                value = row.Integer( field );
            values.Add( field, value );
        }
        if ( kind == BalanceImportKind.Pressure )
        {
            if ( values["maxPressure"] <= 0f || values["threshold"] <= 0f || values["threshold"] > values["maxPressure"] ||
                 values["checkInterval"] <= 0f || values["pressureChanceScale"] > 1f )
                throw row.Error( "압박", "최대 압력과 간격은 양수, 임계치는 최대 압력 이하의 양수, 확률 계수는 0~1이어야 합니다." );
        }
        else if ( kind == BalanceImportKind.Mood )
        {
            if ( values["initialMood"] > 100f || values["missionRecovery"] < 4f || values["missionRecovery"] > 8f )
                throw row.Error( "분위기", "시작 분위기는 0~100, 미션 회복은 현재 Inspector 계약인 4~8이어야 합니다." );
        }
        else
        {
            float sum = values["preparationWeight"] + values["safetyWeight"] + values["moodWeight"] + values["predatorWeight"];
            if ( sum <= 0f || float.IsInfinity( sum ) == true || values["moodAverageRatio"] > 1f ||
                 values["gradeA"] > 100f || values["gradeA"] <= values["gradeB"] || values["gradeB"] <= values["gradeC"] )
                throw row.Error( "평가", "가중치 합은 유한한 양수, 평균 비율은 0~1, 등급은 0~100 및 A > B > C여야 합니다." );
        }
        return values;
    }

    /// <summary>
    /// 수치 자산의 생성 또는 갱신 계획 추가
    /// </summary>
    /// <param name="kind">수치 종류</param>
    /// <param name="row">입력 행</param>
    /// <param name="id">입력 ID</param>
    /// <param name="plan">적용 계획</param>
    /// <param name="bindings">직접 연결</param>
    /// <param name="report">검사 내역</param>
    static void AddNumbers<T> ( BalanceImportKind kind, DataImportRow row, string id, DataImportPlan plan,
        List<DataImportReference> bindings, DataImportReport report ) where T : ScriptableObject
    {
        Dictionary<string, float> values = ReadNumbers( kind, row );
        DataImportAssetChange change = DataImportAssetUtility.Change( plan, id, FindExisting<T>( id, bindings ), serialized =>
        {
            foreach ( KeyValuePair<string, float> pair in values )
            {
                SerializedProperty property = serialized.FindProperty( "_" + pair.Key );
                if ( pair.Key.StartsWith( "grade", StringComparison.Ordinal ) == true )
                    property.intValue = (int)pair.Value;
                else
                    property.floatValue = pair.Value;
            }
        } );
        foreach ( KeyValuePair<string, float> pair in values )
            Describe( change, "_" + pair.Key, pair.Value, report );
        plan.Changes.Add( change );
    }

    /// <summary>
    /// 기존 음식 참조만 사용하는 음식맵 계획 추가
    /// </summary>
    /// <param name="row">입력 행</param>
    /// <param name="id">맵 ID</param>
    /// <param name="plan">적용 계획</param>
    /// <param name="bindings">직접 연결</param>
    /// <param name="report">검사 결과</param>
    static void AddFoodMap ( DataImportRow row, string id, DataImportPlan plan, List<DataImportReference> bindings, DataImportReport report )
    {
        row.CheckFields( "id", "foodDatas" );
        List<FoodData> candidates = DataImportAssetUtility.FindAssets<FoodData>( );
        List<FoodData> foods = new( );
        HashSet<string> ids = new( StringComparer.Ordinal );
        foreach ( string reference in row.List( "foodDatas" ) )
        {
            FoodData food = DataImportAssetUtility.Resolve( reference.Trim( ), bindings, candidates, asset => asset.name );
            if ( string.IsNullOrWhiteSpace( food.FoodId ) == true || ids.Add( food.FoodId ) == false )
                throw row.Error( "foodDatas", "FoodId가 비었거나 중복된 음식은 등록할 수 없습니다." );
            foods.Add( food );
        }
        DataImportAssetChange change = DataImportAssetUtility.Change( plan, id, FindExisting<FoodDataMap>( id, bindings ), serialized =>
        {
            SerializedProperty array = serialized.FindProperty( "_foodDatas" );
            array.arraySize = foods.Count;
            for ( int i = 0; i < foods.Count; i++ )
                array.GetArrayElementAtIndex( i ).objectReferenceValue = foods[i];
        } );
        Describe( change, "_foodDatas", string.Join( ", ", foods.Select( food => food.name ) ) + $" ({foods.Count}개)", report );
        report.Warnings.Add( $"{id}: 음식맵 목록 전체를 입력 목록으로 교체합니다. 음식 데이터 자체는 수정하지 않습니다." );
        plan.Changes.Add( change );
    }

    /// <summary>
    /// 곰 데이터의 취향맵 참조만 갱신
    /// </summary>
    /// <param name="row">입력 행</param>
    /// <param name="id">기존 곰 데이터 ID</param>
    /// <param name="plan">적용 계획</param>
    /// <param name="bindings">직접 연결</param>
    /// <param name="target">명시한 기존 곰 데이터</param>
    /// <param name="report">검사 결과</param>
    static void AddBear ( DataImportRow row, string id, DataImportPlan plan, List<DataImportReference> bindings,
        ScriptableObject target, DataImportReport report )
    {
        row.CheckFields( "id", "likedFoods", "dislikedFoods" );
        EnemyDemandData bear = target as EnemyDemandData ?? Resolve<EnemyDemandData>( row, "id", bindings );
        FoodDataMap liked = Resolve<FoodDataMap>( row, "likedFoods", bindings );
        FoodDataMap disliked = Resolve<FoodDataMap>( row, "dislikedFoods", bindings );
        List<FoodData> likedEntries = DataImportAssetUtility.GetEntries<FoodData>( liked, "_foodDatas" );
        List<FoodData> dislikedEntries = DataImportAssetUtility.GetEntries<FoodData>( disliked, "_foodDatas" );
        HashSet<string> likedIds = new( );
        foreach ( FoodData food in likedEntries )
        {
            if ( food == null || string.IsNullOrWhiteSpace( food.FoodId ) == true || likedIds.Add( food.FoodId ) == false )
                throw row.Error( "likedFoods", "음식맵에 누락 또는 중복 FoodId가 있습니다." );
        }
        HashSet<string> dislikedIds = new( );
        foreach ( FoodData food in dislikedEntries )
        {
            if ( food == null || string.IsNullOrWhiteSpace( food.FoodId ) == true || dislikedIds.Add( food.FoodId ) == false || likedIds.Contains( food.FoodId ) == true )
                throw row.Error( "dislikedFoods", "음식맵에 누락, 중복 또는 좋아하는 음식과 겹치는 FoodId가 있습니다." );
        }
        DataImportAssetChange change = DataImportAssetUtility.Change( plan, id, bear, serialized =>
        {
            serialized.FindProperty( "_likedFoods" ).objectReferenceValue = liked;
            serialized.FindProperty( "_dislikedFoods" ).objectReferenceValue = disliked;
        } );
        Describe( change, "_likedFoods", liked.name, report );
        Describe( change, "_dislikedFoods", disliked.name, report );
        plan.Changes.Add( change );
    }

    /// <summary>
    /// 세 난이도의 시스템 참조를 검사하고 기존 안내 문구를 보존하여 연결
    /// </summary>
    /// <param name="rows">세 난이도 입력</param>
    /// <param name="plan">적용 계획</param>
    /// <param name="bindings">직접 연결</param>
    /// <param name="target">기존 난이도맵</param>
    /// <param name="report">검사 결과</param>
    static void AddDifficulty ( List<DataImportRow> rows, DataImportPlan plan, List<DataImportReference> bindings,
        ScriptableObject target, DataImportReport report )
    {
        if ( target is not GameDifficultyDataMap map || rows.Count != 3 )
            throw new ArgumentException( "갱신할 기존 GameDifficultyDataMap을 지정하고 Easy / Normal / Hard 세 행을 입력해 주세요." );
        Dictionary<GameDifficulty, Dictionary<string, ScriptableObject>> entries = new( );
        HashSet<string> ids = new( StringComparer.Ordinal );
        using SerializedObject original = new( map );
        foreach ( DataImportRow row in rows )
        {
            row.CheckFields( "id", "difficulty", "flow", "pressure", "spawnMap", "minigameMap", "mood", "score" );
            DataImportAssetUtility.ReadId( row, ids );
            GameDifficulty difficulty = row.Kind<GameDifficulty>( "difficulty" );
            if ( entries.ContainsKey( difficulty ) == true )
                throw row.Error( "difficulty", "난이도가 중복됐습니다." );
            GameFlowData flow = Resolve<GameFlowData>( row, "flow", bindings );
            if ( flow.Difficulty != difficulty || flow.CheckSettings( out string reason ) == false )
                throw row.Error( "flow", "흐름 난이도가 다르거나 흐름 설정이 유효하지 않습니다." );
            AnomalyPressureData pressure = Resolve<AnomalyPressureData>( row, "pressure", bindings );
            AnomalySpawnDataMap spawn = Resolve<AnomalySpawnDataMap>( row, "spawnMap", bindings );
            ProduceMinigameDataMap minigame = Resolve<ProduceMinigameDataMap>( row, "minigameMap", bindings );
            MoodData mood = Resolve<MoodData>( row, "mood", bindings );
            ResultScoreData score = Resolve<ResultScoreData>( row, "score", bindings );
            //이미 존재하는 분위기 자산도 수치 임포터와 같은 범위로 검사합니다.
            using ( SerializedObject moodObject = new( mood ) )
            {
                Dictionary<string, string> moodValues = new( ) { { "id", mood.name } };
                foreach ( string field in new[] { "initialMood", "missionRecovery", "resolvedRecovery", "rescueRecovery", "failedDecrease", "departureDecrease", "decrease", "decreaseDelay" } )
                    moodValues.Add( field, moodObject.FindProperty( "_" + field ).floatValue.ToString( System.Globalization.CultureInfo.InvariantCulture ) );
                ReadNumbers( BalanceImportKind.Mood, new DataImportRow( moodValues, row.Location + " / mood" ) );
            }
            if ( pressure.CheckSettings( out reason ) == false || spawn.CheckSettings( out reason ) == false ||
                 minigame.CheckSettings( out reason ) == false || score.IsValid == false )
                throw row.Error( "연결 데이터", string.IsNullOrEmpty( reason ) == true ? "결과 평가 설정이 유효하지 않습니다." : reason );
            Dictionary<string, ScriptableObject> refs = new( )
            {
                { "_flow", flow }, { "_pressure", pressure }, { "_spawnMap", spawn },
                { "_minigameMap", minigame }, { "_mood", mood }, { "_score", score }
            };
            entries.Add( difficulty, refs );
        }
        //행 순서가 달라도 기존 난이도별 안내 문구를 그대로 유지합니다.
        Dictionary<GameDifficulty, (string production, string anomaly)> descriptions = new( );
        SerializedProperty old = original.FindProperty( "_data" );
        for ( int i = 0; i < old.arraySize; i++ )
        {
            SerializedProperty entry = old.GetArrayElementAtIndex( i );
            descriptions[(GameDifficulty)entry.FindPropertyRelative( "_difficulty" ).intValue] =
                (entry.FindPropertyRelative( "_prodDescText" ).stringValue, entry.FindPropertyRelative( "_anomalyDescText" ).stringValue);
        }
        DataImportAssetChange change = DataImportAssetUtility.Change( plan, map.name, map, serialized =>
        {
            SerializedProperty array = serialized.FindProperty( "_data" );
            array.arraySize = 3;
            foreach ( KeyValuePair<GameDifficulty, Dictionary<string, ScriptableObject>> pair in entries )
            {
                SerializedProperty entry = array.GetArrayElementAtIndex( (int)pair.Key );
                entry.FindPropertyRelative( "_difficulty" ).intValue = (int)pair.Key;
                foreach ( KeyValuePair<string, ScriptableObject> reference in pair.Value )
                    entry.FindPropertyRelative( reference.Key ).objectReferenceValue = reference.Value;
                descriptions.TryGetValue( pair.Key, out var text );
                entry.FindPropertyRelative( "_prodDescText" ).stringValue = text.production ?? string.Empty;
                entry.FindPropertyRelative( "_anomalyDescText" ).stringValue = text.anomaly ?? string.Empty;
            }
        } );
        foreach ( var pair in entries )
        {
            int previousIndex = -1;
            for ( int i = 0; i < old.arraySize; i++ )
                if ( old.GetArrayElementAtIndex( i ).FindPropertyRelative( "_difficulty" ).intValue == (int)pair.Key )
                    previousIndex = i;
            foreach ( var reference in pair.Value )
            {
                UnityEngine.Object previous = previousIndex < 0 ? null : old.GetArrayElementAtIndex( previousIndex ).FindPropertyRelative( reference.Key ).objectReferenceValue;
                report.Details.Add( $"{map.name}.{pair.Key}.{reference.Key}: {(previous == null ? "없음" : previous.name)} → {reference.Value.name}" );
            }
        }
        report.Warnings.Add( "기존 난이도맵의 세 난이도 연결을 입력 전체로 교체합니다. 흐름의 시간과 미션 단계 및 기존 안내 문구는 수정하지 않습니다." );
        plan.Changes.Add( change );
    }

    /// <summary>
    /// 종류별 원본을 검사하고 저장 전 적용 계획 구성
    /// </summary>
    /// <param name="kind">입력 종류</param>
    /// <param name="rows">CSV 또는 JSON 입력</param>
    /// <param name="folder">신규 자산 폴더</param>
    /// <param name="bindings">직접 연결</param>
    /// <param name="target">기존 연결 대상</param>
    /// <param name="report">검사 결과</param>
    /// <returns>생성 및 연결 계획</returns>
    public static DataImportPlan Prepare ( BalanceImportKind kind, List<DataImportRow> rows, string folder,
        List<DataImportReference> bindings, ScriptableObject target, DataImportReport report )
    {
        DataImportPlan plan = new( ) { OutputFolder = FoodImportValidator.ValidateOutputFolder( folder ) };
        if ( target != null && target.GetType( ) != GetAssetType( kind ) )
            throw new ArgumentException( "현재 입력 종류와 갱신 대상의 타입이 다릅니다." );
        if ( kind == BalanceImportKind.Difficulty )
            AddDifficulty( rows, plan, bindings, target, report );
        else
        {
            if ( kind == BalanceImportKind.Bear && target != null && rows.Count != 1 )
                throw new ArgumentException( "기존 곰 데이터 하나를 지정했을 때는 한 행만 입력해 주세요." );
            HashSet<string> ids = new( StringComparer.Ordinal );
            foreach ( DataImportRow row in rows )
            {
                try
                {
                    string id = DataImportAssetUtility.ReadId( row, ids );
                    if ( id.Contains( "/" ) == true || id.Contains( "\\" ) == true )
                        throw row.Error( "id", "ID에 폴더 경로를 사용할 수 없습니다." );
                    switch ( kind )
                    {
                        case BalanceImportKind.Pressure: AddNumbers<AnomalyPressureData>( kind, row, id, plan, bindings, report ); break;
                        case BalanceImportKind.Mood: AddNumbers<MoodData>( kind, row, id, plan, bindings, report ); break;
                        case BalanceImportKind.Score: AddNumbers<ResultScoreData>( kind, row, id, plan, bindings, report ); break;
                        case BalanceImportKind.FoodMap: AddFoodMap( row, id, plan, bindings, report ); break;
                        case BalanceImportKind.Bear: AddBear( row, id, plan, bindings, target, report ); break;
                        default: throw new ArgumentOutOfRangeException( nameof( kind ) );
                    }
                }
                catch ( Exception exception ) when ( exception is FormatException || exception is ArgumentException || exception is InvalidOperationException )
                {
                    report.Errors.Add( exception.Message );
                }
            }
        }
        if ( report.CanImport == true )
            DataImportAssetUtility.Preflight( plan, null, null, report );
        return plan;
    }
}
