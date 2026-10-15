using UnityEngine;

/// <summary>
/// 아이템 이동 테스트 - 설정 전달과 수동 실행
/// </summary>
public class ItemDisplacementTest : MonoBehaviour
{
    [SerializeField] ItemCarry _target;     //적용 대상
    [SerializeField] ItemDisplacementSettings _map = new ( );     //이동 설정

    /// <summary>
    /// 플레이 중 지정 아이템의 주변 이동 확인
    /// </summary>
    [ContextMenu ( "Test Displace" )]
    void TestDisplace ( )
    {
        if ( Application.isPlaying == false )
            return;

        if ( _target == null )
        {
            Debug.LogWarning ( "이동할 아이템을 지정해주세요." , this );
            return;
        }

        if ( _target.TryDisplace ( _map , out string reason ) == false )
        {
            Debug.LogWarning ( $"[이동 테스트 실패] {reason}" , this );
            return;
        }

        Debug.Log (
            $"[이동 테스트 성공] {_target.name} / 위치: {_target.transform.position}" ,
            this );
    }
}