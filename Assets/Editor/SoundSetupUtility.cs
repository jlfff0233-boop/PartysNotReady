using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 사운드 구성 도구 - 컴포넌트, 재생기와 데이터 참조 일괄 연결
/// </summary>
public static class SoundSetupUtility
{
    public const int Jump = 25;		//공용 점프 데이터 위치
    public const int PickUp = 26;		//공용 집기 데이터 위치
    public const int DogSatisfied = 27;		//공용 개 만족 데이터 위치
    public const int CrowTake = 28;		//공용 음식 확보 데이터 위치


    /// <summary>
    /// 변경 객체의 프리팹 오버라이드와 씬 변경 표시
    /// </summary>
    /// <param name="target">변경 객체</param>
    static void RecordChange ( Component target )
    {
        EditorUtility.SetDirty( target );
        if ( PrefabUtility.IsPartOfPrefabInstance( target ) == true )
            PrefabUtility.RecordPrefabInstancePropertyModifications( target );
        EditorSceneManager.MarkSceneDirty( target.gameObject.scene );
    }

    /// <summary>
    /// 대상 범위의 사운드 연결 담당 컴포넌트 조회
    /// </summary>
    /// <param name="roots">조회할 최상위 객체</param>
    /// <returns>중복을 제외한 담당 컴포넌트</returns>
    public static List<Component> CollectTargets ( IEnumerable<GameObject> roots )
    {
        List<Component> targets = new( );		//선택 범위의 담당 컴포넌트
        HashSet<Component> found = new( );		//중복 조회된 컴포넌트
        foreach ( GameObject root in roots )
        {
            foreach ( Component target in root.GetComponentsInChildren<Component>( true ) )
            {
                if ( target is Player || target is Npc ||
                     target is EnemyBothersome || target is EnemyDemand ||
                     target is EnemyHarm || target is EnemyKidnap ||
                     target is EnemySteal ||
                     target is CookController || target is ShopPresenter ||
                     target is GameFlowController || target is Button )
                {
                    if ( found.Add( target ) == true )
                        targets.Add( target );
                }
            }
        }
        return targets;
    }

    /// <summary>
    /// 기존 참조가 없으면 같은 객체의 컴포넌트 조회 또는 추가
    /// </summary>
    /// <typeparam name="T">연결할 컴포넌트 타입</typeparam>
    /// <param name="owner">참조를 소유한 컴포넌트</param>
    /// <param name="field">참조 필드</param>
    /// <returns>연결할 컴포넌트</returns>
    static T GetComponent<T> ( Component owner, string field ) where T : Component
    {
        using SerializedObject settings = new( owner );		//담당 참조
        T component = settings.FindProperty( field ).objectReferenceValue as T;
        if ( component != null )
            return component;
        component = owner.GetComponent<T>( );
        return component != null ? component : Undo.AddComponent<T>( owner.gameObject );
    }

    /// <summary>
    /// 새 자식 생성과 Undo 등록
    /// </summary>
    /// <param name="parent">부모 객체</param>
    /// <param name="name">자식 이름</param>
    /// <returns>생성한 객체</returns>
    static GameObject CreateChild ( Transform parent, string name )
    {
        GameObject child = new( name );		//새 사운드 객체
        Undo.RegisterCreatedObjectUndo( child, "사운드 구성" );
        Undo.SetTransformParent( child.transform, parent, "사운드 구성" );
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;
        return child;
    }

    /// <summary>
    /// 역할별 전용 자식의 재생기 조회 또는 생성
    /// </summary>
    /// <param name="owner">재생기를 사용하는 담당 컴포넌트</param>
    /// <param name="field">재생기 참조 필드</param>
    /// <param name="name">전용 자식 이름</param>
    /// <returns>전용 재생기</returns>
    static SoundPlayer GetPlayer ( Component owner, string field, string name )
    {
        using SerializedObject settings = new( owner );		//기존 재생기 참조
        SoundPlayer player = settings.FindProperty( field ).objectReferenceValue as SoundPlayer;
        if ( player != null )
            return player;
        Transform child = owner.transform.Find( name );		//이전 도구 구성 객체
        GameObject target = child != null ? child.gameObject : CreateChild( owner.transform, name );
        player = target.GetComponent<SoundPlayer>( );
        return player != null ? player : Undo.AddComponent<SoundPlayer>( target );
    }

    /// <summary>
    /// 비어 있는 참조 또는 덮어쓰기 대상 연결
    /// </summary>
    /// <param name="settings">변경할 설정</param>
    /// <param name="field">참조 필드</param>
    /// <param name="value">연결할 객체</param>
    /// <param name="overwrite">기존 참조 교체 여부</param>
    static void SetReference ( SerializedObject settings, string field, Object value, bool overwrite )
    {
        //선택하지 않은 공용 데이터는 기존 참조를 지우지 않음
        if ( value == null )
            return;
        SerializedProperty property = settings.FindProperty( field );		//연결할 참조
        if ( overwrite == true || property.objectReferenceValue == null )
            property.objectReferenceValue = value;
    }

    /// <summary>
    /// 담당 컴포넌트의 참조 연결 적용
    /// </summary>
    /// <param name="owner">참조를 소유한 컴포넌트</param>
    /// <param name="field">필드 이름</param>
    /// <param name="value">연결 객체</param>
    /// <param name="overwrite">기존 참조 교체 여부</param>
    static void Connect ( Component owner, string field, Object value, bool overwrite )
    {
        using SerializedObject settings = new( owner );		//담당 참조 설정
        SetReference( settings, field, value, overwrite );
        if ( settings.ApplyModifiedProperties( ) == true )
            RecordChange( owner );
    }

    /// <summary>
    /// 사운드 데이터 필드 일괄 연결
    /// </summary>
    /// <param name="target">사운드 컴포넌트</param>
    /// <param name="sounds">이름별 순서로 준비한 데이터</param>
    /// <param name="overwrite">기존 참조 교체 여부</param>
    /// <param name="fields">필드 이름과 데이터 위치</param>
    static void Bind ( Component target, SoundData [ ] sounds, bool overwrite,
        params (string field, int index) [ ] fields )
    {
        using SerializedObject settings = new( target );		//사운드 데이터 참조
        foreach ( var field in fields )
            SetReference( settings, field.field, sounds [ field.index ], overwrite );
        if ( settings.ApplyModifiedProperties( ) == true )
            RecordChange( target );
    }

    /// <summary>
    /// 단발음과 반복음 소스 분리 및 출력 설정
    /// </summary>
    /// <param name="player">재생기</param>
    /// <param name="output">효과음 출력 그룹</param>
    /// <param name="world">월드 공간 출력 여부</param>
    /// <param name="configureExisting">기존 소스 설정 변경 여부</param>
    static void ConfigurePlayer ( SoundPlayer player, AudioMixerGroup output,
        bool world, bool configureExisting )
    {
        using SerializedObject settings = new( player );		//재생기 소스 참조
        AudioSource oneShot = settings.FindProperty( "_oneShot" ).objectReferenceValue as AudioSource;
        AudioSource loop = settings.FindProperty( "_loop" ).objectReferenceValue as AudioSource;
        bool newOneShot = oneShot == null;		//단발음 소스 연결 필요 여부
        bool newLoop = loop == null || loop == oneShot;		//반복음 소스 분리 필요 여부
        AudioSource [ ] sources = player.GetComponents<AudioSource>( );		//미연결 소스 재사용 후보
        if ( newOneShot == true )
        {
            foreach ( AudioSource source in sources )
            {
                if ( source != loop )
                {
                    oneShot = source;
                    break;
                }
            }
            if ( oneShot == null )
                oneShot = Undo.AddComponent<AudioSource>( player.gameObject );
        }
        if ( newLoop == true )
        {
            loop = null;
            foreach ( AudioSource source in sources )
            {
                if ( source != oneShot )
                {
                    loop = source;
                    break;
                }
            }
            if ( loop == null )
                loop = Undo.AddComponent<AudioSource>( player.gameObject );
        }

        SetReference( settings, "_oneShot", oneShot, true );
        SetReference( settings, "_loop", loop, true );
        settings.FindProperty( "_ignoreListenerPause" ).boolValue = world == false;
        if ( settings.ApplyModifiedProperties( ) == true )
            RecordChange( player );
        if ( newOneShot == true || configureExisting == true )
            ConfigureSource( oneShot, output, world, false );
        if ( newLoop == true || configureExisting == true )
            ConfigureSource( loop, output, world, true );
    }

    /// <summary>
    /// 음원 소스의 기본 재생과 공간 출력 설정
    /// </summary>
    /// <param name="source">설정할 소스</param>
    /// <param name="output">효과음 출력 그룹</param>
    /// <param name="world">월드 공간 출력 여부</param>
    /// <param name="loop">반복 재생 여부</param>
    static void ConfigureSource ( AudioSource source, AudioMixerGroup output, bool world, bool loop )
    {
        Undo.RecordObject( source, "사운드 출력 설정" );
        source.playOnAwake = false;
        source.loop = loop;
        source.volume = 1f;
        source.pitch = 1f;
        source.spatialBlend = world == true ? 1f : 0f;
        //미선택 출력 그룹은 기존 설정을 유지
        if ( output != null )
            source.outputAudioMixerGroup = output;
        RecordChange( source );
    }

    /// <summary>
    /// 씬 루트의 UI 공용 재생기 조회 또는 생성
    /// </summary>
    /// <param name="scene">대상 씬</param>
    /// <returns>UI 공용 재생기 또는 프리팹 편집 중 없음</returns>
    static SoundPlayer GetUiPlayer ( Scene scene )
    {
        //프리팹에 씬 객체 참조를 저장하지 않고 씬 적용에서 연결
        var stage = PrefabStageUtility.GetCurrentPrefabStage( );
        if ( stage != null && stage.scene == scene )
            return null;
        foreach ( GameObject root in scene.GetRootGameObjects( ) )
        {
            if ( root.name == "UI 사운드" && root.TryGetComponent( out SoundPlayer existing ) == true )
                return existing;
        }
        GameObject target = new( "UI 사운드" );		//화면 닫기와 독립적인 재생 객체
        SceneManager.MoveGameObjectToScene( target, scene );
        Undo.RegisterCreatedObjectUndo( target, "사운드 구성" );
        return Undo.AddComponent<SoundPlayer>( target );
    }

    /// <summary>
    /// 월드 담당의 사운드 컴포넌트와 전용 재생기 연결
    /// </summary>
    /// <typeparam name="T">사운드 컴포넌트 타입</typeparam>
    /// <param name="owner">기능 담당</param>
    /// <param name="field">사운드 참조 필드</param>
    /// <param name="playerField">재생기 참조 필드</param>
    /// <param name="playerName">전용 자식 이름</param>
    /// <param name="output">출력 그룹</param>
    /// <param name="configureExisting">기존 소스 설정 변경 여부</param>
    /// <returns>연결한 사운드 컴포넌트</returns>
    static T SetupWorld<T> ( Component owner, string field, string playerField,
        string playerName, AudioMixerGroup output, bool configureExisting ) where T : Component
    {
        T sound = GetComponent<T>( owner, field );		//역할별 사운드
        SoundPlayer player = GetPlayer( owner, playerField, playerName );		//전용 재생기
        Connect( owner, field, sound, true );
        Connect( owner, playerField, player, true );
        ConfigurePlayer( player, output, true, configureExisting );
        return sound;
    }

    /// <summary>
    /// UI 담당의 사운드 컴포넌트와 공용 재생기 연결
    /// </summary>
    /// <typeparam name="T">사운드 컴포넌트 타입</typeparam>
    /// <param name="owner">기능 담당</param>
    /// <param name="field">사운드 참조 필드</param>
    /// <param name="output">출력 그룹</param>
    /// <param name="overwrite">기존 참조 교체 여부</param>
    /// <param name="configureExisting">기존 소스 설정 변경 여부</param>
    /// <returns>연결한 사운드 컴포넌트</returns>
    static T SetupUi<T> ( Component owner, string field, AudioMixerGroup output,
        bool overwrite, bool configureExisting ) where T : Component
    {
        T sound = GetComponent<T>( owner, field );		//UI 사운드
        Connect( owner, field, sound, true );
        ConnectUiPlayer( sound, output, overwrite, configureExisting );
        return sound;
    }

    /// <summary>
    /// UI 공용 재생기 연결과 출력 구성
    /// </summary>
    /// <param name="sound">UI 사운드 컴포넌트</param>
    /// <param name="output">출력 그룹</param>
    /// <param name="overwrite">기존 참조 교체 여부</param>
    /// <param name="configureExisting">기존 소스 설정 변경 여부</param>
    static void ConnectUiPlayer ( Component sound, AudioMixerGroup output,
        bool overwrite, bool configureExisting )
    {
        using SerializedObject settings = new( sound );		//UI 재생기 참조
        SoundPlayer player = settings.FindProperty( "_soundPlayer" ).objectReferenceValue as SoundPlayer;
        if ( player == null || overwrite == true )
            player = GetUiPlayer( sound.gameObject.scene );
        if ( player == null )
            return;
        Connect( sound, "_soundPlayer", player, overwrite );
        ConfigurePlayer( player, output, false, configureExisting );
    }

    /// <summary>
    /// 대상별 사운드 구성과 데이터 대응 적용
    /// </summary>
    /// <param name="targets">담당 컴포넌트 목록</param>
    /// <param name="sounds">기본 25개와 공용 4개 데이터</param>
    /// <param name="output">효과음 출력 그룹</param>
    /// <param name="overwrite">기존 데이터와 UI 재생기 참조 교체 여부</param>
    /// <param name="configureExisting">기존 소스 설정 변경 여부</param>
    public static void Apply ( List<Component> targets, SoundData [ ] sounds,
        AudioMixerGroup output, bool overwrite, bool configureExisting )
    {
        foreach ( Component owner in targets )
        {
            switch ( owner )
            {
                case Player:
                    Bind( SetupWorld<PlayerSound>( owner, "_playerSound", "_soundPlayer", "캐릭터 사운드", output, configureExisting ),
                        sounds, overwrite, ("_walkSound", 0), ("_capturedSound", 1), ("_jumpSound", Jump), ("_landSound", 2),
                        ("_pickUpSound", PickUp), ("_placeSound", 11), ("_throwSound", 12) );
                    break;
                case Npc:
                    Bind( SetupWorld<NpcSound>( owner, "_npcSound", "_soundPlayer", "캐릭터 사운드", output, configureExisting ),
                        sounds, overwrite, ("_walkSound", 0), ("_capturedSound", 1) );
                    break;
                case EnemyBothersome:
                case EnemyDemand:
                case EnemyHarm:
                case EnemyKidnap:
                case EnemySteal:
                    SoundBase enemy = owner switch
                    {
                        EnemyBothersome => SetupWorld<BothersomeSound>( owner, "_enemySound", "_soundPlayer", "캐릭터 사운드", output, configureExisting ),
                        EnemyDemand => SetupWorld<DemandSound>( owner, "_enemySound", "_soundPlayer", "캐릭터 사운드", output, configureExisting ),
                        EnemyHarm => SetupWorld<HarmSound>( owner, "_enemySound", "_soundPlayer", "캐릭터 사운드", output, configureExisting ),
                        EnemyKidnap => SetupWorld<KidnapSound>( owner, "_enemySound", "_soundPlayer", "캐릭터 사운드", output, configureExisting ),
                        _ => SetupWorld<StealSound>( owner, "_enemySound", "_soundPlayer", "캐릭터 사운드", output, configureExisting )
                    };
                    if ( owner is EnemySteal )
                        Bind( enemy, sounds, overwrite, ("_flightSound", 10), ("_takeSound", CrowTake) );
                    else
                        Bind( enemy, sounds, overwrite, ("_walkSound", 0) );
                    if ( owner is EnemyBothersome )
                        Bind( enemy, sounds, overwrite, ("_requestSound", 3), ("_satisfiedSound", DogSatisfied) );
                    if ( owner is EnemyDemand )
                        Bind( enemy, sounds, overwrite, ("_eatSound", 4), ("_satisfiedSound", 5), ("_madSound", 6), ("_attackSound", 7), ("_captureSound", 8) );
                    if ( owner is EnemyHarm )
                        Bind( enemy, sounds, overwrite, ("_attackSound", 7), ("_collectSound", 9) );
                    if ( owner is EnemyKidnap )
                        Bind( enemy, sounds, overwrite, ("_captureSound", 8) );
                    break;
                case CookController:
                    Bind( SetupWorld<ProductionSound>( owner, "_productionSound", "_productionSoundPlayer", "제작 사운드", output, configureExisting ),
                        sounds, overwrite, ("_startSound", 13), ("_processSound", 14), ("_successSound", 15), ("_failureSound", 16), ("_completeSound", 17) );
                    break;
                case ShopPresenter:
                    Bind( SetupUi<ShopSound>( owner, "_sound", output, overwrite, configureExisting ),
                        sounds, overwrite, ("_purchaseSuccessSound", 19), ("_purchaseFailureSound", 20) );
                    break;
                case GameFlowController:
                    Bind( SetupUi<GameFlowSound>( owner, "_flowSound", output, overwrite, configureExisting ),
                        sounds, overwrite, ("_missionCompletedSound", 21), ("_finalRushSound", 22) );
                    Bind( SetupUi<GameResultSound>( owner, "_resultSound", output, overwrite, configureExisting ),
                        sounds, overwrite, ("_successSound", 23), ("_failureSound", 24) );
                    break;
                case Button button:
                    ButtonSound click = button.GetComponent<ButtonSound>( );		//버튼 클릭 사운드
                    if ( click == null )
                        click = Undo.AddComponent<ButtonSound>( button.gameObject );
                    Connect( click, "_button", button, overwrite );
                    ConnectUiPlayer( click, output, overwrite, configureExisting );
                    Bind( click, sounds, overwrite, ("_clickSound", 18) );
                    break;
            }
        }
    }
}
