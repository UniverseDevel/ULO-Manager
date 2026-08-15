# Use Cases

Practical recipes for running the camera unattended: scheduled synchronisation, presence-based mode
switching, snapshots, live video and housekeeping.
Every example uses the `ulo` command line tool from the [application guide](APPLICATION.md).

Credentials come from the environment in all examples, so they never end up in a scheduled task's
command line:

```powershell
$env:ULO_HOST     = '192.168.0.10'
$env:ULO_USER     = 'sync@example.com'
$env:ULO_PASSWORD = '…'
```

```bash
export ULO_HOST=192.168.0.10
export ULO_USER=sync@example.com
export ULO_PASSWORD=…
```

> **Use a dedicated camera account per job.** The camera allows only one session per account, so a
> scheduled download using your personal account will log your phone app out, and vice versa. Create
> separate accounts (for example `sync@`, `mode@`, `monitor@`) in the Setup tab or with
> `ulo users add`.

## 1. Download recordings to a local folder

```powershell
ulo download --out D:\ulo\media --type video
```

Files are organised into `yyyyMMdd` folders (`--flat` puts them all in one). Anything already
present is skipped, so the command is safe to run repeatedly, and the newest file is left behind for
a minute because the camera may still be writing it.

Limit to recent material and expire old copies at the destination:

```powershell
ulo download --out D:\ulo\media --type video --age 24 --retention 720
```

* `--age 24` — only download recordings from the last 24 hours
* `--retention 720` — delete files older than 30 days **at the destination**

## 2. Download to a network share (NFS/SMB)

Pass a UNC path. If the account running the job already has access, nothing else is needed:

```powershell
ulo download --out \\nas\ulo\video --type video --age 24 --retention 720
```

To authenticate explicitly — the equivalent of the old library's `nfs` destination type:

```powershell
ulo download --out \\nas\ulo\video --dest-user nas_user --dest-password '…' --age 24
```

The share is connected before the transfer and disconnected afterwards. Supplying share credentials
this way is Windows-only; on Linux mount the share first and give the mount point as `--out`.

## 3. Download to an FTP server

```powershell
ulo download --out ftp://nas.example.com/ulo/video --dest-user ftp_user --dest-password '…' `
             --type video --age 24 --retention 720
```

Files are fetched locally, uploaded, and the temporary copies removed. Missing directories are
created, existing files are skipped, and retention deletes old files on the server. Anonymous FTP
works — leave the credentials out.

## 4. Scheduled synchronisation

### Windows Task Scheduler

```powershell
$action  = New-ScheduledTaskAction -Execute 'C:\tools\ulo\ulo.exe' `
           -Argument 'download --out \\nas\ulo\video --type video --age 24 --retention 720 --quiet'
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date) `
           -RepetitionInterval (New-TimeSpan -Minutes 30)
Register-ScheduledTask -TaskName 'ULO sync' -Action $action -Trigger $trigger
```

Set `ULO_HOST` / `ULO_USER` / `ULO_PASSWORD` as machine or user environment variables so the task
does not carry the password in its arguments.

### Linux cron / systemd

```cron
*/30 * * * * ULO_HOST=192.168.0.10 ULO_USER=sync@example.com ULO_PASSWORD=… \
             /opt/ulo/ulo download --out /mnt/nas/ulo --type video --age 24 --retention 720 --quiet
```

A systemd timer with an `EnvironmentFile=` is tidier, keeping the password out of the crontab.

## 5. Keep the camera in the right mode

The camera resets to `standard` after an unattended reboot, which silently stops alert recording.
Re-applying the intended mode on a schedule fixes that:

```powershell
ulo mode alert --quiet
```

### Presence-based switching

Arm the camera when nobody is home, disarm when a phone appears — the original
`checkavailability` workflow:

```powershell
ulo availability --hosts 192.168.0.21,192.168.0.22 --rule any `
                 --if-up standard --if-down alert --quiet
```

* `--rule any` — treat the household as present if **any** listed device answers
  (`--rule all` requires all of them)
* `--if-up` / `--if-down` — the mode to apply in each case
* The mode is only written when it differs from the current one; `--force` writes it regardless,
  which is what you want if you are also compensating for reboots

Without `--if-up`/`--if-down` the command only reports, and its exit code is `0` when the devices
are available and `1` when they are not, so it composes with other scripting:

```powershell
ulo availability --hosts 192.168.0.21 --quiet
if ($LASTEXITCODE -ne 0) { ulo mode alert --quiet }
```

## 6. Snapshots and live video

A picture of what the camera sees right now:

```powershell
ulo snapshot --out D:\ulo\current
```

The picture is **not** added to the camera's recordings unless `--store` is given, so a periodic
snapshot job does not fill the internal memory.

Live video, either recorded or played:

```powershell
ulo live --out D:\ulo\live.mp4 --seconds 30
ulo live --play
```

> Do not run a snapshot job and live video at the same time — the camera has a single video
> pipeline and a snapshot request cuts the stream off.

## 7. Watch what the camera is doing

```powershell
ulo watch --interval 10
```

Combines the camera's push events, its system log and polled state into one stream, and marks
whether the camera is upright (usage mode) or upside down (setup mode). Useful for confirming that
motion detection is actually firing, or for catching the unattended reboots.

The log alone:

```powershell
ulo log show --lines 200
ulo log tail
ulo log save --out D:\ulo\logs
```

## 8. Storage housekeeping

```powershell
ulo storage                    # internal and SD card usage
ulo movetocard --wait          # move recordings to the SD card
ulo media days                 # which days exist
ulo media delete 20260813 --yes
ulo clean OldestWeek --yes     # purge by period, administrator only
```

`clean` periods: `OldestDay`, `OldestWeek`, `OldestYear`, `LatestDay`, `LatestWeek`, `LatestYear`,
`All`.

A sensible retention job is to download first and purge afterwards:

```powershell
ulo download --out \\nas\ulo\video --type video --retention 720 --quiet
ulo clean OldestWeek --yes --quiet
```

## 9. Settings backup and restore

```powershell
ulo backup create
ulo backup list
ulo backup restore <name>
```

Backups are stored on the camera itself. Take one before changing the Wi-Fi or doing anything in the
Setup tab.

## 10. Accessibility and remote access

The camera is intentionally reachable only on the local network in these recipes, and that is the
recommendation — see the [security assessment](SECURITY.md). If you need the recordings elsewhere,
synchronise them to a NAS (§2, §3) and reach the NAS instead of exposing the camera.

For a headless machine, everything above works over SSH; the CLI targets `net9.0` and runs on Linux,
where only live video playback needs an external player. See the
[build guide](BUILDING.md#4-linux) for producing a Linux build.

## 11. Related documents

* [Application guide](APPLICATION.md) — every command and switch in full
* [API reference](API.md) — the endpoints these recipes drive
* [Build guide](BUILDING.md) — producing the Windows and Linux builds used above
* [Security assessment](SECURITY.md) — why the camera stays on an isolated segment
