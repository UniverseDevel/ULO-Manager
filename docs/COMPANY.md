# ULO — Manufacturer, Company Status and Community

Research compiled 2026-08-14. Sources are linked inline; claims without a link are marked
*unconfirmed*.

---

## 1. The manufacturer

### 1.1 Mu Design Sàrl

| Field              | Detail                                                        |
|--------------------|---------------------------------------------------------------|
| Legal name         | Mu Design Sàrl                                                |
| Jurisdiction       | Luxembourg                                                    |
| RCS number         | B201812                                                       |
| Registered address | Technoport SA, 9 Avenue des Hauts-Fourneaux, Esch-sur-Alzette |
| Founded            | 2015                                                          |
| Industry           | Consumer electronics / connected objects (B2C)                |
| Founder / CEO      | Vivien Muller                                                 |
| Website            | [mu-design.lu](https://mu-design.lu)                          |
| Designer portfolio | [vivien-muller.fr](https://vivien-muller.fr)                  |

Mu Design is (or was) incubated at **Technoport**, Luxembourg's national technology incubator in
Belval. The company is classified as privately held with no known external investors beyond
crowdfunding.
([PitchBook](https://pitchbook.com/profiles/company/167036-05),
[Cybo](https://www.cybo.com/LU-biz/mu-design))

### 1.2 The designer — Vivien Muller

Vivien Muller is a French industrial designer who gained attention with **Electree**, a
bonsai-shaped solar charger that won several design awards. He went on to create ULO, Bearbot, and
LUA through Mu Design. His portfolio site ([vivien-muller.fr](https://vivien-muller.fr)) is still
online but carries design work rather than products. He later registered a second Luxembourg
company, MuGames Sàrl-S, which published a mobile game and was voluntarily wound up in June 2026
— see §3.4.

### 1.3 ODM / hardware partner

The ULO hardware was designed and manufactured in partnership with **VVDN Technologies** (India), a
large ODM specialising in cameras, networking and IoT. The device's system log prefixes all
platform-level messages with `VVDN:`, and VVDN's involvement extends to the WiFi, Android platform
image and Qualcomm APQ board support package. See the
[security assessment §2](SECURITY.md#2-the-platform-underneath) for evidence.

---

## 2. Products

| Product      | Type                           | Crowdfunding          | Status (2026)                                                                                                                                          |
|--------------|--------------------------------|-----------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Electree** | Solar-powered bonsai charger   | Limited edition       | **Discontinued**, no longer sold ([vivien-muller.fr](https://vivien-muller.fr))                                                                        |
| **ULO**      | Owl-shaped surveillance camera | Kickstarter, Oct 2015 | Shipped to *some* backers; many undelivered; no firmware updates since at least 2019                                                                   |
| **Bearbot**  | Expressive universal remote    | Indiegogo             | **Discontinued**, pledge manager closed ([Indiegogo](https://www.indiegogo.com/en/projects/vivienmuller-14618304/bearbot))                             |
| **LUA**      | Animated smart planter         | Indiegogo             | **Discontinued**, pledge manager closed ([Indiegogo](https://www.indiegogo.com/en/projects/vivienmuller-14618304/lua-the-smart-planter-with-feelings)) |

All four products are effectively **discontinued or out of stock** as of 2026. None are available
for retail purchase through any known channel.

---

## 3. Company status — Mu Design is bankrupt and closed

Mu Design Sàrl was **declared bankrupt by judgment of 16 June 2021**, on its own admission. The
bankruptcy was **closed on 13 March 2023**, and the registered office was struck from the record
the following day. The company no longer exists.

This is settled by the company's own filing history on the RCS, read at
[lbr.lu](https://www.lbr.lu) under RCS number **B201812**. Earlier revisions of this document
recorded the status as unverified, and inferred dormancy from product and server evidence. That
inference was right about the outcome and years late on the date.

### 3.1 The filing history

| Filing | Date | Entry |
|---|---|---|
| `L230044773` | 14 March 2023 | Deletion of the head office by official act |
| `L230043628` | 13 March 2023 | Court order — Bankruptcy: **closure of bankruptcy** |
| `L210113495` | 17 June 2021 | Court order — Bankruptcy: **declaratory decision of bankruptcy** (judgment of 16 June 2021) |
| `L210089479` | 20 May 2021 | Annual accounts, financial year 2020 |
| `L190213879` | 21 October 2019 | Annual accounts, correction to the 2018 filing |
| `L190112432` | 1 July 2019 | Annual accounts, financial year 2018 |
| `L180246752` | 18 December 2018 | Annual accounts, financial year 2017 |
| `L180246599` | 18 December 2018 | Annual accounts, 30 November 2015 to 31 December 2016 |
| `L170190807` | 19 September 2017 | Modification — associates |
| `L170190313` | 19 September 2017 | Modification — managers |
| `L160091975` | 31 May 2016 | Coordinated articles of association |
| `L160091950` | 30 May 2016 | Modification — registered office |
| `L160039575` | 4 March 2016 | Modification — associates |
| `L160034446` | 25 February 2016 | Modification — associates |
| `L150216852` | 1 December 2015 | Registration |

#### The bankruptcy judgment

The court order registered under `L210113495` gives the detail:

| | |
|---|---|
| Procedure | Faillite (bankruptcy), reference **F-2021/00574-L** |
| Opened | **16 June 2021**, filed with the RCS on 17 June 2021 |
| Court | Tribunal d'arrondissement de Luxembourg, Chambre 15 |
| Case / judgment | TAL-2021-05291 / 2021TALCH15/00957 |
| Type | Declaratory decision of bankruptcy |
| How it began | **Aveu** — the company declared its own bankruptcy |
| Scope | Main insolvency proceeding under the EU Insolvency Regulation |
| Registered status recorded | *en faillite* |
| Curator | Me Laurent Bizzotto, Luxembourg |

Two points matter for anyone reading this as a backer. The proceeding was opened on the company's
own admission rather than on a creditor's petition — *aveu* is the director filing the company's
books, which Luxembourg law requires once payments stop. And a curator was appointed, which is who
creditors' claims go to; but the bankruptcy **closed in March 2023**, so that mandate has ended and
there is no longer a claims process to join. See §6.2.

Two further details are worth reading off the filing list. The company filed accounts for financial year 2020 on
**20 May 2021**, four weeks before the bankruptcy judgment — so it was still meeting its filing
obligations to the end. And the last accounts on file cover 2020, so there is no public financial
picture of the final period.

**What the closure does and does not say.** A Luxembourg bankruptcy is closed either after the
estate is distributed or for insufficiency of assets, and the register entry does not say which.
This document does not guess. What the closure does establish is that proceedings are over and the
legal person is gone.

### 3.2 What this settles, and what is still open

Settled:

* The company is bankrupt and closed. There is no entity to contact, sue, or ask for firmware.
* There is a court record, so the earlier statement in §6 that no court judgments were found was
  wrong — it has been corrected.

Still open, and minor by comparison:

* **No IP sale or design auction was found.** Nothing on Metis Partners, in EUIPO transfer records
  or on any IP marketplace. Where the designs, trade marks and firmware rights went in the
  bankruptcy is not public. A curator would have realised whatever had value; nothing indicates
  what that was.
* **No “FTE” or design-sale page was found.** A recollection of a page where Mu Design offered
  designs for sale could not be corroborated.

**How to read the file yourself:**

1. Go to [lbr.lu](https://www.lbr.lu)
2. **Consult** → **FILE OF A COMPANY OR ASSOCIATION**
3. Enter **B201812**
4. The file shows the legal status and the full list of filings above

The portal sits behind a captcha and an authentication gateway, so this cannot be automated; the
endpoints that the EU e-Justice portal still documents for programmatic access now return 404.

Luxembourg has **no separate insolvency register**. A court declaring a bankruptcy notifies the
business register, which records it on the company's own file — which is exactly how the two
entries above appear, and why the company file is the bankruptcy check. Secondary routes exist and
are weaker: the monthly lists of bankruptcy judgments are published in **Mémorial B**, free to
consult on [Legilux](https://legilux.public.lu) but behind a client-side search, and the Luxembourg
Bar's former *Faillites* search at [barreau.lu](https://www.barreau.lu/faillites/) now simply
forwards to the business register.

### 3.3 What the outside evidence had shown

Before the register was read, the case for dormancy rested on this, and it holds up:

* **All four products are discontinued** with no retail availability.
* **No firmware or software updates** have been released for ULO in years.
* **The cloud update server (`34.232.121.46`)** is decommissioned — the AWS elastic IP no longer
  answers.
* **The ULO companion app** has not been updated.
* **Customer communication has effectively ceased** (see §4).
* **The Kickstarter campaign has had 41 updates** total, with the cadence slowing to near-zero.

> **Assessment.** Every one of these pointed the right way. The register later confirmed it: the
> company was already bankrupt when most of this evidence was gathered, and closed in 2023.

---

### 3.4 The founder's later company

Mu Design going quiet did not mean its founder stopped trading. A second Luxembourg company was
registered, ran, and has since been wound up:

| | |
|---|---|
| Name | **MuGames Sàrl-S** |
| RCS number | **B246260** |
| Legal form | Simplified limited liability company (*Sàrl-S*) |
| Registered office | Esch-sur-Alzette — the same town as Mu Design |
| Registered | **14 August 2020** |
| Dissolved and liquidated | **26 June 2026**, filed 30 June 2026 (deposit `L260168745`) |
| Corporate purpose | “the design, creation, realization, production and marketing of games, card games, playful supports and all other recreational processes” |
| Sole shareholder and manager | Vivien Muller |
| Product | *Ommatidia*, a four-player colour-mixing strategy game for mobile ([mugames.net](https://www.mugames.net/)), formerly on the iOS App Store |

#### How it ended

Not a bankruptcy, and not an administrative strike-off. The company was closed **voluntarily and
solvently**, by a written decision of its sole shareholder dated 26 June 2026 and filed with the
register four days later. In a single act he dissolved the company, appointed himself liquidator,
and closed the liquidation.

The declarations in that act are what make it a solvent wind-up rather than an insolvency:

* All liabilities were declared settled or provisioned.
* For any liability not yet known, the shareholder assumed an **irrevocable personal obligation**
  to pay it — which is the standard undertaking that lets a Luxembourg single-member liquidation
  close immediately.
* He took over the company's remaining assets and its liabilities personally, and approved the
  closing balance sheet.
* The appointment of a *commissaire à la liquidation* was formally waived, which is ordinary in a
  solvent single-member wind-up.
* Full discharge was given to the manager.
* The company's books are to be kept for at least five years.

This is the orderly opposite of what happened to Mu Design, which ended in bankruptcy on its own
admission (§3.1). Both facts come from the same register, and neither says anything about the
other.

#### The evidence from outside

The observable trail matches. The game's privacy policy is dated **31 January 2024**, which is the
most recent dated artefact found anywhere; no release, update or announcement after early 2024 was
found, and no Android build. **The app is no longer on the App Store**: Apple's lookup endpoint
returns nothing for its identifier in any storefront tried, a search for *Ommatidia* returns no such
title, and a search for the publisher returns no MuGames application (checked 16 August 2026,
against a control lookup that succeeded). The marketing site is still served, which costs a domain
renewal and nothing else. A product that stops in early 2024, a delisted app and a company wound up
in mid-2026 are the same story told three times.

Source: the Luxembourg Business Registers file for RCS **B246260**, read at
[lbr.lu](https://www.lbr.lu) — **Consult** → **FILE OF A COMPANY OR ASSOCIATION** — including the
liquidation act filed under `L260168745`. The company is also indexed by
[North Data](https://www.northdata.com/MuGames%20S%C3%A0rl-S,%20Esch-Sur-Alzette/B246260), but the
aggregators lagged the closure, which is why the register is the source cited here.

#### Why this is here, and what it is not

It answers the only question this section asks: whether anyone is still in a position to revive ULO.
The founder did keep trading after Mu Design went quiet, the work was games rather than connected
hardware, and that company has now been wound up as well. No corporate vehicle remains through which
ULO could be supported.

MuGames was a **separate legal entity**. Mu Design's obligations to its backers were Mu Design's,
they were dealt with in Mu Design's bankruptcy, and they never transferred to another company merely
because the two shared a director and a town. A person starting a later business is not evidence of
anything about an earlier one, and nothing here should be read as suggesting otherwise.

This entry records company registrations, which are public commercial records. The registered
address is deliberately omitted: the liquidation act shows it is also the sole shareholder's home,
and a residential address serves no reader of a camera's documentation. Personal accounts and social
media are likewise not collected — they are not relevant to a camera, and assembling them would
serve no purpose this repository has.

## 4. Kickstarter campaign and backer outrage

### 4.1 Campaign facts

| Metric        | Value                                                                |
|---------------|----------------------------------------------------------------------|
| Platform      | [Kickstarter](https://www.kickstarter.com/projects/vivienmuller/ulo) |
| Creator       | Vivien Muller                                                        |
| Launched      | 2015-10-05                                                           |
| Ended         | 2015-12-04                                                           |
| Duration      | 60 days                                                              |
| Goal          | ~€217,500                                                            |
| Pledged       | **~€1,618,869** (≈$1.77 M USD)                                       |
| Funding ratio | **813 %** of goal                                                    |
| Backers       | **8,330**                                                            |
| Comments      | **4,664** threads shown by Kickstarter, **6,880** including replies (2026-08-14) |
| Updates       | 41                                                                   |
| Category      | Design → Product Design                                              |
| Reward price  | ~€149 early bird for one ULO unit                                    |
| Delivery est. | Late 2016 (original)                                                 |
| Status        | "Successful" (funding completed; does not imply delivery)            |

### 4.2 What was promised vs. what was delivered

The full promise-by-promise comparison, drawn from the campaign updates themselves, lives in the
[Kickstarter campaign notes](KICKSTARTER.md#8-promises-vs-reality). What matters here is the pattern
it shows about the company rather than the feature list.

Four of the unlocked stretch goals — **facial recognition**, **waterproofing**, **voice control**
and **IFTTT** — never appeared in any shipped firmware. The **1080p upgrade** was funded and
announced, and the camera does carry a Sony sensor described as 1080p capable, but no shipped
firmware ever produced 1080p output: the live stream and the recordings measure
**1280×720** on every unit tested, which is what
[SECURITY.md 3.3](SECURITY.md#33-s3--live-video-needs-no-authentication) and
[API.md 3](API.md#3-websocket-protocols) record. The **open API** was promised and never
published — everything documented in this repository was reverse-engineered from the device.

The delivery failure is the larger one. A significant portion of the **8,330 backers never received
a unit at all**, despite paying in 2015, and the company continued announcing features while that
remained true.

### 4.3 Backer complaints and community response

The Kickstarter comments section (4,664 threads, 6,880 comments including replies) is dominated by
complaints from backers who:

* **Never received the product** despite paying years earlier.
* **Received no response** to emails, Kickstarter messages or social media inquiries.
* **Were denied refunds** or simply ignored when requesting them.
* **Reported the project** to Kickstarter, which responded that it is not an online store and cannot
  guarantee delivery or enforce refunds.

**Trustpilot** reviews for `www.mu-design.lu` are overwhelmingly negative, rated "Poor", with
complaints mirroring those on Kickstarter: unfulfilled orders and no customer service.
([Trustpilot](https://www.trustpilot.com/review/www.mu-design.lu))

The ULO campaign is frequently cited in broader discussions about **crowdfunding risk**, failed
hardware projects, and the lack of consumer protection on platforms like Kickstarter.

### 4.4 Kickstarter's position

Kickstarter's policy states:

* Backing a project is not a purchase; it is a pledge to support a creative project.
* Creators are legally obligated to fulfil their promises or offer refunds, but Kickstarter itself
  does not enforce this.
* Backers can report projects, but Kickstarter's options are limited to suspending future campaigns
  by the same creator.

([Kickstarter Accountability](https://help.kickstarter.com/hc/en-us/sections/115001107133-Accountability))

---

## 5. Other ULO projects on GitHub

A search across GitHub (August 2026) found **no other active projects** working on the ULO camera
besides this repository:

| Repository                                                                | Language | Description                                                                                            |
|---------------------------------------------------------------------------|----------|--------------------------------------------------------------------------------------------------------|
| [UniverseDevel/ULO-Manager](https://github.com/UniverseDevel/ULO-Manager) | C#       | This repository — tooling for the ULO camera, API documentation, security research, firmware analysis |

**No other ULO camera projects exist on GitHub.** The only other result for "ULO" is
[devileya/ulos-android](https://github.com/devileya/ulos-android), which detects "Ulos" textile
patterns by camera and is unrelated to the ULO owl camera.

No alternative firmware, community ROM, or third-party ULO tool was found on GitHub, GitLab, or in
general web searches. This repository appears to be the **only** open-source effort to document and
work with the ULO camera.

---

## 6. Legal actions and lawsuits

There is one court proceeding on record, and it is the one that ended the company: the bankruptcy
declared on **16 June 2021** and closed on **13 March 2023** (§3.1). Earlier revisions of this
document said no court judgments had been found. That was wrong — it reflected searches of press
and case-law databases rather than the company's own RCS file, which is where a Luxembourg
bankruptcy is recorded.

Beyond that proceeding, no separate lawsuit, class action or enforcement action against Mu Design
or its director was found:

| Jurisdiction / Database                          | Result                                                        |
|--------------------------------------------------|-----------------------------------------------------------------|
| Luxembourg RCS (company file B201812)            | **Bankruptcy declared 2021, closed 2023** — see §3.1            |
| Luxembourg courts (pseudonymised keyword search) | No other judgments found mentioning “Mu Design”               |
| US federal courts (CourtListener / PACER)        | No cases found                                                  |
| FTC enforcement actions                          | None targeting ULO or Mu Design                                 |
| General legal news / press                       | No reports of litigation                                        |

### 6.1 Why no group action materialised

Backers discussed legal action in the campaign comments and on Reddit, but nothing organised
formed. The reasons given at the time still explain it:

* **Kickstarter's terms** frame backing as a pledge rather than a purchase, which weakens a
  breach-of-contract claim.
* **Cross-border economics.** Backers are spread across 96 countries and individual pledges were
  around €100–200, so litigation in Luxembourg was never worth one person's while.
* **Partial delivery.** Some units shipped and 41 updates were posted, which makes the picture more
  complicated than a project that delivered nothing.
* **Kickstarter's limited role.** The platform cannot compel delivery or refunds.

Events overtook the discussion. By the time most backers concluded nothing was coming, the company
was already in bankruptcy, and any claim belonged in that proceeding rather than in a new one.

### 6.2 What recourse remains — realistically, none

This section previously listed small-claims and consumer-dispute routes. Those are no longer
applicable, and following them would waste your time: **you cannot sue a company that has ceased to
exist.**

| Route | Status |
|---|---|
| Claim in the bankruptcy | **Closed.** Claims went to the curator during the proceeding; it ended in March 2023 |
| EU Small Claims Procedure | **Not available** — there is no defendant |
| European Consumer Centres (ECC-Net) | **Not available** for the same reason |
| National consumer authority | Will record a complaint; cannot recover money from a closed estate |
| FTC / FBI IC3 (US backers) | Reporting only. Useful as statistics, not as recovery |
| Kickstarter | Cannot compel delivery or refunds, and never could |

A bankruptcy closes either after the estate is distributed or for insufficiency of assets. The
register does not say which applied here, and this document does not guess — but in either case
there is nothing left to claim against.

Personal liability of a director is a separate question in Luxembourg law, and it is one for the
curator to raise inside the proceeding, on evidence, while the proceeding is open. That window
closed in 2023. Nothing in the public record indicates it was raised, and nothing here should be
read as suggesting it should have been.

The honest summary for a backer who never received a unit: **the money is gone, the company is
gone, and there is no longer a process to join.** What remains is the device you may already own,
which is what the rest of this repository is for.

---

## 7. Sources

* [Kickstarter campaign page](https://www.kickstarter.com/projects/vivienmuller/ulo)
* [Trustpilot — Mu Design](https://www.trustpilot.com/review/www.mu-design.lu)
* [PitchBook — Mu Design](https://pitchbook.com/profiles/company/167036-05)
* [Paperjam — Mu Design article](https://paperjam.lu/article/news-mu-design-la-videosurveillance-aux-yeux-malins)
* [Vivien Muller portfolio](https://vivien-muller.fr)
* [Cybo — Mu Design listing](https://www.cybo.com/LU-biz/mu-design)
* [North Data — MuGames Sàrl-S, RCS B246260](https://www.northdata.com/MuGames%20S%C3%A0rl-S,%20Esch-Sur-Alzette/B246260)
* [MuGames — Ommatidia](https://www.mugames.net/)
* [Kickstarter accountability policy](https://help.kickstarter.com/hc/en-us/sections/115001107133-Accountability)
* [OpenCVE — Mongoose](https://app.opencve.io/cve/?product=mongoose&vendor=cesanta)
* [OpenCVE — Civetweb](https://app.opencve.io/cve/?vendor=civetweb_project)
* [Android Vulnerabilities — 4.2.2](http://androidvulnerabilities.org/by/version/4.2.2)
* [Android Vulnerabilities — Qualcomm](https://androidvulnerabilities.org/by/manufacturer/Qualcomm)
* [Nozomi Networks — Mongoose TLS vulns](https://www.nozominetworks.com/blog/hunting-the-mongoose-discovering-10-vulnerabilities-in-the-mongoose-web-server-library)
* [NIST NVD — CVE-2011-3389 (BEAST)](https://nvd.nist.gov/vuln/detail/CVE-2011-3389)
* [NIST NVD — CVE-2014-3566 (POODLE)](https://nvd.nist.gov/vuln/detail/CVE-2014-3566)
* [Luxembourg Business Registers](https://www.lbr.lu)
* [ULO-Manager repository](https://github.com/UniverseDevel/ULO-Manager)