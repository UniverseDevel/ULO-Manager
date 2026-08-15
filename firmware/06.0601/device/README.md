# Device artefacts — firmware `06.0601`

Material captured from a running unit on this firmware version, as opposed to the head firmware
images in `../factory/`. Everything here is public data the device hands to any client that connects.

## `https-server-cert.pem` / `.der`

The TLS certificate the camera presents on ports **443** and **8443**, captured 2026-08-14. The same
file is provided in PEM (text) and DER (binary) form; they encode the identical certificate.

| Field | Value |
|---|---|
| Subject | `CN=localhost` |
| Issuer | `CN=localhost` (self-signed) |
| Serial | `008F2CF5AC1CA88933` |
| Valid from | 2017-01-20 09:45:09 UTC |
| Valid to | 2027-01-18 09:45:09 UTC |
| Signature | `sha1RSA` |
| Key | RSA 2048 bit |
| Basic constraints | `CA = true` |
| Subject alternative name | *absent* |
| SHA-1 thumbprint | `BE483A63136A7680116D8C60A5522D0B97038886` |
| SHA-256 of DER | `7256F5C3382E7CAED2FD8EC04108C6F2DE98C8CB6E9733FEF429C4AC6358661B` |

### Why it is kept here

1. **It dates the platform image.** The certificate was generated on **20 January 2017** and given a
   ten-year life. Nothing on the device regenerates it, so it is baked into the platform image and
   fixes that image no later than that date.
2. **It is almost certainly not unique to this unit.** A per-device certificate would be generated at
   first boot and carry a recent date and a device-specific subject. This one carries a fixed 2017
   date and the generic subject `CN=localhost`, which is what a certificate shipped inside the image
   looks like. The consequence is that **the matching private key ships on every unit** and can be
   read off any one of them, so the certificate authenticates nothing. This is stated as a strong
   inference from the certificate itself — it was not confirmed against a second unit.
3. **It makes pinning possible.** Because the certificate never changes, a client can pin it and get
   a genuine improvement over plain HTTP against a *passive* observer. See below.

### Using it

The CLI can talk to the camera over HTTPS and pin this exact certificate:

```
ulo status --https --pin-cert BE483A63136A7680116D8C60A5522D0B97038886
```

`--https` alone accepts the certificate without checking it, which defeats interception by a passive
observer but not by an active one. Pinning is the useful mode — but note the caveat in point 2: if
the private key is shipped in the image, anyone who has extracted it from any unit can present this
same certificate, and pinning it will not detect them. Pinning raises the cost; it does not close the
hole.

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

The **private key**, which stays on the device. It was not extracted, and none of the analysis in
[`docs/SECURITY.md`](../../../docs/SECURITY.md) required it.
