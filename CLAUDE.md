# WaaoBackend

.NET 9 backend for the WAAO platform.

## Documentation (authoritative)

The full WAAO documentation lives in the collaborator's Obsidian vault, mirrored from the `WaaoDocs` repo.

**Path discovery**: run `waao-docs path` (or read `~/.config/waao-docs/config`). Typical: `<vault>/WAAO/`.

**For backend work, read in this order:**
1. `WAAO/WAAO.md` — root MOC
2. `WAAO/Backend/index.md` — backend MOC
3. `WAAO/Shared/architecture.md`
4. The specific project doc you need (one per `.csproj`):
   - `WAAO/Backend/waao-api.md`
   - `WAAO/Backend/waao-services.md`
   - `WAAO/Backend/waao-services-abstractions.md`
   - `WAAO/Backend/waao-domain-models.md`
   - `WAAO/Backend/waao-infra-ef.md`
5. Cross-cutting:
   - `WAAO/Backend/patterns.md`
   - `WAAO/Backend/api-conventions.md`
   - `WAAO/Backend/data-model.md`
   - `WAAO/Backend/testing.md`

**Sync policy**:
- Run `waao-docs status` at the start of a session
- If it reports `STALE`, run `waao-docs sync` before answering doc-grounded questions
- Default TTL is 24h — `waao-docs sync --force` to override

**Editing policy**:
- Docs are read-only on this side
- Edits happen in Obsidian → `publish-waao-docs` (author machine only)
- DO NOT modify files under `WAAO/` from this repo

## Solution layout (quick reference)
- `src/Waao.API` — HTTP layer → see `WAAO/Backend/waao-api.md`
- `src/Waao.Services` — business logic → see `WAAO/Backend/waao-services.md`
- `src/Waao.Services.Abstractions` — interfaces + DTOs → see `WAAO/Backend/waao-services-abstractions.md`
- `src/Waao.Domain.Models` — entities, enums, events → see `WAAO/Backend/waao-domain-models.md`
- `src/Waao.Infra.EF` — DbContext, migrations, repositories → see `WAAO/Backend/waao-infra-ef.md`
