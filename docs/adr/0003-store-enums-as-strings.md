# ADR-0003: Store enum values as strings in the database

- **Status:** Accepted
- **Date:** 2026-07-04
- **Deciders:** Project owner

## Context

Domain entities use C# enums for small, fixed sets of business states — the first
being `MachineStatus` (`Idle`, `Running`, `Maintenance`, `Down`). EF Core must map
these to a column. By default it stores the enum's **integer** value. The
readability of the stored data and its resilience to future code changes are both
relevant, since this is a demonstration project where the database will be
inspected by hand.

## Decision

Enum properties will be persisted as **strings** via EF Core's
`.HasConversion<string>()` in the entity configuration (e.g. `MachineStatus` is
stored as `nvarchar(50)` holding `'Running'`, `'Down'`, etc.). The C# code
continues to work with the strongly-typed enum; the conversion is transparent.

## Alternatives considered

- **Integer storage (EF default)** — compact (4 bytes) and fast to compare, but
  the stored value is opaque (`3`) and, critically, **fragile**: reordering or
  inserting an enum member silently changes the meaning of existing rows.
- **String storage** — chosen: self-documenting in the database and robust to
  enum reordering, because the stored name maps back regardless of numeric value.
- **Lookup/reference table with a foreign key** — the most normalized option;
  gives DB-enforced referential integrity and a place for status metadata, but
  adds a table, seed data, and joins. Overkill for a small fixed enum with no
  extra attributes today.

## Consequences

### Positive

- The database is human-readable during debugging and ad-hoc queries.
- Safe to reorder or insert enum members without corrupting stored data.
- No schema ceremony (no extra table/seed) compared to a lookup table.

### Negative / trade-offs

- Slightly larger storage and marginally slower comparisons than an `int` —
  negligible at this scale.
- No database-level guarantee that the string is a valid enum member (the
  application enforces validity).

### Follow-ups

- If a status ever needs to carry metadata (display colour, "counts as
  downtime" flag) or requires DB-enforced integrity, promote it to a lookup
  table in a superseding ADR.
