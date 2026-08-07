# macOS OpenCV 비전 검사 데모

C# Avalonia UI와 OpenCvSharp로 만든 macOS용 최소 검사장비 UI입니다.

처리 순서:

1. 이미지 불러오기
2. Gray 변환(비교 화면 표시)
3. WM-811K용 Yellow HSV 또는 일반 Gray Threshold 방식으로 결함 마스크 생성
4. 연결된 흰색 영역을 결함으로 분리
5. 결함 개수, 전체 결함 픽셀 면적, ROI 대비 면적 비율, 최대 단일 결함 면적 계산
6. 세 가지 FAIL 기준 중 하나 이상을 넘으면 FAIL

## 실행 환경

- Apple Silicon(arm64) Mac
- .NET 8 SDK
- JetBrains Rider, Visual Studio Code 또는 `dotnet` CLI

macOS 설치 프로그램으로 SDK를 설치했는데 `dotnet: command not found`가 나오면 다음 경로를
`~/.zprofile`에 등록한 뒤 터미널을 다시 여세요.

```bash
echo 'export PATH="/usr/local/share/dotnet:$PATH"' >> ~/.zprofile
source ~/.zprofile
dotnet --version
```

```bash
cd VisionInspectionDemo
dotnet restore
dotnet run
```

macOS 파일 선택창에 문제가 있으면 Finder에서 PNG/JPG 파일을 앱 창으로 직접 드래그해도
동일하게 이미지를 불러옵니다.

현재 프로젝트는 `RuntimeIdentifier=osx-arm64`로 설정되어 있습니다. Intel Mac이라면 이를
`osx-x64`로 바꾸고 OpenCV 런타임 패키지도 `OpenCvSharp4.runtime.osx.x64`로 변경해야 합니다.

## 샘플 이미지 사용법

첨부된 3×3 이미지는 알고리즘 학습용으로는 적합하지만, 한 장 전체를 바로 검사하면 제목과 테두리도 검출됩니다. `Center`, `Donut`, `Scratch`처럼 **라벨과 바깥 여백을 제외한 타일 하나만 잘라서** 불러오세요.

- `Gray Threshold` 비교 모드에서는 Threshold를 대략 150~200 범위에서 조절합니다.
- WM-811K 맵은 기본 `Yellow HSV` 모드를 사용합니다. 기본 범위는 H 20~40,
  S 100 이상, V 100 이상입니다.
- `Gray Threshold`는 색의 의미가 아니라 밝기를 기준으로 검사하므로 일반 영상처리 비교용입니다.
- 작은 점을 무시할 때: 최소 결함 면적을 올립니다.
- 원형 시편일 때: 원형 ROI를 켭니다.
- 실제 장비에서는 조명, 카메라 노출, 배경, 촬영 위치를 고정해야 합니다.

현재 결함 수는 **서로 연결된 결함 영역(blob)의 개수**입니다. Donut처럼 큰 결함이 하나의
영역으로 연결되어도 PASS가 되지 않도록 다음 항목을 함께 판정합니다.

- FAIL 결함 수: 기본 20개 이상
- FAIL 면적 비율: ROI의 기본 5% 이상
- FAIL 최대 결함: 기본 500px² 이상

전체 결함 면적은 외곽 contour 면적이 아니라 이진 영상의 실제 결함 픽셀을 합산합니다.
따라서 Donut 내부의 검은 구멍을 결함 면적에 잘못 포함하지 않습니다. 기본값은 데모용이며
실제 공정 규격이 아닙니다.
# Wafer-Demo
