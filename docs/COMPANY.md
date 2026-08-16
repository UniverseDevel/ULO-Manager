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
company, MuGames Sàrl-S, which published a mobile game and was struck off the register in June
2026 — see §3.4.

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

## 3. Company status — is Mu Design still operating?

### 3.1 What is confirmed

* The company's RCS registration (**B201812**) has not been publicly marked as dissolved or
  liquidated in any source found. PitchBook and aggregator sites still list it as a "privately held"
  company.
* The domain `mu-design.lu` is still registered. At least one page appeared to show a "for sale"
  indicator on the domain, which can signal a change in business status but is not conclusive.
  (*Noted in search results; not directly confirmed.*)
* PitchBook records a **cancelled crowdfunding round** (2016) but no other funding events.
* A Paperjam article (Luxembourg business press) mentioned Mu Design representing Luxembourg at CES
  and preparing a second-generation ULO. The article's date and whether this ever materialised could
  not be confirmed from the outside.
  ([Paperjam](https://paperjam.lu/article/news-mu-design-la-videosurveillance-aux-yeux-malins))

### 3.2 What is not confirmed

* **Company status could not be verified programmatically.** The Luxembourg Business Registers
  portal ([lbr.lu](https://www.lbr.lu)) uses Cloudflare Turnstile captcha on all company searches,
  blocking automated access. The company's status (active, dissolved, in liquidation, bankrupt, or
  struck off) **must be checked manually** by visiting the portal and searching for RCS number
  **B201812**. The REGINSOL (Register of Insolvency) section on the same portal publishes monthly
  CSV/PDF lists of all bankruptcy declarations, judicial liquidations and administrative
  dissolutions.
* **No IP sale or design auction was found.** No listing on Metis Partners, EUIPO transfer records
  or any IP marketplace was discovered for Mu Design assets. If such a sale occurred, it was not
  publicly advertised through the usual channels.
* **No "FTE" or design-sale page was found.** The user's recollection of a page where Mu Design
  attempted to sell designs could not be corroborated. It is possible this was a temporary listing,
  a broker page, or a misremembered context.

**How to verify manually:**

1. Go to [lbr.lu](https://www.lbr.lu)
2. Click **Consult** → **FILE OF A COMPANY OR ASSOCIATION**
3. Enter **B201812** in the RCS number field
4. The result will show the company's current legal status (active / dissolved / in liquidation /
   bankrupt / struck off)
5. The same page is the bankruptcy check. Luxembourg has **no separate insolvency register**:
   a court declaring a bankruptcy notifies the business register, which records it on the
   company's own file, so the RCS entry shows *en faillite* if one exists. This is how
   [MuGames was established as struck off rather than bankrupt](#34-the-founders-later-company)
6. Two secondary routes, both weaker. The monthly lists of bankruptcy judgments are published in
   **Mémorial B** and are free to consult on [Legilux](https://legilux.public.lu); its search is
   a client-side application, so it has to be driven by hand. The Luxembourg Bar formerly ran a
   *Faillites* search; [barreau.lu/faillites](https://www.barreau.lu/faillites/) now simply
   forwards to the business register

### 3.3 Practical indicators of inactivity

Despite the lack of a formal dissolution filing, the company shows strong signs of being effectively
dormant:

* **All four products are discontinued** with no retail availability.
* **No firmware or software updates** have been released for ULO in years.
* **The cloud update server (`34.232.121.46`)** is decommissioned — the AWS elastic IP no longer
  answers.
* **The ULO companion app** has not been updated.
* **Customer communication has effectively ceased** (see §4).
* **The Kickstarter campaign has had 41 updates** total, with the cadence slowing to near-zero.

> **Assessment:** Mu Design appears to be dormant or defunct in practice, even if not formally
> dissolved. The Luxembourg RCS should be checked directly for a definitive answer.

---

### 3.4 The founder's later company

Mu Design going quiet did not mean its founder stopped trading. A second Luxembourg company was
registered, ran, and has since been struck off:

| | |
|---|---|
| Name | **MuGames Sàrl-S** |
| RCS number | **B246260** |
| Legal form | Simplified limited liability company (*Sàrl-S*) |
| Registered office | Esch-sur-Alzette — the same town as Mu Design |
| Registered | **14 August 2020** |
| Struck off | **30 June 2026** |
| Corporate purpose | “the design, creation, realization, production and marketing of games, card games, playful supports and all other recreational processes” |
| Managing director | Vivien Muller |
| Product | *Ommatidia*, a four-player colour-mixing strategy game for mobile ([mugames.net](https://www.mugames.net/)), formerly on the iOS App Store |

Source: the Luxembourg Business Registers entry for RCS **B246260**, read manually at
[lbr.lu](https://www.lbr.lu) — **Consult** → **FILE OF A COMPANY OR ASSOCIATION** — which is the
only way to get a legal status, since the portal is behind a captcha. The company is also indexed by
[North Data](https://www.northdata.com/MuGames%20S%C3%A0rl-S,%20Esch-Sur-Alzette/B246260), though
aggregators lagged the strike-off, which is why the register itself is the source cited here.

The observable trail matches the register. The game's privacy policy is dated **31 January 2024**,
which is the most recent dated artefact found anywhere; no release, update or announcement after
early 2024 was found, and no Android build. **The app is no longer on the App Store**: Apple's
lookup endpoint returns nothing for its identifier in any storefront tried, a search for
*Ommatidia* returns no such title, and a search for the publisher returns no MuGames application
(checked 16 August 2026, against a control lookup that succeeded). The marketing site is still
served, which costs a domain renewal and nothing else. A product that stops in early 2024, a
delisted app and a company struck off in mid-2026 are the same story told three times.

**What “struck off” does and does not mean.** It is a register status: the company has been removed
from the RCS and no longer exists as a legal person. It is **not** a finding of insolvency or
wrongdoing. Luxembourg can dissolve dormant or non-compliant companies administratively, without
liquidation, and a strike-off can equally follow an ordinary voluntary wind-up. Nothing in the
register entry distinguishes those, and this document does not guess.

**Why this is here, and what it is not.** It answers the only question this section asks: whether
anyone is still in a position to revive ULO. The answer is that the founder did keep trading after
Mu Design went quiet, that the work was games rather than connected hardware, and that this second
company is now struck off too. No corporate vehicle remains through which ULO could be supported.

MuGames was a **separate legal entity**. Mu Design's obligations to its backers were Mu Design's,
and they never transferred to another company merely because the two shared a director and a town.
A person starting a later business is not evidence of anything about an earlier one, and nothing
here should be read as suggesting otherwise. This entry records company registrations, which are
public commercial records. The registered street address is omitted — a *Sàrl-S* is often run from
home, and the town is all the comparison needs. Personal accounts and social media are likewise not
collected: they are not relevant to a camera, and assembling them would serve no purpose this
repository has.

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

As of 2026-08-14, **no lawsuits, court judgments or enforcement actions** were found against Vivien
Muller or Mu Design Sàrl in any publicly accessible source:

| Jurisdiction / Database                          | Result                                                |
|--------------------------------------------------|-------------------------------------------------------|
| Luxembourg courts (pseudonymised keyword search) | No judgments found mentioning "Mu Design" (2023–2026) |
| Luxembourg insolvency                            | Not searchable as a register — see §3.2              |
| US federal courts (CourtListener / PACER)        | No cases found                                        |
| FTC enforcement actions                          | None targeting ULO or Mu Design                       |
| General legal news / press                       | No reports of litigation                              |

### 6.1 Backer discussions about legal action

Kickstarter backers have **discussed** pursuing legal action in the campaign's comment section and
on Reddit, but no organised class action or group complaint has materialised. Reasons include:

* **Kickstarter's terms** frame backing as a pledge, not a purchase, weakening breach-of-contract
  claims.
* **Cross-border complexity.** Mu Design is a Luxembourg Sàrl; backers are spread across 96
  countries. Individual pledge amounts (~€100–200) make cross-border litigation uneconomical.
* **Partial delivery.** The company shipped *some* units and posted 41 updates, which complicates
  a fraud argument compared to a project that delivered nothing.
* **Kickstarter's limited role.** The platform cannot enforce delivery or refunds; it can only
  suspend the creator's ability to launch future campaigns.

### 6.2 Available recourse for affected backers

| Route                                      | Applicability                                                                                          |
|--------------------------------------------|--------------------------------------------------------------------------------------------------------|
| **EU Small Claims Procedure**              | Claims up to €5,000; cross-border within the EU. Creator is in Luxembourg, so EU backers can use this. |
| **European Consumer Centres (ECC-Net)**    | Free cross-border dispute resolution for EU consumers.                                                 |
| **National consumer protection authority** | File a complaint in your home country.                                                                 |
| **FTC (US backers)**                       | Report at [ReportFraud.ftc.gov](https://reportfraud.ftc.gov). Helps build enforcement patterns.        |
| **FBI IC3 (US backers)**                   | Internet crime complaints at [ic3.gov](https://www.ic3.gov) for significant losses.                    |
| **Luxembourg Justice Portal**              | Direct inquiry via `credoc@justice.etat.lu` for case-specific searches.                                |

> **Note on pseudonymisation.** Luxembourg court judgments are published with party names removed.
> A case *could* exist under anonymised identifiers without appearing in keyword searches. A formal
> inquiry to the Luxembourg legal documentation service would be needed to rule this out completely.

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