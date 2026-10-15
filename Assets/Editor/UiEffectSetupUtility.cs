using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// UI 연출 컴포넌트 종류
/// </summary>
public enum UiEffectKind
{
    Panel,      //패널
    Button,     //버튼
    Text,       //텍스트
    KeyPrompt,  //키 안내
    Gauge       //게이지
}

/// <summary>
/// UI 연출 설정 도구 - 컴포넌트 편집과 미리보기 복구
/// </summary>
public static class UiEffectSetupUtility
{
    /// <summary>
    /// 미리보기 중인 연출과 표시 복구 정보
    /// </summary>
    static List<PreviewState> _previews = new( );

    static double _previewStarted;		//미리보기 시작 시각

    /// <summary>
    /// 미리보기 대상 표시값 보관
    /// </summary>
    class PreviewState
    {
        public Component Owner;		//미리보기 초기화 상태를 복구할 뷰
        public Tween Effect;		//미리보기 연출
        public RectTransform [ ] Rects;		//복구할 시각 대상
        public Vector2 [ ] Positions;		//기준 위치
        public Vector3 [ ] Scales;		//기준 크기
        public Quaternion [ ] Rotations;		//기준 회전
        public CanvasGroup [ ] Groups;		//복구할 알파 대상
        public float [ ] Alphas;		//기준 알파
    }


    /// <summary>
    /// 에디터 전환 시 미리보기 종료 연결
    /// </summary>
    [InitializeOnLoadMethod]
    static void Init ()
    {
        AssemblyReloadEvents.beforeAssemblyReload -= StopPreview;
        AssemblyReloadEvents.beforeAssemblyReload += StopPreview;

        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;

        EditorApplication.quitting -= StopPreview;
        EditorApplication.quitting += StopPreview;
    }

    /// <summary>
    /// 플레이 전환 시 표시 복구
    /// </summary>
    /// <param name="state">플레이 전환 상태</param>
    static void HandlePlayModeChanged ( PlayModeStateChange state )
    {
        StopPreview( );
    }

    /// <summary>
    /// 연출 종류에 대응하는 타입 조회
    /// </summary>
    /// <param name="kind">연출 종류</param>
    /// <returns>컴포넌트 타입</returns>
    public static Type GetEffectType ( UiEffectKind kind )
    {
        switch ( kind )
        {
            case UiEffectKind.Panel: return typeof( PanelEffect );
            case UiEffectKind.Button: return typeof( ButtonEffect );
            case UiEffectKind.Text: return typeof( TextEffect );
            case UiEffectKind.KeyPrompt: return typeof( KeyPromptEffect );
            case UiEffectKind.Gauge: return typeof( GaugeEffect );
            default: throw new ArgumentOutOfRangeException( nameof( kind ) );
        }
    }

    /// <summary>
    /// 범위 안의 UI 대상 조회
    /// </summary>
    /// <param name="targets">조회 결과를 저장할 목록</param>
    public static void CollectTargets ( List<GameObject> targets )
    {
        targets.Clear( );

        var stage = PrefabStageUtility.GetCurrentPrefabStage( );

        if ( stage != null )
        {
            CollectRoot( stage.prefabContentsRoot, targets );
            return;
        }

        for ( int i = 0; i < SceneManager.sceneCount; i++ )
        {
            Scene scene = SceneManager.GetSceneAt( i );		//조회할 씬

            if ( scene.isLoaded == false ||
                 EditorSceneManager.IsPreviewScene( scene ) == true )
                continue;

            foreach ( GameObject root in scene.GetRootGameObjects( ) )
                CollectRoot( root, targets );
        }
    }

    /// <summary>
    /// 루트 아래 비활성 UI까지 조회
    /// </summary>
    /// <param name="root">조회 루트</param>
    /// <param name="targets">조회 결과 목록</param>
    static void CollectRoot ( GameObject root, List<GameObject> targets )
    {
        foreach ( RectTransform rect in
            root.GetComponentsInChildren<RectTransform>( true ) )
        {
            if ( rect.GetComponentInParent<Canvas>( true ) != null )
                targets.Add( rect.gameObject );
        }
    }

    /// <summary>
    /// 변경 대상의 프리팹 재정의와 씬 변경 기록
    /// </summary>
    /// <param name="target">변경 대상</param>
    static void RecordChange ( Component target )
    {
        EditorUtility.SetDirty( target );

        if ( PrefabUtility.IsPartOfPrefabInstance( target ) == true )
            PrefabUtility.RecordPrefabInstancePropertyModifications( target );

        EditorSceneManager.MarkSceneDirty( target.gameObject.scene );
    }

    /// <summary>
    /// 같은 오브젝트의 컴포넌트를 조회하거나 Undo로 추가
    /// </summary>
    /// <typeparam name="T">준비할 컴포넌트 타입</typeparam>
    /// <param name="target">구성할 UI</param>
    /// <returns>기존 또는 추가한 컴포넌트</returns>
    static T EnsureComponent<T> ( GameObject target ) where T : Component
    {
        T component = target.GetComponent<T>( );		//기존 컴포넌트

        if ( component == null )
            component = Undo.AddComponent<T>( target );

        return component;
    }

    /// <summary>
    /// 연출 종류에 필요한 컴포넌트 준비
    /// </summary>
    /// <param name="target">구성할 UI</param>
    /// <param name="kind">연출 종류</param>
    static void EnsureComponents ( GameObject target, UiEffectKind kind )
    {
        //관리 창에서 조회한 UI는 이미 RectTransform을 보유
        switch ( kind )
        {
            case UiEffectKind.Panel:
            case UiEffectKind.Text:
            case UiEffectKind.KeyPrompt:
                EnsureComponent<CanvasGroup>( target );
                break;

            case UiEffectKind.Button:
                EnsureComponent<Button>( target );
                break;

            case UiEffectKind.Gauge:
                EnsureComponent<Image>( target );
                break;
        }
    }

    /// <summary>
    /// 선택 대상의 연출 구성 준비와 비어 있는 참조 연결
    /// </summary>
    /// <param name="target">부착할 UI</param>
    /// <param name="kind">연출 종류</param>
    public static void AddEffect ( GameObject target, UiEffectKind kind )
    {
        Type type = GetEffectType( kind );		//추가 타입

        //뷰 추가 시 Reset에서 필요한 컴포넌트를 조회할 수 있도록 먼저 준비
        EnsureComponents( target, kind );

        Component effect = target.GetComponent( type );		//기존 또는 추가할 연출

        if ( effect == null )
            effect = Undo.AddComponent( target, type );

        SerializedObject settings = new( effect );		//새 뷰와 기존 뷰의 참조 설정

        SetReference( settings, "_target", target.GetComponent<RectTransform>( ) );
        SetReference( settings, "_group", target.GetComponent<CanvasGroup>( ) );
        SetReference( settings, "_button", target.GetComponent<Button>( ) );
        SetReference( settings, "_fill", target.GetComponent<Image>( ) );

        //SerializedObject 적용으로 참조 변경도 기존 일괄 Undo 그룹에 포함
        if ( settings.ApplyModifiedProperties( ) == true )
            RecordChange( effect );
    }

    /// <summary>
    /// 비어 있는 참조 필드만 연결하여 사용자 설정 보존
    /// </summary>
    /// <param name="settings">컴포넌트 설정</param>
    /// <param name="name">필드 이름</param>
    /// <param name="value">참조 대상</param>
    static void SetReference (
        SerializedObject settings, string name, UnityEngine.Object value )
    {
        SerializedProperty property = settings.FindProperty( name );		//참조 필드

        if ( property != null && property.objectReferenceValue == null )
            property.objectReferenceValue = value;
    }


    /// <summary>
    /// 참조가 없는 연출 컴포넌트 삭제
    /// </summary>
    /// <param name="target">편집 대상</param>
    /// <param name="kind">삭제할 연출 종류</param>
    public static void RemoveEffect ( GameObject target, UiEffectKind kind )
    {
        Component [ ] effects = target.GetComponents( GetEffectType( kind ) );		//삭제 후보

        foreach ( Component effect in effects )
        {
            if ( HasReferences( effect ) == true )
                continue;

            Undo.DestroyObjectImmediate( effect );
            EditorSceneManager.MarkSceneDirty( target.scene );
        }
    }

    /// <summary>
    /// 연출 전 표시값 저장
    /// </summary>
    /// <param name="effect">미리보기 대상</param>
    /// <returns>표시 복구 정보</returns>
    static PreviewState CapturePreview ( Component effect )
    {
        PreviewState state = new( );		//이번 미리보기 복구 정보

        state.Rects = effect.GetComponentsInChildren<RectTransform>( true );
        state.Positions = new Vector2 [ state.Rects.Length ];
        state.Scales = new Vector3 [ state.Rects.Length ];
        state.Rotations = new Quaternion [ state.Rects.Length ];

        for ( int i = 0; i < state.Rects.Length; i++ )
        {
            state.Positions [ i ] = state.Rects [ i ].anchoredPosition;
            state.Scales [ i ] = state.Rects [ i ].localScale;
            state.Rotations [ i ] = state.Rects [ i ].localRotation;
        }

        state.Groups = effect.GetComponentsInChildren<CanvasGroup>( true );
        state.Alphas = new float [ state.Groups.Length ];

        for ( int i = 0; i < state.Groups.Length; i++ )
            state.Alphas [ i ] = state.Groups [ i ].alpha;

        return state;
    }

    /// <summary>
    /// 연출 종류별 시각 미리보기 실행
    /// </summary>
    /// <param name="effect">연출 컴포넌트</param>
    /// <returns>미리보기 Tween</returns>
    static Tween CreatePreview ( Component effect )
    {
        switch ( effect )
        {
            case PanelEffect panel: return panel.PreviewEffect( );
            case ButtonEffect button: return button.PreviewEffect( );
            case TextEffect text: return text.PreviewEffect( );
            case KeyPromptEffect key: return key.PreviewEffect( );
            case GaugeEffect gauge: return gauge.PreviewEffect( );
            default: return null;
        }
    }

    /// <summary>
    /// 선택 대상 연출을 수동 시간으로 미리보기
    /// </summary>
    /// <param name="targets">선택한 UI 목록</param>
    /// <param name="kind">연출 종류</param>
    public static void Preview (
        IEnumerable<GameObject> targets, UiEffectKind kind )
    {
        StopPreview( );

        if ( EditorApplication.isPlayingOrWillChangePlaymode == true )
            return;

        Type type = GetEffectType( kind );      //미리보기 컴포넌트 타입
        bool completed = false;     //준비 완료 여부

        try
        {
            foreach ( GameObject target in targets )
            {
                if ( target == null || target.activeInHierarchy == false )
                    continue;

                Component effect = target.GetComponent( type );     //미리보기 뷰

                if ( effect == null || CheckPreviewReferences( effect ) == false )
                    continue;

                PreviewState state = CapturePreview( effect );      //연출 전 표시값
                state.Owner = effect;

                //연출 생성 중 실패해도 이미 변경된 표시값을 복구할 수 있도록 먼저 기록
                _previews.Add( state );
            }

            //부모와 자식을 함께 선택해도 모든 기준값을 연출 시작 전에 저장
            foreach ( PreviewState state in _previews )
            {
                state.Effect = CreatePreview( state.Owner );

                if ( state.Effect != null )
                    state.Effect.SetUpdate( UpdateType.Manual, true );
            }

            if ( _previews.Count > 0 )
            {
                _previewStarted = EditorApplication.timeSinceStartup;
                EditorApplication.update += UpdatePreview;
            }

            completed = true;
        }
        finally
        {
            //오류를 숨기지 않고 준비 중 변경된 표시값만 정리
            if ( completed == false )
                StopPreview( );
        }
    }

    /// <summary>
    /// 저장한 표시값 복구
    /// </summary>
    /// <param name="state">표시 스냅샷</param>
    static void RestorePreview ( PreviewState state )
    {
        for ( int i = 0; i < state.Rects.Length; i++ )
        {
            RectTransform rect = state.Rects [ i ];     //복구할 시각 대상

            //미리보기 도중 삭제된 대상은 복구하지 않음
            if ( rect == null )
                continue;

            rect.anchoredPosition = state.Positions [ i ];
            rect.localScale = state.Scales [ i ];
            rect.localRotation = state.Rotations [ i ];
        }

        for ( int i = 0; i < state.Groups.Length; i++ )
        {
            if ( state.Groups [ i ] != null )
                state.Groups [ i ].alpha = state.Alphas [ i ];
        }
    }

    /// <summary>
    /// 미리보기 시간 갱신
    /// </summary>
    static void UpdatePreview ()
    {
        float elapsed = ( float ) (
            EditorApplication.timeSinceStartup - _previewStarted );		//경과 시간

        if ( elapsed >= 2f )
        {
            StopPreview( );
            return;
        }

        foreach ( PreviewState state in _previews )
        {
            if ( state.Effect != null && state.Effect.IsActive( ) == true )
                state.Effect.Goto( elapsed, false );
        }

        SceneView.RepaintAll( );
        EditorApplication.QueuePlayerLoopUpdate( );
    }

    /// <summary>
    /// 미리보기 종료와 표시, 초기화 상태 복구
    /// </summary>
    public static void StopPreview ()
    {
        EditorApplication.update -= UpdatePreview;

        foreach ( PreviewState state in _previews )
        {
            state.Effect?.Kill( false );

            if ( state.Owner != null )
                EndPreview( state.Owner );
        }

        //모든 뷰 정리가 끝난 다음 복구하여 부모와 자식의 표시값을 유지
        foreach ( PreviewState state in _previews )
            RestorePreview( state );

        _previews.Clear( );
        SceneView.RepaintAll( );
    }

    /// <summary>
    /// 뷰별 미리보기 상태 정리
    /// </summary>
    /// <param name="effect">미리보기 대상 뷰</param>
    static void EndPreview ( Component effect )
    {
        switch ( effect )
        {
            case PanelEffect panel:
                panel.EndPreview( );
                break;

            case ButtonEffect button:
                button.EndPreview( );
                break;

            case TextEffect text:
                text.EndPreview( );
                break;

            case KeyPromptEffect key:
                key.EndPreview( );
                break;

            case GaugeEffect gauge:
                gauge.EndPreview( );
                break;
        }
    }


    /// <summary>
    /// 대상 컴포넌트를 참조하는 스크립트 확인
    /// </summary>
    /// <param name="effect">삭제 후보</param>
    /// <returns>참조가 남아 있는지 여부</returns>
    static bool HasReferences ( Component effect )
    {
        foreach ( GameObject root in effect.gameObject.scene.GetRootGameObjects( ) )
        {
            foreach ( MonoBehaviour owner in
                root.GetComponentsInChildren<MonoBehaviour>( true ) )
            {
                if ( owner == null || owner == effect )
                    continue;

                SerializedObject settings = new( owner );		//참조를 검사할 설정
                SerializedProperty property = settings.GetIterator( );		//설정 순회

                while ( property.Next( true ) == true )
                {
                    if ( property.propertyType ==
                         SerializedPropertyType.ObjectReference &&
                         property.objectReferenceValue == effect )
                    {
                        Debug.LogWarning(
                            $"[UI 연출 삭제 보류] {owner.name}.{property.propertyPath}에서 참조합니다.",
                            owner );
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 미리보기 참조가 복구 가능한 범위인지 확인
    /// </summary>
    /// <param name="effect">미리보기 뷰</param>
    /// <returns>참조가 자신 또는 자식에 있는지 여부</returns>
    static bool CheckPreviewReferences ( Component effect )
    {
        SerializedObject settings = new( effect );      //연출 참조 설정

        SerializedProperty targetProperty =
            settings.FindProperty( "_target" );     //시각 대상 필드

        Transform target = targetProperty.objectReferenceValue as Transform;        //시각 대상

        if ( target == null || target.IsChildOf( effect.transform ) == false )
        {
            Debug.LogWarning(
                "[UI 미리보기] 시각 대상은 자신 또는 자식으로 연결해 주세요.",
                effect );
            return false;
        }

        SerializedProperty groupProperty =
            settings.FindProperty( "_group" );      //선택적 알파 대상 필드

        if ( groupProperty != null )
        {
            CanvasGroup group =
                groupProperty.objectReferenceValue as CanvasGroup;      //알파 대상

            //외부 CanvasGroup은 표시 스냅샷에 포함되지 않아 복구 불가
            if ( group == null || group.transform.IsChildOf( effect.transform ) == false )
            {
                Debug.LogWarning(
                    "[UI 미리보기] CanvasGroup은 자신 또는 자식으로 연결해 주세요.",
                    effect );
                return false;
            }
        }

        return true;
    }

}
