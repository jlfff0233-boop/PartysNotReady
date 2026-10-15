#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 플레이 씬 테스트 설정 - 콘텐츠 누락 허용과 AI 실행 제외
/// </summary>
[DefaultExecutionOrder( -900 )]
[DisallowMultipleComponent]
[RequireComponent( typeof( PlayScene ) )]
public class PlaySceneTestSettings : MonoBehaviour
{
    [Header( "----- 테스트 설정 -----" )]
    [SerializeField] bool _allowMissingItemContent;		//공급원 누락과 그릇 없는 구성 허용
    [SerializeField] bool _disableAi;		//적, NPC 행동과 자동 발생 제외

    bool _allowMissingItemContentForPlay;		//이번 플레이의 콘텐츠 누락 허용
    bool _disableAiForPlay;		//이번 플레이의 AI 실행 제외

    /// <summary>
    /// 이번 플레이의 콘텐츠 누락 허용 여부
    /// </summary>
    public bool AllowMissingItemContent => _allowMissingItemContentForPlay;

    /// <summary>
    /// 이번 플레이의 AI 실행 제외 여부
    /// </summary>
    public bool DisableAi => _disableAiForPlay;


    private void Awake ()
    {
        if ( isActiveAndEnabled == false )
            return;

        //실행 중 Inspector 변경은 다음 플레이부터 적용
        _allowMissingItemContentForPlay = _allowMissingItemContent;
        _disableAiForPlay = _disableAi;

        if ( _disableAiForPlay == true )
            PrepareAiTest( );
    }

    /// <summary>
    /// 같은 씬의 AI 행동과 이동 에이전트 중지
    /// </summary>
    void PrepareAiTest ()
    {
        foreach ( Enemy enemy in FindObjectsByType<Enemy>(
            FindObjectsInactive.Include, FindObjectsSortMode.None ) )
        {
            if ( enemy.gameObject.scene != gameObject.scene )
                continue;

            enemy.enabled = false;
            DisableAgents( enemy.gameObject );
        }

        foreach ( Npc npc in FindObjectsByType<Npc>(
            FindObjectsInactive.Include, FindObjectsSortMode.None ) )
        {
            if ( npc.gameObject.scene != gameObject.scene )
                continue;

            npc.PrepareAiTest( );
            DisableAgents( npc.gameObject );
        }

        Debug.LogWarning( "[에디터 테스트] 적과 NPC 행동, 이동 에이전트와 자동 발생을 제외합니다.", this );
    }

    /// <summary>
    /// 대상 아래 이동 에이전트 비활성화
    /// </summary>
    /// <param name="target">적 또는 NPC 객체</param>
    void DisableAgents ( GameObject target )
    {
        foreach ( NavMeshAgent agent in target.GetComponentsInChildren<NavMeshAgent>( true ) )
            agent.enabled = false;
    }
}
#endif
