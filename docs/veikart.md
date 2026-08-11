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
| B-32 | **Brreg-oppslag ved onboarding** | Onboarding-skjemaet (`/admin/virksomheter`) skal slå opp navn fra Brreg når bruker taster inn orgnr. Feltet «Virksomhetsnavn» auto-fylles med offisielt Brreg-navn og gjøres skrivebeskyttet. Feil-tilstand hvis orgnr ikke finnes i Enhetsregisteret. Avslutt med synlig bekreftelse: orgnr + navn — som Digdir-onboarding av 991 825 827 / Digitaliseringsdirektoratet. |
| B-19 | **Delte/nasjonale lister** | Noen lister (f.eks. dynamisk kommuneliste) bør eies sentralt og kunne konsumeres av alle virksomheter. Krever et eierskaps- og publiseringsregime: hvem godkjenner, hvem vedlikeholder, hvem kan kopiere. Avhenger av B-18. |
| B-26 | **Hierarki av målgrupper (kompositt og AND-filter)** | To mekanismer ønskes: (a) En dynamisk målgruppe kan ha flere filterkritierier med AND-logikk — f.eks. næringskode=havner OG selskapsform=IKS. I dag er det ett filter per dimensjon. (b) En målgruppe kan inkludere andre målgrupper som «barn» — f.eks. «Havner» = «Havner KF» + «Havner IKS» + «Havner Andre». Dette gjør det mulig å bygge hierarkier av gjenbrukbare segmenter og kombinere dem i adresselister uten å duplisere data. |
| B-29 | **Høringsinstanser uten orgnr** | Enkelte aktører (f.eks. IPR-er og uformelle råd) er ikke registrert i Brreg og mangler orgnr. Tre alternativer må vurderes og besluttes: (a) tillat mottakere kun med navn og e-postadresse (løsere struktur, krever ny `RecipientType`), (b) krev at alle mottakere er Brreg-enheter og heller legg inn e-postadresse som kontaktfelt per mottaker, eller (c) bruk c/o-felt på en eksisterende orgnr-holder («paraplyenhet») slik at orgnr alltid finnes. Valget påvirker datamodell, eksportformat og konsumentenes forventninger til entydige identifikatorer. |
| B-31 | **Profesjonelle brukergrupper (Brukerutvalg, Tillitsvalgte)** | Noen høringer sendes til faste råd og utvalg (f.eks. Brukerutvalget for et helseforetak, Tillitsvalgte i organisasjoner) der mottakerne er *enkeltpersoner i profesjonell rolle* — ikke virksomheter og ikke frivillige abonnenter. To alternativer må vurderes: (a) Registrering med f-nr slik at de kan motta varsel via Altinn og logge inn med Ansattporten — sikrer sterk identitet, men krever innhenting av f-nr og avhenger av B-02. (b) Vedlikehold med e-postadresser direkte i systemet — enklere å vedlikeholde, men løsere identitetskontroll og ingen Ansattporten-integrasjon. Valget påvirker datamodell (ny `PersonMottaker`-type eller utvidelse av eksisterende), invitasjonsflyt og GDPR-avklaringer rundt lagring av f-nr. Relatert til B-16 (støtte for personer) og B-29 (høringsinstanser uten orgnr). |

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
| B-28 | **Brreg-hendelser og sist-oppdatert** | Abonnere på Brreg sitt [oppdateringer-endepunkt](https://data.brreg.no/enhetsregisteret/api/dokumentasjon/no/index.html#tag/oppdateringer) for å spore endringer på virksomheter i dynamiske og statiske lister. Innfør `sistOppdatert`-felt på listene og varsle redaktøren om endringer som kan påvirke listens innhold (f.eks. virksomhet slettet, skiftet navn, skiftet næringskode). |

---

## Teknisk gjeld

| # | Beskrivelse |
|---|---|
| T-01 | Ingen tester — trenger enhetstester for `AdresselisteService`, `BrregService`, dedup-logikk. Forutsetter at tjenestene først får interface-abstraksjoner (`IAdresselisteService` o.l.) for enkel mocking |
| ~~T-02~~ | ~~`EnsureCreated` — løst av B-08 (Migrations innført)~~ |
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
| B-18 Virksomhetsisolasjon (multi-tenancy) | `/{orgnr}/`-URL-strategi, `Virksomhet`-entitet, alle data scoped per virksomhet |
| B-18a Virksomhets-onboarding og admin-panel | `/admin/virksomheter`, `POST /api/v1/admin/virksomheter`, ingen auto-oppretting |
| Navneoppslag (lim inn liste) | Ny fane i Brreg-søk: lim inn navn, slår opp orgnr for hvert navn fra Brreg |
| Bugfix: redigere målgruppenavn | Inline-redigering av navn i MalgruppeDetalj, inkl. kopier |
| Bugfix: HTTP 400 Brreg-paginering | Guard: `size × (page+1) ≤ 10 000` — avkorter og viser melding |
| Bugfix: virksomhetsdetaljer i flatvisning | OrgDetalj-komponent nå klikkbar i flatvisning (ikke bare trevisning) |
| B-07 Abonnent-API | `POST /api/v1/abonnementslister/{id}/abonnenter`, `DELETE /api/v1/abonnenter/{id}`, `GET …/abonnenter` — allerede implementert |
| B-08 EF Core Migrations | `InitialCreate`-migrasjon, `MigrateAsync()` erstatter `EnsureCreated()`. Nye skjemaendringer: `dotnet ef migrations add <Namn>` |
| B-10 Eksport til Excel | `↓ Excel`-knapp (ClosedXML) i Låst-tilstand — kolonner Orgnr, BrregNavn, Visningsnavn, CoAdresse |
| B-20 Tenor-adapter | `IBrregService`-grensesnitt, `TenorBrregService` mot `tenor.test.brreg.no`, `Tenor:Enabled`-konfig |
| B-25 Select/deselect all | «Velg alle (N)» / «Fravelg alle (N)»-knapp i `static-search`-veiviseren |
| B-27 Sortering i oversiktslister | Nyeste / Alfabetisk / Flest mottakere i Målgrupper, Adresselister og Abonnementslister |
| B-30 c/o og alternativt visningsnavn | Redigerbare Visningsnavn- og CoAdresse-felt i resultatTabellen for `static-orgnr` og `static-navn` |
| B-05 Historikk og revisjon | `AuditLog`-entitet, `AuditLogService`, logging i Adresseliste/TargetGroup-tjenester, `/admin/revisjon`-side |
| B-06 Varsling ved låsing | `VarslingsService` (SMTP, System.Net.Mail), hook i `LåsAsync`, `Varsling`-seksjon i appsettings |
| B-09 PostgreSQL-adapter | `Npgsql.EF.PostgreSQL`, `Database:Provider`-konfig — bytt med env-var `Database__Provider=postgresql` |
| B-22+B-23+B-24 SSB KLASS-koder og multiple select | `SsbKlassService` (IMemoryCache, 24t TTL), dynamiske sektorkode/næringskode-lister fra SSB API, multiple-select i filter-UI |
