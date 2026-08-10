# Kontaktlisteregisteret

Register for å administrere organisasjoner og personer i målgrupper for høringer, undersøkelser og utsendinger. Produserer standardiserte adresselister som konsumeres av andre systemer via API.

## Status

Tidlig PoC — ingen autentisering, SQLite-lagring, norsk referanseimplementasjon.

## Kom i gang

**Forutsetninger:** [.NET 10 SDK](https://dotnet.microsoft.com/download)

```bash
cd src/Kontaktlisteregisteret.Web
dotnet run --urls http://localhost:5100
```

Åpne [http://localhost:5100](http://localhost:5100)

## Kjøre i GitHub Codespaces

Klikk **Code → Codespaces → Create codespace**. Devcontaineren starter appen automatisk.

## Funksjonalitet

Se [docs/funksjonalitet.md](docs/funksjonalitet.md) for fullstendig beskrivelse av UI-funksjonalitet.  
Se [docs/api.md](docs/api.md) for alle API-endepunkter og interne tjenester.

Kort oppsummert:
- **Målgrupper** — dynamiske (Brreg-filterregler) og statiske (søk, opplasting, manuelt)
- **Adresselister** — koble sammen målgrupper og abonnementslister, lås til snapshot, eksporter
- **Abonnementslister** — administrer e-postabonnenter, koble til adresselister
- **Brreg-integrasjon** — søk, hierarkivisning, massevalidering av orgnr
- **c/o-støtte** — samme organisasjon kan ligge i en målgruppe med ulikt visningsnavn

## Arkitektur

Ports and Adapters (heksagonal). Norske tjenester er utskiftbare adaptere bak definerte grensesnitt.

```
src/
└── Kontaktlisteregisteret.Web/   # Blazor Server (.NET 10)
    ├── Data/                     # EF Core + SQLite (domenemodellar)
    ├── Services/                 # BrregService, TargetGroupService,
    │                             # AdresselisteService, AbonnementslisteService
    ├── Pages/                    # Blazor-sider
    └── wwwroot/                  # Statiske ressursar
```

| Port | Adapter | Teneste |
|---|---|---|
| `IEntityRegistry` | `BrregService` | data.brreg.no |
| `IIdentityProvider` | *(ikkje implementert)* | Ansattporten |
| `IMachineAuthProvider` | *(ikkje implementert)* | Maskinporten |

## Dokumentasjon

| Dokument | Innhald |
|---|---|
| [docs/funksjonalitet.md](docs/funksjonalitet.md) | Detaljert skildring av alle funksjonar |
| [docs/veikart.md](docs/veikart.md) | Backlog og prioriteringar |

## Lisens

MIT
