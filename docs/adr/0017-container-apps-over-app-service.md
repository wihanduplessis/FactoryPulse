# ADR-0017: Azure Container Apps over App Service

- **Status:** Accepted
- **Date:** 2026-07-12
- **Deciders:** Project owner

## Context

v0.5 produced a container image. v0.7 needs something in Azure to run it, reachable
over HTTPS. The realistic candidates were **App Service (Linux containers)** and
**Azure Container Apps**.

This is the one decision in the milestone where cost and CV-keyword-matching pull in
opposite directions, so it is worth stating plainly rather than defaulting.

## Decision

**Azure Container Apps**, Consumption plan, **scale-to-zero**, **single revision**.

| | App Service B1 | **Container Apps** |
|---|---|---|
| Cost | ~$13/month, always on | **~$0** — free monthly grant, and idle apps consume nothing |
| Appears in job ads | More often | Less often |
| Cold start | None | **A few seconds** after an idle period |
| Container-native | Retrofitted | Native |

### Why the cheaper, less-advertised option

An honest reckoning: App Service is named in more job adverts, and choosing it would
have been the "safe" CV play. It was rejected anyway.

- The workload is a **portfolio API that is idle almost all of the time** and receives
  a handful of requests when someone clicks the link. Paying $13/month for an
  always-warm instance to serve that is paying for nothing.
- Scale-to-zero means the compute is genuinely free, which is what allows the project
  to stay live for the whole job hunt on a ~$5/month bill.
- **"I chose Container Apps over App Service because scale-to-zero suits an
  intermittently-used API, and here is the ADR"** is a stronger interview answer than
  having used the more common service without having thought about it. The knowledge
  transfers; App Service can be discussed fluently either way.

### Supporting decisions

- **`minReplicas: 0`** — the app switches itself off when idle. The cost is a cold
  start of a few seconds on the first request. For a demo link, irrelevant.
- **`maxReplicas: 1`** — **load-bearing, not laziness.** Two replicas would mean two
  independent in-memory rate limiters each enforcing their own budget (ADR-0015), and
  two instances racing to seed the admin user on startup. One replica sidesteps both.
  A real system would use a distributed rate limiter and seed from the pipeline.
- **`activeRevisionsMode: 'Single'`** — Container Apps can split traffic across
  revisions (its equivalent of App Service's deployment slots). We deliberately do not:
  one environment, one user, and a second live revision doubles the compute. Recorded
  precisely *because* it is a capability we are declining.
- **Image pinned to the commit SHA**, never `latest`. "What is running in production?"
  always has an exact answer.
- **Ingress terminates TLS**, so the app serves plain HTTP inside
  (`UseHttpsRedirection=false`) and reads the true client IP from `X-Forwarded-For`
  (`UseForwardedHeaders=true`) — without which the rate limiter would put every caller
  on earth into a single partition.

## Alternatives considered

- **App Service B1** — see above. Rejected on cost, and on the quality of the story.
- **App Service F1 (free)** — does not support custom containers. Rules itself out.
- **AKS** — absurd for one container, and expensive.
- **Azure Container Instances** — no managed ingress, no scale-to-zero worth the name.

## Consequences

### Positive

- Compute costs approximately nothing; the whole environment runs at ~$5/month (ACR).
- Free managed TLS on an `*.azurecontainerapps.io` hostname — no certificate to buy,
  renew, or forget.
- Pulls from ACR with a **managed identity** (ADR-0018) — no registry credentials.
- Deploying is a Bicep parameter change (`imageTag`), so CI's deploy step is one command.

### Negative / trade-offs

- **Cold starts.** First request after idle waits for the container to start *and* for
  the serverless database to resume — potentially tens of seconds. Acceptable for a
  portfolio; unacceptable for a real product, and the fix (minimum one replica, no
  auto-pause) costs money.
- **Single replica means no high availability.** A deployment is a brief interruption.
  Deliberate.
- App Service appears in more job specs, and this project therefore does not
  demonstrate it. Accepted — with an argument, which is the point.
