using System.Collections.Generic;

using UnityEngine;
using System;

/// <summary>
/// 게임 관리 - 공통 서비스 초기화와 종료
/// </summary>
[DefaultExecutionOrder( -1000 )]
public class GameManager : Singleton<GameManager>
{
    bool _isReady;		//전체 서비스 준비 여부

    SaveManager _save;		//저장 관리
    SoundManager _sound;		//사운드 관리
    SettingManager _settings;		//사용자 설정 관리
    ResourceManager _resource;		//리소스 관리
    PoolingManager _pool;		//객체 풀링 관리


    /// <summary>
    /// 초기화에 성공한 서비스의 종료 함수, 역순으로 실행
    /// </summary>
    Stack<Action> _shutdowns = new( );

    /// <summary>
    /// 저장 관리
    /// </summary>
    public SaveManager Save => _save;

    /// <summary>
    /// 사운드 관리
    /// </summary>
    public SoundManager Sound => _sound;

    /// <summary>
    /// 사용자 설정 관리
    /// </summary>
    public SettingManager Settings => _settings;

    /// <summary>
    /// 리소스 관리
    /// </summary>
    public ResourceManager Resource => _resource;

    /// <summary>
    /// 객체 풀링 관리
    /// </summary>
    public PoolingManager Pool => _pool;

    /// <summary>
    /// 전체 서비스 준비 여부
    /// </summary>
    public bool IsReady => _isReady;

    /// <summary>
    /// 플레이 시작 전 이전 인스턴스 참조 해제
    /// </summary>
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration )]
    static void ResetRuntimeState ()
    {
        ResetInstance( );
    }

    /// <summary>
    /// 서비스 초기화 성공 시 종료 함수 등록
    /// </summary>
    /// <param name="service">초기화할 서비스</param>
    /// <param name="shutdown">종료 함수, 정리가 필요 없으면 null</param>
    /// <returns>초기화 성공 여부</returns>
    bool InitService ( IGameService service, Action shutdown = null )
    {
        if ( service.Init( this ) == false )
            return false;

        if ( shutdown != null )
            _shutdowns.Push( shutdown );

        return true;
    }

    /// <summary>
    /// 공통 서비스를 순서대로 초기화
    /// </summary>
    /// <returns>전체 초기화 성공 여부</returns>
    bool InitServices ()
    {
        bool completed = false;		//전체 초기화 성공 여부

        try
        {
            completed = InitService( _save ) &&
                InitService( _sound, _sound.Shutdown ) &&
                InitService( _settings, _settings.Shutdown ) &&
                InitService( _pool, _pool.Shutdown );

            return completed;
        }
        finally
        {
            //실패 반환과 예외 모두 이미 준비된 서비스 정리
            if ( completed == false )
                ShutdownServices( );
        }
    }

    /// <summary>
    /// 씬 시작 전에 공통 서비스 준비
    /// </summary>
    void Start ()
    {
        //중복 객체는 서비스 초기화를 진행하지 않음
        if ( Instance != this )
            return;

        if ( CollectServices( ) == false )
            return;

        _isReady = InitServices( );
    }

    /// <summary>
    /// 정상 인스턴스의 서비스 종료와 등록 해제
    /// </summary>
    protected override void OnDestroy ()
    {
        if ( Instance != this )
            return;

        try
        {
            ShutdownServices( );
        }
        finally
        {
            base.OnDestroy( );
        }
    }


    /// <summary>
    /// 필수 서비스 조회와 구성 확인
    /// </summary>
    /// <returns>서비스 구성 유효 여부</returns>
    bool CollectServices ()
    {
        _save = GetComponent<SaveManager>( );
        _sound = GetComponent<SoundManager>( );
        _settings = GetComponent<SettingManager>( );
        _resource = GetComponent<ResourceManager>( );
        _pool = GetComponent<PoolingManager>( );

        if ( _save == null || _sound == null || _settings == null ||
             _resource == null || _pool == null )
        {
            Debug.LogError(
                "[게임 초기화 실패] SaveManager / SoundManager / " +
                "SettingManager / ResourceManager / PoolingManager " +
                "구성을 확인해 주세요.", this );
            return false;
        }

        return true;
    }

    /// <summary>
    /// 준비된 서비스를 역순으로 종료하고 리소스 캐시 정리
    /// </summary>
    void ShutdownServices ()
    {
        _isReady = false;

        while ( _shutdowns.Count > 0 )
        {
            //중복 종료를 막도록 실행 전에 목록에서 제거
            _shutdowns.Pop( ).Invoke( );
        }

        //풀과 서비스 정리가 끝난 뒤 프리팹 캐시 참조 해제
        if ( _resource != null )
            _resource.ClearCache( );
    }

}
