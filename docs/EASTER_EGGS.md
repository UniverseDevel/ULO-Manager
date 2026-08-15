# Easter eggs and oddities

Things found on the device that are not features, not vulnerabilities, and not worth a section in
the security assessment — but are too good to lose. Everything here was observed directly; anything
that has not been verified says so.

## The music video that ships with every camera

**Status: confirmed, present on both firmware versions.**

The web application's asset folder for sounds contains one sound and one thing that is not a sound:

```
Index of /assets/sounds/
  camera-click.wav                230.1k
  snapshot_20170829_092122.mp4      2.3M     <- 30 seconds of an Indian pop song
```

```
http://<ULO_IP>/assets/sounds/snapshot_20170829_092122.mp4
```

No authentication — it is a front-end asset, served like a stylesheet. It is present on **both** a
`06.0601` and a `10.1308` unit, and the two copies are byte-for-byte identical
(`SHA-256 16F1792DFB6C2B78358E8D08E8AE7BDFCC6674145249AC2CD1A7FD93083165D9`, 2 396 108 bytes), so it
ships in the image rather than being something a particular camera picked up.

| Property | Value |
|---|---|
| Duration | 30.21 s |
| Video | H.264 (Constrained Baseline), 636 × 360, 25 fps, 535 kb/s |
| Audio | AAC LC, 44.1 kHz stereo, 96 kb/s |
| Container | `mp42` |
| Created (inside the file) | 2017-08-27 05:24:04 UTC |
| Name says | `snapshot_20170829_092122` — the camera's own naming convention, `yyyyMMdd_HHmmss` |
| Served since | at least the 2019-06-23 web app build |

**What it actually is:** thirty seconds of a Bollywood song sequence. The clip closes on a caption
bar that credits it in full:

```
Film  : Tere Naal Love Ho Gaya
Song  : Main waari Jaavan (sad)
Music : Tips
```

*Tere Naal Love Ho Gaya* is a 2012 Hindi film; **Tips** is the Indian music label that published the
soundtrack, and its logo sits in the top-right corner throughout. The bottom-right corner carries a
*"Made With VivaVideo"* stamp — the watermark the free tier of that mobile video editor burns into
whatever it exports. Both marks are in the picture, not in the metadata; the file has no encoder
tags at all.

So somebody cut thirty seconds out of a music video on their phone, saved it under a file name
shaped like a ULO snapshot, and it rode into production inside the front-end assets — where it has
sat in the `sounds` folder of every unit for years, still served today, on both firmware versions.
That the vendor thereby distributes a piece of a commercial music video on every camera it sells is
left as an exercise for the reader.

The file is **not** copied into this repository: it is a third party's copyrighted material. The
description above is enough, and any unit will hand you the original at the URL above.

## Small curiosities, all verified

* **The camera answers `success` where a header belongs.** On `10.1308`, `POST /api/v1/snapshot`
  puts a bare `success` line inside the HTTP header block — no colon, not a header, not valid HTTP.
  Every standard client throws the whole response away because of it. See
  [`API.md` §2.2](API.md#22-camera-and-recording).
* **The vendor's TLS certificate has an email address of `none@none.lu`.** Issued by `Mu Design CA`
  of Esch, Luxembourg, and stamped by OpenSSL with the comment *"OpenSSL Generated Server
  Certificate"*. Details in [`firmware/10.1308/device/`](../firmware/10.1308/device/README.md).
* **The older firmware's certificate declares itself a certificate authority.** `CA = true` on a
  leaf certificate for `CN=localhost`, signed with SHA-1, generated in 2017. See
  [`firmware/06.0601/device/`](../firmware/06.0601/device/README.md).
* **The camera keeps a picture of whatever it last looked at, and hands it to anybody.**
  `http://<ULO_IP>/media/loginPicture.jpg`, no authentication — it is the backdrop of the login
  screen, which is to say the login screen is a photograph of your room.
* **The eyes have an animation engine with a demo mode.** Strings such as `playAnimRandomly`,
  `updateDemoModeState`, `Animation not found !` and `Animation already to be played !` sit in the
  firmware ([`SECURITY.md` §5](SECURITY.md)), complete with the space before the exclamation mark
  that gives away a French keyboard.
