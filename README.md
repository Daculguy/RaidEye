# RaidEye

RaidEye is a Windows laptop activity guard. Arm it from the tray, leave the laptop, and it logs the first meaningful activity after arming. This proof of concept also includes a quick notification prototype using ntfy or a generic webhook.

## Current scope

- Windows tray app with Arm, Disarm, Test Notification, Open Event Log, and Open Config actions.
- Detects keyboard/mouse input after arming using the Windows `GetLastInputInfo` API.
- Also listens for session unlock/logon and resume-from-sleep events.
- Writes local JSONL events to `%LOCALAPPDATA%\RaidEye\events.jsonl`.
- Optional push prototype:
  - ntfy topic push, useful with the ntfy iPhone app.
  - Generic JSON webhook for automation services, SMS gateways, or custom relays.

## Build

This repo intentionally builds without the .NET SDK by using the Windows .NET Framework compiler that ships with Windows.

```powershell
.\scripts\build.ps1
```

The executable is written to:

```text
.\dist\RaidEye.exe
```

## Smoke test

```powershell
$p = Start-Process .\dist\RaidEye.exe -ArgumentList '--self-test' -Wait -PassThru
$p.ExitCode
```

The self-test sends a harmless synthetic `F24` key press so the activity watcher can prove it detects post-arm input.

The self-test writes:

```text
%LOCALAPPDATA%\RaidEye\self-test.txt
```

## Run

```powershell
.\dist\RaidEye.exe
```

RaidEye appears in the Windows notification area. Right-click the tray icon to arm or disarm it.

## Configure notifications

Open the tray menu and choose **Open Config**, or edit:

```text
%LOCALAPPDATA%\RaidEye\settings.ini
```

For ntfy:

```ini
ntfy_enabled=true
ntfy_base_url=https://ntfy.sh
ntfy_topic=raideye-your-random-topic-name
```

On iPhone, install the ntfy app and subscribe to the same topic. Then use **Test Notification** from the tray menu.

For a generic webhook:

```ini
webhook_enabled=true
webhook_url=https://example.com/raideye-hook
```

RaidEye sends a JSON payload with the event type, timestamp, laptop name, and message.

## Local files

RaidEye stores user-local files under:

```text
%LOCALAPPDATA%\RaidEye\
```

- `settings.ini`: local configuration.
- `events.jsonl`: append-only event log.

## MVP behavior

When you arm RaidEye, it ignores activity for a short grace period so the arming click does not trigger an alert. After that, the first keyboard or mouse input creates an `ActivityDetected` event and sends a notification if a provider is configured.

By default, RaidEye alerts once per arming to avoid spam. You can change that with:

```ini
trigger_once_per_arming=false
trigger_cooldown_seconds=300
```
