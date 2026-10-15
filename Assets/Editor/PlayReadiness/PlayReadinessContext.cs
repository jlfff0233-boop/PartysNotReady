using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 직렬화된 현재 설정을 읽는 점검 문맥
/// </summary>
public class PlayReadinessContext
{
    /// <summary>
    /// 검사 대상 객체와 연결 자산
    /// </summary>
    public List<Object> Objects = new( );
    /// <summary>
    /// 필드별 점검 결과
    /// </summary>
    public List<PlayReadinessResult> Results = new( );
    /// <summary>
    /// 같은 객체의 중복 수집 방지
    /// </summary>
    HashSet<int> _visited = new( );
    /// <summary>
    /// 같은 규칙과 필드의 중복 보고 방지
    /// </summary>
    HashSet<string> _reported = new( );
    /// <summary>
    /// 실제 플레이 씬에서 도달한 객체
    /// </summary>
    HashSet<int> _connected = new( );
    public bool HasScene;        //월드 검사 가능 여부
    public Scene Scene;        //전체 수집한 씬 범위
    public float SampleRadius = 1f;        //NavMesh 표본 반경
    public int AgentType;        //검사할 NavMesh 에이전트 종류

    /// <summary>
    /// 중복 없이 객체와 비활성 자식 컴포넌트 수집
    /// </summary>
    /// <param name="target">검사 대상</param>
    /// <param name="connected">씬에서 연결된 대상 여부</param>
    public void Add ( Object target, bool connected )
    {
        if ( target == null )
            return;
        if ( connected == true )
            _connected.Add( target.GetInstanceID( ) );
        if ( _visited.Add( target.GetInstanceID( ) ) == false )
            return;
        Objects.Add( target );
    }

    /// <summary>
    /// 사용자 설정의 구조체와 배열만 순회하고 문자열 내부 구현은 제외
    /// </summary>
    /// <param name="serialized">조회할 직렬화 설정</param>
    /// <returns>사용자 설정의 속성 사본 목록</returns>
    public static IEnumerable<SerializedProperty> EnumerateProperties ( SerializedObject serialized )
    {
        SerializedProperty property = serialized.GetIterator( );
        bool enterChildren = true;
        while ( property.Next( enterChildren ) == true )
        {
            enterChildren = property.propertyType == SerializedPropertyType.Generic ||
                property.propertyType == SerializedPropertyType.ManagedReference;
            if ( property.propertyPath.StartsWith( "m_", System.StringComparison.Ordinal ) == true )
            {
                enterChildren = false;
                continue;
            }
            yield return property.Copy( );
        }
    }

    /// <summary>
    /// 직렬화 참조로 연결된 데이터와 프리팹 수집
    /// </summary>
    /// <param name="owner">참조 소유자</param>
    public void CollectReferences ( Object owner )
    {
        bool connected = _connected.Contains( owner.GetInstanceID( ) );
        if ( owner is GameObject root )
        {
            //자식 하나씩 예약하여 깊은 계층도 한 번씩만 순회합니다.
            foreach ( Transform child in root.transform )
                Add( child.gameObject, connected );
            foreach ( Component component in root.GetComponents<Component>( ) )
            {
                if ( component == null )
                    Report( root, "씬", "missing-script", "Components", PlayReadinessStatus.Error,
                        "Missing Script를 제거하거나 원래 스크립트를 복구해 주세요." );
                else
                    Add( component, connected );
            }
            return;
        }
        if ( owner is Transform )
            return;
        using SerializedObject serialized = new( owner );
        foreach ( SerializedProperty property in EnumerateProperties( serialized ) )
        {
            if ( property.propertyType != SerializedPropertyType.ObjectReference )
                continue;
            Object value = property.objectReferenceValue;
            if ( value is ScriptableObject || value is GameObject || value is Component )
            {
                Add( value, connected );
                if ( value is Component component )
                    Add( component.gameObject, connected );
            }
            if ( value == null && property.objectReferenceInstanceIDValue != 0 )
                Report( owner, "씬", "missing-reference", property.propertyPath,
                    PlayReadinessStatus.Error, "삭제되었거나 로드할 수 없는 참조입니다. 연결 대상을 다시 지정해 주세요." );
        }
    }

    /// <summary>
    /// 중복 없는 판정 추가
    /// </summary>
    /// <param name="owner">설정 소유자</param>
    /// <param name="group">시스템 구역</param>
    /// <param name="rule">규칙 식별자</param>
    /// <param name="field">직렬화 경로</param>
    /// <param name="status">판정</param>
    /// <param name="message">현재 상태와 조치</param>
    /// <param name="related">관련 연결 대상</param>
    public void Report ( Object owner, string group, string rule, string field,
        PlayReadinessStatus status, string message, Object related = null )
    {
        string key = $"{( owner == null ? 0 : owner.GetInstanceID( ) )}/{rule}/{field}";
        if ( _reported.Add( key ) == false )
            return;
        Results.Add( new PlayReadinessResult
        {
            Owner = owner, Related = related, Group = group, Rule = rule, Field = field,
            Status = status, Message = message,
            Scope = owner != null && _connected.Contains( owner.GetInstanceID( ) ) == true
                ? "현재 연결" : "선택 / 폴더 / 프리팹"
        } );
    }

    /// <summary>
    /// 명시한 필수 참조만 검사
    /// </summary>
    /// <param name="owner">설정 소유자</param>
    /// <param name="group">시스템 구역</param>
    /// <param name="fields">필수 필드 목록</param>
    public void Require ( Object owner, string group, params string[] fields )
    {
        CheckReferences( owner, group, PlayReadinessStatus.Error, fields );
    }

    /// <summary>
    /// 선택 참조의 누락을 필수 오류와 구분
    /// </summary>
    /// <param name="owner">설정 소유자</param>
    /// <param name="group">시스템 구역</param>
    /// <param name="fields">선택 필드 목록</param>
    public void Optional ( Object owner, string group, params string[] fields )
    {
        CheckReferences( owner, group, PlayReadinessStatus.Warning, fields );
    }

    /// <summary>
    /// 명시된 참조 계약에 맞춰 연결 상태 판정
    /// </summary>
    /// <param name="owner">설정 소유자</param>
    /// <param name="group">시스템 구역</param>
    /// <param name="missingStatus">누락 시 판정</param>
    /// <param name="fields">직렬화 필드 목록</param>
    void CheckReferences ( Object owner, string group, PlayReadinessStatus missingStatus, string[] fields )
    {
        using SerializedObject serialized = new( owner );
        foreach ( string field in fields )
        {
            SerializedProperty property = serialized.FindProperty( field );
            if ( property == null || property.propertyType != SerializedPropertyType.ObjectReference )
            {
                Report( owner, group, "schema", field, PlayReadinessStatus.Blocked,
                    "검사 규칙과 현재 스크립트 필드가 다릅니다. 점검 규칙을 갱신해 주세요." );
                continue;
            }
            Object value = property.objectReferenceValue;
            Report( owner, group, "reference", field,
                value == null ? missingStatus : PlayReadinessStatus.Passed,
                value == null ? $"{( missingStatus == PlayReadinessStatus.Warning ? "선택 기능 미연결: " : string.Empty )}{owner.GetType( ).Name}.{field}에 {property.type} 형식의 대상을 연결해 주세요."
                    : $"연결: {value.name} ({value.GetType( ).Name})", value );
            if ( owner is Component component && value is Component reference &&
                 EditorUtility.IsPersistent( owner ) == false && EditorUtility.IsPersistent( value ) == false &&
                 component.gameObject.scene != reference.gameObject.scene )
                Report( owner, group, "other-scene", field, PlayReadinessStatus.Warning,
                    "다른 씬의 컴포넌트를 참조합니다. 해당 씬의 동시 로드와 참조 수명을 확인해 주세요.", value );
        }
    }

    /// <summary>
    /// 전체 수집한 실제 씬에 속하는 컴포넌트인지 확인
    /// </summary>
    /// <param name="owner">검사할 컴포넌트</param>
    /// <returns>월드 관계 검사 가능 여부</returns>
    public bool CanCheckScene ( Component owner )
    {
        return HasScene == true && owner != null && EditorUtility.IsPersistent( owner ) == false &&
            owner.gameObject.scene == Scene;
    }

    /// <summary>
    /// 프리팹 로드 상태에 영향받지 않는 활성 설정 확인
    /// </summary>
    /// <param name="owner">검사할 기능</param>
    /// <returns>컴포넌트와 모든 부모가 활성 설정인지 여부</returns>
    public static bool IsConfiguredActive ( Behaviour owner )
    {
        if ( owner.enabled == false )
            return false;
        for ( Transform parent = owner.transform; parent != null; parent = parent.parent )
        {
            if ( parent.gameObject.activeSelf == false )
                return false;
        }
        return true;
    }

    /// <summary>
    /// 데이터맵 종류에 대응하는 실제 등록 배열 필드 조회
    /// </summary>
    /// <param name="owner">조회할 데이터맵</param>
    /// <returns>등록 배열 필드 또는 빈 문자열</returns>
    public static string GetMapField ( Object owner )
    {
        return owner switch
        {
            MissionDataMap => "_missions",
            ItemDataMap => "_items",
            FoodDataMap => "_foodDatas",
            EventSettingsDataMap => "_entries",
            AppearanceMap => "_appearances",
            MaterialMap => "_materials",
            RecipeDataMap or CookGoalDataMap or PreparationGoalDataMap or
            FoodPlacementDataMap or ItemPlacementMissionDataMap or AnomalySpawnDataMap or
            GameDifficultyDataMap or ProduceMinigameDataMap or ShopDataMap => "_data",
            _ => string.Empty
        };
    }

    /// <summary>
    /// 대상의 데이터맵 원본 배열 읽기
    /// </summary>
    /// <param name="owner">데이터맵</param>
    /// <returns>등록 참조 목록, 연결 누락 또는 검사 불가 시 빈 목록</returns>
    public List<Object> ReadMap ( Object owner )
    {
        List<Object> values = new( );
        if ( owner == null )
            return values;
        using SerializedObject serialized = new( owner );
        string field = GetMapField( owner );
        SerializedProperty array = field.Length == 0 ? null : serialized.FindProperty( field );
        if ( array == null || array.isArray == false )
        {
            Report( owner, "데이터", "map-schema", field, PlayReadinessStatus.Blocked,
                "맵의 등록 배열을 확인할 수 없습니다. 빈 데이터맵으로 판정하지 않습니다. 검사 규칙을 갱신해 주세요." );
            return values;
        }
        for ( int i = 0; i < array.arraySize; i++ )
        {
            SerializedProperty element = array.GetArrayElementAtIndex( i );
            if ( element.propertyType != SerializedPropertyType.ObjectReference )
            {
                Report( owner, "데이터", "map-shape", field, PlayReadinessStatus.Blocked,
                    "구조체/설정 항목으로 이루어진 맵은 전용 검사로 읽어야 합니다. 참조 목록으로 판정하지 않습니다." );
                return new List<Object>( );
            }
            values.Add( element.objectReferenceValue );
        }
        return values;
    }
}
