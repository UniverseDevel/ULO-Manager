# Legal notes

This repository documents the ULO camera by Mu Design Sarl. It is not affiliated with, endorsed by
or licensed from Mu Design. Everything published here is either original work under the project's
[GPL-3.0 licence](../LICENSE), a public record, or a factual description of material that is **not**
redistributed.

The licence covers this project's own work and nothing else. [NOTICE](../NOTICE) draws that line
explicitly: which files the GPL-3.0 grant applies to, and which third parties hold the rights this
project cannot and does not grant. A licence file sitting at the root of a repository reads as an
offer over everything beneath it, so the boundary is stated rather than assumed.

## 1. What this repository does not include

Documenting a closed device means reading a lot of the vendor's material. Describing it is lawful
commentary; republishing it is not, however freely the device hands it over. So the findings are
here and the artefacts are not.

Where a document below relies on something not included, this is where it came from, so any finding
can be checked against your own unit:

| Not included | Where the original comes from |
|---|---|
| The vendor's web application source and its source maps | `GET http://<ULO_IP>/build/main.js.map` from a unit on your network, reconstructed with any source-map tool. Hashes to check it against are in `firmware/*/webapp/README.md`. |
| The STM32 head firmware images | Vendor update packages. Versions and CRC names are recorded in the firmware folders. |
| Qualcomm APQ8016 and DragonBoard 410c documentation | Qualcomm Developer Network and Arrow, free registration. Unlike the FCC exhibits, these are not public record. |
| The vendor user manual | The identical document is a public FCC exhibit: [`docs/fcc/User manual.pdf`](fcc/). |
| The official Android application | It was withdrawn from Google Play in February 2024. Its endpoints and BLE identifiers are documented in [`apk/README.md`](../apk/README.md). |
| The music video described in [Easter eggs](EASTER_EGGS.md) | `GET http://<ULO_IP>/assets/sounds/…` on any unit. It is a third party's copyrighted clip and its presence on the device is the point, not the file. |

Two things that are included, deliberately. The `.dts` and `.dtsi` device trees under
[`docs/fcc/hardware_docs/`](fcc/hardware_docs/) come from the Linux kernel, are GPL-2.0 and carry
their SPDX headers, so they are compatible with this repository. The vendor TLS certificates under
`firmware/*/device/` are public certificates rather than keys, contain no secret, and are the
evidence for the certificate findings in [SECURITY.md](SECURITY.md).

## 2. History

Removing a file from the current tree does not remove it from a repository. Every commit that ever
contained it still carries it, and a forge serves those commits, so anything that should not be
published has to come out of history rather than only out of the tip.

Where that has been necessary here it was done with `git filter-repo --invert-paths`. It is not
automated: it rewrites every commit hash and has to be force-pushed, so it is a deliberate one-off
rather than part of the project. It is also not the whole story — unreachable objects linger on a
forge until it collects them, forks keep their own copies, and an existing clone will reintroduce
the old history on a pull rather than a fresh clone.

## 3. FCC exhibits

The files in [`docs/fcc/`](fcc/) are US public records, published by the FCC under FCC ID
`2ANJS-ULO1` and freely redistributable. `Confidentiality Request.pdf` is itself part of the public
filing and lists the exhibits the applicant asked to have withheld — schematics, block diagrams,
operational description, tune-up procedure and internal photos of shielded areas. **None of the
withheld exhibits are in this repository**, and none should be added: a confidential FCC exhibit
that leaks does not become public, and the schematics listed as missing in
[`fcc/README.md`](fcc/README.md) must be obtained by measurement on your own hardware, not by
sourcing the withheld document.

## 4. Personal data

No third party's personal data is published here, and none is quoted or attributed.

The campaign's reception is discussed in [COMPANY.md](COMPANY.md) and
[KICKSTARTER.md](KICKSTARTER.md) in aggregate: what backers reported, how often, and what it says
about the device. Commenters are not named and their comments are not reproduced. People named
anywhere in this repository are named in a public record or in their professional capacity, which
is unavoidable when documenting a company's product.

Output from the [forensics](../forensics/README.md) scripts is not published either. It captures
device names, network names, account names and log excerpts from whichever unit it was pointed at,
which is exactly the material [SECURITY.md](SECURITY.md) warns the device leaks.

All user names, passwords, e-mail addresses and IP addresses in the documentation and the source
are invented examples. The credentials quoted in
[SOURCE_ANALYSIS.md §5](SOURCE_ANALYSIS.md#5-demo-mode-and-shipped-mock-data) are the vendor's
own shipped mock data — fictional test values, not anyone's account.

## 5. Statements about the company and its founder

Mu Design Sàrl and Vivien Muller are named throughout, which is unavoidable in documentation of
their product and legitimate. The rule this repository follows is that **only verifiable facts are
asserted**: what the device does, what the filings say, what was promised in the campaign and what
was delivered, each with a source.

Characterisations of intent — fraud, deception, theft — are not asserted, and third-party
accusations are not repeated as claims of their own. Where backers' anger is relevant it is
reported as what backers said, in aggregate and unattributed. Under Luxembourg and EU law repeating
an allegation is publishing it, and there is no intermediary safe harbour for a repository's own
text.

## 6. Security research

The findings in [SECURITY.md](SECURITY.md) and [ACCESS_RESEARCH.md](ACCESS_RESEARCH.md) come from
testing hardware the author owns, on the author's own network. That is lawful. The same techniques
applied to a device you do not own are not, and computer-misuse law in most jurisdictions turns on
authorisation rather than on technique or intent.

Two consequences for what is written here:

* The documentation describes the attack surface of a device on your own network. It is not a
  method for reaching someone else's camera, and nothing here should be written as though it were —
  no instructions aimed at other people's units, and no impersonation of vendor infrastructure that
  live devices may still contact.
* The vendor's cloud endpoints are dead, and the domains are recorded only to document what the
  device tries to reach. Registering an abandoned vendor domain to collect credentials from units
  still calling home would be unlawful interception, whoever does it.

## 7. Reporting a problem

If you own rights in anything published here, or believe something in this repository is inaccurate
or unlawful, open an issue on the repository and it will be removed or corrected. Nothing here is
worth a dispute: every excluded item above is documented well enough that the description alone
serves the purpose the file served.