# Veikart og backlog

## Nåværende status

PoC med kjernefunksjonalitet for målgrupper, adresselister og abonnementslister. Ingen autentisering, SQLite-lagring, norsk referanseimplementasjon.

---

## Prioritert backlog

### 🔴 Høy prioritet

| # | Funksjon | Beskrivelse |
|---|---|---|
| B-01 | **Lås opp adresseliste** | Mulighet til å gå tilbake fra Låst til Klar/Utkast, slette snapshot og redigere på nytt |
| B-02 | **Autentisering** | Integrasjon med Ansattporten (ID-porten for ansatte). Alle sider krever innlogging |
| B-03 | **Maskinporten-beskyttet API** | Digdir definerer scopene (`digdir:kontaktliste.read`, `digdir:kontaktliste.write`). Den enkelte virksomhet tildeler scopene til sine egne klienter (systemer som skal lese/skrive adresselister) via Samarbeidsportalen eller Maskinportens selvbetjenings-API — standard delegering, ingen manuell håndtering hos Digdir per klient. |
| B-04 | **Roller og autorisasjon** | Rollemodell per virksomhet med tre nivåer: **Administrator** (administrerer brukertilganger i egen virksomhet), **Redaktør** (oppretter og redigerer lister, målgrupper, abonnementslister), **Leser** (kan se og eksportere, ikke redigere). Roller tildeles via **Altinn Autorisasjon** — virksomhetsadministrator delegerer roller til ansatte i Altinn, applikasjonen verifiserer ved innlogging. Avhenger av B-02 (Ansattporten) og B-18 (virksomhetsisolasjon). |
| B-18 | **Virksomhetsisolasjon (multi-tenancy)** | Datamodellen utvides med `Virksomhet`-entitet (orgnr, navn, status, onboardetAt). Alle lister, målgrupper og abonnementslister knyttes til én virksomhet. URL-strategi: `/{orgnr}/adresselister`, `/{orgnr}/malgrupper` osv. — orgnr i URL gir stateless, bookmarkbart og synlig tenant-kontekst. Alle spørringer filtrerer på orgnr fra URL. Arkitekturelt forutsetning for B-02 og B-04. |
| B-18a | **Virksomhets-onboarding og admin-API** | Virksomheter opprettes **ikke** automatisk — de må eksplisitt onboardes. Admin-API (`GET/POST/DELETE /api/v1/admin/virksomheter`) beskyttes av `digdir:kontaktliste.admin` som Digdir tildeler til de aktuelle aktørene (f.eks. Digdir selv eller delegerte onboardingsaktører). Etter onboarding: den onboardede virksomheten styrer selv hvilke av sine klienter som får `digdir:kontaktliste.read`/`.write` via Maskinporten (se B-03). Innlogget bruker via Ansattporten avvises med tydelig melding hvis virksomheten ikke er onboardet. Avhenger av B-18. |

### 🟡 Middels prioritet

| # | Funksjon | Beskrivelse |
|---|---|---|
| B-05 | **Historikk og revisjon** | Logg over hvem som opprettet, låste og eksporterte lister |
| B-06 | **Varsling** | E-postvarsling ved låsing, eller når en dynamisk målgruppe endrer seg |
| B-07 | **Abonnent-API** | Ekstern tjeneste kan POST/DELETE abonnenter via API med Maskinporten-token |
| B-08 | **Databasemigreringer** | Flytte fra `EnsureCreated` til EF Core Migrations for produksjonsklar skjemaoppdatering |
| B-09 | **PostgreSQL-adapter** | Bytte ut SQLite med PostgreSQL for produksjon |
| B-10 | **Eksport til flere formater** | Excel (.xlsx), FHIR-bundle, eller tilpasset format per mottakersystem |
| B-19 | **Delte/nasjonale lister** | Noen lister (f.eks. dynamisk kommuneliste) bør eies sentralt og kunne konsumeres av alle virksomheter. Krever et eierskaps- og publiseringsregime: hvem godkjenner, hvem vedlikeholder, hvem kan kopiere. Avhenger av B-18. |
| B-20 | **Tenor-kobling og testmodus** | Integrasjon med Tenor (Digdir/Skatteetaten) for syntetiske testpersoner og testvirksomheter. I testmodus swappes `BrregService` med en `TenorService` via det eksisterende adapter-grensesnittet — ingen kodeendringer i domenet. Nyttig for utvikling og demo uten reelle data. |

### 🟢 Lavere prioritet / idéer

| # | Funksjon | Beskrivelse |
|---|---|---|
| B-11 | **Regelmotor for ekskludering** | Automatisk ekskludering basert på regler (f.eks. "ekskluder alle med færre enn 5 ansatte") |
| B-12 | **Sammenligning av snapshots** | Vis diff mellom to låste versjoner av samme liste |
| B-13 | **Webhook-støtte** | Publiser hendelse til ekstern URL når liste låses |
| B-14 | **Søk på tvers** | Global søk på tvers av målgrupper, lister og organisasjoner |
| B-15 | **Selvbetjeningsportal for abonnenter** | La organisasjoner selv melde seg på/av via e-postlenke |
| B-16 | **Støtte for personer** | Fysiske personer (ikke bare organisasjoner) i målgrupper |
| B-17 | **Internasjonal støtte** | Adapter-grensesnitt støtter allerede andre lands enhetsregistre — implementer f.eks. dansk CVR |
| B-21 | **Frontend-redesign: Designsystemet.no + React** | Erstatt Blazor Server-frontend med React og Digdir sitt designsystem (Designsystemet.no). Minimal API-laget beholdes uendret. Forutsetter at API-kontrakten er stabil og godt testet. Gir bedre UX-konsistens med øvrige Digdir-produkter. |

---

## Teknisk gjeld

| # | Beskrivelse |
|---|---|
| T-01 | Ingen tester — trenger enhetstester for `AdresselisteService`, `BrregService`, dedup-logikk. Forutsetter at tjenestene først får interface-abstraksjoner (`IAdresselisteService` o.l.) for enkel mocking |
| T-02 | `EnsureCreated` — databasen må slettes ved skjemaendringer; bytt til Migrations (se B-08) |
| T-03 | Ingen feilgrenser i UI — ukjente feil vises bare som tom side |
| T-04 | BrregService cacher ikke — samme søk gjøres på nytt ved hver navigasjon |
| T-05 | SQLitePCLRaw.lib.e_sqlite3 har kjent sårbarhet (transitiv avhengighet fra EF Core 10) |
| T-06 | `SeedAsync` (~300 linjer hardkodet data) ligger i `Program.cs` — flytt til `Data/SeedData.cs` |
| T-07 | API-endepunkter (~140 linjer) ligger i `Program.cs` — flytt til egen `ApiEndpoints`-klasse med extension-metode på `IEndpointRouteBuilder` |
| T-08 | Ingen OpenAPI/Swagger — legg til `Microsoft.AspNetCore.OpenApi` (innebygd i .NET 10) for maskinlesbar API-spec og enklere klientgenerering hos konsumenter |

---

## Fullført

| Funksjon | Kommentar |
|---|---|
| Statiske og dynamiske målgrupper | Inkl. Brreg-sync og orgnr-opplasting |
| c/o-støtte i målgrupper | Samme org med ulikt visningsnavn/c/o-adresse |
| Adresselister med livssyklus | Utkast → Klar → Låst |
| Snapshot ved låsing | Inkl. abonnenter, ekskludering, dedup på (RecipientId, Visningsnavn, CoAdresse) |
| Abonnementslister | Opprett, administrer e-poster, koble til adresselister |
| JSON- og CSV-eksport | Inkl. visningsnavn, brregNavn, coAdresse, Unicode-tegn |
| Brreg-søk med filtre | Organisasjonsform, næringskode, sektorkode, aktivitetsstatus |
| Hierarkivisning i Brreg | Overordnet enhet og underenheter |
| Oppgradering til .NET 10 | EF Core 10.0.0 |
