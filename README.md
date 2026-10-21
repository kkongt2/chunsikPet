# 춘식이 데스크톱 펫

Windows 작업표시줄 위의 캐릭터, 알림, 포스트잇, 타이머·스톱워치 프로그램입니다.
만든이: **kkongt2.kang** · 현재 버전: **1.40.3**

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

## 추가된 사용자 기능 (1.33~1.39)

- 알림 행의 **▶ 보기**, 포스트잇의 **표시 미리보기**: 실제 발송 없이 모션·색상·문구를 확인합니다.
- 트레이의 **알림 이력 · 놓친 알림**: 최근 300건, 확인/다시 알림 상태, 다시 보기를 제공합니다. 절전 복귀 시 최신 누락 알림을 알림별 하나씩 복구합니다(최대 7일).
- **방해 금지**: 30분·1시간·직접 해제, 전체 화면 자동 숨김. 대기 알림은 해제 후 이어서 표시합니다.
- **설정 백업**: 단일 `.chunsik` 파일. 설정 창은 검증·저장 후 닫히며, 복원 후 EXE를 다시 실행해야 합니다. 복원 전 설정은 사용자 데이터 폴더에 보관됩니다. 알림 이력과 실행 중인 타이머는 백업 대상이 아닙니다.
- 포스트잇 완료 후 **12초 되돌리기**와 완료 날짜 표시.
- 캐릭터·알림·포스트잇·시간 도구·프로그램의 **통합 설정 탭**.
- 간격 알림의 **활동 시간 · 자리 비움**: 기본 제한 없음, 원하는 시간대만 동작하거나 입력 5분 이상 없으면 중단. 활동 복귀 후 간격을 새로 계산합니다. 자정을 넘긴 시간대도 현재 날짜의 요일 선택이 적용됩니다.
- 모니터·DPI 변경에 따른 창 위치 복구, 작은 화면의 알림 설정 가로 스크롤.

버전은 `VERSION`, 실행 파일 속성, 프로그램 정보, [CHANGELOG.md](CHANGELOG.md)에 기록합니다. 새 기능은 마이너 버전, 호환성을 유지하는 수정은 패치 버전을 올립니다.

GitHub Actions에서 Windows Release 빌드와 스케줄 테스트를 실행하고, 성공한 실행의 **Artifacts → ChunsikPet-Windows-x64**에서 EXE를 받을 수 있습니다.

### 수동으로 GitHub Release 게시

1. **Actions → Publish latest Windows EXE → Run workflow**에서 **main**을 선택해 실행합니다.
2. 실행 시점의 `main` 최신 커밋과 `VERSION`을 읽습니다. Windows x64 EXE 빌드와 스케줄 테스트가 통과하면 `v버전` 태그, 변경 내역을 담은 Release 게시글, 첨부 파일 **ChunsikPet.exe**를 만들고 Latest로 지정합니다.
3. 같은 태그의 Release가 이미 있으면 초안을 포함해 빌드·게시를 건너뜁니다. 기존 태그는 덮어쓰지 않으며, 태그만 존재하고 다른 커밋을 가리키면 오류로 중단합니다.

별도 토큰 설정 없이 워크플로의 `GITHUB_TOKEN`을 사용합니다. 게시 중 업로드에 실패하면 Release는 초안으로 남습니다. 재시도하려면 실패한 초안을 삭제한 후 다시 실행하세요. 공개된 기존 Release는 수정하지 않습니다. 새 버전은 `VERSION`과 `Program.cs`의 두 버전 특성을 함께 변경해야 합니다.

검증 범위와 수동 절차: [tests/WINDOWS-CHECKLIST.md](tests/WINDOWS-CHECKLIST.md). 물리 화면·절전·보안 제품 판정은 자동 테스트로 보장하지 않습니다.

## 앉은 자세 마우스 회피 (1.40.0)

마우스 피하기 ON 상태에서 ‘모두 잠깐 쉬기’ 또는 캐릭터의 ‘잠깐 쉬기’를 켜도 커서를 피합니다. 자동으로 앉아 쉬던 캐릭터에도 적용됩니다. 상체를 세운 앉은 자세와 바닥 접촉 높이를 유지하고 양쪽 발만 움직이며 천천히 이동합니다. 커서가 멀어지거나 마우스 피하기를 끄면 이동을 멈추며, 수동 쉬기는 해제하지 않습니다. 알림 모션·드래그·수면 중 회피 제외 규칙은 유지합니다.
