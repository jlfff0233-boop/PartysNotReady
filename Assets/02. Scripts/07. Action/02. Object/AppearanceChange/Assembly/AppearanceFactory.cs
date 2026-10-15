using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 외형 생성 모듈 - 비활성 후보 생성과 사용이 끝난 외형 정리
/// </summary>
public class AppearanceFactory
{
    Transform _prepareRoot;     //활성 콜백을 막는 생성용 비활성 부모


    /// <summary>
    /// 설정에서 비교에 사용할 외형 원본 수집
    /// </summary>
    /// <param name="data">조회할 외형 설정</param>
    /// <param name="sources">설정별 외형 원본을 저장할 목록</param>
    void AddSource ( AppearanceData data, Dictionary<AppearanceData, GameObject> sources )
    {
        if ( data == null || data.Prefab == null || sources.ContainsKey( data ) )
            return;

        //전체 프리팹과 내부 외형을 각각 지정해도 같은 원본으로 비교
        AppearanceComponents [ ] views = data.Prefab.GetComponentsInChildren<AppearanceComponents>( true );
        sources.Add( data, views.Length == 1 ? views [ 0 ].gameObject : null );
    }

    /// <summary>
    /// 현재 외형과 후보의 원본 식별 정보 조회
    /// </summary>
    /// <param name="appearances">외형 후보 목록</param>
    /// <param name="current">현재 외형 설정</param>
    /// <returns>설정별 단일 외형 원본, 찾지 못한 원본은 null</returns>
    public Dictionary<AppearanceData, GameObject> GetSources (
        IReadOnlyList<AppearanceData> appearances, AppearanceData current )
    {
        Dictionary<AppearanceData, GameObject> sources = new( );
        AddSource( current, sources );
        if ( appearances != null )
        {
            foreach ( AppearanceData data in appearances )
                AddSource( data, sources );
        }
        return sources;
    }


    /// <summary>
    /// 외형 생성용 비활성 부모 확보
    /// </summary>
    /// <param name="appearanceRoot">현재 외형 전용 부모</param>
    void PrepareAppearanceRoot ( Transform appearanceRoot )
    {
        if ( _prepareRoot != null )
            return;

        //생성 직후 참조 연결 전에 외형의 활성 콜백이 실행되지 않도록 차단
        GameObject prepareObject = new( "Appearance Prepare" );
        prepareObject.SetActive( false );
        prepareObject.transform.SetParent( appearanceRoot, false );
        _prepareRoot = prepareObject.transform;
    }

    /// <summary>
    /// 후보 외형 생성과 배치 보정값 적용
    /// </summary>
    /// <param name="data">생성할 외형 설정</param>
    /// <param name="appearanceRoot">후보를 배치할 현재 외형 전용 부모</param>
    /// <param name="candidate">생성한 외형</param>
    /// <param name="reason">생성 실패 사유</param>
    /// <returns>외형 생성 성공 여부</returns>
    public bool TryCreateCandidate (
        AppearanceData data, Transform appearanceRoot,
        out AppearanceComponents candidate, out string reason )
    {
        candidate = null;
        reason = string.Empty;

        if ( data == null )
        {
            reason = "생성할 외형 설정이 없습니다.";
            return false;
        }

        if ( AppearanceChecker.TryGetPrefabAppearance(
            data.Prefab, out AppearanceComponents source, out reason ) == false )
        {
            reason = $"{data.name}: {reason}";
            return false;
        }

        PrepareAppearanceRoot( appearanceRoot );

        //선택한 외형 오브젝트와 그 자식만 복제
        //원본 프리팹의 부모에 있는 Rigidbody와 아이템 기능은 포함하지 않음
        candidate = Object.Instantiate( source, _prepareRoot );
        candidate.gameObject.SetActive( false );
        candidate.transform.SetParent( appearanceRoot, false );

        //원본 프리팹 부모의 Transform 대신 SO의 최종 로컬 보정값 적용
        candidate.transform.localPosition = data.LocalPosition;
        candidate.transform.localRotation = data.LocalRotation;
        candidate.transform.localScale = data.LocalScale;
        return true;
    }

    /// <summary>
    /// 사용하지 않는 외형 비활성화와 삭제
    /// </summary>
    /// <param name="appearance">정리할 외형</param>
    public void DisposeAppearance ( AppearanceComponents appearance )
    {
        if ( appearance == null )
            return;

        appearance.gameObject.SetActive( false );
        Object.Destroy( appearance.gameObject );
    }

    /// <summary>
    /// 생성용 비활성 부모 정리
    /// </summary>
    public void Dispose ()
    {
        if ( _prepareRoot != null )
            Object.Destroy( _prepareRoot.gameObject );
        _prepareRoot = null;
    }

}
