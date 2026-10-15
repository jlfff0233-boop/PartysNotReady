using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 버튼 사운드 - 실제 클릭음 재생
/// </summary>
[RequireComponent( typeof( Button ) )]
public class ButtonSound : MonoBehaviour
{
    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] Button _button;		//클릭 알림 제공 대상
    [SerializeField] SoundPlayer _soundPlayer;		//공통 화면 사운드 재생

    [Header( "----- 사운드 -----" )]
    [SerializeField] SoundData _clickSound;		//클릭음

    /// <summary>
    /// 버튼 클릭 알림 연결
    /// </summary>
    void Awake ()
    {
        if ( _soundPlayer != null )
            _soundPlayer.SetIgnoreListenerPause( true );
        Subscribe( );
    }

    /// <summary>
    /// 파괴 시 버튼 클릭 알림 연결 해제
    /// </summary>
    void OnDestroy ()
    {
        Unsubscribe( );
    }


    /// <summary>
    /// 실제 클릭음 재생
    /// </summary>
    void HandleClick ()
    {
        //클릭 처리로 패널이 닫혀도 공통 소스에서 재생
        if ( _soundPlayer != null )
            _soundPlayer.PlayOneShot( _clickSound );
    }

    /// <summary>
    /// 버튼 클릭 알림 연결 해제
    /// </summary>
    void Unsubscribe ()
    {
        if ( _button != null )
            _button.onClick.RemoveListener( HandleClick );
    }

    /// <summary>
    /// 버튼 클릭 알림 연결
    /// </summary>
    void Subscribe ()
    {
        Unsubscribe( );
        _button.onClick.AddListener( HandleClick );
    }


    /// <summary>
    /// 같은 오브젝트의 버튼 연결
    /// </summary>
    void Reset ()
    {
        _button = GetComponent<Button>( );
    }

}
