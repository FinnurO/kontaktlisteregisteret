# Funksjonalitet

## Målgrupper (`/malgrupper`)

Målgrupper er samlinger av organisasjoner eller personer som brukes som grunnlag for adresselister.

### Typer

| Type | Beskrivelse |
|---|---|
| **Statisk** | Manuelt vedlikeholdt liste. Organisasjoner legges til via søk, orgnr-opplasting eller manuell registrering. |
| **Dynamisk** | Filterregler mot Brreg. Resultatlisten synkroniseres ved behov. Filtre: organisasjonsform, næringskode, sektorkode, aktivitetsstatus. |

### Funksjoner

- Opprett ny målgruppe (statisk eller dynamisk)
- Søk i Brreg og legg til enkeltorganisasjoner
- Last opp liste med orgnummer — valideres mot Brreg, ugyldige flagges
- Rediger navn og beskrivelse
- c/o-støtte: samme organisasjon kan legges til flere ganger med ulikt visningsnavn og c/o-adresse (f.eks. "Forum for Barnekonvensjonen c/o Norges Røde Kors")
- Synkroniser dynamisk målgruppe mot Brreg
- Slett målgruppe

### c/o-funksjon

Når en organisasjon i målgruppen har en underorganisasjon eller et sekretariat som ønsker å motta post, kan man legge til organisasjonen på nytt med:
- **Visningsnavn** — erstatter Brreg-navn i eksport (f.eks. "Forum for Barnekonvensjonen")
- **c/o-adresse** — tilleggsopplysning som vises i adresselisten

---

## Adresselister (`/adresselister`)

Adresselister er det ferdige produktet — en sammenstilling av mottakere fra én eller flere målgrupper og abonnementslister.

### Livssyklus

```
Utkast → Klar → Låst
```

| Status | Beskrivelse |
|---|---|
| **Utkast** | Under arbeid. Målgrupper og abonnementslister kan kobles til og fra. |
| **Klar** | Markert som ferdig for gjennomgang. Kan fortsatt redigeres. |
| **Låst** | Snapshot tatt. Mottakerlisten er fryst og kan eksporteres. |

### Funksjoner

- Opprett ny adresseliste med tittel og beskrivelse
- Koble til en eller flere målgrupper
- Koble til en eller flere abonnementslister
- Forhåndsvisning av mottakere (live, før låsing) med filtrering og paginering
- Trevisning — mottakerne gruppert per målgruppe
- Ekskludering — huk av enkeltorganisasjoner som ikke skal være med
- Ad-hoc statisk gruppe — opprett en ny statisk målgruppe direkte fra adresselisten
- Lås liste — tar snapshot av alle tilknyttede målgrupper og abonnementslister
- Eksporter låst liste som JSON eller CSV
- Kopier adresseliste

### Snapshot-logikk

Ved låsing:
1. Alle mottakere fra tilknyttede målgrupper tas med (med gjeldende visningsnavn og c/o-adresse)
2. Ekskluderte organisasjoner hoppes over
3. Abonnenter fra tilknyttede abonnementslister legges til som egne mottakere (type: `subscriber`)
4. Duplikater fjernes basert på `(RecipientId, Visningsnavn, CoAdresse)`

### Eksportformat (JSON)

```json
[
  {
    "organisasjonsnummer": "864139442",
    "navn": "Forum for Barnekonvensjonen",
    "brregNavn": "Norges Røde Kors",
    "type": "organization",
    "orgForm": "FLI",
    "postnummer": null,
    "poststed": null,
    "coAdresse": "c/o Norges Røde Kors",
    "kildeMålgruppeId": 4,
    "kildeAbonnementslisteId": null
  }
]
```

---

## Abonnementslister (`/abonnenter`)

Abonnementslister er navngitte lister med e-postadresser. De kan kobles til adresselister og tas med i snapshot ved låsing.

### Funksjoner

- Opprett ny abonnementsliste med navn og beskrivelse
- Legg til e-postadresser manuelt
- Se når abonnenter ble lagt til og fra hvilken kilde (manuell / API)
- Slett enkeltabonnenter
- Slett abonnementsliste
- Koble abonnementsliste til én eller flere adresselister

### API-støtte

Abonnenter kan legges til via API:

```
POST /abonnementslister/{id}/abonnenter
{ "epost": "eksempel@digdir.no" }
```

---

## Brreg-integrasjon

All oppslag mot Enhetsregisteret går via `BrregService` mot `data.brreg.no`.

### Søk (`/brreg`)

- Fritekst på navn eller orgnummer
- Filter på organisasjonsform (AS, FLI, STI, kommuner m.m.)
- Filter på næringskode (NACE-prefix)
- Filter på institusjonell sektorkode
- Filter på aktivitetsstatus (aktive, konkurs, under avvikling)
- Ekspander enkeltresultat for å se detaljer og underenheter

### Massevalidering

Fra målgruppesiden: lim inn en liste med orgnummer, valider mot Brreg, importer gyldige direkte til målgruppen.

### Hierarkivisning

Klikk på en organisasjon for å se overordnet enhet og underenheter (driftsenheter/avdelinger).

---

## Teknisk

| Komponent | Teknologi |
|---|---|
| Rammeverk | Blazor Server, .NET 10 |
| Database | SQLite via EF Core 10 |
| Brreg-API | data.brreg.no REST API |
| Stil | Egendefinert CSS (Altinn-inspirert) |
| JS-interop | `window.downloadFile`, `window.scrollIntoView` |
