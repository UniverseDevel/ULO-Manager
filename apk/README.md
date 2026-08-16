# ULO Android App (com.ulo.camera)

The official ULO Android app by Vivien Muller. Removed from Google Play on 2024-02-28.

## Known versions

| Version | Date         | Min Android         |
|---------|--------------|---------------------|
| 1.1     | Jan 3, 2018  | 4.4+                |
| 1.2     | Apr 17, 2018 | 4.4+                |
| 1.3     | Sep 22, 2018 | 4.4+                |
| 1.4     | Sep 26, 2018 | 4.4+                |
| 1.5     | Nov 12, 2018 | 4.4+                |
| 1.6     | Dec 15, 2018 | 4.4+                |
| 1.8     | Mar 17, 2019 | 4.4+ (last version) |

## APK Analysis Findings (v1.8)

### Cloud API endpoints (dead — `app.ulo.camera` no longer resolves)

```
https://app.ulo.camera/api/login
https://app.ulo.camera/api/register
https://app.ulo.camera/api/logout
https://app.ulo.camera/api/forgotpassword
https://app.ulo.camera/api/editProfile
https://app.ulo.camera/api/reset-password
https://app.ulo.camera/device/list
https://app.ulo.camera/device/alertmode
https://app.ulo.camera/device/create-snapshot
https://app.ulo.camera/device/getPercentage
https://app.ulo.camera/device/geteyedata
https://app.ulo.camera/device/request-downloadvideo
https://app.ulo.camera/device/rnbsUpdate
https://app.ulo.camera/device/sdautoUpdate
```

### Firmware/S3 server (dead — `34.232.121.46` AWS EC2, timed out)

```
http://34.232.121.46/ulo/index.php/s3/listAllObjects
```

PHP frontend with S3 storage backend for firmware distribution.
Secondary IP `184.72.239.149` also dead.

### BLE GATT UUIDs (WiFi provisioning)

| UUID                                   | Notes                                             |
|----------------------------------------|---------------------------------------------------|
| `a3ceb858-9de1-11e7-abc4-cec278b6b50a` | **Main service** (confirmed in device system.txt) |
| `ae6cbefe-9de1-11e7-abc4-cec278b6b50a` | Characteristic (same vendor block)                |
| `7e321084-9e01-11e7-abc4-cec278b6b50a` | Characteristic                                    |
| `85dd749a-9e01-11e7-abc4-cec278b6b50a` | Characteristic                                    |
| `90b2d87e-9e01-11e7-abc4-cec278b6b50a` | Characteristic                                    |
| `50262240-d9af-11e7-9296-cec278b6b50a` | Characteristic                                    |
| `69a13270-0cb1-11e8-ba89-0ed5f89f718b` | 2018 addition                                     |
| `7fda2cf4-4140-11e8-842f-0ed5f89f718b` | 2018 addition                                     |
| `838c482a-3eff-11e8-b467-0ed5f89f718b` | 2018 addition                                     |
| `838c4f14-3eff-11e8-b467-0ed5f89f718b` | 2018 addition                                     |
| `838c518a-3eff-11e8-b467-0ed5f89f718b` | 2018 addition                                     |
| `d006b82c-493e-11e8-842f-0ed5f89f718b` | 2018 addition                                     |
| `ed3aab2e-9614-11e8-9eb6-529269fb1459` | Latest (2018)                                     |
| `01f2416e-75d9-11e8-adc0-fa7ae01bbebc` | 2018 addition                                     |
| `22005854-7856-11e8-adc0-fa7ae01bbebc` | 2018 addition                                     |
| `00002902-0000-1000-8000-00805f9b34fb` | Standard CCCD                                     |

Suffix `cec278b6b50a` = original 2017 provisioning set.
Suffix `0ed5f89f718b` / `fa7ae01bbebc` = 2018 cloud access additions.

### Technology stack

| Component | Library                        |
|-----------|--------------------------------|
| Video     | ExoPlayer (fMP4 WebSocket)     |
| Push      | Firebase Cloud Messaging (FCM) |
| BLE       | Android GATT client            |
| HTTP      | OkHttp / Retrofit              |
| Broadcast | `com.ulo.camera.REMOTE` action |

### What the endpoints tell us

1. **BLE provisioning** — These UUIDs can be enumerated with nRF Connect on a unit you own. The
   WiFi characteristics are how the app provisions the camera, so provisioning without the
   withdrawn app is possible on your own hardware.
2. **FOTA transport** — `http://34.232.121.46/ulo/index.php/s3/listAllObjects` shows the firmware
   server was plain HTTP with no transport authentication: a unit had no way to distinguish a
   genuine image from a substituted one. The host is dead, so this documents how the update path
   was built rather than a live weakness. See [SECURITY.md](../docs/SECURITY.md).
3. **Abandoned cloud domain** — `app.ulo.camera` no longer resolves, while units in the field may
   still try to reach it. That is a risk inherent to abandoned infrastructure and is recorded here
   as such. Standing up a server on that name to receive those credentials would be unlawful
   interception, and is not something this project does or endorses — see
   [LEGAL.md](../docs/LEGAL.md).
## App details

| Property           | Value             |
|--------------------|-------------------|
| Package            | `com.ulo.camera`  |
| Developer          | Vivien Muller     |
| Size               | ~59 MB            |
| Play Store removed | February 28, 2024 |
