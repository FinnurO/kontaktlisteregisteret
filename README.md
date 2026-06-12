# Kontaktlisteregisteret

A registry for managing organizations and persons in target groups for hearings, surveys, and outreach. Produces standardized address lists consumed by other systems via API.

## Status

Early-stage PoC — no authentication, SQLite storage, Norwegian reference implementation.

## Features (PoC)

- **Target groups** — dynamic (Brreg filter rules) and static (upload or search)
- **Live Brreg search** — search the Norwegian Business Registry by name or org number
- **Hierarchy browser** — explore parent/child organization relationships from Brreg
- **Org number upload** — paste a list of org numbers, validate against Brreg, import valid entries
- **Export** — download target group as JSON or CSV
- **SQLite** — zero-config local storage

## Architecture

Ports and Adapters (hexagonal). Norwegian services are swappable adapters behind defined interfaces, enabling reuse in other countries.

```
src/
└── Kontaktlisteregisteret.Web/   # Blazor Server app (PoC: all-in-one)
    ├── Data/                     # EF Core + SQLite (domain models)
    ├── Services/                 # BrregService (IEntityRegistry adapter)
    │                               TargetGroupService (domain logic)
    ├── Pages/                    # Blazor pages
    └── wwwroot/                  # Static assets
```

## Running locally

**Prerequisites:** [.NET 8 SDK](https://dotnet.microsoft.com/download)

```bash
cd src/Kontaktlisteregisteret.Web
dotnet run
```

Open [http://localhost:5000](http://localhost:5000)

## Running in GitHub Codespaces

Click **Code → Codespaces → Create codespace**. The devcontainer starts the app automatically on port 5000.

## Adapters — Norwegian reference implementation

| Port | Adapter | Service |
|---|---|---|
| `IEntityRegistry` | `BrregService` | data.brreg.no |
| `IIdentityProvider` | *(not yet implemented)* | Ansattporten |
| `IMachineAuthProvider` | *(not yet implemented)* | Maskinporten |

## License

MIT
