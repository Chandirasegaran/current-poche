; Windows installer for Current Pochu!
; Built by Packaging/build.sh with: makensis -DVERSION=... -DSRC=... -DOUT=...
!include "MUI2.nsh"

Name "Current Pochu!"
OutFile "${OUT}"
InstallDir "$PROGRAMFILES64\Current Pochu"
InstallDirRegKey HKLM "Software\SegarGames\CurrentPochu" "InstallDir"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
Unicode true

!define MUI_ICON "currentpochu.ico"
!define MUI_UNICON "currentpochu.ico"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\CurrentPochu"
!define MUI_FINISHPAGE_RUN "$INSTDIR\CurrentPochu.exe"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Section "Current Pochu!"
  SetOutPath "$INSTDIR"
  File /r "${SRC}/*"
  File "currentpochu.ico"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateDirectory "$SMPROGRAMS\Current Pochu"
  CreateShortcut "$SMPROGRAMS\Current Pochu\Current Pochu!.lnk" "$INSTDIR\CurrentPochu.exe" "" "$INSTDIR\currentpochu.ico"
  CreateShortcut "$SMPROGRAMS\Current Pochu\Uninstall.lnk" "$INSTDIR\Uninstall.exe"
  CreateShortcut "$DESKTOP\Current Pochu!.lnk" "$INSTDIR\CurrentPochu.exe" "" "$INSTDIR\currentpochu.ico"

  WriteRegStr HKLM "Software\SegarGames\CurrentPochu" "InstallDir" "$INSTDIR"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayName" "Current Pochu!"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "Publisher" "Segar Games"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\currentpochu.ico"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  Delete "$SMPROGRAMS\Current Pochu\Current Pochu!.lnk"
  Delete "$SMPROGRAMS\Current Pochu\Uninstall.lnk"
  RMDir "$SMPROGRAMS\Current Pochu"
  Delete "$DESKTOP\Current Pochu!.lnk"
  RMDir /r "$INSTDIR"
  DeleteRegKey HKLM "${UNINSTALL_KEY}"
  DeleteRegKey HKLM "Software\SegarGames\CurrentPochu"
SectionEnd
