# ADR-0007: Use manual mapping in the Application layer

- **Status:** Accepted
- **Date:** 2026-07-04
- **Deciders:** Project owner

## Context

With entities in the Domain layer and DTOs in the Application layer, something
must map between them (entity → response DTO, request DTO → entity). Per
[ADR-0005](0005-services-return-dtos.md) this mapping is an Application-layer
responsibility. The usual options are a convention-based mapper (AutoMapper), a
source generator (Mapperly), or hand-written mapping code.

## Decision

Mapping is done with **hand-written mapping code** in the Application layer —
small, explicit mapping methods/extensions (e.g. `machine.ToDto()`,
`request.ToEntity()`) kept alongside the DTOs.

## Alternatives considered

- **AutoMapper** — long the default, but it maps by runtime reflection and
  convention, turning a compile-time-checkable operation into "magic" that fails
  at runtime and is awkward to debug. It has also moved toward **commercial
  licensing**, which is undesirable for a portfolio/learning project.
- **Mapperly** — a compile-time **source generator** (MIT-licensed, no runtime
  reflection); a strong modern option.
- **Manual mapping** (chosen) — the DTOs here are simple, so explicit mapping is
  a few lines each. It has zero dependencies, is fully visible and debuggable,
  compiler-checked, and reads clearly to a reviewer.

## Consequences

### Positive

- No dependency and no licensing question.
- Mapping is explicit and compile-time safe; a missing field is a build error or
  plainly visible, not a silent runtime surprise.
- Easy to read and to unit-test.

### Negative / trade-offs

- More boilerplate as the number of entities/DTOs grows — acceptable at this
  scale, and revisitable later.

### Follow-ups

- If mapping boilerplate becomes significant, adopt **Mapperly** (source
  generator) as the free, compile-time replacement — recorded via a superseding
  ADR at that time.
