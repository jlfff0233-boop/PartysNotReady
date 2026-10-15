# DATA_IMPORT

## PURPOSE

대량 설정 데이터의 임포트와 확장 계획을 관리합니다. 현재 구현 / 계획 / 정책 미정을 구분하며 코드 구현 완료 기록은 [DONE](../DONE.md)을 따릅니다.

## SYSTEM_OVERVIEW

입력 → 파싱 / 검증 → 적용 계획 → 에셋 반영 → Map / 저장 순서로 진행합니다.

검증은 원본 위치와 값 / ID / 참조 / 경로를 확인하며 적용 계획은 생성 / 갱신 / 보존 / 목록 제외를 구분합니다.

- 적용 전 오류: 에셋 변경 없이 오류 위치를 보고합니다.
- 적용 중 오류: 이미 반영한 변경과 실패 지점을 보고하며 전체 자동 롤백은 보장하지 않습니다.

입력에 없는 에셋은 삭제하지 않습니다. Map에서 빠지는 항목과 에셋 삭제는 다르며 생성 / 수정 / 보존 / 저장 결과를 구분합니다.

## CURRENT_IMPLEMENTATION

- 기존 소스: Assets/Editor/Importer/Food. FoodSourceParser / DelimitedTextParser, FoodImportValidator, FoodDataAssetImporter / RecipeDataAssetImporter와 각 Window가 있습니다.
- 메뉴는 Tools/Importer/Food/Data와 Tools/Importer/Food/Recipe입니다. Food / Recipe 모두 JSON / CSV를 읽으며 원본 입력 / 값 / 참조 / 출력 경로를 검증합니다.
- 정의되지 않은 enum / flags 비트, NaN / Infinity, 잘못된 CSV 따옴표 경계를 거절합니다. DataMap 경로 충돌과 Legacy 이관 대상 중복도 적용 전에 검사합니다.
- 같은 ID의 기존 SO를 재사용하고 지원되는 Legacy Food ID를 이관합니다. 에셋을 삭제·재생성하지 않아 기존 GUID를 보존합니다. 입력에 없는 에셋을 자동 삭제하지 않습니다.
- Food의 관리 대상 외 필드와 Recipe의 기존 Minigame / Result Prefab 설정은 보존합니다. FoodDataMap은 입력 목록으로 교체하며 빠지는 항목을 사전에 알립니다.
- 적용 중 실패는 이미 반영한 변경과 실패 결과를 보고합니다. 전체 작업의 자동 롤백을 보장하지 않습니다. 플레이 / 전환 중 적용을 막고 실제 변경 에셋을 저장합니다.
- 생성 / 값 반영 / Legacy ID 변경 / 저장과 DataMap 적용 / 저장 결과를 구분합니다. 에셋 저장에는 같은 에셋의 다른 미저장 변경도 함께 포함됩니다.
- 구현 / 실행 확인 이력은 [DONE](../DONE.md), 검증 작업은 [TODO](../TODO.md#data_and_asset_follow_up)를 따릅니다.

## HUB_FILTER_AND_PRESENCE

- DataImporterHubWindow는 전체 / 음식 / 변칙 / 미션 및 준비 / 일반 아이템 / 밸런스 및 난이도 / 적 분류로 작업 목록을 필터링합니다. 필터는 표시만 제한하며 기존 작업 / 선행 연결을 삭제하지 않습니다.
- DataImportPresenceView / IDataImportPresenceProvider는 입력별 대응 에셋의 현재 존재를 표시합니다. 수동 생성한 에셋도 존재할 수 있으므로 임포트 완료 이력 / 최근 임포트 날짜 / 입력과 현재 값의 완전 일치를 보장하지 않습니다. 임포트 시각 기록 기능은 없습니다.
- BalanceDataImporterWindow는 Pressure / Mood / Score / 음식 Map / Difficulty / Bear 연결 입력을 처리합니다. Difficulty는 세 난이도 설정을 연결하며 테스트를 위해 임시 선택한 Easy를 전체 난이도의 확정 값으로 취급하지 않습니다.
- 일반 ItemData는 별도 목록 Map을 자동 생성하지 않습니다. 음식 Map, 일반 배치 미션 Map, 담비 수집 대상 연결의 용도를 구분합니다. 사용자 임포트 보고는 DONE에, 밸런스와 실제 실행 검증은 TODO에 기록합니다.

## ITEM_IMPORT

- 일반 아이템 메뉴: Tools/Importer/Item/Data. ItemDataImporterWindow는 공통 DataImporterWindow를 사용하며 ItemDataAssetImporter.PrepareItems가 적용 계획을 구성합니다.
- JSON은 dataType="ItemData", records 배열을 사용합니다. CSV 열은 id / itemName / capacityCost이며 이름은 필수, 용량은 1 이상 정수입니다.
- 일반 ItemData만 생성 / 갱신합니다. FoodData와 서포트 파생 타입은 직접 연결과 출력 경로 조회에서도 갱신 대상으로 인정하지 않습니다. 별도 런타임 ID 필드를 추가하지 않고 에셋 이름 또는 직접 연결로 기존 데이터를 찾습니다.
- 관리 필드는 _itemName / _capacityCost이며 기존 에셋의 이름과 참조는 보존합니다. 아이템 목록 Map은 자동 생성하지 않으며 적 수집 대상 목록을 임의로 변경하지 않습니다.
- 일반 배치 메뉴: Tools/Importer/GameFlow/Item Placement. ItemPlacementImporterWindow와 ItemPlacementAssetImporter.PreparePlacement가 ItemPlacementMissionData / Map을 구성합니다.
- JSON은 dataType="ItemPlacementMissionData", records 배열을 사용합니다. CSV 열은 id / itemData / placementMission / locations입니다. JSON locations는 문자열 배열, CSV는 |로 구분한 목록입니다.
- 아이템은 프로젝트의 일반 ItemData, 미션은 참조 MissionDataMap이나 직접 연결 / 경로 / GUID로 해석합니다. 동일 아이템 종류의 중복 설정과 비어 있거나 None / 미정의 / 중복 장소를 거절합니다.
- 설정은 직접 연결, 고유 에셋 이름 또는 기존 Map의 동일 아이템 설정으로 재사용합니다. _itemData / _placementMission / _locations를 반영하고 Map의 _data를 입력 전체로 교체합니다. 입력에 없는 에셋은 삭제하지 않습니다.
- 통합 창은 일반 아이템의 ID별 검사 / 적용 결과를 제공하며 기존 미션 항목의 결과 Map을 일반 배치 항목에 연결합니다. 아이템 변경은 배치 / 준비 평가 검사를, 미션 변경은 배치 검사를 무효화합니다. 선행 연결은 항목당 하나이며 아이템은 먼저 적용한 프로젝트 에셋을 조회합니다.
- PreparationGoalImporterWindow는 일반 ItemData 참조를 지원합니다. 기존 준비 평가 Map을 유지하는 재임포트에는 음식과 일반 소품의 전체 목표 입력을 사용합니다. 부분 입력은 Map에서 나머지 목표를 제외합니다.
- 파일별 메뉴와 적용 순서는 [입력 안내](../../DataImport/RabbitParty_2026-10-01/ImportInstructions.json), 실제 적용 기록은 [DONE](../DONE.md#데이터--콘텐츠-준비), 남은 작업은 [TODO](../TODO.md#manual_setup_checklist)를 따릅니다.

## SCOPE

- 대상별 구현 / 적용 상태는 [DONE](../DONE.md#데이터--콘텐츠-준비), 추가 구현 / 입력 적용 / 실행 검증은 [TODO](../TODO.md#manual_setup_checklist)를 단일 원본으로 사용합니다.
- 새 대상의 입력 형식 / 식별 / Map 교체 / 실패 정책은 [PENDING_DECISIONS](../PENDING_DECISIONS.md#data_import)에서 결정합니다. 소수 설정까지 모두 범용 임포터 대상으로 확대하지 않습니다.

## IMPORT_FLOW

1. 원본 예시와 관리 필드, 식별·참조 기준을 확정합니다. 기존 파서 / 파일 선택 / 결과 보고 기능의 재사용 범위를 확인합니다.
2. 파싱과 데이터 검증을 먼저 수행하고 생성 / 변경 / 보존 / 목록 제외 / 오류 목록을 표시합니다. CSV는 행·열, JSON은 ID·필드 등 원본에서 찾을 수 있는 위치를 보고합니다.
3. 출력 경로와 타입 충돌, 중복 식별자, 참조 누락을 적용 전에 검사합니다. 신규 데이터끼리의 참조는 전체 입력을 기준으로 검사합니다.
4. 기존 에셋을 재사용하고 관리 필드만 반영합니다. 신규 에셋 생성과 참조 연결의 순서를 나누어 입력 순서에 의존하지 않게 합니다.
5. 재임포트 / 이름 변경 / 누락 데이터 / 적용 실패를 확인하고 생성·수정·보존·실패 결과를 구분합니다.

- Unity 에디터 API로 에셋을 처리하며 YAML / .meta를 직접 편집하지 않습니다. 에셋 저장과 참조 보존은 플레이 실행 코드에 넣지 않습니다.
- 기존 도구에 없는 완전한 롤백이나 범용 임포트 프레임워크를 먼저 만들지 않습니다. 참조 유지 / 반복 실행 / 수동 값 보존이 우선입니다.
- 새 대상의 입력 형식 / ID / Map 갱신 / 실패 정책은 [PENDING_DECISIONS](../PENDING_DECISIONS.md#data_import)에서 결정합니다.

## VERIFICATION

검증 항목은 [TODO](../TODO.md#data_and_asset_follow_up)에서 관리하며 판정 기준은 위 입출력 / 참조 보존 / 실패 처리 계약을 따릅니다.
