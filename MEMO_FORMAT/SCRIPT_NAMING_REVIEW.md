# 스크립트 이름 정리

## STATUS

- 이 문서는 변경 전 파일명과 확정 이름 / 유지 판단을 관리합니다. 작업 상태와 남은 Unity 확인은 [TODO](TODO.md#script_naming_review), 코드 반영 / 정적 확인 이력은 [DONE](DONE.md#스크립트-이름-정리)을 따릅니다.
- 명명 / 수정 기준은 [GUIDELINE](GUIDELINE.md#script_naming_and_summary)을 따릅니다. 이름 변경과 책임 분리 / 게임플레이 변경은 별도 범위입니다.

## NAMING_CRITERIA

| 이름 | 적용할 책임 |
| --- | --- |
| System | 여러 사건이나 게임 규칙의 진행과 결과 관리 |
| Controller | 특정 대상의 행동, 입력과 실행 순서 제어 |
| Manager | 여러 대상의 등록, 소속과 수명 관리 |
| Presenter | Model과 View의 입력 / 표시 연결 |
| 기능명 | 교체 실행이나 결과 기록처럼 구체적인 책임 표현 |

## ANOMALY

| 변경 전 파일명 | 확정 이름 | 판단 근거 |
| --- | --- | --- |
| AnomalyEventPresenter | AnomalyEventSystem | 사건 발생 요청 / 결과 / 공통 진단 계약의 기반 |
| ItemDropPresenter | DropAnomalySystem | 낙하 사건들의 시간 / 재배치 / 종료 관리, 다른 유형과 이름 형식 통일 |
| OpenCloseAnomalyPresenter | OpenCloseAnomalySystem | 개폐 사건 발생 / 대응 / 종료 관리 |
| ShakeAnomalyPresenter | ShakeAnomalySystem | 흔들림 사건 진행 / 조사 결과 / 취소 관리 |
| RotateAnomalyPresenter | RotateAnomalySystem | 회전 사건 진행 / 해결 / 종료 관리 |
| DisplaceAnomalyPresenter | DisplaceAnomalySystem | 이동 사건 진행과 배치 결과 관리 |
| ReplaceAnomalyPresenter | ReplaceAnomalySystem | 대체 사건 발생 / 조사 / 복구 관리 |
| SwapAnomalyPresenter | SwapAnomalySystem | 두 대상 선정과 교체 사건 수명 관리 |
| AddAnomalyPresenter | AddAnomalySystem | 추가 사건 발생 / 조사 제거 / 종료 관리 |
| RemoveAnomalyPresenter | RemoveAnomalySystem | 제거 사건과 정상 복구 결과 관리 |
| AnomalyPressurePresenter | AnomalySpawnSystem | 방별 압력, 방 선정, 적 / 사건 후보와 발생 요청 관리 |

- AnomalySpawnSystem는 압력 갱신과 발생 요청을 함께 담당하여 AnomalySpawnSystem으로 확정했습니다.

## OBJECT_AND_CHARACTER_ACTION

| 변경 전 파일명 | 확정 이름 | 판단 근거 |
| --- | --- | --- |
| CapturePresenter | CaptureController | 대상 확보 / 보유 / 해제 / 결과 전달 |
| OpenClosePresenter | OpenCloseController | 개폐 상태와 뚜껑 배치 제어 |
| RotatePresenter | 유지 | RotateModel의 각도 상태와 RotateView의 적용을 연결하므로 Presenter 역할이 명확합니다. |
| ShakePresenter | 유지 | ShakeModel의 상태 / 조사 ID와 ShakeView의 움직임 / 복구를 연결합니다. |
| PresencePresenter | PresenceController | 등장 / 제거 / 조사 / 복구 제어 |
| ReplacePresenter | ReplaceController | 매트리얼 / 외형 변경 기능 선택과 조사 복원 제어 |
| MaterialChangePresenter | 유지 | Model의 선택 / 복원 상태와 View의 슬롯 적용을 연결합니다. |
| AppearanceChangePresenter | AppearanceChangeController | 외형 생성 / 배치 / 참조 연결 / 복원 순서 제어 |
| SwapPresenter | ItemSwapper | 두 아이템의 배치 검사 / 교체 / 실패 복구를 실행하는 일반 클래스 |

- ItemSwapper는 교체 실행 모듈, SwapAnomalySystem은 교체 사건 관리로 구분합니다.

## GAMEPLAY_PRODUCTION_AND_INPUT

| 변경 전 파일명 | 확정 이름 | 판단 근거 |
| --- | --- | --- |
| GameFlowPresenter | GameFlowController | 시간 / 미션 단계 / 종료 / 평가 / 화면 전환 조율 |
| PreparationPresenter | PreparationSystem | 현재 유효한 준비 상태와 배치 기록 추적 |
| MoodPresenter | MoodSystem | 시간 / 미션 / 사건 / NPC 결과를 분위기에 반영 |
| ResultEvaluationPresenter | ResultEvaluationSystem | 결과 기록 수집과 최종 점수 / 등급 확정 |
| MissionPresenter | MissionResultRecorder | 제작 / 배치 결과를 미션 진행에 전달, 단계 진행 자체는 GameFlow가 담당 |
| IngredientStockPresenter | IngredientStockManager | 공급원별 재고, 대여 재료의 소속과 반환 대기 관리 |
| EnemyScenePresenter | EnemySceneController | 씬 배치 설정 전달과 적 초기화 / 시작 / 종료 조율 |
| ProductPresenter | ProductionManager | 여러 그릇 등록 / 해제, 레시피 설정과 제작 결과 전달 |
| CookPresenter | CookController | 개별 그릇의 재료 배치 / 세션 연결 / 내용물 / 폐기 제어 |
| ProduceMinigamePresenter | ProduceMinigameController | 회차 진행 / 입력 판정 / 제작 세션 반영, 직접 View 참조 없음 |
| PlayInputPresenter | PlayInputController | 플레이 입력 활성화 / 취소, 상호작용 / 커서 / 상태 제한 제어 |
| RecipeInputPresenter | RecipeInputController | 제작 대상 유지와 재료 입력 / 제작 요청 제어 |
| PlaySupportPresenter | PlaySupportController | 반입 아이템 준비와 효과 시작 / 갱신 / 종료 제어 |

- ProductionManager는 여러 제작 대상 관리, CookController는 개별 그릇 제어로 구분합니다.

## PRESENTER_KEEP_AND_HIDE

| 변경 전 파일명 | 확정 이름 또는 유지 | 판단 근거 / 대안 |
| --- | --- | --- |
| ShopPresenter | 유지 | ShopModel의 구매 처리와 ShopView 입력 / 갱신 연결 |
| ItemSelectPresenter | 유지 | 선택 Model과 Panel의 입력 / 표시 연결 |
| PlayLogPresenter | 유지 | 게임 결과 이벤트를 표시용 로그로 변환 |
| HidePresenter | HideController | 잠복 시간과 숨김 / 충돌 / 이동 복구 요청을 연결합니다. |

## RELATED_EDITORS

확정한 런타임 이름에 맞춰 Editor 클래스와 대상 타입을 변경했습니다. 관련 Editor 6개의 파일명 변경도 사용자 작업으로 완료했습니다.

| 변경 전 파일명 | 확정 이름 |
| --- | --- |
| AnomalyEventPresenterEditor | AnomalyEventSystemEditor |
| GameFlowPresenterEditor | GameFlowControllerEditor |
| PreparationPresenterEditor | PreparationSystemEditor |
| MissionPresenterEditor | MissionResultRecorderEditor |
| ProductPresenterEditor | ProductionManagerEditor |
| CookPresenterEditor | CookControllerEditor |

## ORIGINAL_REVIEW_ORDER

| 순서 | 범위 | 개수 |
| --- | --- | --- |
| 1 | 변칙 사건 | 11 |
| 2 | 개별 대상의 기능 | 9 |
| 3 | 게임 진행 / 제작 / 입력 | 13 |
| 4 | Presenter 유지 후보 | 4 |
| 연동 | 관련 Editor | 6 |


- 위 개수는 변경 전 검토 묶음이며 실제 변경 대상 수가 아닙니다.

## VIEW_AND_MODEL_REVIEW

- 실제 실행 코드와 확정한 Presenter / 실행 모듈 이름을 기준으로 판단합니다. UI 입력 전달 / 시각 연출은 View 역할에 포함하지만 공용 연출 모듈은 기능 이름으로 구분합니다. Transform 적용만으로 일괄 변경하지 않습니다.
- Model의 규칙 / 판단 / 런타임 상태 책임은 유지할 수 있습니다. 계산 전용 / 시간 진행 / 수량 집계는 기능명으로, 여러 규칙을 함께 소유하는 Model은 구체적인 기능명을 보완하는 방향을 추천합니다.
- 아래 표는 이름 변경만을 검토합니다. 메서드 이동 / 클래스 분리 / 상속 변경은 포함하지 않습니다. 실제 제어 기능이나 제공하는 컴포넌트 구성이 드러나는 이름을 추천합니다.

### VIEW_CHANGE_CANDIDATES

| 변경 전 파일명 | 확정 이름 | 실제 역할 / 기능 | 변경 사유 |
| --- | --- | --- | --- |
| ItemView | ItemWorldController | ItemHold / ItemPhysics / ItemPlacement 연결, 보유 / 해제 / 투척 / 밀기 / 배치 / 위치 복원, 접촉 알림과 외형 Collider 교체 | 화면 표시보다 실제 월드 아이템의 상태 적용을 제어. World는 카메라 보유 표시를 제외한다는 뜻이 아니라 실제 아이템 오브젝트를 다룬다는 의미 |
| PresenceView | PresenceDisplayController | 대상 활성 / 숨김과 물리 정지, 빈자리 조사 Collider, 복구 대기 표시 / 충돌 복구, 외형 연결과 공간 검사 | 단순 표시를 넘어 표시 대상의 물리 / 조사 영역을 함께 제어. PresenceController 후보는 등장 / 조사 / 복구 규칙과 순서, 이 클래스는 실제 표시와 충돌 적용을 담당 |
| HideView | HideActivationController | 모델 루트 / Collider / NavMeshAgent 상태 저장, 숨김 / 복구, 숨김을 유지한 이동 복구 | 잠복에 필요한 오브젝트 / 충돌 / 이동 기능의 활성 상태를 제어. Activation은 활성 / 비활성 적용 책임이며 잠복 시간과 시작 순서는 상위 HideController 담당 |
| AppearanceView | AppearanceComponents | Renderer / Collider 목록과 대표 Collider, 색 변경 Renderer / 매트리얼 슬롯 제공 | 직접 외형을 표시하거나 변경하지 않고 외형을 구성하는 컴포넌트 참조를 제공. Components는 해당 구성을 표현하며 AppearanceData의 SO 설정과 구별 |

- PresenceDisplayController와 HideActivationController는 상위 Controller의 규칙 / 순서 제어와 실제 적용 책임을 구별하기 위해 기능명을 구체화합니다.

- HideActivationController는 HideActivationController, HideController는 HideController로 확정하여 실제 활성 상태 적용과 잠복 진행 연결을 구분합니다.

### MODEL_FUNCTION_NAME_CANDIDATES

| 변경 전 파일명 | 확정 이름 | 실제 역할 / 기능 | 변경 사유 |
| --- | --- | --- | --- |
| ResultScoreModel | ResultScoreCalculator | 평가축 가중치 적용, 제외 축 재분배, 정수 점수 배분과 등급 산출 | 런타임 상태 없이 입력에서 결과를 계산하는 전용 계산기 |
| HideModel | HideTimer | 잠복 잔여 시간 감소와 대기 종료 판정 | 잠복 전체의 규칙보다 시간 진행에 한정 |
| StunModel | StunTimer | 긴 스턴 시간 유지, 잔여 시간 감소와 초기화 | 스턴 적용이나 입력 차단을 실행하지 않는 시간 모듈 |
| RefillModel | RefillTimer | 재충전 시작 / 취소, 경과 / 잔여 시간과 진행률, 남은 시간 단축 | 실제 재고 / 물건 복구는 호출자가 처리하며 완료 후 Cancel까지 대기 상태 유지 |
| RampageModel | RampageLossCounter | 공격마다 재료 소실 횟수 초기화, 재료 카테고리와 소실 상한 확인, 소실 횟수 집계 | 난동 문맥과 소실 집계 역할을 명시하며 재료 제한은 클래스 Summary와 실제 판정으로 설명 |
| AnomalySpawnModel | AnomalySpawnSelector | 발생 가능한 그룹의 가중치 선택, 그룹별 재발생 가능 시각 기록 | 실제 발생 요청은 기존 AnomalySpawnSystem 후보가 담당. 대기 기록도 소유하므로 검사 전용 Checker는 부적합 |

### MODEL_MORE_SPECIFIC_NAME_CANDIDATES

Model 역할은 유지하고, 실제 책임이 드러나도록 기능명을 구체화한 항목입니다.

| 변경 전 파일명 | 확정 이름 | 실제 역할 / 기능 | 변경 사유 |
| --- | --- | --- | --- |
| NpcModel | NpcSafetyModel | 정상 / 포획 / 이탈 전환, 포획 가능과 게임 잔류 판정 | NPC 일반 행동과 이동 전체를 소유하지 않음 |
| EnemyRoomModel | EnemyRoomActivityModel | 방별 저지 누적 / 처리 이력, 현재 활동 방, 보복 / 방 이동 선택 | 방 데이터나 경로보다 적의 방 활동 공통 규칙. 기존 EnemyRoomActivity 실행 모듈과 Model 접미사로 구별하며 HarmRetaliationModel / KidnapModel의 기반 타입 |
| HarmModel | HarmRetaliationModel | 해결 사건 중복 방지와 방별 저지 집계, 공격 허용, 추격 중단 거리 판단 | 공격 / 피해 적용 전체보다 저지에 따른 보복 판단이 중심 |
| DemandModel | FoodDemandModel | 음식 취향 / 만족도와 재요구 / 만족 / 분노 판정, NPC 수와 확률에 따른 납치 분기 | 음식 요구 규칙을 명시. 납치 분기도 있으므로 Satisfaction만으로 축소하지 않음 |
| ProductModel | ProductionModel | 레시피 재료 등록 / 수량 검사, 제작 시작 / 취소, 성공 횟수, 실패 소비 후보와 완성 상태 | 결과물보다 제작 진행 규칙이 중심. 기존 ProductionSession의 실제 재료 소비 / 결과 생성과 구별 |

- KidnapModel은 사건 실패에 따른 납치 허용, NPC / 플레이어 선택, 접근 제한시간과 구조 저지 이력을 함께 담당합니다. KidnapTargetSelector로 바꾸면 나머지 책임이 가려지므로 유지 후보입니다.
- ResultEvaluationModel의 최종 평가 전체는 기존 ResultEvaluationSystem 후보, 점수 가중치 / 등급은 ResultScoreCalculator 후보가 담당합니다.

### MODEL_EXCLUDED_CHANGE_CANDIDATES

| 변경 전 파일명 | 확정 이름 또는 유지 | 실제 역할 / 기능 | 변경 후보 제외 사유 |
| --- | --- | --- | --- |
| ItemCapacityModel | 유지 | 최대 / 사용 / 잔여 용량, 수용 가능 검사와 용량 증감 | ItemCapacity는 Model 접미사를 없애는 정도라 명확해지는 이득이 작음. 현재도 용량 상태와 증감 규칙을 표현 |
| StealModel | 유지 | 가까운 음식 후보 선택, 확보 거리 / 장애물 검사, 탐색 시간, 재탈취 기회와 탈출 완료 판정 | ItemStealModel은 대상만 보완하며 탐색 / 확보 / 재시도 책임을 더 잘 표현하지 않음. 현재 이름도 절도 규칙의 범위에 맞음 |
| ResultEvaluationModel | 유지 | 시도 시작 / 성공 / 실패 / 취소 / 진행 중 종료 집계, 중복 방지, 포식자 대응 점수와 NPC 안전 점수 산출 | PredatorResultModel은 NPC 안전 점수 계산을 충분히 표현하지 못함. 결과 집계와 안전 / 대응 점수를 함께 담당하므로 이름만 좁히면 일부 책임이 가려짐 |

### VIEW_EFFECT_AND_KEEP_CANDIDATES

| 변경 전 파일명 | 확정 이름 또는 유지 | 역할 / 기능과 판단 근거 |
| --- | --- | --- |
| TitleView | 유지 | 타이틀 버튼 요청 전달과 표시 / 입력 허용 |
| LobbyView | 유지 | 로비 버튼 요청 전달과 표시 / 입력 허용 |
| DifficultySelectView | 유지 | 난이도 선택 / 확정 / 취소 입력 전달, 선택 테두리와 설명 표시. 상위 DifficultySelectPanel과 이름 중복을 피함 |
| ShopView | 유지 | 상품 슬롯 / 수량 팝업, 구매 요청 전달, 보유량 / 잔액 / 안내 표시. 결제는 ShopModel 담당 |
| GameFlowView | GameFlowHud | 개별 진행 / 미션 / 로그 화면을 연결하며, 최근 표시 책임 분리 후 상위 Hud 역할이 명확합니다. |
| GameResultView | 유지 | 확정된 결과 / 미션 / 점수 / 보상 표시, 복귀 요청 전달. 지급이나 종료 판정을 실행하지 않음 |
| SupportItemSlotView | 유지 | 아이콘 / 남은 횟수 / 소진 색상 표시. 효과 사용을 실행하지 않음 |
| MaterialChangeView | 유지 | Renderer 매트리얼 슬롯 조회 / 교체. 후보 선정과 복원 규칙은 별도 Model / Presenter 담당 |
| RotateView | 유지 | 자식 Transform의 기준 회전과 축에 각도 적용. 회전 입력 / 조사 판단을 소유하지 않음 |
| ShakeView | 유지 | 자식 Transform의 반복 흔들림과 기준 자세 복구. 사건 시작 / 조사 종료 판단을 소유하지 않음 |
| PanelEffectView | PanelEffect | 패널 등장 / 퇴장 Tween, 시각 값과 패널 입력 / 활성 수명 적용 |
| ButtonEffectView | ButtonEffect | 포인터 / 선택 / 클릭에 따른 크기 연출과 복구. 버튼 업무를 실행하지 않음 |
| TextEffectView | TextEffect | 텍스트 강조 / 실패 / 점멸과 연출 종료 알림 |
| KeyPromptEffectView | KeyPromptEffect | 키 이미지 표시와 반복 눌림 연출 |
| GaugeEffectView | GaugeEffect | 표시 진행률 보간과 완료 강조. 실제 게임 판정을 수행하지 않음 |
| GameProgressView | 유지 | 시간 / 분위기 / 손님 수와 마무리 안내 표시를 담당합니다. |
| MissionView | 유지 | 목표 목록과 완료 화면 유지, 최신 표시 요청을 관리합니다. |
| MissionGoalView | 유지 | 목표 이름 / 수량 / 완료 색상과 최초 강조를 표시합니다. |
| GameLogView | 유지 | 로그 항목 생성 / 스크롤 / 표시 시간과 정리를 담당합니다. |

### MODEL_KEEP_CANDIDATES

| 변경 전 파일명 | 확정 이름 또는 유지 | 역할 / 기능과 유지 근거 |
| --- | --- | --- |
| GameFlowModel | 유지 | 시간 / Ready / Playing / FinalRush / Ended 전환, 종료 사유와 성공 / 실패 판정. 게임 진행 규칙을 함께 소유 |
| MoodModel | 유지 | 시간 감소 / 미션 회복 / 사건 / 포획 결과 반영, 중복 방지와 시간 평균 분위기 |
| PreparationModel | 유지 | 제작 결과 / 유효 배치 기록, 목표별 물건 수와 카테고리 가중 준비 점수. 단순 배치 카운터보다 범위가 넓음 |
| ApproachModel | 유지 | 접근 상태와 제한시간, 경로 / 거리 기반 도착 / 실패 판정. 이동은 Approacher / Mover 담당 |
| ProduceMinigameModel | 유지 | Timing / ClickArea 회차 시간, 핸들 이동 계산, 목표 생성 / 만료와 입력 판정, 난이도 효과. Judge만으로 진행 상태 책임을 표현하기 어려움 |
| ResistModel | 유지 | 구조 / 탈출 진행도, 시간 감소 / 제한시간과 성공 확정. 구조와 자기 탈출의 공용 규칙 |
| CarryModel | 유지 | View / World / 미보유 전환 허용과 보유 중 밀기 / 이동 가능 판정이며 기존 ItemHold 실행 모듈과 구별됩니다. |
| OpenCloseModel | 유지 | 열림 상태와 중복 전환 거절. 실제 뚜껑 배치는 별도 실행 담당 |
| PresenceModel | 유지 | 등장 / 제거 / 조사 ID / 정상 복구 대기와 요청 허용 판단. 단순 활성 상태보다 규칙이 넓음 |
| AppearanceChangeModel | 유지 | 외형 후보 중복 / 동일 외형 제외, 무작위 후보 소진, 원본과 변경 수명 / 복원 조건. Selector로 축소하면 변경 수명 책임이 가려짐 |
| MaterialChangeModel | 유지 | 매트리얼 후보 선택, 원본 / 변경 상태와 복원 허용. 선정 외 상태도 소유 |
| ReplaceModel | 유지 | 변경 유형의 가중치 후보 소진, 조사 ID와 변경 / 종료 / 복원 조건. 색 변경과 외형 변경을 묶는 규칙 |
| RotateModel | 유지 | 현재 각도, 유효 / 중복 각도 검사와 다음 각도 계산 |
| ShakeModel | 유지 | 흔들림 상태 전환과 회차 ID. 시간 / Transform 적용은 View 담당 |
| ThrowModel | 유지 | 투척 / 지면 접촉 상태, 이동 / 회전 감속 계산과 정지 조건 유지시간. Decelerator로 축소하면 접촉 / 진행 상태가 가려짐 |
| AnomalyPressureModel | 유지 | 방별 압력 / 미방문 시간 / 임계 도달 시각 / 발생 대기와 발생 성공 반영 |
| FetchPlayModel | 유지 | 공 놀이 회차, 범위 이탈 / 회수 인정, 완료 횟수와 만족 판정 |
| KidnapModel | 유지 | 납치 허용 / 표적 유형 / 접근 제한시간 / 구조 저지 이력. 납치 규칙의 범위에 맞음 |
| IngredientStockModel | 유지 | 개별 공급원 수량, 획득 예약 / 확정 / 소비 / 반환 대기와 재보충이며 IngredientStockManager의 여러 공급원 관리와 구별됩니다. |
| SupportItemSelectionModel | 유지 | 보유 목록, 반입 선택 / 해제와 최대 종류 제한. 선택 UI와 분리된 규칙 |
| ShopModel | 유지 | 구매 수량 / 가격 / 잔액 / 보유 한도 검증과 결제. ShopPresenter 유지 후보와 MVP 연결이 명확함 |

### EFFECT_PLAYER_AND_ADDITIONAL_KEEP

| 변경 전 파일명 | 확정 이름 또는 유지 | 실제 책임과 판단 근거 |
| --- | --- | --- |
| UiEffectPlayer | EffectPlayer | Tween 교체 / 중단과 소유 객체 수명 관리이며 UI 외 화면 / 카메라 / 스턴 연출에서도 사용합니다. |
| ProductionEffect | 유지 | 완성 파티클 재생과 정리입니다. |
| ScreenEffect / CameraEffect / StunEffect / CaptureEffect | 유지 | 상태에 따른 화면 / 카메라 / 모델 자세 연출과 복구입니다. |
| 기타 추가 / 독립 파일화 스크립트 | 유지 | 데이터 / 설정 / 기록 / 구성 항목과 사운드는 현재 기능 이름을 보존합니다. |

### VIEW_AND_MODEL_REVIEW_ORDER

| 순서 | 범위 | 개수 |
| --- | --- | --- |
| 1 | View의 실행 제어 / 기능 연출 / 상위 HUD 이름 변경 | 10 |
| 2 | 구체적인 기능명으로 바꿀 Model 후보 | 6 |
| 3 | 기능명을 구체화할 Model 후보 | 5 |
| 유지 | View / Model 유지, 변경 후보 제외 포함 | 13 / 24 |

- View / Model은 21개 이름 변경과 37개 유지를 확정했습니다. Model 접미사는 규칙 / 판단 / 상태 책임이 명확한 경우 유지하며, 공용 UI 연출 5개는 기능 이름으로 변경했습니다.
- 클래스 / 생성자 / 타입 참조 / 상속 / RequireComponent / Editor 도구 / 타입 문자열과 담당 문서 참조를 함께 정리했습니다. 파일명 변경을 완료했으며 Unity 컴파일 결과와 실행 확인은 남아 있습니다.
- ItemWorldController / PresenceDisplayController / AppearanceComponents는 외형 변경 / 배치 / 변칙 / 풀링 / 제작 도구에서 함께 사용하므로 개별 기능 내부 Rename보다 영향 범위가 넓습니다. EnemyRoomActivityModel은 HarmRetaliationModel / KidnapModel 상속과 함께 확인합니다.
- Unity 스크립트 파일 Rename은 기존 기준에 따라 사용자가 프로젝트 창에서 진행합니다. 직렬화 필드명 / 씬 / 프리팹 / .meta는 이번 검토에서 변경하지 않습니다.

