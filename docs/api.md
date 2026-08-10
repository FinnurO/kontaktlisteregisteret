# API-dokumentasjon — Kontaktlisteregisteret

**Basis-URL (dev):** `http://localhost:5100`  
**Autentisering:** Ikke aktivert i PoC. I produksjon kreves Maskinporten-token med scope `digdir:kontaktliste.read`.

---

## Eksternt eksponerte endepunkter

Alle endepunkter er prefixet `/api/v1`. Alle svar er JSON.

---

### Adresselister

#### `GET /api/v1/adresselister`

Returnerer alle låste adresselister.

**Respons 200**
```json
[
  {
    "id": 1,
    "tittel": "Kommuner Rogaland 2025",
    "beskrivelse": "Alle kommuner i Rogaland",
    "status": "Låst",
    "antallMottakere": 23,
    "låstAt": "2025-06-01T12:00:00Z",
    "opprettetAt": "2025-05-15T08:30:00Z"
  }
]
```

---

#### `GET /api/v1/adresselister/{id}`

Metadata for én låst adresseliste.

**Path-parameter:** `id` (int)

**Respons 200** — samme felt som i listen over.  
**Respons 404**
```json
{ "error": "Adresseliste ikke funnet" }
```

---

#### `GET /api/v1/adresselister/{id}/mottakere`

Snapshot av alle mottakere i én låst adresseliste. Returnerer aldri dobbeltoppføringer — samme orgnr kan forekomme flere ganger dersom det er lagt til med ulikt visningsnavn (c/o-mønster).

**Path-parameter:** `id` (int)

**Respons 200**
```json
[
  {
    "organisasjonsnummer": "964967725",
    "navn": "Stavanger kommune",
    "brregNavn": null,
    "coAdresse": null,
    "type": "organization",
    "orgForm": "KOMM",
    "postadresse": {
      "adresse": "Pb. 8001",
      "postnummer": "4068",
      "poststed": "STAVANGER"
    },
    "kildeMålgruppeId": 3
  },
  {
    "organisasjonsnummer": "970188290",
    "navn": "Norges Røde Kors v/ Krisesenter",
    "brregNavn": "Norges Røde Kors",
    "coAdresse": "v/ Krisesenter",
    "type": "organization",
    "orgForm": "FLI",
    "postadresse": null,
    "kildeMålgruppeId": 5
  }
]
```

**Feltbeskrivelser:**

| Felt | Type | Beskrivelse |
|------|------|-------------|
| `organisasjonsnummer` | string | 9-sifret orgnr fra Brreg |
| `navn` | string | Visningsnavn (overstyrt) eller Brreg-navn |
| `brregNavn` | string \| null | Brreg-navn dersom `navn` er overstyrt, ellers null |
| `coAdresse` | string \| null | C/o-adressenotat, f.eks. «v/ Krisesenter» |
| `type` | string | `"organization"`, `"person"` eller `"subscriber"` |
| `orgForm` | string \| null | Organisasjonsformkode fra Brreg, f.eks. `"KOMM"` |
| `postadresse` | objekt \| null | Postadresse fra Brreg |
| `kildeMålgruppeId` | int \| null | ID til målgruppen som la til denne mottakeren |

**Respons 404**
```json
{ "error": "Adresseliste ikke funnet" }
```

---

### Abonnementslister

#### `GET /api/v1/abonnementslister`

Returnerer alle abonnementslister.

**Respons 200**
```json
[
  {
    "id": 1,
    "navn": "Nyhetsbrev Digdir",
    "beskrivelse": "Generell nyhetsliste",
    "antallAbonnenter": 142,
    "opprettetAt": "2025-01-10T09:00:00Z"
  }
]
```

---

#### `GET /api/v1/abonnementslister/{id}/abonnenter`

Alle abonnenter for én liste.

**Path-parameter:** `id` (int)

**Respons 200**
```json
[
  {
    "id": 77,
    "epost": "ola@example.com",
    "lagtTilAt": "2025-03-01T14:22:00Z",
    "kilde": "Api"
  }
]
```

`kilde` er `"Manuell"` (lagt til via UI) eller `"Api"`.

**Respons 404**
```json
{ "error": "Abonnementsliste ikke funnet" }
```

---

#### `POST /api/v1/abonnementslister/{id}/abonnenter`

Registrerer en ny abonnent.

**Path-parameter:** `id` (int)  
**Request-body:**
```json
{ "epost": "ola@example.com" }
```

**Respons 201** — Location-header peker på `/api/v1/abonnenter/{nyId}`
```json
{
  "id": 78,
  "epost": "ola@example.com",
  "lagtTilAt": "2025-08-10T10:00:00Z"
}
```

**Respons 400** — ugyldig e-postformat
```json
{ "error": "Ugyldig e-postadresse" }
```

**Respons 404** — listen finnes ikke
```json
{ "error": "Abonnementsliste ikke funnet" }
```

**Respons 409** — e-postadressen er allerede registrert på listen
```json
{ "error": "E-postadressen er allerede registrert" }
```

---

#### `DELETE /api/v1/abonnenter/{id}`

Fjerner én abonnent. Uavhengig av hvilken liste abonnenten tilhører.

**Path-parameter:** `id` (int)

**Respons 204** — ingen body.  
**Respons 404**
```json
{ "error": "Abonnent ikke funnet" }
```

---

## Interne tjenester

Brukes internt av Blazor-komponentene. Ikke eksponert over HTTP.

---

### `AdresselisteService`

Håndterer livssyklus for adresselister — opprettelse, redigering, låsing og snapshot-generering.

| Metode | Signatur | Beskrivelse |
|--------|----------|-------------|
| `GetAllAsync` | `→ Task<List<Adresseliste>>` | Alle lister med full eager loading (målgrupper, mottakere, abonnementslister) |
| `GetAsync` | `(int id) → Task<Adresseliste?>` | Én liste med full eager loading |
| `GetLåsteAsync` | `→ Task<List<Adresseliste>>` | Kun låste lister (brukes av API-endepunktene) |
| `CreateAsync` | `(string tittel, string? beskrivelse, string? opprettetAv) → Task<Adresseliste>` | Oppretter ny liste med status Utkast |
| `UpdateAsync` | `(int id, string tittel, string? beskrivelse) → Task<bool>` | Oppdaterer tittel/beskrivelse (blokkert hvis låst) |
| `AddMålgruppeAsync` | `(int adresselisteId, int målgruppeId) → Task<bool>` | Kobler målgruppe til liste |
| `RemoveMålgruppeAsync` | `(int adresselisteId, int målgruppeId) → Task<bool>` | Frakobler målgruppe |
| `AddAbonnementslisteAsync` | `(int adresselisteId, int abonnementslisteId) → Task<bool>` | Kobler abonnementsliste |
| `RemoveAbonnementslisteAsync` | `(int adresselisteId, int abonnementslisteId) → Task<bool>` | Frakobler abonnementsliste |
| `GetEkskluderte` *(static)* | `(Adresseliste liste) → HashSet<string>` | Leser EkskluderteJson til sett av orgnr |
| `SetEkskluderteAsync` | `(int id, HashSet<string> ekskluderte) → Task` | Lagrer ekskluderte orgnr |
| `SetStatusAsync` | `(int id, AdresselisteStatus nyStatus) → Task<bool>` | Endrer status (ikke mulig tilbake fra Låst) |
| `LåsAsync` | `(int id) → Task<(bool Ok, string? Error)>` | Tar snapshot av mottakere fra alle målgrupper + abonnenter, setter status Låst. Irreversibel. |
| `KopierAsync` | `(int id) → Task<Adresseliste>` | Kopierer liste (Utkast) med samme målgrupper og eksklusjoner |
| `DeleteAsync` | `(int id) → Task` | Sletter liste (cascade i DB) |
| `GetLiveMottakere` *(static)* | `(Adresseliste liste) → List<LiveMottaker>` | Live union av alle tilknyttede målgrupper, deduplicert på (RecipientId, Visningsnavn, CoAdresse) |

**Merknad om `LåsAsync`:** Deduplicering skjer på trippelen `(RecipientId, Visningsnavn, CoAdresse)` slik at samme organisasjon kan forekomme to ganger dersom den er lagt til med ulikt visningsnavn (c/o-mønster).

---

### `TargetGroupService`

Håndterer målgrupper — både dynamiske (Brreg-regler) og statiske (fast liste).

| Metode | Signatur | Beskrivelse |
|--------|----------|-------------|
| `GetAllAsync` | `→ Task<List<TargetGroup>>` | Alle målgrupper inkl. members og recipients |
| `GetAsync` | `(int id) → Task<TargetGroup?>` | Én målgruppe |
| `CreateDynamicAsync` | `(string name, TargetGroupScope scope, DynamicCriteria criteria) → Task<TargetGroup>` | Oppretter dynamisk gruppe og kjører første synk mot Brreg |
| `CreateStaticAsync` | `(string name, TargetGroupScope scope, List<Recipient> recipients) → Task<TargetGroup>` | Oppretter statisk gruppe |
| `KopierAsync` | `(int id) → Task<TargetGroup>` | Kopierer gruppe inkl. Visningsnavn/CoAdresse |
| `DeleteAsync` | `(int id) → Task` | Sletter gruppe |
| `SaveCriteriaAsync` | `(TargetGroup group) → Task` | Lagrer oppdatert DynamicCriteriaJson |
| `SyncDynamicGroupAsync` | `(TargetGroup group) → Task` | Henter alle matchende enheter fra Brreg og erstatter members |
| `SetVisningsnavnAsync` | `(int memberId, string? visningsnavn, string? coAdresse) → Task` | Setter visningsnavn/c/o på én member-rad |
| `RemoveMemberAsync` | `(int memberId) → Task` | Fjerner én member-rad |
| `AddMedVisningsnavnAsync` | `(int groupId, BrregEnhet e, string visningsnavn, string? coAdresse) → Task` | Upsert recipient + legg til med eksplisitt visningsnavn |
| `AddMembersAsync` | `(int groupId, List<Recipient> recipients) → Task` | Legger til mottakere (upsert recipients, sjekker duplikat) |
| `ExportJsonAsync` | `(int groupId) → Task<byte[]>` | Eksporterer gruppe som JSON (UTF-8) |
| `ExportCsvAsync` | `(int groupId) → Task<byte[]>` | Eksporterer gruppe som CSV |
| `GetCriteria` | `(TargetGroup g) → DynamicCriteria?` | Deserialiserer DynamicCriteriaJson |
| `BrregEnhetToRecipient` *(static)* | `(BrregEnhet e) → Recipient` | Konverterer Brreg-enhet til domenemodell |

---

### `AbonnementslisteService`

| Metode | Signatur | Beskrivelse |
|--------|----------|-------------|
| `GetAllAsync` | `→ Task<List<Abonnementsliste>>` | Alle lister inkl. abonnenter |
| `GetAsync` | `(int id) → Task<Abonnementsliste?>` | Én liste inkl. abonnenter |
| `OpprettAsync` | `(string navn, string? beskrivelse, string? opprettetAv) → Task<Abonnementsliste>` | Oppretter ny liste |
| `OppdaterAsync` | `(int id, string navn, string? beskrivelse) → Task<bool>` | Oppdaterer navn/beskrivelse |
| `SlettListeAsync` | `(int id) → Task<bool>` | Sletter liste |
| `LeggTilAsync` | `(int listeId, string epost, AbonnentKilde kilde) → Task<(bool Ok, string? Error, Abonnent? Abonnent)>` | Validerer e-post, sjekker duplikat, legger til abonnent |
| `SlettAbonnentAsync` | `(int id) → Task<bool>` | Fjerner én abonnent |
| `AntallAbonnenterAsync` | `(int listeId) → Task<int>` | Teller abonnenter |

---

### `BrregService`

Klient mot `https://data.brreg.no/enhetsregisteret/api`. Singleton via HttpClient factory. Timeout 15 sek.

**Tilstandsegenskaper (leses etter kall):**

| Egenskap | Type | Beskrivelse |
|----------|------|-------------|
| `LastError` | `string?` | Feilmelding fra siste kall, null ved suksess |
| `LastTotalElements` | `int` | Totalt antall treff (fra Brreg page-objekt) |
| `LastTotalPages` | `int` | Totalt antall sider |

**Metoder:**

| Metode | Signatur | Beskrivelse |
|--------|----------|-------------|
| `SearchAsync` | `(string query, string? orgform, string? nacePrefix, string? sektorKode, int size=20, int page=0) → Task<List<BrregEnhet>>` | Søk i Enhetsregisteret — én side. Setter `LastTotalElements` og `LastTotalPages`. |
| `SearchAllPagesAsync` | `(string? orgform, string? nacePrefix, string? sektorKode, string? aktivitet) → Task<List<BrregEnhet>>` | Henter alle sider (200 per kall) og slår dem sammen |
| `GetByOrgnrAsync` | `(string orgnr) → Task<BrregEnhet?>` | Oppslag på orgnr — prøver enheter, så underenheter |
| `GetChildrenAsync` | `(string orgnr) → Task<List<BrregEnhet>>` | Alle underenheter og datterselskaper |
| `ValidateOrgnrListAsync` | `(IEnumerable<string> orgnrs) → Task<List<OrgnrValidationResult>>` | Batch-validerer orgnr-liste mot Brreg |
| `EvaluateDynamicCriteriaAsync` | `(DynamicCriteria criteria) → Task<List<BrregEnhet>>` | Evaluerer alle Brreg-regler inkl. subenheter og klientside-filtrering |

---

## Datamodell

### Domeneentiteter

```
Adresseliste
├── AdresselisteMålgruppe[]     → TargetGroup
├── AdresselisteMottaker[]      → Recipient  (snapshot, kun i Låst-tilstand)
└── AdresselisteAbonnementsliste[] → Abonnementsliste

TargetGroup
├── DynamicCriteriaJson         (null for statiske grupper)
└── TargetGroupMember[]
        ├── RecipientId         → Recipient
        ├── Visningsnavn?
        └── CoAdresse?

Abonnementsliste
└── Abonnent[]
```

### Statuser og enumer

| Type | Verdier |
|------|---------|
| `AdresselisteStatus` | `Utkast`, `Klar`, `Låst` |
| `TargetGroupType` | `Dynamic`, `Static` |
| `TargetGroupScope` | `Private`, `Shared` |
| `RecipientType` | `Organization`, `Person`, `Subscriber` |
| `AbonnentKilde` | `Manuell`, `Api` |
| `ValidationStatus` | `Ok`, `NotFound`, `Deleted`, `InvalidFormat` |

### `DynamicCriteria`-felt

| Felt | Type | Standard | Beskrivelse |
|------|------|---------|-------------|
| `OrgForm` | `string?` | null | Brreg-kode, f.eks. `"KOMM"`, `"AS"` |
| `NacePrefix` | `string?` | null | NACE-prefiksfilter, f.eks. `"86"` |
| `SektorKode` | `string?` | null | Institusjonell sektorkode, f.eks. `"6100"` |
| `Aktivitet` | `string` | `"aktive"` | `"aktive"`, `"konkurs"`, `"avvikling"` eller `""` (alle) |
| `AktivitetFilter` | `string?` | null | Klientside-filter på aktivitet-feltet, f.eks. `"Barnehage"` |
| `IncludeSubUnits` | `bool` | false | Hent også underenheter for alle treff |
| `ExcludedOrgnrs` | `List<string>` | `[]` | Orgnr som alltid ekskluderes fra gruppen |

---

## Kjente begrensninger (PoC)

- Ingen autentisering — alle endepunkter er åpne
- Ingen OpenAPI/Swagger-spesifikasjon
- Ingen paginering på API-endepunktene (returnerer alltid full liste)
- Ingen write-operasjoner for adresselister via API (kun les)
- SQLite som database — ikke egnet for produksjon
