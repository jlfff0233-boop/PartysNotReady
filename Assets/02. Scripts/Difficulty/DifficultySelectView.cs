using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 난이도 버튼 입력 전달과 선택 강조, 설명 표시
/// </summary>
public class DifficultySelectView : MonoBehaviour
{
    [Header ( "----- 컴포넌트 -----" )]
    [SerializeField] Button _easyButton;		//쉬움 요청
    [SerializeField] Button _normalButton;		//보통 요청
    [SerializeField] Button _hardButton;		//어려움 요청
    [SerializeField] Button _selectButton;		//선택 확정 요청
    [SerializeField] Button _cancelButton;		//선택 취소 요청
    [SerializeField] TMP_Text _infoText;		//전달받은 설명 표시

    Outline _easyOutline;		//쉬움 버튼의 테두리
    Outline _normalOutline;		//보통 버튼의 테두리
    Outline _hardOutline;		//어려움 버튼의 테두리

    /// <summary>
    /// 클릭한 난이도의 선택 요청
    /// </summary>
    public event Action<GameDifficulty> OnDifficultyRequested;

    /// <summary>
    /// 선택 확정 요청
    /// </summary>
    public event Action OnConfirmed;

    /// <summary>
    /// 선택 취소 요청
    /// </summary>
    public event Action OnCancelled;


    /// <summary>
    /// 각 버튼의 선택 테두리를 한 번 조회
    /// </summary>
    public void Init ( )
    {
        //비활성 로비에서도 패널의 초기 선택 표시 전에 캐시 준비
        _easyOutline = _easyButton.GetComponent<Outline> ( );
        _normalOutline = _normalButton.GetComponent<Outline> ( );
        _hardOutline = _hardButton.GetComponent<Outline> ( );
    }

    private void OnEnable ( )
    {
        _easyButton.onClick.AddListener ( HandleEasyRequested );
        _normalButton.onClick.AddListener ( HandleNormalRequested );
        _hardButton.onClick.AddListener ( HandleHardRequested );
        _selectButton.onClick.AddListener ( HandleConfirmed );
        _cancelButton.onClick.AddListener ( HandleCancelled );
    }

    private void OnDisable ( )
    {
        _easyButton.onClick.RemoveListener ( HandleEasyRequested );
        _normalButton.onClick.RemoveListener ( HandleNormalRequested );
        _hardButton.onClick.RemoveListener ( HandleHardRequested );
        _selectButton.onClick.RemoveListener ( HandleConfirmed );
        _cancelButton.onClick.RemoveListener ( HandleCancelled );
    }


    /// <summary>
    /// 쉬움 버튼 입력 전달
    /// </summary>
    void HandleEasyRequested ( )
    {
        OnDifficultyRequested?.Invoke ( GameDifficulty.Easy );
    }

    /// <summary>
    /// 보통 버튼 입력 전달
    /// </summary>
    void HandleNormalRequested ( )
    {
        OnDifficultyRequested?.Invoke ( GameDifficulty.Normal );
    }

    /// <summary>
    /// 어려움 버튼 입력 전달
    /// </summary>
    void HandleHardRequested ( )
    {
        OnDifficultyRequested?.Invoke ( GameDifficulty.Hard );
    }

    /// <summary>
    /// 선택 버튼 입력 전달
    /// </summary>
    void HandleConfirmed ( )
    {
        OnConfirmed?.Invoke ( );
    }

    /// <summary>
    /// 취소 버튼 입력 전달
    /// </summary>
    void HandleCancelled ( )
    {
        OnCancelled?.Invoke ( );
    }


    /// <summary>
    /// 전달받은 난이도의 선택 테두리 표시
    /// </summary>
    /// <param name="difficulty">강조할 난이도</param>
    public void ShowSelection ( GameDifficulty difficulty )
    {
        _easyOutline.enabled = difficulty == GameDifficulty.Easy;
        _normalOutline.enabled = difficulty == GameDifficulty.Normal;
        _hardOutline.enabled = difficulty == GameDifficulty.Hard;
    }

    /// <summary>
    /// 전달받은 설명 표시
    /// </summary>
    /// <param name="description">표시할 설명</param>
    public void SetDescription ( string description )
    {
        _infoText.text = description;
    }

    /// <summary>
    /// 난이도 버튼 입력 허용 설정
    /// </summary>
    /// <param name="interactable">입력 허용 여부</param>
    public void SetInteractable ( bool interactable )
    {
        _easyButton.interactable = interactable;
        _normalButton.interactable = interactable;
        _hardButton.interactable = interactable;
        _selectButton.interactable = interactable;
        _cancelButton.interactable = interactable;
    }
}
