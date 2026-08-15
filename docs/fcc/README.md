# FCC Filing Documents — ULO Camera (FCC ID: 2ANJS-ULO1)

Official FCC filing for the ULO camera by Mu Design SARL, submitted 2017-09-16.

## Downloaded documents

| File                          | Contents                                                                    |
|-------------------------------|-----------------------------------------------------------------------------|
| `Internal photos.pdf`         | PCB photos — 18 close-ups of all boards                                     |
| `External photos.pdf`         | Device exterior photos                                                      |
| `Test setup.pdf`              | Test setup photos for radiated/conducted emission                           |
| `ID Label and Location.pdf`   | Product label — **Made in India by VVDN**, WiFi default: `ulo` / `MuDesign` |
| `Test report.pdf`             | Full RF test report (234 pages)                                             |
| `User manual.pdf`             | User guide — confirms micro-USB type B, NiMH×3, microSD slot                |
| `Exposure.pdf`                | RF exposure declaration                                                     |
| `RF Exposure Info.pdf`        | RF exposure test results                                                    |
| `Authorization Letter.pdf`    | Authorization letter from Mu Design                                         |
| `Confidentiality Request.pdf` | Confidentiality request — specifies which documents are locked and why      |

## Key technical facts from the documents

| Property      | Value                                                                           | Source         |
|---------------|---------------------------------------------------------------------------------|----------------|
| Model number  | MUDL_ULOC                                                                       | Test report p5 |
| Brand         | ULO                                                                             | Test report p5 |
| Frequency     | 2.4–2.4835 GHz (802.11 b/g/n + BLE)                                             | Test report p5 |
| Antenna       | PCB antenna, −1.24 dBi gain                                                     | Test report p5 |
| Power supply  | Battery 1.2V DC × 3 (NiMH) + USB 5V DC                                          | Test report p5 |
| Manufacturer  | VVDN Technologies (India)                                                       | Diagram label  |
| Default WiFi  | SSID `ulo`, password `MuDesign`                                                 | Diagram label  |
| MAC prefix    | `20:F8:5E`                                                                      | Diagram label  |
| Setup mode    | Flip camera upside-down                                                         | User manual p2 |
| Applicant     | Mu Design SARL, 9 avenue des Hauts-Fourneaux, 4362 Esch-sur-Alzette, Luxembourg | Cover letter   |
| CEO           | Vivien Muller                                                                   | Cover letter   |
| Testing dates | August 14 – September 7, 2017                                                   | Test report p5 |

## Confidential documents — not yet obtained

These documents would directly answer the hardware access questions in
[ACCESS_RESEARCH.md](../ACCESS_RESEARCH.md) Attempt 3 (UART pad locations, USB data routing,
boot-config strap values).

| Document                | Size   | What it would reveal                                                  |
|-------------------------|--------|-----------------------------------------------------------------------|
| Schematics              | 970 KB | UART pin locations, USB data routing, boot-config straps, power rails |
| Block Diagram           | 131 KB | System architecture, SoC identification, interconnects                |
| Operational Description | 353 KB | System operation, boot process, firmware structure                    |
| Antenna Spec            | 262 KB | Antenna design details                                                |

### How to request confidential FCC documents

The filing has **Long-Term Confidentiality: Yes** at the application level. The locked documents
(schematics, block diagram, operational description, antenna spec) will not be released
automatically — they require action.

**Option 1: FOIA request** (most likely to succeed)

File at https://www.fcc.gov/foia or https://foia.gov with:

- **FCC ID:** 2ANJS-ULO1
- **Application ID:** OMF/EkAA76cHFIqe8+C+BQ==
- **Request:** "All exhibits including schematics, block diagram, operational description,
  and antenna specification for FCC ID 2ANJS-ULO1"
- **Argument:** The applicant (Mu Design SARL, Luxembourg) is dissolved and no longer in
  business. The product has been on the market since 2017. Trade secret protection serves no
  legitimate purpose for a defunct company with no successor. The documents were prepared by
  a third-party test lab (WTS Taiwan) using standard Qualcomm reference designs.
- **TCB:** Eurofins Product Service GmbH (JoergKusig@eurofins.de) — they processed the grant

**Option 2: Contact the test lab directly**

| Role                    | Name          | Email                  | Phone             |
|-------------------------|---------------|------------------------|-------------------|
| Responsible Party (CEO) | Vivien Muller | ceo@mu-design.lu       | +352 54 55 80 233 |
| Technical Contact (WTS) | Danny Sung    | danny@wts-lab.com      | +886-2-6606-8877  |
| TCB (Eurofins)          | Joerg Kusig   | JoergKusig@eurofins.de | —                 |

The test lab (WTS Taiwan) prepared all documents and may provide copies if the applicant
consents — but the applicant company no longer exists.

**Option 3: Download the Confidentiality Request letter**

The filing includes a `Confidentiality Request` letter (85 KB, 1 page) authored by Daniela
Eckert at Eurofins. This document specifies exactly which items are confidential and the
stated justification. It may reveal an expiry date or conditions for release. Download from:
https://fccid.io/2ANJS-ULO1/Letter/Confidentiality-Request-3564622

## Hardware analysis from Internal Photos

Examined all 18 pages. The device has **4 PCBs** connected by flat flex cables:

### PCB 1 — Main board (round, ~65mm diameter)

- **P/N:** PCB_501-1-00555_A1, Rev1.0, February 2017
- **Branding:** "ulo by Mu Design & VVDN Technologies"
- **Components (front):** 5× FFC connectors (camera, display, motor, battery, sensor),
  micro-USB port (right edge), micro-SD card slot, multiple test points (TP1, TP5 on right edge)
- **Components (back, shields removed — page 11):**
    - 3 large BGA ICs under two RF shields:
        - **Largest** (top-right) — likely **Qualcomm APQ8016** SoC
        - **Second** (center-right) — likely **eMMC flash** (internal storage, 1718 MB measured via API)
        - **Third** (bottom-right) — likely **LPDDR RAM** or WiFi/BT combo
    - 1 smaller QFP/QFN IC (upper-left) — possibly **PM8916** PMIC or STM32 MCU
    - Crystal oscillator visible
- **USB connector:** Appears to have **4 traces** (not just 2 for power) — **USB data lines likely connected**
- **Test points on board edge:** Could be UART TX/RX — need probing with oscilloscope

### PCB 2 — Battery/power board (triangular)

- **P/N:** PCB-501-1-00556_B1, Rev1.1, May 2017
- **Features:** 3× NiMH AA battery holders, DC barrel jack, USB charging circuit
- **ICs:** U1, U4 (power management / charging), plus small passives
- **Test points:** TP1-TP9
- **Batteries:** 3× Panasonic eneloop BK-3MCCE NiMH AA, 1.2V, 1900mAh

### PCB 3 — Motor/display controller (triangular)

- **P/N:** 501-1-00557_B1, Rev1.1, April 2017
- **Features:** FFC connector (to main board), 2 small ICs (U1, U3), test points TP1-TP6
- **Purpose:** Drives the eye display and head motor

### PCB 4 — Sensor board (small, rounded)

- **P/N:** PCB_501-1-00558_A1, Wk/Yr 0917
- **Features:** Single 8-pin SOIC IC (U1), JST connector (J1), IR window cutout
- **Purpose:** Likely proximity/ambient light sensor or accelerometer (orientation detection)

### Other components

- **Antenna:** FPC WiFi/BT antenna, marking "ED63M2A128-011", ~20mm, connected via U.FL
- **Display:** Small LCD/OLED panel (~30mm) for eye animations, flex-cable connected
- **Camera:** Dual-lens module (main camera + IR illuminator), flex-cable connected
- **Speaker:** Small speaker with wire leads in head assembly
- **Magnet:** Neodymium in base for wall mounting

### Key observations for device access

1. **USB data lines appear connected** — EDL mode via modified cable is worth trying
2. **Test points TP1/TP5 on main board edge** — prime candidates for UART TX/RX
3. **RF shields are removable** — soldered metal cans, can be pried off for chip identification
4. **No visible boot-select switch or jumper** — boot source may be fixed by resistor strapping
5. **All 4 PCBs have test points** — comprehensive debug access was designed in

## Filing details

| Field        | Value                                         |
|--------------|-----------------------------------------------|
| FCC ID       | 2ANJS-ULO1                                    |
| Applicant    | Mu Design SARL                                |
| Product      | ULO1 Camera                                   |
| Frequency    | 2402-2480 MHz (Bluetooth + WiFi 2.4 GHz)      |
| Certified by | Vivien Muller, CEO                            |
| Test firm    | Worldwide Testing Services (Taiwan) Co., Ltd. |
| Filed        | 2017-09-16                                    |

## Reference hardware documentation

Downloaded to `hardware_docs/`:

| File                                 | Contents                                                                                  |
|--------------------------------------|-------------------------------------------------------------------------------------------|
| `APQ8016E_Datasheet.pdf`             | Qualcomm Snapdragon 410E processor datasheet — pin descriptions, boot config, peripherals |
| `DragonBoard410c_HardwareManual.pdf` | Reference board manual — same SoC as ULO, UART/USB/SD pin mapping                         |
| `DragonBoard410c_Schematics.pdf`     | Full reference board schematics — UART, boot straps, USB, eMMC wiring                     |
| `apq8016-sbc.dts`                    | Linux device tree for APQ8016 — exact peripheral assignment                               |
| `msm8916.dtsi`                       | Base MSM8916/APQ8016 device tree — register addresses, UART nodes                         |

### UART mapping (from device tree)

| Linux device | Hardware block | Register    | Role in ULO                                  |
|--------------|----------------|-------------|----------------------------------------------|
| `ttyHSL0`    | `blsp_uart2`   | `0x78b0000` | **Debug console** (stdout-path) — root shell |
| `ttyHSL1`    | `blsp_uart1`   | `0x78af000` | **STM32 MCU link** (confirmed in system.txt) |

### Online references

| Resource                          | URL                                                                                        |
|-----------------------------------|--------------------------------------------------------------------------------------------|
| DragonBoard 410c Hardware Docs    | https://www.96boards.org/documentation/consumer/dragonboard/dragonboard410c/hardware-docs/ |
| MSM8916 Mainlining (postmarketOS) | https://wiki.postmarketos.org/wiki/MSM8916_Mainlining                                      |
| postmarketOS DragonBoard 410c     | https://wiki.postmarketos.org/wiki/Arrow_DragonBoard_410c_(arrow-db410c)                   |
| Linux Qualcomm Camera Subsystem   | https://www.kernel.org/doc/html/latest/media/qcom_camss.html                               |
| Qualcomm EDL mode guide           | https://www.thecustomdroid.com/qualcomm-edl-mode-guide/                                    |
| VVDN Camera Engineering           | https://www.vvdntech.com/vision                                                            |
