# Design: Production Orders (v0.3)

Design-first specification for the Production Orders module. Locks decisions,
contracts, business-rule placement, and per-issue acceptance criteria **before**
any code is written.

> Status: Proposed &bull; Target: v0.3 &bull; Depends on: v0.2 (Machine Management)

---

## 1. Goals & what's new

Production Orders turns FactoryPulse from CRUD into a real domain. It introduces
concepts the Machine module did not:

- **Entity relationship** — `Machine (1) — (many) ProductionOrder`
- **Uniqueness** → first real use of `ErrorType.Conflict` (HTTP 409)
- **A lifecycle / state machine** → a **rich domain model** with enforced transitions
- **Cross-field & cross-aggregate rules**
- **Pagination** (`PagedResult<T>`) and **filtering**

---

## 2. Locked decisions

| # | Decision | Rationale |
|---|----------|-----------|
| D1 | **Rich domain model** for `ProductionOrder` (private setters + behaviour methods) | It has a real lifecycle; Rule 5 demands enforced transitions. New ADR. |
| D2 | Add **`Retired`** to `MachineStatus` | Rule 1 references a retired machine, which does not exist today. |
| D3 | **Audit fields** (`CreatedAt`/`UtcNow`, `UpdatedAt`) + all dates stored **UTC** | Consistency with `Machine` and existing convention. |
| D4 | **Navigation property** `ProductionOrder.Machine` (FK + nav); **no** reverse collection on `Machine` | Enables `Include`; keeps `Machine` lean. |
| D5 | Status changes only via **dedicated action endpoints** (start/complete/cancel), not a generic status field in PUT | RESTful; matches the rich model; makes invalid transitions impossible from the API shape. |
| D6 | **`Result` still governs expected outcomes**; the entity guards invariants | Honours [ADR-0006](../adr/0006-result-for-expected-outcomes.md). See §6. |
| D7 | `PagedResult<T>` in `Application/Common`; `pageSize` clamped to **max 100** | Reusable; prevents abusive page sizes. |

---

## 3. Domain model

### 3.1 `ProductionOrderStatus` (enum, stored as string)

```
Planned  →  Running  →  Completed        (terminal)
   └──────────┴──────→  Cancelled        (terminal)
```

| Value | Meaning |
|-------|---------|
| `Planned` | Created, not started (initial state) |
| `Running` | In production |
| `Completed` | Finished (terminal) |
| `Cancelled` | Cancelled (terminal — cannot be restarted, Rule 5) |

Stored as a string via `HasConversion<string>()`, per [ADR-0003](../adr/0003-store-enums-as-strings.md).

### 3.2 `ProductionOrder` entity (rich domain model)

**All setters private.** State is created and changed only through methods, so the
entity can never hold an invalid combination.

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `Guid` | PK, sequential GUID (EF) |
| `OrderNumber` | `string` | **unique** (DB index + service check) |
| `ProductName` | `string` | required, max 200 |
| `Quantity` | `int` | > 0 |
| `StartDate` | `DateTime` | UTC |
| `EndDate` | `DateTime?` | UTC; set on completion |
| `Status` | `ProductionOrderStatus` | starts `Planned` |
| `MachineId` | `Guid` | FK → `Machine` |
| `Machine` | `Machine?` | navigation property |
| `CreatedAt` / `UpdatedAt` | `DateTime` | UTC audit fields |

**Behaviour (the state machine):**

| Method | Allowed from | Effect |
|--------|--------------|--------|
| `Create(...)` (static factory) | — | new order, `Status = Planned` |
| `Start()` | `Planned` | → `Running` |
| `Complete(endDate)` | `Running` | → `Completed`, sets `EndDate` |
| `Cancel()` | `Planned`, `Running` | → `Cancelled` |
| `UpdateDetails(productName, quantity)` | `Planned`, `Running` | edit mutable fields |

**Transition guards (rule defined once):**

```
CanStart    => Status == Planned
CanComplete => Status == Running
CanCancel   => Status == Planned || Status == Running
```

Each behaviour method checks its guard and throws
`InvalidProductionOrderTransitionException` (a **domain exception** in
`Domain/Exceptions`) if violated. This guarantees the invariant. The **service**
checks the same `Can*` predicate first and returns a clean `Result` for the
expected user-facing case — so the exception is a backstop that should never fire
in normal operation (see §6).

### 3.3 `Machine` change (D2)

Add `Retired` to `MachineStatus`:
`Idle, Running, Maintenance, Down, Retired`. (Stored as string, so no data
migration — only the ProductionOrder table migration is needed.)

---

## 4. DTOs

**Response — `ProductionOrderDto`**
```
Id, OrderNumber, ProductName, Quantity, StartDate, EndDate?,
Status (string), MachineId, MachineName?, CreatedAt, UpdatedAt
```

**`CreateProductionOrderRequest`**
```
OrderNumber, ProductName, Quantity, StartDate, MachineId
```
(no Id/Status/EndDate/audit — Status starts Planned; over-posting protection)

**`UpdateProductionOrderRequest`** (mutable non-status fields only)
```
ProductName, Quantity
```
(OrderNumber is immutable; status changes via action endpoints, not here)

**`CompleteProductionOrderRequest`**
```
EndDate?   // optional; defaults to UtcNow if omitted
```

---

## 5. Validation (FluentValidation)

| Validator | Rules |
|-----------|-------|
| `CreateProductionOrderRequestValidator` | `OrderNumber` NotEmpty + MaxLength(50); `ProductName` NotEmpty + MaxLength(200); `Quantity` GreaterThan(0) **(R3)**; `StartDate` not default; `MachineId` NotEmpty |
| `UpdateProductionOrderRequestValidator` | `ProductName` NotEmpty + MaxLength(200); `Quantity` GreaterThan(0) |
| `CompleteProductionOrderRequestValidator` | (if `EndDate` supplied) not default; `EndDate >= StartDate` is checked in the **service** (cross-field vs the loaded entity) |

---

## 6. Business rules → where each lives

The core lesson of this sprint: rules live in different layers depending on what
they need.

| Rule | Layer | Mechanism | Error → HTTP |
|------|-------|-----------|--------------|
| **R3** Quantity > 0 | Validator | `GreaterThan(0)` | Validation → 400 |
| **R4** Completed ⇒ EndDate | Domain + Service | `Complete(endDate)` always sets `EndDate` (structural); service validates `EndDate >= StartDate` | Validation → 400 |
| **R2** OrderNumber unique | Service + DB | `OrderNumberExistsAsync` check → Conflict; unique index as hard backstop | Conflict → 409 |
| **R1** Not a retired machine | Service | load `Machine`; must exist and `Status != Retired` | Validation/Conflict → 400/409 |
| **R5** Cancelled cannot restart | Domain | `CanStart` false for `Cancelled`; service returns `InvalidTransition` | Conflict → 409 |

**Placement principle:** input-shape rules → **validator**; rules needing other
data (DB / other aggregates) → **service**; rules about an entity's own lifecycle
→ the **entity**.

**On ADR-0006 (Result vs exceptions):** expected user actions are validated by the
service and returned as `Result` failures (no exception). The entity's transition
guards throw only as an **invariant backstop** — if one ever throws, it means a
service check was bypassed (a bug), which the `GlobalExceptionHandler` turns into
a 500. In normal flow they never throw.

---

## 7. Contracts

### 7.1 `IProductionOrderRepository`
```
Task<ProductionOrder?> GetByIdAsync(Guid id, CancellationToken ct)          // Include(Machine)
Task<(IReadOnlyList<ProductionOrder> Items, int TotalCount)>
        GetPagedAsync(ProductionOrderQueryParameters query, CancellationToken ct)
Task<bool> OrderNumberExistsAsync(string orderNumber, CancellationToken ct)  // R2
Task AddAsync(ProductionOrder order, CancellationToken ct)
void Update(ProductionOrder order)
void Remove(ProductionOrder order)
Task<int> SaveChangesAsync(CancellationToken ct)
```

### 7.2 `IProductionOrderService`
```
Task<Result<ProductionOrderDto>>            GetByIdAsync(Guid id, CancellationToken ct)
Task<Result<PagedResult<ProductionOrderDto>>> GetPagedAsync(ProductionOrderQueryParameters q, CancellationToken ct)
Task<Result<ProductionOrderDto>>            CreateAsync(CreateProductionOrderRequest r, CancellationToken ct)
Task<Result<ProductionOrderDto>>            UpdateAsync(Guid id, UpdateProductionOrderRequest r, CancellationToken ct)
Task<Result<ProductionOrderDto>>            StartAsync(Guid id, CancellationToken ct)
Task<Result<ProductionOrderDto>>            CompleteAsync(Guid id, CompleteProductionOrderRequest r, CancellationToken ct)
Task<Result<ProductionOrderDto>>            CancelAsync(Guid id, CancellationToken ct)
Task<Result>                                 DeleteAsync(Guid id, CancellationToken ct)
```

### 7.3 Pagination & filtering (`Application/Common`)
```
PagedResult<T>  { IReadOnlyList<T> Items; int Page; int PageSize; int TotalCount; int TotalPages; }

ProductionOrderQueryParameters {
    int Page = 1;
    int PageSize = 20;          // clamped to [1, 100]
    ProductionOrderStatus? Status;
    Guid? MachineId;
    string? Product;            // contains-match on ProductName
}
```
The repository composes the query from the non-null filters; it must **not**
expose `IQueryable` upward (keeps [ADR-0004](../adr/0004-use-repository-pattern.md) intact).

---

## 8. API endpoints

| Method | Route | Body | Success | Notes |
|--------|-------|------|---------|-------|
| GET | `/api/orders?page=&pageSize=&status=&machineId=&product=` | — | 200 `PagedResult<ProductionOrderDto>` | pagination + filtering |
| GET | `/api/orders/{id:guid}` | — | 200 `ProductionOrderDto` | 404 if missing |
| POST | `/api/orders` | `CreateProductionOrderRequest` | 201 + Location | R1, R2, R3 |
| PUT | `/api/orders/{id:guid}` | `UpdateProductionOrderRequest` | 200 | edit ProductName/Quantity |
| POST | `/api/orders/{id:guid}/start` | — | 200 | R5 (transition) |
| POST | `/api/orders/{id:guid}/complete` | `CompleteProductionOrderRequest` | 200 | R4 |
| POST | `/api/orders/{id:guid}/cancel` | — | 200 | transition |
| DELETE | `/api/orders/{id:guid}` | — | 204 | |

Controller stays thin: call service → `result.Match(...)`. Reuses `ApiController.HandleFailure`
(the new `Conflict` errors map to 409 via the existing `ErrorType` switch — no controller change needed).

---

## 9. Errors catalog additions

```
Errors.ProductionOrder.NotFound            (NotFound)   → 404
Errors.ProductionOrder.DuplicateOrderNumber(Conflict)   → 409   (R2)
Errors.ProductionOrder.MachineNotFound     (Validation) → 400   (R1: machine doesn't exist)
Errors.ProductionOrder.MachineRetired      (Conflict)   → 409   (R1: machine retired)
Errors.ProductionOrder.InvalidTransition   (Conflict)   → 409   (R5 / illegal start/complete/cancel)
Errors.ProductionOrder.EndDateBeforeStart  (Validation) → 400   (R4)
```

---

## 10. Persistence

- **New table** `ProductionOrder` with FK `MachineId → Machine(Id)`, `OnDelete: Restrict`
  (don't let deleting a machine silently delete its orders).
- **Unique index** on `OrderNumber`.
- **Configuration** `ProductionOrderConfiguration : IEntityTypeConfiguration<ProductionOrder>`
  (lengths, `Status` as string, the relationship, the unique index).
- One EF migration adds the table. `MachineStatus.Retired` needs no schema change (string enum).

---

## 11. Issue breakdown (GitHub milestone "v0.3 — Production Orders")

Each issue = one `feature/<thing>` branch → PR → merge.

| # | Issue | Acceptance criteria |
|---|-------|---------------------|
| 1 | `feature/machine-retired-status` | `Retired` added to `MachineStatus`; builds; existing tests/endpoints unaffected |
| 2 | `feature/production-order-entity` | Entity (private setters), `ProductionOrderStatus`, `Create/Start/Complete/Cancel/UpdateDetails`, `Can*` guards, `InvalidProductionOrderTransitionException`; unit-testable in isolation |
| 3 | `feature/production-order-persistence` | EF config (FK, unique index, string status), migration created & `database update` succeeds; table visible in SQL |
| 4 | `feature/production-order-dtos` | `ProductionOrderDto`, `Create/Update/CompleteProductionOrderRequest` |
| 5 | `feature/production-order-mapping` | `ToDto()` / `ToEntity()` (or `Create` factory) manual mappings |
| 6 | `feature/paged-result` | `PagedResult<T>` + `ProductionOrderQueryParameters` (pageSize clamped) in Common |
| 7 | `feature/production-order-repository` | `IProductionOrderRepository` + impl (GetById w/ Include, GetPaged w/ filters, OrderNumberExists); registered in DI |
| 8 | `feature/production-order-validators` | Create/Update/Complete validators (R3, shapes); auto-registered |
| 9 | `feature/production-order-service` | Service returns `Result<...>`; enforces R1, R2, R4, R5; transitions via entity; logs business events |
| 10 | `feature/production-order-controller` | 8 endpoints; pagination + filtering; `Match` → HTTP; 201/200/204/400/404/409 verified in Swagger |
| 11 | `feature/production-order-errors` | Errors catalog entries (may fold into #9) |
| 12 | `feature/adr-rich-domain-model` | ADR recording D1 (rich domain model for aggregates with a lifecycle) |

Suggested order: 1 → 2 → 3 → (4,5,6,8 in any order) → 7 → 9 → 10 → 12.

---

## 12. Acceptance test walkthrough (end of sprint)

1. `POST /api/orders` with a valid body → **201**, status `Planned`.
2. `POST` same `OrderNumber` again → **409** DuplicateOrderNumber (R2).
3. `POST` with `Quantity: 0` → **400** (R3).
4. `POST` referencing a **retired** machine → **400/409** (R1).
5. `POST /orders/{id}/start` → **200** `Running`; call again → **409** InvalidTransition.
6. `POST /orders/{id}/cancel` then `/start` → **409** (R5, cannot restart).
7. `POST /orders/{id}/complete` (on a Running order) → **200** `Completed`, `EndDate` set (R4).
8. `GET /api/orders?page=1&pageSize=5&status=Running` → paged, filtered result.
9. Logs show `Created/Started/Completed/Cancelled order {OrderId}` business events.

---

## 13. New ADR to add

- **ADR-0009 — Use a rich domain model for aggregates with a lifecycle** (D1/D6):
  private setters + behaviour methods + invariant guards; expected outcomes still
  flow through `Result` at the service boundary.
