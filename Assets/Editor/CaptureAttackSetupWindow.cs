using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 포획 공격 설정 창 - 에디터 API로 오른손 연결과 공격 클립 이벤트 설정
/// </summary>
public class CaptureAttackSetupWindow : EditorWindow
{
    const string _clipPath = "Assets/08. Meshes/Attack.fbx"; //공유 공격 모델 경로
    const string _controllerPath = "Assets/05. Animations/Enemy/EnemyAttack.controller"; //공유 공격 제어기 경로
    static readonly string[] _prefabPaths = //공유 클립을 사용하는 적 프리팹
    {
        "Assets/04. Prefabs/Enemy/1_EnemyDemand.prefab",
        "Assets/04. Prefabs/Enemy/4_EnemyKidnap.prefab",
        "Assets/04. Prefabs/Enemy/3_EnemyHarm.prefab"
    };

    /// <summary>
    /// 검토할 프리팹과 오른손 선택 정보
    /// </summary>
    List<PrefabTarget> _targets = new List<PrefabTarget>( );
    AnimationClip _clip; //이벤트를 넣을 원본 클립
    int _hitFrame = -1; //사용자가 확인한 명중 프레임
    int _attachFrame = -1; //사용자가 확인한 장착 프레임
    string _status = "대상 조회를 눌러 현재 연결을 확인하세요."; //조회 및 적용 결과
    Vector2 _scroll; //대상 목록 스크롤 위치

    /// <summary>
    /// 프리팹 설정 대상 - 영구 오브젝트 대신 경로로 선택 결과 보관
    /// </summary>
    class PrefabTarget
    {
        public string Path; //원본 프리팹 경로
        public string[] BonePaths; //선택 가능한 계층 경로
        public int HandIndex; //선택한 오른손 경로 인덱스
        public bool HasCapture; //포획 기능 포함 여부
        public string Details; //현재 캐리 및 공격 위치
    }

    /// <summary>
    /// 프리팹 연결 - 한 번의 조회와 적용에서 사용하는 에디터 참조
    /// </summary>
    class Connections
    {
        public AttackAnimatorHandler Handler; //공격 이벤트 핸들러
        public Animator Animator; //이벤트를 받는 실제 애니메이터
        public Transform Carry; //현재 캐리 포인트
        public Transform[] AttackPoints; //포획용 공격 판정 위치
    }

    /// <summary>
    /// 포획 공격 설정 메뉴 열기
    /// </summary>
    [MenuItem( "Tools/Utilities/Initial Setup/Capture Attack Setup" )]
    static void Open ()
    {
        GetWindow<CaptureAttackSetupWindow>( "포획 공격 설정" ).minSize = new Vector2( 640f, 620f );
    }

    private void OnGUI ()
    {
        EditorGUILayout.HelpBox( "곰·여우: 캐리/포획 공격 위치를 오른손으로 이동합니다. 기존 로컬 위치·회전·크기는 유지합니다.\n" +
            "곰·여우·담비: 실제 Animator에 공격 이벤트 전달기를 연결합니다.\n" +
            "공유 Attack 클립: 명중/장착 이벤트를 넣고 Attack 종료 시점을 1로 설정합니다.", MessageType.Info );

        if ( GUILayout.Button( "1. 대상 조회 / 새로고침" ) == true )
            RunEditorAction( ScanTargets );

        _scroll = EditorGUILayout.BeginScrollView( _scroll );
        foreach ( PrefabTarget target in _targets )
        {
            EditorGUILayout.LabelField( System.IO.Path.GetFileNameWithoutExtension( target.Path ), EditorStyles.boldLabel );
            EditorGUILayout.LabelField( target.Details, EditorStyles.wordWrappedLabel );
            if ( target.HasCapture == true )
                target.HandIndex = EditorGUILayout.Popup( "오른손 본 (직접 확인)", target.HandIndex, target.BonePaths );
            EditorGUILayout.Space( );
        }
        EditorGUILayout.EndScrollView( );

        if ( _clip != null )
        {
            EditorGUILayout.LabelField( $"{_clip.name} / {_clip.frameRate:g} fps / 0~{Mathf.RoundToInt( _clip.length * _clip.frameRate )} 프레임" );
            if ( GUILayout.Button( "Attack 모델 선택 (Inspector의 Animation 탭에서 프레임 확인)" ) == true )
            {
                Selection.activeObject = AssetDatabase.LoadMainAssetAtPath( _clipPath );
                EditorGUIUtility.PingObject( Selection.activeObject );
            }
        }
        _hitFrame = EditorGUILayout.IntField( "AttackHit: 손이 닿는 프레임", _hitFrame );
        _attachFrame = EditorGUILayout.IntField( "CaptureAttach: 손을 내리는 프레임", _attachFrame );
        EditorGUILayout.HelpBox( "프레임은 0부터 시작하는 클립 내 상대 프레임입니다. 명중 < 장착 < 마지막 프레임 순서로 지정하세요.\n" +
            "적용하면 위 프리팹·공유 클립·컨트롤러를 저장합니다. 열린 씬의 연결 예외는 콘솔에 표시합니다. 씬은 직접 저장하세요.", MessageType.Info );
        using ( new EditorGUI.DisabledScope( EditorApplication.isPlayingOrWillChangePlaymode || _targets.Count != _prefabPaths.Length ) )
        {
            if ( GUILayout.Button( "2. 설정 적용 및 에셋 저장" ) == true )
                RunEditorAction( ApplySettings );
        }
        EditorGUILayout.HelpBox( _status, MessageType.None );
    }

    /// <summary>
    /// 에디터 작업 실패를 숨기지 않고 창과 콘솔에 함께 표시
    /// </summary>
    /// <param name="action">사용자가 실행한 조회 또는 적용</param>
    void RunEditorAction ( Action action )
    {
        try
        {
            action( );
        }
        catch ( Exception exception )
        {
            _status = "작업 중단: " + exception.Message + "\n적용 중 실패한 경우 콘솔에서 이미 저장된 대상을 확인하세요.";
            Debug.LogException( exception );
        }
    }

    /// <summary>
    /// 프리팹을 임시로 열어 연결을 조회하고 닫기
    /// </summary>
    void ScanTargets ()
    {
        _targets.Clear( );
        _clip = AssetDatabase.LoadAllAssetsAtPath( _clipPath ).OfType<AnimationClip>( ).Single( clip => clip.name == "mixamo.com" );
        foreach ( string path in _prefabPaths )
        {
            GameObject root = PrefabUtility.LoadPrefabContents( path );
            try
            {
                Connections links = ReadConnections( root );
                string[] paths = new[] { "(오른손 본 선택 필요)" }.Concat( root.GetComponentsInChildren<Transform>( true )
                    .Where( bone => bone != root.transform )
                    .Select( bone => AnimationUtility.CalculateTransformPath( bone, root.transform ) ) ).ToArray( );
                Transform hand = links.Animator.isHuman == true ? links.Animator.GetBoneTransform( HumanBodyBones.RightHand ) : null;
                int handIndex = hand != null ? Array.IndexOf( paths, AnimationUtility.CalculateTransformPath( hand, root.transform ) ) : 0;
                _targets.Add( new PrefabTarget
                {
                    Path = path,
                    BonePaths = paths,
                    HandIndex = Mathf.Max( 0, handIndex ),
                    HasCapture = links.Carry != null,
                    Details = links.Carry == null ? "이벤트 전달기 연결 대상 (포획 기능 없음)" :
                        $"현재 캐리: {AnimationUtility.CalculateTransformPath( links.Carry, root.transform )}\n" +
                        "포획 공격 위치: " + string.Join( ", ", links.AttackPoints.Select( point => AnimationUtility.CalculateTransformPath( point, root.transform ) ) )
                } );
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents( root );
            }
        }
        _status = "조회 완료. 오른손 본과 두 이벤트 프레임을 확인한 뒤 적용하세요. 휴머노이드가 아니면 오른손을 직접 선택해야 합니다.";
    }

    /// <summary>
    /// 필수 참조와 이벤트 수신 구조를 한 곳에서 확인
    /// </summary>
    /// <param name="root">적 루트</param>
    /// <returns>설정에 필요한 참조</returns>
    static Connections ReadConnections ( GameObject root )
    {
        AttackAnimatorHandler handler = root.GetComponentsInChildren<AttackAnimatorHandler>( true ).Single( );
        Animator animator = (Animator)ReadReference( handler, "_animator" );
        if ( animator == null || animator.transform.IsChildOf( root.transform ) == false )
            throw new InvalidOperationException( root.name + ": Animator 연결을 확인하세요." );
        if ( animator.runtimeAnimatorController != AssetDatabase.LoadAssetAtPath<AnimatorController>( _controllerPath ) )
            throw new InvalidOperationException( root.name + ": 예상한 EnemyAttack 컨트롤러가 아닙니다." );

        AttackAnimationEventRelay[] relays = animator.GetComponents<AttackAnimationEventRelay>( );
        if ( relays.Length > 1 || ( relays.Length > 0 && handler.gameObject == animator.gameObject ) )
            throw new InvalidOperationException( root.name + ": 중복 공격 이벤트 수신기가 있습니다." );

        Component owner = root.GetComponent<EnemyKidnap>( );
        string attackerField = "_attacker";
        if ( owner == null )
        {
            owner = root.GetComponent<EnemyDemand>( );
            attackerField = "_kidnapAttacker";
        }
        Connections links = new Connections { Handler = handler, Animator = animator };
        if ( owner == null )
            return links;

        Component capturer = ReadReference( owner, "_capturer" );
        Component attacker = ReadReference( owner, attackerField );
        links.Carry = (Transform)ReadReference( capturer, "_carryPoint" );
        SerializedProperty points = new SerializedObject( attacker ).FindProperty( "_atkPoints" );
        links.AttackPoints = Enumerable.Range( 0, points.arraySize )
            .Select( index => (Transform)points.GetArrayElementAtIndex( index ).objectReferenceValue ).ToArray( );
        if ( links.Carry == null || links.AttackPoints.Length == 0 || links.AttackPoints.Any( point => point == null ) == true )
            throw new InvalidOperationException( root.name + ": 캐리/포획 공격 위치 연결이 비어 있습니다." );
        return links;
    }

    /// <summary>
    /// 기존 직렬화 필드의 필수 컴포넌트 참조 읽기
    /// </summary>
    /// <param name="owner">참조 소유 컴포넌트</param>
    /// <param name="field">직렬화 필드 이름</param>
    /// <returns>연결된 컴포넌트</returns>
    static Component ReadReference ( Component owner, string field )
    {
        Component reference = (Component)new SerializedObject( owner ).FindProperty( field ).objectReferenceValue;
        if ( reference == null )
            throw new InvalidOperationException( owner.name + ": " + field + " 연결이 비어 있습니다." );
        return reference;
    }

    /// <summary>
    /// 현재 선택한 본이 캐리/공격 위치를 안전하게 받을 수 있는지 검사
    /// </summary>
    /// <param name="root">적 루트</param>
    /// <param name="target">선택한 설정</param>
    /// <returns>검증한 오른손 본, 포획 기능이 없으면 null</returns>
    static Transform ValidateTarget ( GameObject root, PrefabTarget target )
    {
        Connections links = ReadConnections( root );
        if ( target.HasCapture != ( links.Carry != null ) )
            throw new InvalidOperationException( root.name + ": 조회 이후 구조가 변경되었습니다. 다시 조회하세요." );
        if ( target.HasCapture == false )
            return null;
        Transform hand = target.HandIndex > 0 ? root.transform.Find( target.BonePaths[target.HandIndex] ) : null;
        if ( hand == null || hand.IsChildOf( links.Animator.transform ) == false )
            throw new InvalidOperationException( root.name + ": Animator 아래의 오른손 본을 선택하세요." );
        foreach ( Transform point in links.AttackPoints.Append( links.Carry ) )
        {
            if ( point.IsChildOf( root.transform ) == false || hand.IsChildOf( point ) == true )
                throw new InvalidOperationException( root.name + ": 캐리/공격 위치가 외부 오브젝트이거나 선택한 본의 상위입니다." );
        }
        return hand;
    }

    /// <summary>
    /// 열린 씬에서 대상 프리팹의 적 인스턴스만 수집
    /// </summary>
    /// <returns>적 루트와 설정 대상 쌍</returns>
    List<KeyValuePair<GameObject, PrefabTarget>> FindSceneTargets ()
    {
        List<KeyValuePair<GameObject, PrefabTarget>> results = new List<KeyValuePair<GameObject, PrefabTarget>>( );
        for ( int i = 0; i < SceneManager.sceneCount; i++ )
        {
            Scene scene = SceneManager.GetSceneAt( i );
            if ( scene.isLoaded == false || EditorSceneManager.IsPreviewScene( scene ) == true )
                continue;
            foreach ( Enemy enemy in scene.GetRootGameObjects( ).SelectMany( root => root.GetComponentsInChildren<Enemy>( true ) ) )
            {
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot( enemy.gameObject );
                PrefabTarget target = _targets.SingleOrDefault( entry => entry.Path == path );
                if ( target != null )
                    results.Add( new KeyValuePair<GameObject, PrefabTarget>( enemy.gameObject, target ) );
            }
        }
        return results;
    }

    /// <summary>
    /// 전체 입력을 먼저 검증한 뒤 프리팹, 컨트롤러, 임포터 순서로 적용
    /// </summary>
    void ApplySettings ()
    {
        if ( EditorApplication.isPlayingOrWillChangePlaymode == true )
            throw new InvalidOperationException( "플레이 모드를 종료한 뒤 적용하세요." );
        if ( _targets.Count != _prefabPaths.Length )
            throw new InvalidOperationException( "대상 조회가 완료되지 않았습니다. 다시 조회하세요." );
        var prefabStage = PrefabStageUtility.GetCurrentPrefabStage( );
        if ( prefabStage != null && _prefabPaths.Contains( prefabStage.assetPath ) == true )
            throw new InvalidOperationException( "대상 프리팹 편집 모드를 닫고 적용하세요. 편집 중인 내용과 충돌할 수 있습니다." );
        if ( _clip == null || _hitFrame < 0 || _attachFrame <= _hitFrame || _attachFrame >= Mathf.RoundToInt( _clip.length * _clip.frameRate ) )
            throw new InvalidOperationException( "0 ≤ 명중 프레임 < 장착 프레임 < 마지막 프레임 순서로 입력하세요." );

        ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath( _clipPath );
        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if ( clips.Length == 0 )
            clips = importer.defaultClipAnimations;
        ModelImporterClipAnimation attackClip = clips.Single( clip => clip.name == _clip.name );
        float frameCount = attackClip.lastFrame - attackClip.firstFrame;
        if ( _attachFrame >= frameCount )
            throw new InvalidOperationException( "임포터의 클립 범위가 변경되었습니다. 다시 조회하고 프레임을 확인하세요." );
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>( _controllerPath );
        AnimatorState attackState = controller.layers[0].stateMachine.states.Single( state => state.state.name == "Attack" ).state;
        if ( attackState.motion != _clip || attackState.transitions.Length == 0 ||
            attackState.transitions.Any( transition => transition.hasExitTime == false || transition.conditions.Length != 0 ) == true )
            throw new InvalidOperationException( "Attack 상태의 클립/종료 전환 구조가 변경되었습니다. 컨트롤러를 확인하세요." );

        //임시로 연 프리팹에서 설정을 먼저 시도하고 저장하지 않고 닫는다. 중첩 프리팹의 부모 변경 제한도 여기서 확인한다.
        foreach ( PrefabTarget target in _targets )
        {
            GameObject root = PrefabUtility.LoadPrefabContents( target.Path );
            try
            {
                ConfigureTarget( root, target );
                if ( HasExpectedConnections( root, target ) == false )
                    throw new InvalidOperationException( target.Path + ": 임시 설정 검증 실패. 중첩 프리팹의 계층 제한을 확인하세요." );
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents( root );
            }
        }
        List<KeyValuePair<GameObject, PrefabTarget>> sceneTargets = FindSceneTargets( );
        foreach ( var pair in sceneTargets )
            ValidateTarget( pair.Key, pair.Value );

        if ( EditorUtility.DisplayDialog( "포획 공격 설정 저장", $"프리팹 3개와 공유 Attack 클립/컨트롤러를 저장합니다.\n" +
            $"명중 {_hitFrame} → 장착 {_attachFrame} 프레임\n열린 씬 대상 {sceneTargets.Count}개 (씬은 직접 저장)\n" +
            "기존 캐리/공격 포인트의 로컬 오프셋은 유지합니다. 에셋 저장은 씬 Undo에 포함되지 않습니다.", "적용 및 저장", "취소" ) == false )
            return;

        foreach ( PrefabTarget target in _targets )
        {
            GameObject root = PrefabUtility.LoadPrefabContents( target.Path );
            try
            {
                ConfigureTarget( root, target );
                PrefabUtility.SaveAsPrefabAsset( root, target.Path, out bool success );
                if ( success == false )
                    throw new InvalidOperationException( target.Path + " 저장 실패" );
                Debug.Log( "[포획 공격 설정] 저장: " + target.Path );
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents( root );
            }
        }

        foreach ( AnimatorStateTransition transition in attackState.transitions )
        {
            transition.exitTime = 1f;
            EditorUtility.SetDirty( transition );
        }
        EditorUtility.SetDirty( controller );
        AssetDatabase.SaveAssetIfDirty( controller );
        Debug.Log( "[포획 공격 설정] 저장: " + _controllerPath );

        //임포터 이벤트는 초가 아닌 클립 범위 내 0~1 시간이다. 기존의 다른 이벤트는 유지한다.
        attackClip.events = attackClip.events.Where( entry => entry.functionName != "AttackHit" && entry.functionName != "CaptureAttach" )
            .Concat( new[]
            {
                new AnimationEvent { functionName = "AttackHit", time = _hitFrame / frameCount },
                new AnimationEvent { functionName = "CaptureAttach", time = _attachFrame / frameCount }
            } ).OrderBy( entry => entry.time ).ToArray( );
        importer.clipAnimations = clips;
        importer.SaveAndReimport( );
        Debug.Log( "[포획 공격 설정] 저장: " + _clipPath );

        //재임포트된 실제 클립의 초 단위 이벤트가 지정한 프레임에 들어갔는지도 확인한다.
        _clip = AssetDatabase.LoadAllAssetsAtPath( _clipPath ).OfType<AnimationClip>( ).Single( clip => clip.name == "mixamo.com" );
        AnimationEvent[] events = AnimationUtility.GetAnimationEvents( _clip );
        VerifyEvent( events, "AttackHit", _hitFrame );
        VerifyEvent( events, "CaptureAttach", _attachFrame );
        foreach ( PrefabTarget target in _targets )
        {
            GameObject root = PrefabUtility.LoadPrefabContents( target.Path );
            try
            {
                if ( HasExpectedConnections( root, target ) == false )
                    throw new InvalidOperationException( target.Path + ": 저장 후 연결 검증에 실패했습니다." );
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents( root );
            }
        }

        //원본 프리팹 변경은 Unity가 씬 인스턴스에 반영한다. 개별 오버라이드는 자동으로 없애지 않는다.
        int sceneExceptions = 0;
        foreach ( var pair in sceneTargets )
        {
            if ( HasExpectedConnections( pair.Key, pair.Value ) == false )
            {
                sceneExceptions++;
                Debug.LogWarning( "[포획 공격 설정] 씬 개별 연결/계층이 원본과 다릅니다. Overrides에서 해당 항목을 확인하세요: " +
                    pair.Key.scene.name + "/" + pair.Key.name, pair.Key );
            }
        }
        _status = $"에셋 설정 완료. 열린 씬 연결 예외 {sceneExceptions}개 (콘솔 확인). 씬은 Ctrl+S로 저장하세요.\n" +
            "플레이 모드에서 명중 → 오른손 장착 → 도주 순서와 손 안의 위치를 확인하세요.";
    }

    /// <summary>
    /// 이벤트 전달기 연결 후 기존 포인트를 오른손 아래로 이동
    /// </summary>
    /// <param name="root">적 루트</param>
    /// <param name="target">오른손 선택 결과</param>
    static void ConfigureTarget ( GameObject root, PrefabTarget target )
    {
        Transform hand = ValidateTarget( root, target );
        Connections links = ReadConnections( root );
        if ( links.Handler.gameObject != links.Animator.gameObject )
        {
            AttackAnimationEventRelay relay = links.Animator.GetComponent<AttackAnimationEventRelay>( );
            if ( relay == null )
                relay = links.Animator.gameObject.AddComponent<AttackAnimationEventRelay>( );
            SerializedObject serialized = new SerializedObject( relay );
            serialized.FindProperty( "_handler" ).objectReferenceValue = links.Handler;
            serialized.ApplyModifiedProperties( );
        }
        if ( hand == null )
            return;

        //기존 포인트를 재사용하여 CaptureController/Attacker 참조 유지
        foreach ( Transform point in links.AttackPoints.Append( links.Carry ).Distinct( ) )
        {
            if ( point.parent == hand )
                continue;
            point.SetParent( hand, false );
        }
    }

    /// <summary>
    /// 재임포트한 이벤트의 개수와 실제 프레임 확인
    /// </summary>
    /// <param name="events">실제 클립 이벤트</param>
    /// <param name="functionName">검사할 함수</param>
    /// <param name="frame">요청한 클립 내 프레임</param>
    void VerifyEvent ( AnimationEvent[] events, string functionName, int frame )
    {
        AnimationEvent[] matches = events.Where( entry => entry.functionName == functionName ).ToArray( );
        if ( matches.Length != 1 || Mathf.Abs( matches[0].time * _clip.frameRate - frame ) > 0.1f )
            throw new InvalidOperationException( functionName + ": 재임포트 후 이벤트 프레임이 요청과 다릅니다." );
    }

    /// <summary>
    /// 저장된 전달기 참조와 오른손 부모 연결 확인
    /// </summary>
    /// <param name="root">검사할 적 루트</param>
    /// <param name="target">기대하는 오른손 선택</param>
    /// <returns>모든 연결이 설정과 일치하는지 여부</returns>
    static bool HasExpectedConnections ( GameObject root, PrefabTarget target )
    {
        Transform hand = ValidateTarget( root, target );
        Connections links = ReadConnections( root );
        AttackAnimationEventRelay relay = links.Animator.GetComponent<AttackAnimationEventRelay>( );
        if ( links.Handler.gameObject != links.Animator.gameObject &&
            ( relay == null || new SerializedObject( relay ).FindProperty( "_handler" ).objectReferenceValue != links.Handler ) )
            return false;
        return hand == null || links.AttackPoints.Append( links.Carry ).All( point => point.parent == hand );
    }
}
