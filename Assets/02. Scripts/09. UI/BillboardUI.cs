using UnityEngine;

/// <summary>
/// 월드 UI 방향 보정 - 카메라 정면 방향에 맞춰 회전
/// </summary>
public class BillboardUI : MonoBehaviour
{
    [Header( "----- 카메라 -----" )]
    [SerializeField] Camera _cam;       //방향 기준 카메라

    private void OnEnable ()
    {
        //직접 지정하지 않았다면 메인 카메라 사용
        if ( _cam == null )
            _cam = Camera.main;
    }

    private void Update ()
    {
        //카메라의 정면 방향에 UI 방향을 맞춤
        transform.forward = _cam.transform.forward;
    }
}
