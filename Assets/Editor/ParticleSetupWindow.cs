using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 파티클 구성 창 - 곰 부스러기 생성과 제작 완성 효과 연결
/// </summary>
public class ParticleSetupWindow : EditorWindow
{
    [SerializeField] EnemyDemand _bear;		//씬의 곰
    [SerializeField] Transform _mouth;		//입 앞 방출 지점
    [SerializeField] Material _crumbMaterial;		//조각용 파티클 머티리얼, 선택
    [SerializeField] GameObject _successPrefab;		//임포트한 완성 효과
    [SerializeField] Vector3 _successOffset = new( 0f, 0.15f, 0f );		//그릇별 공통 위치 보정
    [SerializeField, Min( 0.01f )] float _successScale = 1f;		//그릇별 공통 크기
    [SerializeField] bool _replace;		//기존 연결 교체 여부

    string _status = string.Empty;		//최근 구성 결과

    private void OnGUI ()
    {
        using ( new EditorGUI.DisabledScope( EditorApplication.isPlayingOrWillChangePlaymode ) )
        {
            _replace = EditorGUILayout.Toggle( "기존 연결 교체", _replace );
            EditorGUILayout.LabelField( "곰 부스러기", EditorStyles.boldLabel );
            _bear = (EnemyDemand)EditorGUILayout.ObjectField( "씬의 곰", _bear, typeof( EnemyDemand ), true );
            _mouth = (Transform)EditorGUILayout.ObjectField( "입 앞 지점 (+Z 방출)", _mouth, typeof( Transform ), true );
            _crumbMaterial = (Material)EditorGUILayout.ObjectField( "조각 머티리얼 (선택)", _crumbMaterial, typeof( Material ), false );
            if ( GUILayout.Button( "곰 부스러기 생성 및 연결" ) )
                CreateCrumbs( );

            EditorGUILayout.Space( );
            EditorGUILayout.LabelField( "제작 완성 효과", EditorStyles.boldLabel );
            _successPrefab = (GameObject)EditorGUILayout.ObjectField( "효과 프리팹", _successPrefab, typeof( GameObject ), false );
            _successOffset = EditorGUILayout.Vector3Field( "공통 위치 보정", _successOffset );
            _successScale = Mathf.Max( 0.01f, EditorGUILayout.FloatField( "공통 크기", _successScale ) );
            if ( GUILayout.Button( "현재 씬 모든 그릇에 완성 효과 연결" ) )
                CreateSceneSuccess( );
        }
        EditorGUILayout.HelpBox(
            "씬 인스턴스에만 적용합니다. 임포트 원본은 변경하지 않습니다. 기존 연결 교체 시 이전 효과 객체는 보존합니다.",
            MessageType.Info );
        if ( string.IsNullOrEmpty( _status ) == false )
            EditorGUILayout.HelpBox( _status, MessageType.Info );
    }


    /// <summary>
    /// 구성 창 열기
    /// </summary>
    [MenuItem( "Tools/Utilities/Initial Setup/Particle Setup" )]
    static void Open ()
    {
        GetWindow<ParticleSetupWindow>( "파티클 구성" );
    }


    /// <summary>
    /// 참조를 연결하고 씬의 프리팹 오버라이드 기록
    /// </summary>
    /// <param name="owner">참조 소유자</param>
    /// <param name="field">연결 필드</param>
    /// <param name="value">연결할 객체</param>
    void Connect ( Component owner, string field, Object value )
    {
        using SerializedObject settings = new( owner );
        settings.FindProperty( field ).objectReferenceValue = value;
        settings.ApplyModifiedProperties( );
        PrefabUtility.RecordPrefabInstancePropertyModifications( owner );
        EditorSceneManager.MarkSceneDirty( owner.gameObject.scene );
    }

    /// <summary>
    /// 이미 연결된 효과 조회
    /// </summary>
    /// <param name="owner">참조 소유자</param>
    /// <param name="field">조회 필드</param>
    /// <returns>기존 효과 또는 없음</returns>
    Object GetReference ( Component owner, string field )
    {
        using SerializedObject settings = new( owner );
        return settings.FindProperty( field ).objectReferenceValue;
    }

    /// <summary>
    /// Undo 가능한 자식 효과 객체 생성
    /// </summary>
    /// <param name="parent">효과 부모</param>
    /// <param name="name">객체 이름</param>
    /// <returns>생성한 객체</returns>
    GameObject CreateRoot ( Transform parent, string name )
    {
        GameObject root = new( name );
        Undo.RegisterCreatedObjectUndo( root, "파티클 구성" );
        Undo.SetTransformParent( root.transform, parent, "파티클 구성" );
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        root.layer = parent.gameObject.layer;
        return root;
    }

    /// <summary>
    /// 기본 URP 파티클 머티리얼 조회 또는 생성
    /// </summary>
    /// <returns>조각 머티리얼 또는 생성 실패 시 없음</returns>
    Material GetCrumbMaterial ()
    {
        if ( _crumbMaterial != null )
            return _crumbMaterial;

        const string path = "Assets/07. Materials/BearCrumbs.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>( path );
        if ( existing != null )
            return existing;

        Shader shader = Shader.Find( "Universal Render Pipeline/Particles/Unlit" );
        if ( shader == null || AssetDatabase.IsValidFolder( "Assets/07. Materials" ) == false )
        {
            _status = "기본 URP 셰이더 또는 머티리얼 폴더가 없습니다. 조각용 머티리얼을 직접 지정해 주세요.";
            return null;
        }
        //다른 에셋이 같은 경로를 사용하더라도 덮어쓰지 않음
        Material material = new( shader );
        material.name = "BearCrumbs";
        AssetDatabase.CreateAsset( material, AssetDatabase.GenerateUniqueAssetPath( path ) );
        return material;
    }

    /// <summary>
    /// 작은 조각의 방출과 이동 설정
    /// </summary>
    /// <param name="particle">부스러기 효과</param>
    /// <param name="material">조각 머티리얼</param>
    /// <param name="mesh">조각 메시</param>
    void ConfigureCrumbs ( ParticleSystem particle, Material material, Mesh mesh )
    {
        particle.Stop( true, ParticleSystemStopBehavior.StopEmittingAndClear );
        ParticleSystem.MainModule main = particle.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve( 0.25f, 0.5f );
        main.startSpeed = new ParticleSystem.MinMaxCurve( 0.25f, 0.65f );
        main.startSize = new ParticleSystem.MinMaxCurve( 0.01f, 0.025f );
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve( 0f, Mathf.PI * 2f );
        main.startRotationY = new ParticleSystem.MinMaxCurve( 0f, Mathf.PI * 2f );
        main.startRotationZ = new ParticleSystem.MinMaxCurve( 0f, Mathf.PI * 2f );
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color( 0.38f, 0.16f, 0.05f ), new Color( 0.85f, 0.58f, 0.25f ) );
        main.gravityModifier = 0.45f;
        main.maxParticles = 32;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.EmissionModule emission = particle.emission;
        emission.rateOverTime = 15f;
        ParticleSystem.ShapeModule shape = particle.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.01f;
        ParticleSystem.SizeOverLifetimeModule size = particle.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve( 1f,
            AnimationCurve.Linear( 0f, 1f, 1f, 0f ) );
        ParticleSystem.RotationOverLifetimeModule rotation = particle.rotationOverLifetime;
        rotation.enabled = true;
        rotation.separateAxes = true;
        rotation.x = 2f;
        rotation.y = 3f;
        rotation.z = 1f;

        ParticleSystemRenderer renderer = particle.GetComponent<ParticleSystemRenderer>( );
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = mesh;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    /// <summary>
    /// 선택한 곰의 입 지점에 부스러기 생성과 연결
    /// </summary>
    void CreateCrumbs ()
    {
        if ( IsSceneTarget( _bear ) == false || _mouth == null ||
             ( _mouth != _bear.transform && _mouth.IsChildOf( _bear.transform ) == false ) )
        {
            _status = "씬의 곰과 그 곰 아래의 입 앞 지점을 지정해 주세요.";
            return;
        }
        if ( _replace == false && GetReference( _bear, "_eatEffect" ) != null )
        {
            _status = "먹기 효과가 이미 연결되어 있습니다. 교체하려면 기존 연결 교체를 켜 주세요.";
            return;
        }
        Mesh mesh = Resources.GetBuiltinResource<Mesh>( "Cube.fbx" );
        if ( mesh == null )
        {
            _status = "기본 조각 메시를 찾지 못했습니다.";
            return;
        }
        Material material = GetCrumbMaterial( );
        if ( material == null )
            return;

        Undo.IncrementCurrentGroup( );
        int group = Undo.GetCurrentGroup( );
        Undo.SetCurrentGroupName( "곰 부스러기 생성" );
        GameObject root = CreateRoot( _mouth, "BearCrumbs" );
        root.SetActive( false );
        ParticleSystem particle = Undo.AddComponent<ParticleSystem>( root );
        ConfigureCrumbs( particle, material, mesh );
        Connect( _bear, "_eatEffect", particle );
        root.SetActive( true );
        Undo.CollapseUndoOperations( group );
        Selection.activeGameObject = root;
        _status = "곰 부스러기 생성과 Eat Effect 연결 완료. 입 지점의 +Z 방향과 실제 크기를 확인해 주세요.";
    }

    /// <summary>
    /// 반복 사용하는 Cartoon FX 인스턴스의 자동 삭제 해제
    /// </summary>
    /// <param name="instance">씬에 배치한 효과</param>
    void PreserveImportedEffect ( GameObject instance )
    {
        foreach ( MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>( true ) )
        {
            if ( behaviour == null || behaviour.GetType( ).FullName != "CartoonFX.CFXR_Effect" )
                continue;
            using SerializedObject settings = new( behaviour );
            SerializedProperty clear = settings.FindProperty( "clearBehavior" );
            //확인한 패키지의 None 값, 원본 에셋은 수정하지 않음
            clear.enumValueIndex = 0;
            settings.ApplyModifiedProperties( );
            PrefabUtility.RecordPrefabInstancePropertyModifications( behaviour );
        }
    }

    /// <summary>
    /// 임포트한 효과를 그릇 아래에 배치하고 제작 효과 연결
    /// </summary>
    /// <param name="product">배치할 조리용 그릇</param>
    /// <returns>효과 구성 여부</returns>
    bool CreateSuccess ( CookController product )
    {
        if ( IsSceneTarget( product ) == false || _successPrefab == null ||
             PrefabUtility.IsPartOfPrefabAsset( _successPrefab ) == false )
        {
            _status = "씬의 조리용 그릇과 프로젝트의 효과 프리팹을 지정해 주세요.";
            return false;
        }
        if ( _successPrefab.GetComponentsInChildren<ParticleSystem>( true ).Length == 0 )
        {
            _status = "선택한 프리팹에는 ParticleSystem이 없습니다. VFX Graph 효과는 이 연결 방식으로 재생할 수 없습니다.";
            return false;
        }
        ProductionEffect effect = GetReference( product, "_productionEffect" ) as ProductionEffect;
        if ( effect != null && _replace == false && GetReference( effect, "_successParticle" ) != null )
        {
            _status = "완성 효과가 이미 연결되어 있습니다. 교체하려면 기존 연결 교체를 켜 주세요.";
            return false;
        }

        Undo.IncrementCurrentGroup( );
        int group = Undo.GetCurrentGroup( );
        Undo.SetCurrentGroupName( "제작 완성 효과 구성" );
        GameObject root = CreateRoot( product.transform, "ProductionSuccess" );
        root.transform.localPosition = _successOffset;
        root.transform.localScale = Vector3.one * _successScale;
        //참조 할당 전 ProductionEffect.OnEnable이 실행되지 않도록 비활성 상태에서 구성
        root.SetActive( false );
        //최상위 제어기는 입자를 내지 않고 모든 자식 효과를 함께 제어
        ParticleSystem control = Undo.AddComponent<ParticleSystem>( root );
        ParticleSystem.MainModule controlMain = control.main;
        controlMain.playOnAwake = false;
        controlMain.loop = false;
        ParticleSystem.EmissionModule controlEmission = control.emission;
        controlEmission.enabled = false;
        control.GetComponent<ParticleSystemRenderer>( ).enabled = false;
        control.Stop( true, ParticleSystemStopBehavior.StopEmittingAndClear );

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab( _successPrefab, root.transform );
        Undo.RegisterCreatedObjectUndo( instance, "제작 완성 효과 구성" );
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        PreserveImportedEffect( instance );
        foreach ( ParticleSystem particle in instance.GetComponentsInChildren<ParticleSystem>( true ) )
        {
            Undo.RecordObject( particle, "제작 완성 효과 설정" );
            particle.Stop( true, ParticleSystemStopBehavior.StopEmittingAndClear );
            ParticleSystem.MainModule main = particle.main;
            main.playOnAwake = false;
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.None;
            PrefabUtility.RecordPrefabInstancePropertyModifications( particle );
        }
        if ( effect == null )
            effect = Undo.AddComponent<ProductionEffect>( root );
        Connect( effect, "_successParticle", control );
        Connect( product, "_productionEffect", effect );
        root.SetActive( true );
        Undo.CollapseUndoOperations( group );
        Selection.activeGameObject = root;
        return true;
    }

    /// <summary>
    /// 현재 씬의 모든 조리용 그릇에 공통 효과 일괄 연결
    /// </summary>
    void CreateSceneSuccess ()
    {
        if ( _successPrefab == null ||
             PrefabUtility.IsPartOfPrefabAsset( _successPrefab ) == false ||
             _successPrefab.GetComponentsInChildren<ParticleSystem>( true ).Length == 0 )
        {
            _status = "ParticleSystem이 포함된 효과 프리팹을 지정해 주세요.";
            return;
        }
        UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene( );
        int created = 0; //새로 연결한 그릇
        int skipped = 0; //기존 연결을 보존한 그릇
        Undo.IncrementCurrentGroup( );
        int group = Undo.GetCurrentGroup( );
        Undo.SetCurrentGroupName( "현재 씬 제작 효과 일괄 연결" );
        foreach ( CookController product in Object.FindObjectsByType<CookController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None ) )
        {
            if ( IsSceneTarget( product ) == false || product.gameObject.scene != scene )
                continue;
            if ( CreateSuccess( product ) == true )
                created++;
            else
                skipped++;
        }
        Undo.CollapseUndoOperations( group );
        _status = $"현재 씬: 연결 {created}개, 기존 연결 보존 {skipped}개. 위치 보정과 크기는 모든 새 효과에 동일하게 적용했습니다.";
    }


    /// <summary>
    /// 씬에 배치한 대상인지 확인
    /// </summary>
    /// <param name="target">구성 대상</param>
    /// <returns>씬 대상 여부</returns>
    bool IsSceneTarget ( Component target )
    {
        return target != null && EditorUtility.IsPersistent( target ) == false &&
            target.gameObject.scene.IsValid( ) &&
            target.gameObject.scene.isLoaded == true &&
            PrefabStageUtility.GetPrefabStage( target.gameObject ) == null;
    }

}
