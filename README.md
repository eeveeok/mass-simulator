# 🚢 Autonomous Ship Simulator (자율운항선박 시뮬레이터)

> **실제 해상 물리 환경을 모사하고 최적 경로 탐색 및 회피 알고리즘을 검증하기 위한 고성능 시뮬레이터입니다.**

---

## 📌 프로젝트 개요
본 프로젝트는 풍향, 조류, 마찰력 등 복합적인 해상 변수를 파라미터화하여 현실적인 테스트 환경을 구축하고, 선박의 전역/국소 경로 탐색 및 안정화 제어 로직을 구현한 시뮬레이션 시스템입니다.

## 🚀 주요 기능

### 🌊 현실적인 해상 환경 구축
- **물리 파라미터화**: 풍향, 조류 강도, 선박 마찰력 등 외부 물리 요소를 실시간 반영
- **고정밀 모사**: 실제 해상 환경과 유사한 변동성을 가진 테스트 베드 구현

### 🛤️ 지능형 경로 탐색 및 주행
- **A* Algorithm**: 목적지까지의 전역 최적 경로(Global Path) 탐색 로직 구현
- **APF**: 장애물 척력과 목적지 인력을 활용한 부드러운 회피 기동 및 경로 생성
- **Raycasting**: 실시간 주변 지형 및 장애물 감지 기능을 통해 회피 경로 사전 예측
  **이미지 인식 기반 경로 선택**: OpenCV 기반 경로 선택 알고리즘 구현

### ⚓ 선박 제어 및 최적화
- **PID Control**: 속도 및 방향 유지의 안정성을 극대화한 자동 제어 로직
- **FSM**: 탐색, 회피, 복귀, 정지 등 상태별 자동 전환 로직 설계
- **Performance Optimization**: **Object Pooling** 기법을 적용하여 대규모 환경에서도 프레임 드롭 최소화

### 📊 실시간 분석 시스템
- **UI & Logging**: 항로별 이동 시간, 타겟과의 오차율, 제어 안정성 지표 시각화
- **데이터 분석**: 시뮬레이션 결과 데이터를 실시간으로 모니터링하여 로직 개선 근거 확보

## 🛠 Tech Stack
- **Engine**: Unity, OpenCV
- **Language**: C#
- **Algorithms**: A*, APF, PID Control, Raycasting

## 📂 프로젝트 구조 (Example)
```text
├── Assets
│   └── Scripts
│       ├── AutoNavigation     # FSM 기반 상태 자동 전환 및 자율 주행 메인 로직
│       ├── Boat               # 선박의 물리 엔진 설정 및 PID 제어 시스템
│       ├── Camera             # 시뮬레이션 모니터링용 카메라 제어
│       ├── FigureDetection    # Raycasting 및 장애물 탐지 알고리즘
│       ├── Lidar              # 레이저 기반 주변 환경 스캔 및 데이터 처리
│       ├── PathFinding        # A* 및 APF 기반 전역/국소 경로 탐색
│       └── Utils              # Object Pooling 및 범용 유틸리티 클래스

<img width="1489" height="1054" alt="image" src="https://github.com/user-attachments/assets/6932b1a5-1525-40e9-9fb3-abccc1b89d37" />
<img width="977" height="1053" alt="image" src="https://github.com/user-attachments/assets/92274606-d16e-4259-8c76-19ebf6f7d056" />
