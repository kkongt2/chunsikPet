# 춘식이 데스크톱 펫

Windows 작업표시줄 위의 캐릭터, 알림, 포스트잇, 타이머·스톱워치 프로그램입니다.
만든이: **kkongt2.kang** · 현재 버전: **1.36.0**

## Visual Studio에서 EXE 빌드

1. **Visual Studio 2022**를 설치하거나 Visual Studio Installer에서 **수정**을 선택합니다. Visual Studio 2019에서도 열 수 있는 .NET Framework 프로젝트입니다.
2. 워크로드 **.NET 데스크톱 개발**을 선택합니다. 개별 구성 요소에서 **.NET Framework 4.8 SDK / Targeting Pack**도 설치합니다. 런타임만으로는 빌드할 수 없습니다.
3. 저장소를 Clone하거나 **Code → Download ZIP**으로 내려받아 압축을 풉니다.
4. 루트의 **ChunsikPet.sln**을 Visual Studio로 엽니다.
5. 상단 구성을 **Release**, 플랫폼을 **x64**로 선택합니다.
6. **빌드 → 솔루션 빌드** (`Ctrl+Shift+B`)를 누릅니다.
7. 생성 파일: **ChunsikDesktopPet\bin\x64\Release\ChunsikPet.exe**

실행 테스트는 `F5`(디버깅) 또는 `Ctrl+F5`(디버깅 없이 실행)로 할 수 있습니다. 기존 춘식이가 실행 중이면 트레이 메뉴에서 먼저 종료하세요.
이미지와 휴일 CSV는 EXE에 포함됩니다. 배포할 때는 EXE 하나만 복사하면 됩니다. 대상 PC에는 .NET Framework 4.8 런타임이 필요합니다. PDB는 디버깅용이며 배포에 필수는 아닙니다.

### 명령줄 빌드

Visual Studio의 **Developer Command Prompt**에서 저장소 루트로 이동합니다.

```bat
msbuild ChunsikPet.sln /t:Rebuild /p:Configuration=Release /p:Platform=x64 /m
```

외부 NuGet 패키지를 사용하지 않으므로 개발 도구가 설치되어 있으면 오프라인으로 빌드할 수 있습니다.

### 빌드 오류 해결

- `MSB3644` 또는 .NET Framework 참조 어셈블리 없음: **4.8 Developer Pack** 설치 후 Visual Studio를 다시 엽니다.
- WPF 관련 참조 없음: .NET 데스크톱 개발 워크로드와 4.8 SDK/Targeting Pack 설치를 확인하세요.
- 출력 EXE를 덮어쓸 수 없음: 실행 중인 춘식이를 종료하세요.

[Microsoft 개발자 팩 안내](https://learn.microsoft.com/dotnet/framework/install/guide-for-developers)

## 실행 구조와 저장 위치

- 사전 컴파일된 C# / WPF / .NET Framework 4.8, Windows x64 프로그램입니다.
- PowerShell 실행, 실행 시 소스 컴파일, 난독화·패커는 사용하지 않습니다.
- 내장 PNG와 휴일 CSV만 `%LOCALAPPDATA%\ChunsikDesktopPet\assets-v32`에 준비합니다.
- 사용자 알림·할 일·캐릭터 설정은 `%LOCALAPPDATA%\ChunsikDesktopPet`에 저장합니다. 업데이트 후에도 기존 설정을 읽습니다.
- 공휴일 정보는 오프라인 CSV를 사용합니다. 임시공휴일 및 향후 법령 변경은 별도 데이터 갱신이 필요합니다.
- 빌드된 파일에는 디지털 서명이 없습니다. 직접 빌드해도 회사 보안 정책의 승인을 대체하지 않습니다. 차단될 때는 보안 담당자의 검토가 필요합니다.

## 소스 구성

| 파일 | 역할 |
| --- | --- |
| Program.cs | 실행 진입점, 내장 데이터 준비, 버전 정보 |
| Pet.cs | 캐릭터 이동·모션·상호작용 |
| Reminders.cs | 알림 스케줄과 설정 화면 |
| Todos.cs | 포스트잇과 완료 애니메이션 |
| TimeTools.cs | 타이머·스톱워치 |
| PetOptions.cs | 캐릭터 환경 설정 |
| TrayController.cs | 트레이 메뉴·프로그램 정보 |
| PaydayCalendar.cs | 월급날·공휴일 계산 |

현재 소스는 Microsoft C# 컴파일러로 컴파일을 확인했습니다. Windows에서의 전체 UI 동작 및 보안 제품의 판정은 별도 검증이 필요합니다.
캐릭터 관련 권리는 해당 권리자에게 있으며, 이 저장소가 캐릭터의 상업적 이용 허락을 제공하지는 않습니다.
