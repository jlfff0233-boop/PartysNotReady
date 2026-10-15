using System;
using UnityEngine;

/// <summary>
/// 구조 상호작용 중재 - 화면 중앙 대상 탐지와 구조 입력 전달
/// </summary>
public class RescueInteracter : MonoBehaviour
{
    [Header ( "----- 설정 데이터 -----" )]
    [SerializeField, Min ( 0f )] float _rescueDist = 3f;       //구조 대상 탐지 거리
    [SerializeField] LayerMask _detectionLayerMask;       //구조 대상 탐지 레이어

    object _supportOwner;		//구조 효과 적용자
    float _supportMultiplier = 1f;		//구조 입력 배율

    Camera _camera;     //탐지 기준 카메라
    Npc _target;        //현재 구조 대상

    /// <summary>
    /// 유효한 구조 입력의 효과 적용
    /// </summary>
    Action _applyRescueSupport;

    /// <summary>
    /// 현재 구조 대상
    /// </summary>
    public Npc Target => _target;

    /// <summary>
    /// 구조 대상 변경 이벤트
    /// </summary>
    public event Action<Npc> OnTargetChanged;


    /// <summary>
    /// 탐지 카메라와 구조 효과 연결
    /// </summary>
    /// <param name="camera">플레이어 카메라</param>
    /// <param name="applyRescueSupport">구조 진행 전 효과 적용</param>
    public void Init ( Camera camera, Action applyRescueSupport )
    {
        _camera = camera;
        _applyRescueSupport = applyRescueSupport;
        SetTarget( null );
    }

    private void Update ( )
    {
        UpdateTarget ( );
    }

    private void OnDisable ( )
    {
        SetTarget ( null );
    }


    /// <summary>
    /// 구조 효과 적용
    /// </summary>
    /// <param name="owner">적용자</param>
    /// <param name="multiplier">입력 배율</param>
    /// <returns>적용 여부</returns>
    public bool TryApplySupportProgress ( object owner, float multiplier )
    {
        if ( owner == null || _supportOwner != null ||
             float.IsNaN( multiplier ) == true || float.IsInfinity( multiplier ) == true ||
             multiplier < 1f )
            return false;

        _supportOwner = owner;
        _supportMultiplier = multiplier;
        return true;
    }

    /// <summary>
    /// 구조 효과 복원
    /// </summary>
    /// <param name="owner">적용자</param>
    public void ClearSupportProgress ( object owner )
    {
        if ( ReferenceEquals( _supportOwner, owner ) == false )
            return;

        _supportOwner = null;
        _supportMultiplier = 1f;
    }


    /// <summary>
    /// 대상 재확인 후 구조 시도
    /// </summary>
    /// <returns>구조 완료 여부</returns>
    public bool TryRescue ( )
    {
        //현재 구조 대상 확인
        if ( TryGetTarget ( out Npc target ) == false )
            return false;

        //첫 입력부터 구조 효과 적용
        _applyRescueSupport?.Invoke( );

        //입력 한 번으로 구조가 진행되며, 완료 여부는 대상이 반환
        bool completed = target.TryRescue ( _supportMultiplier );

        //구조 완료나 포획 종료 시 탐지 대상 해제
        if ( target == null || target.CanRescue == false )
            SetTarget ( null );

        return completed;
    }


    #region ----- 대상 탐지 -----

    /// <summary>
    /// 현재 시선과 거리 조건에 맞는 구조 대상 탐지
    /// </summary>
    /// <param name="target">탐지한 NPC</param>
    /// <returns>구조 가능한 NPC 탐지 여부</returns>
    bool TryFindTarget ( out Npc target )
    {
        target = null;

        if ( _camera == null || isActiveAndEnabled == false )
            return false;

        //포획 중 트리거로 전환된 NPC 몸체도 탐지
        if ( InteractionRaycaster.TryGetHit(
            _camera, _rescueDist, _detectionLayerMask,
            QueryTriggerInteraction.Collide, out RaycastHit hit ) == false )
            return false;

        Npc npc = hit.collider.GetComponentInParent<Npc> ( );

        if ( npc == null || npc.CanRescue == false )
            return false;

        target = npc;
        return true;
    }

    /// <summary>
    /// 대상 변경 시 저장하고 이벤트 전달
    /// </summary>
    /// <param name="target">새로운 구조 대상</param>
    void SetTarget ( Npc target )
    {
        if ( ReferenceEquals ( _target , target ) == true )
            return;

        _target = target;
        OnTargetChanged?.Invoke ( _target );
    }

    private void UpdateTarget ( )
    {
        TryFindTarget ( out Npc target );
        SetTarget ( target );
    }

    /// <summary>
    /// 현재 구조 대상 갱신 후 조회
    /// </summary>
    /// <param name="target">구조 대상</param>
    /// <returns>대상 조회 여부</returns>
    public bool TryGetTarget ( out Npc target )
    {
        UpdateTarget ( );
        target = _target;

        return target != null && target.CanRescue == true;
    }

    #endregion
}
