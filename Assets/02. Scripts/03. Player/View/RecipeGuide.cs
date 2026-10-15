using UnityEngine;
using TMPro;

/// <summary>
/// 레시피 안내 - 입력 문구 표시와 대상 그릇의 화면 위치 추적
/// </summary>
public class RecipeGuide : MonoBehaviour
{
    [Header( "----- 화면 위치 -----" )]
    [SerializeField] Camera _cam;		//그릇을 보는 메인 카메라
    [SerializeField] RectTransform _positionRoot;		//화면 위치를 적용할 부모

    CookController _target;     //현재 안내 대상

    /// <summary>
    /// 레시피 입력 문구 설정과 최초 안내 숨김
    /// </summary>
    private void Awake ()
    {
        //비활성 자식도 포함해 기존 안내 텍스트에 입력 문구 표시
        TMP_Text guideText = GetComponentInChildren<TMP_Text>( true );     //레시피 안내 텍스트
        guideText.text = "레시피 확인";

        //대상이 연결되기 전에는 안내 전체를 숨김
        Clear( );
    }

    private void LateUpdate ()
    {
        //표시 중 파괴되거나 비활성화된 그릇은 정리
        if ( _target == null || _target.isActiveAndEnabled == false )
        {
            Clear( );
            return;
        }

        if ( UpdatePosition( ) == false )
            Clear( );
    }

    private void OnDisable ()
    {
        //안내가 외부에서 비활성화되어도 이전 대상 정리
        ClearTarget( );
    }


    /// <summary>
    /// 그릇의 안내 위치를 화면 UI 좌표로 변환하여 적용
    /// </summary>
    /// <returns>화면에 표시할 수 있는 위치인지 여부</returns>
    bool UpdatePosition ()
    {
        //월드 위치를 화면 위치로 받기
        Vector3 screenPoint =
            _cam.WorldToScreenPoint( _target.GuidePoint.position );

        //카메라 뒤와 화면 밖의 안내는 표시하지 않음
        if ( screenPoint.z <= 0f ||
             _cam.pixelRect.Contains(
                 new Vector2( screenPoint.x, screenPoint.y ) ) == false )
            return false;

        //위치 기준 Canvas
        RectTransform parent =
            _positionRoot.parent as RectTransform;

        //Overlay Canvas의 좌표 변환에는 UI 카메라를 지정하지 않음
        if ( RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent, screenPoint, null, out Vector2 localPoint ) == false )
            return false;

        //자식의 눌림 연출과 분리하여 위치 전용 부모만 이동
        _positionRoot.localPosition =
            new Vector3( localPoint.x, localPoint.y, 0f );

        return true;
    }

    /// <summary>
    /// 이전 안내 대상 정리
    /// </summary>
    void ClearTarget ()
    {
        _target = null;
    }

    /// <summary>
    /// 레시피 안내 숨김
    /// </summary>
    public void Clear ()
    {
        ClearTarget( );
        gameObject.SetActive( false );
    }

    /// <summary>
    /// 대상 그릇 연결과 안내 표시
    /// </summary>
    /// <param name="target">안내할 그릇, 없으면 숨김</param>
    public void SetTarget ( CookController target )
    {
        if ( target == null )
        {
            Clear( );
            return;
        }

        //같은 대상은 안내 유지
        if ( _target == target )
            return;

        //이전 안내를 정리한 뒤 새 대상 연결
        ClearTarget( );

        _target = target;

        //이전 위치에서 잠깐 나타나지 않도록 위치부터 적용
        if ( UpdatePosition( ) == false )
        {
            Clear( );
            return;
        }

        gameObject.SetActive( true );
    }


}
