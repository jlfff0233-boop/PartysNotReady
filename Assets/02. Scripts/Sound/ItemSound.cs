using UnityEngine;

/// <summary>
/// 아이템 사운드 이전용 데이터 - 기존 음원 참조 보존
/// </summary>
public class ItemSound : MonoBehaviour
{
    [Header ( "----- 사운드 -----" )]
    [SerializeField] SoundData _pickUpSound;		//집기
    [SerializeField] SoundData _placeSound;		//정상 배치
    [SerializeField] SoundData _throwSound;		//투척
}
