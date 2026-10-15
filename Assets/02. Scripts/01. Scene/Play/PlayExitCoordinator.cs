using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이 종료 정리 - 제작 중단, 재고 정리와 씬 대여 객체 반환
/// </summary>
public class PlayExitCoordinator
{
    Scene _scene;		//반환할 객체의 사용 씬
    ProductionManager _product;		//제작 중단과 소비 정리
    IngredientStockManager _ingredientStock;		//재고 정리
    Action _stopPlay;		//입력과 플레이 중단 요청
    UnityEngine.Object _context;		//정리 실패 로그의 대상
    bool _isPreparing;		//종료 정리 재진입 차단
    bool _isPrepared;		//씬 이동 준비 완료 여부

    /// <summary>
    /// 씬 이동 준비 완료 여부
    /// </summary>
    public bool IsPrepared => _isPrepared;

    /// <summary>
    /// 플레이 종료에 필요한 시스템 연결
    /// </summary>
    /// <param name="scene">정리할 플레이 씬</param>
    /// <param name="product">제작 관리자</param>
    /// <param name="ingredientStock">재고 관리자</param>
    /// <param name="stopPlay">플레이 중단 처리</param>
    /// <param name="context">로그 대상</param>
    public PlayExitCoordinator (
        Scene scene, ProductionManager product, IngredientStockManager ingredientStock,
        Action stopPlay, UnityEngine.Object context )
    {
        _scene = scene;
        _product = product;
        _ingredientStock = ingredientStock;
        _stopPlay = stopPlay;
        _context = context;
    }

    /// <summary>
    /// 플레이와 제작을 종료하고 씬의 대여 객체 반환
    /// </summary>
    /// <returns>씬 이동 가능한 정리 완료 여부</returns>
    public bool TryPrepareExit ()
    {
        if ( _isPrepared == true )
            return true;
        if ( _isPreparing == true )
            return false;

        _isPreparing = true;

        try
        {
            _stopPlay( );

            if ( _product != null && _product.Stop( ) == false )
            {
                Debug.LogWarning( "[씬 이동 대기] 제작 소비 정리가 끝나지 않았습니다.", _context );
                return false;
            }

            if ( _ingredientStock != null && _ingredientStock.ClearStock( ) == false )
                return false;

            GameManager game = GameManager.Instance;
            if ( game == null || game.IsReady == false )
            {
                Debug.LogWarning( "[씬 이동 대기] 풀링 서비스를 사용할 수 없습니다.", _context );
                return false;
            }

            if ( game.Pool.AllReturnToPool( _scene ) == false )
                return false;

            _isPrepared = true;
            return true;
        }
        finally
        {
            _isPreparing = false;
        }
    }
}
