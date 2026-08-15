re over h# Device artefacts — firmware `10.1308`

Material captured from a running unit on this firmware version, as opposed to the head firmware
images in `../factory/`. Everything here is public data the device hands to any client that connects.

## `https-server-cert.pem` / `.der`

The TLS certificate the camera presents on ports **443** and **8443**, captured 2026-08-15 from a
live unit (identical on both ports). The same file is provided in PEM (text) and DER (binary) form;
they encode the identical certificate.

| Field | Value |
|---|---|
| Subject | `C=LU, S=Luxembourg, O=MuDesign, OU=IT, CN=*.ulo.camera, E=none@none.lu` |
| Issuer | `C=LU, S=Luxembourg, L=Esch, O=MuDesign, OU=IT, CN=Mu Design CA, E=none@none.lu` |
| Serial | `1002` |
| Valid from | 2018-07-10 14:14:52 UTC |
| Valid to | 2028-07-07 14:14:52 UTC |
| Signature | `sha256RSA` |
| Key | RSA 2048 bit |
| Basic constraints | End entity (`CA = false`) |
| Key usage | Digital Signature, Key Encipherment (critical) |
| Extended key usage | Server Authentication |
| Netscape comment | `OpenSSL Generated Server Certificate` |
| Subject key identifier | `438032daabfa27cef1bea4467a4a4b3a2a0822ec` |
| Authority key identifier | `e7bfca034541b49a7f0124109dae51b1d406d071` (issuer serial `0087f49006de591398`) |
| SHA-1 thumbprint | `F9D58AB359661D967BBBC7285B7D080EC193EE60` |
| SHA-256 of DER | `E0BFE12775FD862FC4B1684C1E5E451AB42AEBF5EC983CD60DEF4884E5F9AA58` |

The camera sends **only the leaf**: the chain it presents is one element long, so `Mu Design CA`
itself is not served and cannot be archived from the network. That also means a client cannot build
a path to the root even if it wanted to — the certificate is unverifiable without the vendor CA out
of band, which is why this application pins the leaf instead.

## What changed since `06.0601`

Both units were measured on the same network minutes apart, so this is a real firmware difference
rather than two different products:

| | `06.0601` | `10.1308` |
|---|---|---|
| Subject | `CN=localhost` | `CN=*.ulo.camera` (with O/OU/L/E set) |
| Issuer | itself — self-signed | `CN=Mu Design CA` — a vendor CA |
| Signature | `sha1RSA` | `sha256RSA` |
| Generated | 2017-01-20 | 2018-07-10 |
| Expires | 2027-01-18 | 2028-07-07 |
| Basic constraints | `CA = true` (a leaf claiming to be a CA) | `CA = false`, correct for a server |
| Key usage / EKU | absent | Digital Signature + Key Encipherment, Server Authentication |
| SHA-1 thumbprint | `BE483A63136A7680116D8C60A5522D0B97038886` | `F9D58AB359661D967BBBC7285B7D080EC193EE60` |

The newer firmware fixes the obvious defects of the old one: `sha1` is gone, the leaf no longer
claims `CA = true`, and the certificate carries a proper subject and key usage.

**What it does not fix is the important part.** `*.ulo.camera` is a wildcard for a vendor domain, and
the matching private key sits on the device, so it is still one key shared by every unit rather than
a per-device identity. The 06.0601 note in [`../../06.0601/device/README.md`](../../06.0601/device/README.md)
inferred a shared key from the certificate alone and could not confirm it against a second unit;
this capture confirms the *shape* of the arrangement — a fixed, image-baked certificate with a
generic name, now issued by the vendor's own CA. Two units on the same firmware would be needed to
confirm the key is byte-for-byte identical across devices.

Note also that the two firmware versions present **different public keys** (the SHA-256 of the
subject public key differs), so a client that pins must pin per firmware version, not once per
product.

### Using it

The CLI can talk to the camera over HTTPS and pin this exact certificate:

```
ulo status --https --pin-cert F9D58AB359661D967BBBC7285B7D080EC193EE60
```

`--https` alone accepts the certificate without checking it, which defeats interception by a passive
observer but not by an active one. Pinning is the useful mode, with the same caveat as on 06.0601:
if the private key ships in the image, anyone who has extracted it from any unit can present this
certificate too. Pinning raises the cost; it does not close the hole.

Verify a unit presents the archived certificate:

```
openssl s_client -connect <ULO_IP>:443 </dev/null 2>/dev/null \
  | openssl x509 -noout -fingerprint -sha1
```

```powershell
$c = [Security.Cryptography.X509Certificates.X509Certificate2]::new('https-server-cert.der')
$c | Format-List Subject, Issuer, NotBefore, NotAfter, Thumbprint
```

### What is not here

The **private key**, which stays on the device, and the **`Mu Design CA` certificate**, which the
camera does not send. Neither was needed for any of the analysis in
[`docs/SECURITY.md`](../../../docs/SECURITY.md).
