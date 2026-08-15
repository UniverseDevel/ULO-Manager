# Web application source maps

The camera's built-in web application (Ionic 3 / Angular 5, built with Webpack) ships its source
maps alongside the compiled bundles. They are served without authentication at:

```
http://<ULO_IP>/build/main.js.map      6.1 MB   full source map (912 source files)
http://<ULO_IP>/build/main.css.map     4 bytes  broken — contains only "null"
```

The two files are **identical on both firmware versions** tested (06.0601 and 10.1308):

| File | SHA-256 |
|---|---|
| `main.js.map` | `F8EA29F0289A4AF4A169D872F5294ABB97D5BFCFBB39CACA1AD2B06F9CA2F4D9` |
| `main.css.map` | `74234E98AFE7498FB5DAF1F36AC2D78ACC339464F950703B8C019892F982B90B` |

This confirms that both firmware versions bundle the **same web application** (built 2018-05-03 on
the 10.1308 unit, 2017-12-18 on the 06.0601 unit based on the JS bundle dates — but the source maps
are byte-identical, so the build is the same and only the packaging timestamp differs).

## What the source map exposes

A source map lets any browser's developer tools reconstruct the original TypeScript source, file by
file, with full variable names and comments. In effect it is the web app's source code. The vendor's
own files are extracted into `src/` beside this README — 173 TypeScript files, ~419 KB:

```
src/
├── app/                   App bootstrap (app.component.ts, app.module.ts, main.ts)
├── forms/                 User and admin forms
├── pages/
│   ├── errors/            Connection-lost screen
│   ├── live/              Main camera view, recorder, file browser, eye settings
│   ├── login/             Authentication
│   ├── settings/          All admin panels (wifi, backup, quality, accounts…)
│   └── setup/             First-run / factory-reset wizard
├── providers/             API services, models, mocks, WebSocket
├── shared/                Base components, pipes
└── widgets/               Calendar, carousel, progress bars, media players
```

The remaining ~739 sources are third-party (`node_modules/`) and webpack internals; those are not
extracted here because they are standard open-source libraries (Angular 5, Ionic 3, RxJS, Moment.js)
and add nothing to the analysis.

## Security relevance

Source maps are a development convenience that should never ship in production — they give any
attacker the complete API surface, the authentication flow, the token handling and the WebSocket
protocol in readable form, rather than having to reverse-engineer the minified bundle. Everything
documented in [`docs/API.md`](../../docs/API.md) was cross-referenced against this map.

See also [`docs/SECURITY.md`](../../docs/SECURITY.md) and
[`docs/ACCESS_RESEARCH.md`](../../docs/ACCESS_RESEARCH.md).

## Why both firmware folders hold a copy

The hash is identical, so one copy would suffice — but the folder structure records *what each
firmware version ships*, not just the unique artefacts, so a reader looking at one firmware's folder
sees everything that unit serves, without having to check whether a file is the same as the other.
