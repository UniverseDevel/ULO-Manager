# Legal notes

This repository documents the ULO camera by Mu Design Sàrl. It is not affiliated with, endorsed by
or licensed from Mu Design. Everything published here is either original work under the project's
[GPL-3.0 licence](../LICENSE), a public record, or a factual description of material that is **not**
redistributed.

## 1. What this repository deliberately does not contain

Research on a closed device produces copies of the vendor's material. Describing that material is
lawful commentary; republishing it is not, regardless of how easy the device makes it to obtain.
The following is therefore excluded from version control and listed in
[`.gitignore`](../.gitignore). Each item stays reproducible — the documentation records what it is,
where it comes from and how to get it yourself.

| Excluded | Why | How to obtain it yourself |
|---|---|---|
| `apk/*.apk` | The official ULO Android app is Mu Design's proprietary software. Removal from Google Play did not place it in the public domain, and mirror sites are not a licence. | Extract from a device that has it installed, or an APK mirror. |
| `firmware/*/webapp/src/` | The vendor's TypeScript source, reconstructed from the source map. Shipping a source map by mistake is not a licence grant. | `GET http://<ULO_IP>/build/main.js.map`, then reconstruct with any source-map tool. |
| `firmware/*/webapp/main.js.map`, `main.css.map` | The same material in original form. | Same URL; hashes are recorded in `firmware/*/webapp/README.md`. |
| `firmware/*/factory/*.bin` | Proprietary STM32 head firmware. | Vendor update packages; versions and CRC names are recorded in the firmware folders. |
| `docs/fcc/hardware_docs/*.pdf` | Qualcomm APQ8016 and DragonBoard 410c documentation, redistributed under restricted terms — unlike the FCC exhibits, these are not public record. | Qualcomm Developer Network / Arrow, free registration. |
| `docs/ULO-Users-Manual-*.pdf` | Vendor copyright. | The identical document is a public FCC exhibit: `docs/fcc/User manual.pdf`. |
| `assets/images/ulo-*.png` | Vendor product photography, with no attribution or licence. | Vendor and press material. |
| `docs/ks/` | Scraped Kickstarter comments containing backers' names and contact details — personal data under the GDPR, and outside Kickstarter's terms of use. | Kickstarter's public campaign page. |
| `firmware/*/assets/sounds/*.mp4` | The clip described in [Easter eggs](EASTER_EGGS.md) is a third party's copyrighted music video. | `GET http://<ULO_IP>/assets/sounds/…` on any unit. |

The `.dts` / `.dtsi` device tree files under `docs/fcc/hardware_docs/` are kept: they come from the
Linux kernel and are GPL-2.0, compatible with this repository.

The vendor TLS certificates under `firmware/*/device/` are kept. They are public certificates, not
keys, they contain no secret, and they are the evidence for the findings in
[SECURITY.md](SECURITY.md).

## 1a. History

Untracking a file stops it being published from now on; it does not remove it from the repository.
Every commit that ever contained the file still carries it, and the forge serves those commits, so
the material stays downloadable until history itself is rewritten.

That rewrite has been done with `git filter-repo --invert-paths`, dropping the paths above from
every commit. It is not something this repository automates: it rewrites every commit hash, has to
be force-pushed, and is a one-off act of maintenance rather than a part of the project.

Rewriting is also not the end of it. Unreachable objects stay reachable by SHA on GitHub until their
support team garbage-collects them, forks keep their own copy, and existing clones reintroduce the
old history on the next pull rather than a fresh clone.
## 2. FCC exhibits

The files in [`docs/fcc/`](fcc/) are US public records, published by the FCC under FCC ID
`2ANJS-ULO1` and freely redistributable. `Confidentiality Request.pdf` is itself part of the public
filing and lists the exhibits the applicant asked to have withheld — schematics, block diagrams,
operational description, tune-up procedure and internal photos of shielded areas. **None of the
withheld exhibits are in this repository**, and none should be added: a confidential FCC exhibit
that leaks does not become public, and the schematics listed as missing in
[`fcc/README.md`](fcc/README.md) must be obtained by measurement on your own hardware, not by
sourcing the withheld document.

## 3. Personal data

No third party's personal data belongs in this repository.

* Backer names, e-mail addresses and comments are excluded (`docs/ks/`). Where the campaign's
  reception is discussed in [COMPANY.md](COMPANY.md), it is summarised in aggregate rather than
  quoted with attribution.
* Forensic output is excluded (`ulo_probe_results.json`): it captures device names, SSIDs, account
  names and log excerpts from a live unit.
* All user names, passwords, e-mail addresses and IP addresses in the documentation and source are
  invented examples. The credentials quoted in
  [SOURCE_ANALYSIS.md §5](SOURCE_ANALYSIS.md#5-demo-mode-and-shipped-mock-data) are the vendor's own
  shipped mock data — fictional test values, not anyone's account.

## 4. Statements about the company and its founder

Mu Design Sàrl and Vivien Muller are named throughout, which is unavoidable in documentation of
their product and legitimate. The rule this repository follows is that **only verifiable facts are
asserted**: what the device does, what the filings say, what was promised in the campaign and what
was delivered, each with a source.

Characterisations of intent — fraud, deception, theft — are not asserted, and third-party
accusations are not repeated as claims of their own. Where backers' anger is relevant it is
reported as what backers said, in aggregate and unattributed. Under Luxembourg and EU law repeating
an allegation is publishing it, and there is no intermediary safe harbour for a repository's own
text.

## 5. Security research

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

## 6. Reporting a problem

If you own rights in anything published here, or believe something in this repository is inaccurate
or unlawful, open an issue on the repository and it will be removed or corrected. Nothing here is
worth a dispute: every excluded item above is documented well enough that the description alone
serves the purpose the file served.