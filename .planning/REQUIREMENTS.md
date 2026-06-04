# Requirements: PersonsAPI

**Defined:** 2026-06-02
**Core Value:** A correctly layered, richly modeled API that proves Clean and Hexagonal Architecture work together — where the domain drives everything and infrastructure is a detail.

## v1 Requirements (Completed — v1.0)

All v1.0 requirements were validated and shipped. See `.planning/milestones/v1.0-milestone/` for archive.

### Architecture

- ✓ **ARCH-01**: Rich Person domain model with calculated Age from DateOfBirth — v1.0
- ✓ **ARCH-02**: Clean Architecture layer separation: Domain → Application → Infrastructure → Api — v1.0
- ✓ **ARCH-03**: Hexagonal Architecture: ports in Domain/Application, adapters in Infrastructure and Api — v1.0
- ✓ **ARCH-04**: Controllers with proper HTTP semantics (not Minimal API) — v1.0
- ✓ **ARCH-05**: CQRS via Mediator.SourceGenerator 3.0.2 with FluentValidation pipeline behavior — v1.0

### Persistence

- ✓ **PERS-01**: EF Core InMemory persistence adapter (PersonDbContext + PersonRepository) — v1.0
- ✓ **PERS-02**: Seeded in-memory data for immediate testing (3 persons via DataSeeder) — v1.0
- ✓ **PERS-03**: IPersonRepository port in Application layer — v1.0

### API

- ✓ **API-01**: Full CRUD + PATCH operations (GET all, GET by id, POST, PUT, PATCH, DELETE) — v1.0
- ✓ **API-02**: RFC 9457 Problem Details for all error responses — v1.0
- ✓ **API-03**: OpenAPI documentation + Scalar interactive UI — v1.0

## v2 Requirements (Current — v2.0)

Requirements for cloud deployment milestone. Each maps to roadmap phases.

### Observability

- ✓ **OBS-01**: Developer can see structured JSON logs from the running API in Google Cloud Logging — Phase 5 (2026-06-03)
- ✓ **OBS-02**: `/health` endpoint returns HTTP 200 OK and enables Cloud Run liveness probe — Phase 5 (2026-06-03)

### Docker

- [x] **DOCK-01**: Developer can build the API into a Docker image from the solution root using `docker build`
- [x] **DOCK-02**: Developer can run the full API locally with `docker compose up` and reach all endpoints at port 8080

### Cloud Run

- [ ] **CLOUD-01**: API is publicly reachable at a Google Cloud Run HTTPS URL after manual deployment

### CI/CD

- [ ] **CICD-01**: Every push to `main` automatically triggers build → tests → push to Artifact Registry → deploy to Cloud Run via GitHub Actions

## v3 Requirements (Deferred)

Acknowledged but deferred to future milestone. Not in current roadmap.

### Persistence

- **PERS-NEW-01**: Developer can persist person data across API restarts (real database migration)
- **PERS-NEW-02**: Integration tests use EF Core SQLite in-memory (not production DB)

### Security

- **SEC-01**: User cannot create, update, or delete persons without a valid JWT token
- **SEC-02**: Admin role can perform all operations; reader role can only perform GET operations

### Observability (advanced)

- **OBS-03**: Cloud Run logs show INFO/WARNING/ERROR severity icons in Google Cloud Console
- **OBS-04**: Separate liveness (`/health/live`) and readiness (`/health/ready`) endpoints

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Cloud SQL (real database) | EF InMemory retained for v2.0; database persistence is v3 scope |
| Workload Identity Federation (WIF) | Service account JSON key is acceptable for learning milestone; WIF adds GCP org setup complexity |
| Alpine base image | Debian is the safe default; Alpine has known culture/globalization issues with DateOnly |
| `Serilog.Sinks.GoogleCloudLogging` | stdout JSON is sufficient for Cloud Logging ingestion; GCP-native sink only needed for severity icons |
| Kubernetes / GKE | Cloud Run is the correct target; K8s adds unnecessary operational complexity |
| Minimal API endpoints | Explicitly excluded — controllers only |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| OBS-01 | Phase 5 | Pending |
| OBS-02 | Phase 5 | Pending |
| DOCK-01 | Phase 6 | Complete |
| DOCK-02 | Phase 6 | Complete |
| CLOUD-01 | Phase 7 | Pending |
| CICD-01 | Phase 8 | Pending |

**Coverage:**

- v2 requirements: 6 total
- Mapped to phases: 6
- Unmapped: 0 ✓

---
*Requirements defined: 2026-06-02*
*Last updated: 2026-06-02 after v2.0 milestone start*
