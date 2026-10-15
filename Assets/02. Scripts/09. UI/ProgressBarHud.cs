using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 진행바 HUD - 진행률 UI 표시와 갱신
/// </summary>
public class ProgressBarHud : MonoBehaviour
{
    [SerializeField] GameObject _progressObj;      //진행률 UI
    [SerializeField] Image _progressBar;      //진행바 이미지

    /// <summary>
    /// 진행률 UI 표시 설정
    /// </summary>
    /// <param name="isVisible">표시 여부</param>
    public void SetVisible ( bool isVisible )
    {
        _progressObj.SetActive( isVisible );
    }

    /// <summary>
    /// 진행률 갱신
    /// </summary>
    /// <param name="ratio">0~1 범위의 진행률</param>
    public void SetProgress ( float ratio )
    {
        _progressBar.fillAmount = Mathf.Clamp01( ratio );
    }
}
