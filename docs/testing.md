# E2E-testsuite — Playwright

## Oversikt

Prosjektet har en Playwright-basert e2e-testsuite som dekker de fire
hoveddomenene: målgrupper, adresselister, abonnementslister og Brreg-søk.

Testene kjøres mot en lokal instans av appen på `http://localhost:5100`.
Playwright starter appen automatisk dersom den ikke allerede kjører.

---

## Struktur

```
tests/e2e/
├── playwright.config.ts      # Playwright-konfigurasjon
├── package.json              # npm-prosjekt for testsuiten
├── helpers/
│   └── nav.ts                # Delte hjelpefunksjoner (goto, slett*, ORGNR, TP)
└── specs/
    ├── malgrupper.spec.ts    # Statisk orgnr, dynamisk filter, navneoppslag, rename
    ├── adresselister.spec.ts # Opprett, koble til MG, Klar, Lås, eksport JSON/CSV
    ├── abonnenter.spec.ts    # Opprett liste, legg til/slett abonnent, valider e-post
    └── brreg.spec.ts         # Fritekst-søk, org.form-filter, hierarki, massevalidering
```

### Viktige konstanter i `helpers/nav.ts`

| Konstant | Verdi | Formål |
|---|---|---|
| `ORGNR` | `991825827` | Digitaliseringsdirektoratet — seed-virksomhet |
| `TP` | `[E2E]` | Prefiks på all testdata; brukes til opprydding |
| `BASE` | `http://localhost:5100/991825827` | Rot-URL for alle virksomhets-sider |

---

## Kjøre testene

**Forutsetning:** .NET 10 SDK installert og appen kompilerer uten feil.

### Første gangs oppsett

```bash
cd tests/e2e
npm install
npx playwright install chromium
```

### Kjør alle tester (headless)

```bash
cd tests/e2e
npm run test:e2e
```

### Kjør med visuelt UI (anbefalt under utvikling)

```bash
npm run test:e2e:ui
```

### Kjør med synlig nettleser

```bash
npm run test:e2e:headed
```

### Oppdater screenshot-baselines

Gjøres når du med vilje endrer utseendet på en side:

```bash
npm run test:e2e:update-snapshots
```

### Åpne HTML-rapport etter kjøring

```bash
npm run test:e2e:report
```

---

## Konfigurasjon

`playwright.config.ts` inneholder de viktigste innstillingene:

| Innstilling | Verdi | Begrunnelse |
|---|---|---|
| `fullyParallel` | `false` | SQLite tåler ikke parallelle skriv |
| `workers` | `1` | Én sekvensielle worker |
| `actionTimeout` | `15 000 ms` | SignalR round-trip i Blazor Server |
| `navigationTimeout` | `30 000 ms` | Ventetid for sidenavigasjon |
| `reuseExistingServer` | `true` (lokalt) | Gjenbruker kjørende app |
| `maxDiffPixelRatio` | `0.05` | Tillater 5 % pikselavvik i screenshots |

---

## Screenshot-baselines

Visuelle regresjonstester lagrer PNG-baselines ved første kjøring.
Disse sjekkes inn i `tests/e2e/specs/__screenshots__/`.

Tester som tar screenshots maskerer dynamisk innhold (`.muted`, `time`, `strong`)
slik at datoer og tellere ikke forårsaker falske feil.

---

## Testdata og opprydding

- Alle testdata prefixes med `[E2E]`
- `beforeEach`/`afterAll` i hver spec-fil rydder opp rester fra feilet kjøring
- Testene er deterministiske: hver test oppretter og sletter sin egen data

---

## MCP-integrasjon (Playwright MCP Server)

`.mcp.json` i prosjektroten konfigurerer Playwright som MCP-server for Claude:

```json
{
  "mcpServers": {
    "playwright": {
      "command": "npx",
      "args": ["-y", "@playwright/mcp@latest", "--browser", "chromium"],
      "env": { "PLAYWRIGHT_NO_BROWSER_SANDBOX": "1" }
    }
  }
}
```

Dette lar Claude kjøre og inspisere appen direkte via MCP under utvikling.

---

## CI/CD

For GitHub Actions, sett miljøvariabelen `CI=true`.
Da vil Playwright starte appen selv (ignorerer `reuseExistingServer`).

Eksempel-workflow:

```yaml
- name: Install Node dependencies
  run: npm ci
  working-directory: tests/e2e

- name: Install Playwright browsers
  run: npx playwright install chromium --with-deps
  working-directory: tests/e2e

- name: Run e2e tests
  run: npm run test:e2e
  working-directory: tests/e2e
  env:
    CI: true
```
