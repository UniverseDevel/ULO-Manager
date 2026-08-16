# ULO Kickstarter Campaign - Technical Summary

> Compiled from 41 Kickstarter updates, ~6885 backer comments, campaign description, FAQ, and reward tiers.
> Focus: Technical details useful for understanding, maintaining, and hacking the ULO device.

---

## 1. Campaign Overview

| Field                     | Details                                                       |
|---------------------------|---------------------------------------------------------------|
| **Creator**               | Vivien Muller / Mu Design (Luxembourg)                        |
| **Campaign**              | October–December 2015                                         |
| **Funded**                | December 4, 2015                                              |
| **Backers**               | 8,330                                                         |
| **Amount Raised**         | €1,618,869 (received ~€1.4M after fees/refunds)               |
| **Avg per Ulo**           | ~€147 (shipping included)                                     |
| **Promised Delivery**     | November 2016                                                 |
| **Actual First Shipment** | January 5, 2018 (US backers)                                  |
| **Last Update**           | May 27, 2019 (#41)                                            |
| **Company Status**        | Mu Design filed for bankruptcy (announced on Indiegogo ~2020) |

### What Was Promised

- Cute owl-shaped surveillance camera with animated OLED/LCD eyes
- Wi-Fi connectivity, motion detection, live streaming
- iOS/Android/web app
- Voice & face recognition (stretch goal)
- IFTTT integration (stretch goal)
- HomeKit compatibility (stretch goal)
- Waterproofing (stretch goal — later removed)
- 1080p camera upgrade (stretch goal at €500K — reached)
- Voice control in EN/FR/DE/ES (stretch goal at €1M — reached)
- Open source software promised

### Reward Tiers (from campaign)

| Tier       | Price  | Contents                     |
|------------|--------|------------------------------|
| Early Bird | €129   | 1 Ulo + worldwide shipping   |
| Regular    | €149   | 1 Ulo + worldwide shipping   |
| Duo        | €269   | 2 Ulos + worldwide shipping  |
| Triple     | €399   | 3 Ulos + worldwide shipping  |
| 5-pack     | €599   | 5 Ulos + worldwide shipping  |
| 10-pack    | €1,099 | 10 Ulos + worldwide shipping |

**Note:** EU backers were hit with unexpected customs duties averaging €68 (up to €111+) since units shipped from India,
not within the EU as originally implied.

---

## 2. Update Timeline

| #     | Date         | Title                                 | Key Content                                                                                                                                                                                                                                                                                                                           |
|-------|--------------|---------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| 1     | Oct 12, 2015 | New Recipes                           | IFTTT recipe examples; emotion animations list (Happy, Grumpy, Surprised, Upset, Agitated, Puzzled, Blinks, Squint, eye color changes)                                                                                                                                                                                                |
| 2     | Oct 31, 2015 | Happy Uloween!                        | Halloween themed post                                                                                                                                                                                                                                                                                                                 |
| 3     | Nov 1, 2015  | Stretch Goals                         | Campaign funded; stretch goals announced (1080p camera, voice control, face recognition, waterproof, HomeKit)                                                                                                                                                                                                                         |
| 4     | Nov 5, 2015  | Staff Pick & Stretch Goals            | Kickstarter Staff Pick; first stretch goal reached                                                                                                                                                                                                                                                                                    |
| 5     | Nov 27, 2015 | One million                           | €1M reached; all stretch goals unlocked                                                                                                                                                                                                                                                                                               |
| 6     | Dec 4, 2015  | Thank You!                            | Campaign ended; pre-orders at €169; shipping info collection planned for Sep 2016                                                                                                                                                                                                                                                     |
| 7     | Jan 29, 2016 | Full Speed Ahead                      | Team diagram; budget breakdown; Gantt chart; beta units planned Nov 2016                                                                                                                                                                                                                                                              |
| 8     | Apr 8, 2016  | AMOLED, Cloud & Waterproofness        | **KEY TECHNICAL:** Original 1.22" TFT screens unsuitable; found 1.38" TFT alternative; discovered affordable 1.39" AMOLED screens; Cortex A8 processor with SPI interface; AMOLED requires MIPI interface and more powerful processor; waterproofing removed; cloud subscription proposed then retracted                              |
| 9     | Apr 10, 2016 | AMOLED, Cloud & Waterproofness Vol.2  | Clarification: no monthly subscription required; offline voice/face recognition (USB power only); face recognition limited to 3 users locally; microSD slot confirmed                                                                                                                                                                 |
| 10    | May 31, 2016 | Mechanical, Software & Hardware       | Fired hardware team PDCi (6 months delays); **Linux-based solution** confirmed; alternative non-Linux solution investigated but too expensive; switched to 4x AA/LR06 NiMH rechargeable batteries; induction charger accessory planned; micro-USB charging                                                                            |
| 11    | Jun 27, 2016 | New team members                      | New communication officer (Mélissa); new hardware expert (25+ years experience); product easily disassemblable; hardware upgradable; recyclable plastic; tube packaging                                                                                                                                                               |
| 12    | Jul 28, 2016 | Summer overview                       | **TECHNICAL:** Hardware solution integrates computer-equivalent components; facial recognition and eye tracking use two different processors; PIR motion detector behind IR-transparent black plastic shell; software to be open source; PCB production at Estelec (France); beta tests planned Jan 2017                              |
| 13    | Aug 8, 2016  | Refunds                               | Refund process details; refunds done manually via bank card                                                                                                                                                                                                                                                                           |
| 14    | Sep 6, 2016  | Ulo's look                            | TFT vs AMOLED comparison; custom TFT with reflective film selected (AMOLED supplier tripled price); custom TFT eliminates luminous intensity variance across viewing angles                                                                                                                                                           |
| 15    | Oct 5, 2016  | Inside view                           | **KEY TECHNICAL:** Two-board architecture: "head" board (brain, LCD screens, tap sensor, tracking) and Wi-Fi camera board (separate); head board: 300h sleep mode, 4h active use; camera can be turned off independently to save battery; camera behind two-way mirror beak                                                           |
| 16    | Nov 7, 2016  | Autumnal news                         | **1 GHz quad-core processor** integrated; only using 20% capacity; 80% available for future AI (Alexa/Cortana); eye animations rendered in real-time (not pre-calculated); Synfig Studio for custom animations                                                                                                                        |
| 17    | Dec 12, 2016 | Questions-based update                | **CAMERA SPECS:** 1080p resolution, 110° FOV, Sony image sensor, IR cut filter, H.264:720p 30fps live stream; HomeKit chip added (MFi certification pending); router config needed for remote access; free cloud access planned post-delivery                                                                                         |
| 18    | Jan 11, 2017 | Insight into software                 | **UI FEATURES:** Wi-Fi/email/notification settings, video quality, voice & face recognition, voice commands, alert mode, access from everywhere, eye customization (pupil/iris size, color, reflection), behaviors config, calendar view, live stream, snapshots, SD card backup, battery level, multi-Ulo support; responsive web UI |
| 19    | Feb 9, 2017  | Technical perspective                 | **BOARD DETAILS:** Capacitive board (tap), camera board (low-res + high-res behind beak), main board (Wi-Fi antenna, speaker, microphone, LCD screens, cameras), base board (3x NiMH rechargeable batteries); 3 power modes: batteries only / USB only / USB+batteries; USB recharges internal NiMH batteries                         |
| 20    | Mar 16, 2017 | UI demo + Recap on boards             | UI demo at http://www.ulo.camera; configuration mode entered by placing Ulo upside-down; switched production partner from Estelec (France) to **VVDN Technologies** (India); main board PCB photos shown                                                                                                                              |
| 21    | Apr 10, 2017 | Final testing & adjustments           | Final boards received in Luxembourg; custom TFT with reflective film + black casing; 2D animator hired; production target July 2017                                                                                                                                                                                                   |
| 22–31 | May–Dec 2017 | Production updates                    | Various production delays; tooling; injection molding; VVDN assembly line in Gurgaon, India; quality issues; software development continues                                                                                                                                                                                           |
| 32    | Dec 23, 2017 | Shipping Ulo                          | Production on track; first batch shipping in days; iOS/Android apps submitted; customs/VAT warning (DAP not DDP)                                                                                                                                                                                                                      |
| 33    | Jan 5, 2018  | First Batch                           | First batch shipped to US single-unit backers via DHL from VVDN Technologies, B-22 Infocity-1 Sector-34, Gurgaon 122001, India                                                                                                                                                                                                        |
| 34    | Jan 29, 2018 | Shipping, Cost, Setup & Support       | Apps on App Store and Play Store; cost breakdown: €147 avg per unit; support at https://support.ulo.camera; **firmware update recommended on first use**                                                                                                                                                                              |
| 35    | Feb 14, 2018 | Custom duties (EU)                    | EU customs averaging €68 extra; European deliveries paused                                                                                                                                                                                                                                                                            |
| 36    | Feb 26, 2018 | Software update, Assembly             | **Firmware + app update coming; WiFi SSID auto-detection** (no manual entry); end-of-line testing added; cloud access ~2 months away; support agent Ashish Shukla                                                                                                                                                                     |
| 37    | Mar 26, 2018 | Ulo deliveries                        | All KS units shipped; software team fixing bugs; 2 cloud access methods in development: auto UPnP port opening + cloud app                                                                                                                                                                                                            |
| 38    | Mar 28, 2018 | Missing deliveries                    | Form for missing deliveries                                                                                                                                                                                                                                                                                                           |
| 39    | Apr 16, 2018 | Zip/Postal Code errors                | Address correction for ~150 backers; DHL/UPS re-shipping                                                                                                                                                                                                                                                                              |
| 40    | Jun 27, 2018 | Access from everywhere, Cloud & Color | **KEY TECHNICAL:** Two remote access modes: (1) "Access from everywhere" via UPnP auto-config, (2) Cloud access via cloud server registration; **magenta/pink tone** caused by camera not filtering infrared light — color balance option promised; cloud access free (higher storage extra cost)                                     |
| 41    | May 27, 2019 | News from Ulo + team                  | Final update; cannot afford refunds; new product "Lua" on Indiegogo; Mu Design bankruptcy shortly after                                                                                                                                                                                                                               |

---

## 3. Technical Information

### Processor & Architecture

- **Main processor:** 1 GHz quad-core (ARM-based, likely Cortex-A series)
- **Head board processor:** Originally Cortex A8; upgraded during development
- **Two-board architecture:** Head board (brain) + Wi-Fi camera board (separate)
- **Screen interface:** SPI (for TFT); MIPI considered for AMOLED but abandoned
- **OS:** Linux-based
- **Facial recognition + eye tracking:** Handled by two different processors

### Camera

- **Resolution:** 1080p sensor, H.264 720p 30fps live stream
- **FOV:** 110°
- **Image sensor:** Sony (specific model unknown from KS data)
- **IR cut filter:** Between lens and image sensor
- **Night vision:** Via infrared — black plastic shell is IR-transparent
- **Location:** Behind two-way mirror beak
- **Dual cameras:** Low-resolution (motion tracking) + high-resolution (capture)
- **Known issue:** Pink/magenta tone due to inadequate IR filtering in certain lighting

### Display

- **Type:** Custom TFT LCD (round, ~1.39")
- **Enhancement:** Reflective film to normalize luminous intensity across viewing angles
- **Casing:** Black (to blend with shell)
- **Rendering:** Real-time animation engine (not pre-rendered frames)
- **Customization tool:** Synfig Studio (open source)

### Power

- **Battery:** 3x NiMH rechargeable batteries (in base board)
- **Originally planned:** 4x AA/LR06 rechargeable (changed to 3x NiMH)
- **Charging:** Via micro-USB cable (charges internal batteries)
- **3 modes:** Battery only / USB only / USB + battery (USB active, batteries charge during sleep)
- **Battery life:** ~300 hours sleep mode, ~4 hours active use (head board only)
- **Induction charger:** Planned accessory (never released)

### Connectivity

- **Wi-Fi:** 802.11 b/g/n (2.4 GHz assumed)
- **Setup:** Initially manual SSID entry required; later firmware added auto-detection
- **Remote access:** Two methods:
    1. **Direct Access** — via UPnP auto port forwarding on router
    2. **Cloud Access** — via cloud server registration (free basic, paid higher storage)
- **HomeKit:** Apple MFi chip added but certification never completed
- **Bluetooth:** Mentioned in early development but unclear in final product

### Other Hardware

- **Shell:** Black plastic, opaque to visible light but transparent to infrared (for PIR sensor + IR LEDs)
- **Sensors:** Capacitive touch (tap detection), PIR motion detector
- **Speaker:** Small, for sound effects (not strong enough for intercom)
- **Microphone:** Present (for voice recognition)
- **Storage:** Internal storage + microSD card slot for backup
- **Packaging contents:** Ulo unit, 3x NiMH batteries, 32GB microSD card, USB cable

### Manufacturing

- **Initial PCB production:** Estelec (France)
- **Final manufacturing:** VVDN Technologies Pvt Ltd, B-22 Infocity-1 Sector-34, Gurgaon 122001, India
- **Assembly:** Automated production line with end-of-line testing (each unit goes through full setup + feature test)
- **Shell:** Injection molded recyclable plastic

---

## 4. Firmware & Software History

### Known Firmware Versions

- **08.0904** — Referenced by Mu Design support as a required intermediate version
- Users who skipped from older versions directly to the latest encountered bricking/boot loop issues
- **Intermediate versions required** for safe updating (specific version numbers not publicly documented)

### Firmware Update Procedure

1. Connect to Ulo via the app (Direct Access mode)
2. Check for available updates in app
3. Install update — takes ~20 minutes
4. App may freeze during update — close and restart app after completion
5. **Critical:** Do NOT skip intermediate firmware versions — sequential updates required

### Known Firmware Issues

- Skipping intermediate firmware versions causes boot loop (spinning arrow on screen)
- Firmware update files were hosted on mu-design.lu — **site is now down**
- No known public archive of firmware files
- App removed from App Store (iOS); last app update was ~2019
- Cloud servers are offline ("server down" error)

### Software Stack

- **Device OS:** Linux-based
- **Web UI:** Hosted on device itself (not cloud); accessible via browser on same network
- **Mobile apps:** iOS (App Store) and Android (Play Store) — both discontinued
- **Cloud backend:** Mu Design operated cloud servers — now defunct
- **Animation engine:** Real-time rendering, Synfig Studio compatible

---

## 5. Known Issues & Bugs

### Critical Issues

| Issue                          | Description                                                                                                                                                              |
|--------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Pink/magenta video**         | Camera produces pink-tinted images/video due to insufficient IR filtering; especially noticeable outdoors. Color balance fix was promised but never confirmed as shipped |
| **Firmware bricking**          | Skipping intermediate firmware versions causes boot loop (spinning arrow). Version 08.0904 is a required intermediate step                                               |
| **Wi-Fi setup failures**       | Many users unable to connect; early firmware required manual SSID entry (typo-prone); app crashes during Wi-Fi configuration                                             |
| **App crashes**                | Android app crashes during first-time configuration; iOS app also unstable                                                                                               |
| **Cloud access offline**       | Cloud servers permanently down after Mu Design bankruptcy                                                                                                                |
| **Remote access broken**       | UPnP-based "access from everywhere" reportedly unreliable; cloud alternative now dead                                                                                    |
| **Constant logout**            | App doesn't maintain login session despite "remember me" checkbox                                                                                                        |
| **No night vision**            | Despite IR capability, practical night vision mode not well-implemented                                                                                                  |
| **Short video clips**          | Motion detection records only ~5-second clips                                                                                                                            |
| **False motion alerts**        | Captures many videos when nothing is moving                                                                                                                              |
| **Email notifications broken** | SMTP/email notification feature doesn't work for many users                                                                                                              |

### Hardware Issues

| Issue                            | Description                                                                                                                                        |
|----------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------|
| **Battery won't charge**         | Some units' batteries don't take charge; must be kept plugged in via USB                                                                           |
| **Internal buzzing/shorting**    | Reports of internal shorting, buzzing sounds; one user reported near-fire                                                                          |
| **Uneven eye displays**          | Eyes appear uneven when looking in certain directions                                                                                              |
| **Power connector not soldered** | At least one unit received with unsoldered power connector, bent board, and partially removed shields (appeared to be a prototype sent by mistake) |
| **Overheating**                  | Some units get hot during operation                                                                                                                |

---

## 6. Community Findings

### Setup Tips (from backers)

1. **Initial setup procedure:**
    - Insert batteries and/or plug in USB
    - Wait for Ulo to wake up (eyes appear)
    - Turn Ulo upside-down to enter configuration mode
    - Launch app → Direct Access → login with email/password
    - Device search takes 10-20 seconds
    - Walk through Wi-Fi setup steps

2. **Wi-Fi SSID must be typed exactly** (case-sensitive) in older firmware; newer firmware adds auto-detection

3. **Cloud access setup** (when servers were active):
    - Update app and Ulo firmware first
    - Choose Cloud Access at app launch
    - Register cloud account
    - Register Ulo to cloud account
    - Backend activation may take some time

4. **Remote access via router:**
    - Enable UPnP on router
    - Or manually configure port forwarding
    - Access Ulo's web interface from any browser

### Hardware Observations

- Unit comes with 3x NiMH batteries and 32GB microSD card
- Device is easily disassembled (as designed)
- Internal storage can hold ~30-40 photos and 3 videos (~23% of storage)
- Storage expandable via microSD card
- Shell plastic is IR-transparent (by design, for PIR sensor)
- Speaker is small/quiet — not suitable for two-way communication

### Firmware Solutions Mentioned

- A cloud access workaround was posted on the mu-design.lu forum by a backer, but the forum was deleted
- No known community firmware archives or reverse-engineering efforts documented on Kickstarter

---

## 7. Hardware Details

### Internal Architecture (from Update #19)

```
┌─────────────────────────┐
│   Capacitive Board      │ ← Touch/tap detection
├─────────────────────────┤
│   Camera Board          │ ← Behind beak (two-way mirror)
│   - Low-res camera      │   Motion tracking
│   - High-res camera     │   Photo/video capture (Sony sensor)
│   - IR cut filter       │
├─────────────────────────┤
│   Main Board            │ ← "Brain" / 1GHz quad-core
│   - Wi-Fi antenna       │
│   - Speaker             │
│   - Microphone          │
│   - 2x LCD screen conn  │
│   - Camera connections   │
├─────────────────────────┤
│   Base Board            │ ← Power management
│   - 3x NiMH batteries   │
│   - Micro-USB port      │
│   - Battery charging    │
└─────────────────────────┘
```

### Key Components

| Component        | Specification                                         |
|------------------|-------------------------------------------------------|
| Processor        | 1 GHz quad-core (ARM Cortex-A series)                 |
| Second processor | Dedicated to eye tracking/animations                  |
| Displays         | 2x custom round TFT LCD (~1.39") with reflective film |
| Camera sensor    | Sony (1080p capable)                                  |
| Camera lens      | 110° FOV with IR cut filter                           |
| Batteries        | 3x NiMH rechargeable                                  |
| Storage          | Internal + microSD slot (32GB card included)          |
| Connectivity     | Wi-Fi 802.11 b/g/n                                    |
| Audio            | Speaker (small) + microphone                          |
| Touch            | Capacitive sensor (top of head)                       |
| Motion           | PIR sensor (through IR-transparent shell)             |
| Charging         | Micro-USB                                             |
| OS               | Linux                                                 |

### Physical Design

- Owl-shaped body with black plastic shell
- Two round LCD "eyes" on front surface
- Beak is a two-way mirror (camera behind it)
- Top of head has capacitive touch sensor
- Base contains batteries and USB port
- Easily disassemblable (user-replaceable batteries)
- **Configuration mode:** Turn device upside-down

---

## 8. Promises vs Reality

| Feature                  | Promised                     | Delivered                                              |
|--------------------------|------------------------------|--------------------------------------------------------|
| **Delivery date**        | November 2016                | January 2018+ (14+ months late)                        |
| **Camera resolution**    | 1080p (stretch goal)         | 1080p sensor, but live stream limited to 720p          |
| **Waterproofing**        | Stretch goal at €500K        | ❌ Removed (Update #8, April 2016)                      |
| **AMOLED eyes**          | Strongly considered          | ❌ Custom TFT used instead (AMOLED too expensive)       |
| **Voice recognition**    | Stretch goal reached         | ⚠️ Partially — only works on USB power, limited        |
| **Face recognition**     | Stretch goal reached         | ⚠️ Partially — limited to 3 faces, reliability unclear |
| **HomeKit**              | Stretch goal, MFi chip added | ❌ Never certified/released                             |
| **IFTTT integration**    | Promised in Update #1        | ❌ Never implemented ("still achievable in the future") |
| **Open source**          | Promised in Update #12       | ❌ Never released                                       |
| **Cloud access**         | Free basic cloud             | ⚠️ Launched but servers now permanently offline        |
| **Remote access**        | "Access from everywhere"     | ⚠️ Required UPnP or cloud; cloud now dead              |
| **Color balance fix**    | Promised in Update #40       | ❌ Never confirmed as shipped; pink/magenta persists    |
| **Synology NAS support** | Mentioned by creator         | ❌ Never implemented                                    |
| **ONVIF compatibility**  | Requested by backers         | ❌ Never implemented                                    |
| **Manufacturing in EU**  | Implied (Luxembourg company) | ❌ Manufactured in India (VVDN)                         |
| **Customer support**     | https://support.ulo.camera   | ❌ Website now offline                                  |
| **Refunds**              | Promised to all requesters   | ❌ Most never refunded; company went bankrupt           |

---

## 9. Reported Behaviour (from the comment thread)

Recurring technical reports from the campaign's comment thread, summarised. Individual comments are
not quoted and commenters are not named: they are private individuals who wrote on a crowdfunding
page, not a public record, and the technical substance is what matters here. See
[LEGAL.md](LEGAL.md).

| Area | What was reported | Corroborated by |
|---|---|---|
| **Firmware update chain** | Units on old firmware cannot jump straight to the newest release; the intermediate versions have to be installed in order. The vendor confirmed this in the thread, naming `08.0904` as the release to pass through. With the download servers gone, the intermediate images are unobtainable, which strands any unit that was never kept current. | [SECURITY.md](SECURITY.md), and the three-layer version format in [SOURCE_ANALYSIS.md](SOURCE_ANALYSIS.md#1-firmware-version-structure) |
| **Failed update leaves a spinning arrow** | Units bricked at a rotating-arrow screen after updating. Consistent with a partial install of the three firmware layers. | - |
| **Pink / magenta tint** | Widely reported across many units. Caused by the camera not filtering infrared; the vendor promised a colour-balance fix in a later firmware, which never shipped. | Update #40, [SECURITY.md](SECURITY.md) |
| **Resolution below expectation** | The live stream is 720p rather than the 1080p promised in the campaign, which drew repeated complaints. | Update #17 |
| **Assembly defects** | At least one unit arrived non-functional with the power connector unsoldered, a warped board and shields partly removed - the reporter judged it a prototype rather than a production unit. | - |
| **LAN-only in practice** | Remote access frequently did not work, leaving the camera reachable only on the local network, and some units only at very short range. Remote viewing was the device's main selling point. | [API.md](API.md), [SECURITY.md](SECURITY.md) |
| **Cloud access dead** | Cloud login returns a server-down error. The endpoints no longer resolve. | [apk/README.md](../apk/README.md) |
| **Vendor forum deleted** | The support forum on `mu-design.lu` was removed, taking community workarounds - including a cloud-access workaround - with it. | [COMPANY.md](COMPANY.md) |
| **Working units** | Units that were set up successfully were reported as working as intended: sharp image, responsive eye behaviours, and storage sufficient for roughly 30-40 photos plus a few short videos. | [USE_CASES.md](USE_CASES.md) |

---
## 10. Useful Links & Resources

### Official (mostly defunct)

| Resource            | URL                                                   | Status                        |
|---------------------|-------------------------------------------------------|-------------------------------|
| Kickstarter page    | https://www.kickstarter.com/projects/vivienmuller/ulo | ✅ Active (read-only)          |
| Support site        | https://support.ulo.camera                            | ❌ Offline                     |
| Company site        | https://mu-design.lu                                  | ❌ Offline                     |
| UI Demo             | http://www.ulo.camera                                 | ❌ Likely offline              |
| UI Demo credentials | User: `ulo` / Password: `nFb8vx7q`                    | Historical                    |
| Android app         | Google Play Store                                     | ❌ Removed                     |
| iOS app             | Apple App Store                                       | ❌ Removed (last update ~2019) |
| Lua (successor)     | https://www.indiegogo.com/projects/lua                | ❌ Project also failed         |

### Kickstarter Update URLs

| Update            | URL                                                                 |
|-------------------|---------------------------------------------------------------------|
| #41 (Final)       | https://www.kickstarter.com/projects/vivienmuller/ulo/posts/2517132 |
| #40 (Cloud/Color) | https://www.kickstarter.com/projects/vivienmuller/ulo/posts/2223938 |
| #20 (UI Demo)     | https://www.kickstarter.com/projects/vivienmuller/ulo/posts/1831210 |

### Key Technical Facts for Reverse Engineering

- **OS:** Linux-based (unknown distribution)
- **Firmware format:** Unknown; updates delivered via app → device
- **Firmware version scheme:** `08.XXXX` format (e.g., 08.0904)
- **Web interface:** Device hosts its own web server (accessible via browser on same network)
- **Configuration trigger:** Physically turn device upside-down
- **Manufacturer:** VVDN Technologies (India) — may have additional documentation
- **Processor:** 1 GHz quad-core ARM
- **Camera:** Sony sensor with H.264 encoding
- **The mu-design.lu forum** reportedly contained community-posted solutions (now deleted)

### What's Still Unknown (needs investigation)

- Exact processor model (Cortex-A7 quad? Allwinner? Rockchip? Other?)
- Exact camera sensor model (Sony IMX series?)
- UART/serial console availability and pinout
- SSH/telnet access possibilities
- Bootloader type (U-Boot?)
- Partition layout and filesystem
- Wi-Fi chipset
- Whether SD card boot is possible
- Full firmware version list and download locations
- API endpoints exposed by the web interface
- FCC ID (check FCC database for internal photos and detailed specs)
