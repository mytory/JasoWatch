# JasoWatch

JasoWatch는 Windows 폴더에 들어오는 분해된 Unicode 파일명을 자동으로 NFC 형식으로 바꾸는 가벼운 트레이 유틸리티입니다. 예를 들어 `한글 문서.pdf`를 `한글 문서.pdf`로 정리합니다.

파일의 내용은 건드리지 않고 이름만 바꿉니다. 설치 과정 없이 EXE 하나로 실행됩니다.

## 다운로드와 실행

1. [Releases](../../releases)에서 최신 `JasoWatch-win-x64.zip`을 받습니다.
2. 압축을 풀고 `JasoWatch.exe`를 실행합니다.
3. 작업 표시줄 오른쪽의 숨겨진 아이콘 영역에서 JasoWatch 아이콘을 찾습니다.

Windows SmartScreen 경고가 표시될 수 있습니다. 이 첫 버전은 코드 서명이 되어 있지 않기 때문입니다. 신뢰할 수 있는 이 저장소에서 받은 파일인지 확인한 뒤 **추가 정보 → 실행**을 선택할 수 있습니다.

## 사용 방법

기본 감시 폴더는 Windows의 다운로드 폴더입니다. 새 파일이 완전히 내려받아지고 잠금이 풀리면, 이름에 NFC로 조합할 수 있는 문자가 있을 때 자동으로 정리합니다.

트레이 아이콘을 오른쪽 클릭하면 다음 작업을 할 수 있습니다.

- **감시 일시 중지 / 재개**: 자동 정리를 잠시 멈춥니다.
- **다운로드 폴더 열기**: 현재 감시 폴더를 엽니다. 아이콘을 두 번 클릭해도 됩니다.
- **기존 파일명 정리**: 이미 폴더에 있던 파일과 폴더를 한 번 검사합니다.
- **설정**: 감시 폴더, 하위 폴더 포함 여부, 제외 규칙, Windows 시작 시 자동 실행을 바꿉니다.

`.crdownload`, `.part`, `.tmp` 파일과 `~$`로 시작하는 파일은 기본적으로 건너뜁니다. 설정에서 이 규칙을 바꾸거나 기본값으로 되돌릴 수 있습니다.

## 알아둘 점

- Windows 10/11 64비트용입니다.
- 네트워크 공유 폴더와 ARM64/32비트 Windows는 지원하지 않습니다.
- 같은 이름이 이미 있으면 기존 항목을 덮어쓰지 않고 `이름 1.ext`처럼 번호를 붙입니다.
- 호환 자모(`ㅎㅏㄴㄱㅡㄹ`)를 한글로 조합하지는 않습니다. Unicode 표준 NFC 정규화만 적용합니다.

## 개발과 검증

.NET 10 SDK에서 다음을 실행합니다.

```powershell
dotnet test .\JasoWatch.Tests\JasoWatch.Tests.csproj -c Release
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

MIT License
