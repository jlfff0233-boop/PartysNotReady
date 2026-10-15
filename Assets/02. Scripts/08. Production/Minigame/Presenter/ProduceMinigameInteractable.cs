using UnityEngine;

/// <summary>
/// 제작 미니게임 상호작용 - 진행 중인 제작 회차 판정 요청
/// </summary>
public class ProduceMinigameInteractable : MonoBehaviour
{
    [SerializeField] CookController _product;		//제작 대상

    /// <summary>
    /// 제작 미니게임 가능 여부
    /// </summary>
    public bool CanPlayMinigame =>
        isActiveAndEnabled == true &&
        _product != null &&
        _product.Session.Minigame.CanPlayMinigame;


    /// <summary>
    /// 제작 대상 연결
    /// </summary>
    void Init ()
    {
        if ( _product == null )
            _product = GetComponentInParent<CookController>( );

        if ( _product == null )
        {
            Debug.LogError(
                "[제작 미니게임 초기화 실패] 제작 대상을 연결해 주세요.", this );
        }
    }


    /// <summary>
    /// 제작 대상 초기화
    /// </summary>
    void Awake ()
    {
        Init( );
    }


    /// <summary>
    /// 현재 제작 미니게임 회차 판정
    /// </summary>
    /// <returns>판정 반영 여부</returns>
    public bool TryPlayMinigame ()
    {
        if ( CanPlayMinigame == false )
            return false;

        return _product.Session.Minigame.TryCheckTiming( out _ );
    }
}
