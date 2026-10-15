using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>
/// HUD, 애니메이션, 화면 연출과 사운드 구성 검사
/// </summary>
public static class PlayPresentationSetupChecker
{
    /// <summary>
    /// 표현 기능의 필수 연결과 선택 음향 검사
    /// </summary>
    /// <param name="context">검사 문맥</param>
    /// <param name="owner">표현 기능 소유자</param>
    public static void Check ( PlayReadinessContext context, Object owner )
    {
        string[] fields = owner.GetType( ).Name switch
        {
            "PlayerHud" => new[] { "_holdGauge", "_holdEffect", "_actionText", "_rescueGauge", "_rescueGuide", "_rescueEffect", "_rescueKeyEffect" },
            "GameFlowHud" => new[] { "_progressView", "_missionView", "_logView" },
            "MissionView" => new[] { "_missionBox", "_titleText", "_missionText" },
            "MissionGoalView" => new[] { "_nameText", "_effect" },
            "GameProgressView" => new[] { "_progressBox", "_remainingTimeText", "_moodPercentText", "_moodBar", "_guestCountText", "_guestCountBar", "_notificationText", "_noticeEffect" },
            "GameResultView" => new[] { "_resultCanvas", "_titleText", "_resultEtcText", "_playTimeText", "_guestText", "_missionResultText", "_titleButton", "_scoreEtcText", "_content", "_logTextPrefab", "_totalScoreText", "_panelEffect" },
            "ProduceHud" => new[] { "_recipePanel", "_actionPanel", "_closeButton", "_noticeText", "_noticeEffect", "_recipe", "_minigame" },
            "ProduceRecipePanel" => new[] { "_currentRecipeText", "_recipeListRoot", "_recipeTextPrefab", "_sortDropdown", "_prevButton", "_nextButton", "_cookButton" },
            "ProduceMinigamePanel" => new[] { "_actionButton", "_successCountText", "_progressBar", "_timingBarGroup", "_clickAreaGroup", "_timingBar", "_handle", "_hitPointShowArea", "_hitPointPrefab" },
            "SupportItemSlotView" => new[] { "_icon", "_usesCountText" },
            "ShopItemSlot" => new[] { "_icon", "_nameText", "_ownedText", "_buyText", "_nameEffect", "_ownedEffect", "_countInput", "_minusButton", "_plusButton", "_buyButton" },
            "ShopView" => new[] { "_goldText", "_messageText", "_closeButton", "_goldEffect", "_messageEffect", "_slotPrefab", "_content", "_popupPrefab", "_viewport" },
            "ProgressBarHud" => new[] { "_progressObj", "_progressBar" },
            "PanelEffect" or "TextEffect" or "KeyPromptEffect" => new[] { "_target", "_group" },
            "GaugeEffect" => new[] { "_fill", "_target" },
            "ButtonEffect" => new[] { "_button", "_target" },
            "CameraEffect" or "StunEffect" or "CaptureEffect" => new[] { "_pivot" },
            "ScreenEffect" => new[] { "_volume" },
            "AnimatorHandler" => new[] { "_animator" },
            "SoundPlayer" => new[] { "_oneShot", "_loop" },
            "SoundManager" => new[] { "_mixer", "_bgm", "_sfx" },
            _ => Array.Empty<string>( )
        };
        if ( fields.Length > 0 )
            context.Require( owner, "표현", fields );
        if ( owner is PlayerHud )
            context.Optional( owner, "표현", "_recipeGuide" );
        if ( owner is ProduceMinigamePanel )
            context.Optional( owner, "표현", "_roundTimeBar" );
        if ( owner is MissionView )
        {
            using SerializedObject serialized = new( owner );
            Object root = serialized.FindProperty( "_goalRoot" ).objectReferenceValue;
            Object prefab = serialized.FindProperty( "_goalPrefab" ).objectReferenceValue;
            bool paired = ( root == null ) == ( prefab == null );
            context.Report( owner, "표현", "mission-list-pair", "_goalRoot / _goalPrefab",
                paired == true ? PlayReadinessStatus.Passed : PlayReadinessStatus.Error,
                paired == false ? "목표 목록 루트와 프리팹은 함께 연결하거나 함께 비워야 합니다."
                    : root == null ? "목표 목록을 사용하지 않고 기존 _missionText로 표시합니다." : "목표 목록 루트·프리팹 연결됨", prefab );
        }
        if ( owner is BillboardUI billboard )
        {
            using SerializedObject serialized = new( owner );
            Camera camera = serialized.FindProperty( "_cam" ).objectReferenceValue as Camera;
            bool sceneAvailable = context.CanCheckScene( billboard );
            if ( camera == null && sceneAvailable == true )
                camera = Camera.main;
            context.Report( owner, "표현", "billboard-camera", "_cam",
                camera != null ? PlayReadinessStatus.Passed : sceneAvailable == true ? PlayReadinessStatus.Error : PlayReadinessStatus.Blocked,
                camera != null ? "명시한 카메라 또는 실제 Camera.main 대체 경로 사용 가능"
                    : sceneAvailable == true ? "명시한 카메라와 Camera.main 모두 없습니다. 카메라를 연결하거나 MainCamera 태그를 확인해 주세요."
                    : "자동 Camera.main 경로는 실제 씬 전체에서 확인해야 합니다.", camera );
        }
        if ( owner.GetType( ).Name == "PlayerHud" || owner.GetType( ).Name == "ProduceRecipePanel" || owner is TargetHighlight )
        {
            using SerializedObject serialized = new( owner );
            string field = owner is TargetHighlight ? "_renderers" : owner.GetType( ).Name == "PlayerHud" ? "_supportSlots" : "_ingredientTexts";
            SerializedProperty array = serialized.FindProperty( field );
            if ( array.arraySize == 0 )
                context.Report( owner, "표현", "view-array", field, PlayReadinessStatus.Warning,
                    "표시 대상 배열이 비어 있습니다. 해당 슬롯·재료 표시·강조 대상이 필요하면 등록해 주세요." );
            for ( int i = 0; i < array.arraySize; i++ )
                context.Require( owner, "표현", $"{field}.Array.data[{i}]" );
        }
        if ( owner is AnimatorHandler )
            CheckAnimator( context, owner );
        if ( owner is ScreenEffect )
        {
            using SerializedObject serialized = new( owner );
            Volume volume = serialized.FindProperty( "_volume" ).objectReferenceValue as Volume;
            if ( volume != null )
            {
                bool valid = volume.sharedProfile != null && volume.sharedProfile.TryGet<Vignette>( out _ ) == true;
                context.Report( owner, "표현", "vignette", "_volume.sharedProfile",
                    valid == true ? PlayReadinessStatus.Passed : PlayReadinessStatus.Error,
                    valid == true ? "공유 Volume Profile에 Vignette 존재 · 카메라 Volume Layer와 실제 효과는 플레이 확인 필요"
                        : "화면 연출 Volume의 Profile에 Vignette를 추가해 주세요.", volume.sharedProfile );
            }
        }
        if ( owner is PlayScene )
            CheckCameraVolume( context, owner );
        if ( owner is SoundData sound )
            CheckSoundData( context, sound );
        if ( owner is MonoBehaviour )
            CheckSoundReferences( context, owner );
        if ( owner is SoundPlayer )
        {
            using SerializedObject serialized = new( owner );
            AudioSource oneShot = serialized.FindProperty( "_oneShot" ).objectReferenceValue as AudioSource;
            AudioSource loop = serialized.FindProperty( "_loop" ).objectReferenceValue as AudioSource;
            if ( oneShot != null && loop != null )
                context.Report( owner, "표현", "audio-sources", "_oneShot / _loop",
                    oneShot != loop ? PlayReadinessStatus.Passed : PlayReadinessStatus.Error,
                    "단발음과 반복음에는 서로 다른 AudioSource를 연결해야 합니다." );
        }
        if ( owner is AudioSource audio )
        {
            context.Report( owner, "표현", "audio-routing", "outputAudioMixerGroup",
                audio.outputAudioMixerGroup == null ? PlayReadinessStatus.Warning : PlayReadinessStatus.Passed,
                $"Mixer: {( audio.outputAudioMixerGroup == null ? "미연결" : audio.outputAudioMixerGroup.name )} · SpatialBlend {audio.spatialBlend} · PlayOnAwake {audio.playOnAwake}", audio.outputAudioMixerGroup );
        }
        if ( owner.GetType( ).Name == "EnemySound" )
            context.Report( owner, "표현", "legacy-sound", "EnemySound", PlayReadinessStatus.Warning,
                "구 공통 적 사운드가 남아 있습니다. 적별 전용 Sound의 연결을 확인한 후 불필요한 컴포넌트를 정리해 주세요." );
    }

    /// <summary>
    /// 연결된 Controller의 실제 애니메이션 파라미터 검사
    /// </summary>
    /// <param name="context">검사 문맥</param>
    /// <param name="owner">애니메이터 핸들러</param>
    static void CheckAnimator ( PlayReadinessContext context, Object owner )
    {
        using SerializedObject serialized = new( owner );
        Animator animator = serialized.FindProperty( "_animator" ).objectReferenceValue as Animator;
        if ( animator == null )
            return;
        context.Report( owner, "표현", "animator-controller", "_animator.runtimeAnimatorController",
            animator.runtimeAnimatorController == null ? PlayReadinessStatus.Error : PlayReadinessStatus.Passed,
            "Animator Controller를 연결해 주세요.", animator.runtimeAnimatorController );
        if ( animator.runtimeAnimatorController == null )
            return;
        Dictionary<string, AnimatorControllerParameterType> expected = new( )
        {
            { "MoveSpeed", AnimatorControllerParameterType.Float },
            { "Jump", AnimatorControllerParameterType.Trigger },
            { "Attack", AnimatorControllerParameterType.Trigger },
            { "Kidnap", AnimatorControllerParameterType.Bool },
            { "IsCaptured", AnimatorControllerParameterType.Bool }
        };
        foreach ( KeyValuePair<string, AnimatorControllerParameterType> pair in expected )
        {
            bool valid = Array.Exists( animator.parameters, p => p.name == pair.Key && p.type == pair.Value );
            context.Report( owner, "표현", "animator-parameter", pair.Key,
                valid == true ? PlayReadinessStatus.Passed : PlayReadinessStatus.Error,
                $"Controller 파라미터 {pair.Key}: {pair.Value} 필요", animator.runtimeAnimatorController );
        }
        context.Report( owner, "표현", "root-motion", "_animator.applyRootMotion",
            animator.applyRootMotion == true ? PlayReadinessStatus.Warning : PlayReadinessStatus.Passed,
            "스크립트 이동과 중복되지 않도록 Apply Root Motion을 꺼 주세요." );
        if ( animator.avatar != null )
            context.Report( owner, "표현", "avatar", "_animator.avatar",
                animator.avatar.isValid == true ? PlayReadinessStatus.Passed : PlayReadinessStatus.Error,
                "연결된 Avatar의 유효성 검사", animator.avatar );
    }

    /// <summary>
    /// 음원 후보와 원본 음량·피치·간격 검사
    /// </summary>
    /// <param name="context">검사 문맥</param>
    /// <param name="sound">사운드 설정</param>
    static void CheckSoundData ( PlayReadinessContext context, SoundData sound )
    {
        int count = sound.Clips == null ? 0 : sound.Clips.Length;
        int validClips = sound.Clips == null ? 0 : Array.FindAll( sound.Clips, clip => clip != null ).Length;
        bool valid;
        context.Report( sound, "표현", "sound-clips", "_clips",
            validClips == 0 ? PlayReadinessStatus.Error : validClips == count ? PlayReadinessStatus.Passed : PlayReadinessStatus.Warning,
            $"재생 가능한 음원 {validClips}/{count}개 · 빈 후보는 SoundPlayer에서 제외합니다." );
        Vector2 volume = sound.VolumeRange;
        Vector2 pitch = sound.PitchRange;
        valid = PlayDataSetupChecker.Finite( volume.x ) == true && PlayDataSetupChecker.Finite( volume.y ) == true &&
            volume.x >= 0f && volume.y <= 1f && volume.x <= volume.y &&
            PlayDataSetupChecker.Finite( pitch.x ) == true && PlayDataSetupChecker.Finite( pitch.y ) == true &&
            pitch.x > 0f && pitch.x <= pitch.y &&
            PlayDataSetupChecker.Finite( sound.Cooldown ) == true && sound.Cooldown >= 0f;
        context.Report( sound, "표현", "sound-range", "음량 / 피치 / 쿨다운",
            valid == true ? PlayReadinessStatus.Passed : PlayReadinessStatus.Error,
            $"음량 {volume}, 피치 {pitch}, 간격 {sound.Cooldown}: 음량 0~1, 양수 피치, 음수 없는 간격과 오름차순 범위 필요" );
    }

    /// <summary>
    /// 선택 음향의 누락을 경고로 분리
    /// </summary>
    /// <param name="context">검사 문맥</param>
    /// <param name="owner">음향 연결 소유자</param>
    static void CheckSoundReferences ( PlayReadinessContext context, Object owner )
    {
        using SerializedObject serialized = new( owner );
        foreach ( SerializedProperty property in PlayReadinessContext.EnumerateProperties( serialized ) )
        {
            if ( property.propertyType != SerializedPropertyType.ObjectReference ||
                 ( property.name.EndsWith( "Sound" ) == false &&
                   property.name.EndsWith( "soundPlayer", StringComparison.OrdinalIgnoreCase ) == false && property.name.EndsWith( "Bgm" ) == false ) )
                continue;
            Object sound = property.objectReferenceValue;
            context.Report( owner, "표현", "sound-reference", property.propertyPath,
                sound == null ? PlayReadinessStatus.Warning : PlayReadinessStatus.Passed,
                sound == null ? $"음향 미연결: {owner.GetType( ).Name}.{property.propertyPath}에 {property.type} 대상과 재생 담당을 지정해 주세요."
                    : $"음향 연결: {sound.name}", sound );
        }
    }

    /// <summary>
    /// 플레이 카메라의 Post Processing과 화면 연출 볼륨 레이어 확인
    /// </summary>
    /// <param name="context">검사 문맥</param>
    /// <param name="owner">플레이 씬</param>
    static void CheckCameraVolume ( PlayReadinessContext context, Object owner )
    {
        using SerializedObject root = new( owner );
        Camera camera = root.FindProperty( "_camera" ).objectReferenceValue as Camera;
        Object effect = root.FindProperty( "_screenEffect" ).objectReferenceValue;
        if ( camera == null || effect == null )
            return;
        using SerializedObject settings = new( effect );
        Volume volume = settings.FindProperty( "_volume" ).objectReferenceValue as Volume;
        if ( volume == null )
            return;
        UniversalAdditionalCameraData data = camera.GetComponent<UniversalAdditionalCameraData>( );
        bool valid = data != null && data.renderPostProcessing == true &&
            ( data.volumeLayerMask.value & ( 1 << volume.gameObject.layer ) ) != 0;
        context.Report( owner, "표현", "camera-volume", "_camera / _screenEffect._volume",
            valid == true ? PlayReadinessStatus.Passed : PlayReadinessStatus.Error,
            valid == true ? "Post Processing 활성 및 연출 Volume 레이어 포함 · 화면 효과는 플레이 확인 필요"
                : "플레이 카메라의 URP Additional Camera Data, Post Processing, Volume Layer Mask에 연출 Volume 레이어를 확인해 주세요.", camera );
    }

    /// <summary>
    /// 아이템·캐릭터·제작 재생 담당 공유와 Mixer 노출 파라미터 검사
    /// </summary>
    /// <param name="context">검사 문맥</param>
    public static void CheckRelations ( PlayReadinessContext context )
    {
        Dictionary<Object, Object> players = new( );
        foreach ( Object owner in context.Objects )
        {
            if ( owner is MonoBehaviour == false )
                continue;
            using SerializedObject serialized = new( owner );
            string field = owner is CookController ? "_productionSoundPlayer" : "_soundPlayer";
            SerializedProperty property = serialized.FindProperty( field );
            bool exclusive = owner is ItemCarry || owner is Player || owner is Enemy || owner is Npc || owner is CookController;
            if ( exclusive == true && property != null && property.objectReferenceValue != null )
            {
                Object player = property.objectReferenceValue;
                if ( players.TryGetValue( player, out Object previous ) == true && previous != owner )
                    context.Report( owner, "표현", "shared-player", field, PlayReadinessStatus.Error,
                        $"{previous.name} ({previous.GetType( ).Name})와 같은 SoundPlayer 사용: 반환/정지 시 다른 대상의 반복음도 정지할 수 있습니다. 전용 SoundPlayer로 분리해 주세요.", previous );
                else
                    players [ player ] = owner;
            }
            if ( owner.GetType( ).Name == "SoundManager" )
            {
                AudioMixer mixer = serialized.FindProperty( "_mixer" ).objectReferenceValue as AudioMixer;
                if ( mixer == null )
                    continue;
                foreach ( string parameter in new[] { "MasterVolume", "BgmVolume", "SfxVolume" } )
                {
                    bool valid = mixer.GetFloat( parameter, out _ );
                    context.Report( owner, "표현", "mixer-parameter", parameter,
                        valid == true ? PlayReadinessStatus.Passed : PlayReadinessStatus.Error,
                        $"AudioMixer 노출 파라미터: {parameter}", mixer );
                }
            }
        }
    }
}
