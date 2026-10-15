﻿# ROADMAP

## CURRENT_PHASE

- 전체 단계는 플레이 기반 → 제작 지원 → 콘텐츠 / 연출 구성 → 전체 플레이 검증 → 후속 확장 순서입니다. 현재 우선순위는 [TODO](TODO.md#current_todo)에서 관리합니다.
- 기능별 구현과 사용자 확인은 [DONE](DONE.md), 현재 우선 작업과 실행 검증은 [TODO](TODO.md#current_todo)를 따릅니다.
- 공통 매니저와 풀링의 상세 범위 / 적용 전 확인은 [TODO](TODO.md#pooling_and_managers)를 따릅니다. 이 문서에서는 전체 개발 단계만 관리합니다.
- 콘텐츠 / 연출 구성에는 애니메이션 제어, 맵 경로, 오디오와 이펙트 연결을 포함합니다. 준비 이력은 [DONE](DONE.md#데이터--콘텐츠-준비)를 따릅니다.
- 현재는 남은 오류와 반복 활동 / 통합 플레이를 확인하는 단계입니다. 창밖 풍경과 담비 도주 보완의 확인 범위는 DONE을 따릅니다. 추락 복귀와 개 FeedbackPlayer 이전은 보류하며 다른 적 순차 이전을 자동 진행하지 않습니다. 세부 우선순위는 [TODO](TODO.md#current_todo)를 따릅니다.
- Hard 전용 Distortion은 연기 가능한 후속입니다. 맵 / 모델 / 애니메이션 작업의 필수 선행 조건으로 두지 않습니다.
- 멀티플레이와 확장 콘텐츠는 별도 범위입니다. 세부 작업은 [TODO](TODO.md)를 따릅니다.

## MILESTONES

| 단계 | 목표 |
| --- | --- |
| 1. 상점 | 구매 / 다음 판 전달 / 시작 실패 처리 / 결과 후 복귀 |
| 2. 난이도 | 설정 맵 / 명시적 초기화 / 제작과 발생 설정 / 미션 랜덤 / 타이틀 선택 전달. 상세 순서는 GAMEPLAY_CYCLE, 최종 수치는 맵 검증 후 조정 |
| 3. 제작 지원 | 컴포넌트 일괄 설정 Tools / 변칙 설정 등 대량 데이터 임포터 |
| 4. 기본 동작 확인 | 구매 → 난이도 선택 → 플레이 → 결과 → 다음 판의 기본 왕복, 치명적인 오류 정리 |
| 5. 콘텐츠 / 연출 구성 | 애니메이션 제어 / 맵 경로 / 오디오 / 이펙트와 게임 기능 연결. 상세는 [TODO](TODO.md#current_todo) |
| 6. 전체 플레이 검증 | 준비한 데이터 / 프리팹 / 맵으로 난이도별 진행, 결과와 반복 플레이 확인 |
| 7. 후속 확장 | Hard Distortion / 추가 콘텐츠 / 최종 밸런스. 멀티플레이는 별도 범위 |
| 범위 결정 | 클래스 / 성장 / 시설 / 장비, 복잡한 제작·행동 확장의 MVP 포함 여부 |

- 아이템 / 상점: [SUPPORT_ITEM_CATALOG](System/SUPPORT_ITEM_CATALOG.md), [REWARD_ITEM](System/REWARD_ITEM.md).
- 제작 지원: [EDITOR_TOOLS](System/EDITOR_TOOLS.md), [DATA_IMPORT](System/DATA_IMPORT.md). 작업 전 승인 항목: [PENDING_DECISIONS](PENDING_DECISIONS.md).
- 난이도 / 사건: [GAMEPLAY_CYCLE](System/GAMEPLAY_CYCLE.md#difficulty_plan), [ANOMALY](System/ANOMALY.md#6-distortion--표현-왜곡), [ANOMALY_CATALOG](System/ANOMALY_CATALOG.md).
