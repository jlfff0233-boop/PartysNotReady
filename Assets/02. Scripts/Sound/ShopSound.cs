using UnityEngine;

/// <summary>
/// 상점 사운드 - 구매 성공과 실패음 재생
/// </summary>
public class ShopSound : MonoBehaviour
{
    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] SoundPlayer _soundPlayer;		//공통 화면 사운드 재생

    [Header( "----- 사운드 -----" )]
    [SerializeField] SoundData _purchaseSuccessSound;		//구매 성공음
    [SerializeField] SoundData _purchaseFailureSound;		//구매 실패음

    ShopPresenter _shop;		//구매 결과 알림

    /// <summary>
    /// 상점 구매 결과 연결
    /// </summary>
    /// <param name="shop">구매 결과 알림 제공 대상</param>
    public void Init ( ShopPresenter shop )
    {
        Unsubscribe( );
        _shop = shop;

        if ( isActiveAndEnabled == true )
            Subscribe( );
    }

    /// <summary>
    /// 재활성화 시 구매 결과 알림 연결
    /// </summary>
    void OnEnable ()
    {
        Subscribe( );
    }

    /// <summary>
    /// 비활성화 시 구매 결과 알림 연결 해제
    /// </summary>
    void OnDisable ()
    {
        Unsubscribe( );
    }

    /// <summary>
    /// 파괴 시 구매 결과 알림 연결 해제
    /// </summary>
    void OnDestroy ()
    {
        Unsubscribe( );
    }


    /// <summary>
    /// 구매 결과에 맞는 안내음 재생
    /// </summary>
    /// <param name="purchased">구매 성공 여부</param>
    void HandlePurchaseCompleted ( bool purchased )
    {
        if ( _soundPlayer != null )
            _soundPlayer.PlayOneShot(
                purchased == true
                    ? _purchaseSuccessSound
                    : _purchaseFailureSound );
    }

    /// <summary>
    /// 구매 결과 알림 연결 해제
    /// </summary>
    void Unsubscribe ()
    {
        if ( _shop != null )
            _shop.OnPurchaseCompleted -= HandlePurchaseCompleted;
    }

    /// <summary>
    /// 구매 결과 알림 연결
    /// </summary>
    void Subscribe ()
    {
        Unsubscribe( );

        //최초 활성화는 초기화보다 먼저 호출될 수 있음
        if ( _shop == null )
            return;

        _shop.OnPurchaseCompleted += HandlePurchaseCompleted;
    }


}
