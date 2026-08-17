---
doc_id: cloud-pattern-catalogue
title: Cloud pattern catalogue
type: index
version: 1.0.0
status: active
created: 2026-08-17
updated: 2026-08-17
owner: Brijesh Patel
change_summary: Initial catalogue. The forty-four patterns as retrieved on 2026-08-16, organised into six learning tiers, with the section order every pattern README follows.
---

# Cloud pattern catalogue

The contract for this repository. It records which patterns exist, what order they are best learned
in, and the shape every pattern README follows.

**A pattern whose name is plain text has not been implemented yet; one that links to a README is
done.** That is how the remaining work stays visible, so keep it accurate.

## The source, and why it is frozen

Retrieved from <https://learn.microsoft.com/en-us/azure/architecture/patterns/> on **2026-08-16**.
The page's own `ms.date` was 2026-05-03 and it had last changed on 2026-08-15.

**This is a dated snapshot, not a live view.** The summaries below are the catalogue's own wording,
reproduced as published rather than paraphrased, so that a later divergence between this file and
the source shows up as a visible difference instead of a silent falsehood. A claim about "the
forty-four patterns" with no date attached stops being true without anything reporting it.

The pillars are the Microsoft Well-Architected Framework pillars that the source assigns to each
pattern. They are **information, not classification** — a pattern commonly serves several, so they
do not partition the catalogue and are not used as categories here.

## How the tiers work

The source catalogue is one flat alphabetical table, which teaches nothing about what to read
first. These six tiers are ordered by **dependency**: each uses ideas the previous one established.

Every pattern sits in **exactly one** tier, so the tier is this repository's category, and each
pattern README names its tier as a subtitle directly beneath the title. `tools/tier_check.py`
enforces the partition.

| Tier | Category | Gloss | Patterns |
|---|---|---|---|
| 1 | Resilience | keeping a service working when its dependencies are not | 6 |
| 2 | Messaging | decoupling senders from receivers | 9 |
| 3 | Data management | storing and reading data at scale | 8 |
| 4 | Coordination | agreeing on outcomes across services | 6 |
| 5 | Edge and gateway | what sits between clients and services | 9 |
| 6 | Deployment and topology | where components run | 6 |

## Tier 1 — Resilience

Keeping a service working when its dependencies are not. Start here: everything later assumes a
call can fail.

| Pattern | What it does | Pillars |
|---|---|---|
| [Retry](../patterns/Retry/README.md) | Enable applications to handle anticipated temporary failures by retrying failed operations. | Reliability |
| [Circuit Breaker](../patterns/CircuitBreaker/README.md) | Handle faults that might take a variable amount of time to fix when an application connects to a remote service or resource. | Reliability, Performance Efficiency |
| [Throttling](../patterns/Throttling/README.md) | Control the consumption of resources from applications, tenants, or services. | Reliability, Security, Cost Optimization, Performance Efficiency |
| [Rate Limiting](../patterns/RateLimiting/README.md) | Avoid or minimize throttling errors by controlling the consumption of resources. | Reliability |
| [Health Endpoint Monitoring](../patterns/HealthEndpointMonitoring/README.md) | Implement functional checks in an application that external tools can access through exposed endpoints at regular intervals. | Reliability, Operational Excellence, Performance Efficiency |
| [Bulkhead](../patterns/Bulkhead/README.md) | Isolate elements of an application into pools so that if one fails, the others continue to function. | Reliability, Security, Performance Efficiency |

Retry comes before Circuit Breaker because a breaker is what you add once retrying stops helping.

## Tier 2 — Messaging

Decoupling senders from receivers. Every pattern here needs a queue, so the one that introduces
buffering comes first.

| Pattern | What it does | Pillars |
|---|---|---|
| [Queue-Based Load Leveling](../patterns/QueueBasedLoadLeveling/README.md) | Use a queue that creates a buffer between a task and a service to smooth intermittent heavy loads. | Reliability, Cost Optimization, Performance Efficiency |
| [Competing Consumers](../patterns/CompetingConsumers/README.md) | Enable multiple concurrent consumers to process messages that they receive on the same messaging channel. | Reliability, Cost Optimization, Performance Efficiency |
| [Priority Queue](../patterns/PriorityQueue/README.md) | Prioritize requests sent to services so that requests with a higher priority are processed more quickly. | Reliability, Performance Efficiency |
| [Publisher-Subscriber](../patterns/PublisherSubscriber/README.md) | Enable an application to announce events to multiple consumers asynchronously, without coupling senders to receivers. | Reliability, Security, Cost Optimization, Operational Excellence, Performance Efficiency |
| [Idempotent Consumer](../patterns/IdempotentConsumer/README.md) | Handle duplicate message delivery so that processing a message multiple times has the same effect as processing it once. | Reliability |
| [Sequential Convoy](../patterns/SequentialConvoy/README.md) | Process a set of related messages in a defined order without blocking other message groups. | Reliability |
| [Claim Check](../patterns/ClaimCheck/README.md) | Split a large message into a claim check and a payload to avoid overwhelming a message bus. | Reliability, Security, Cost Optimization, Performance Efficiency |
| [Messaging Bridge](../patterns/MessagingBridge/README.md) | Build an intermediary to enable communication between messaging systems that are otherwise incompatible. | Cost Optimization, Operational Excellence |
| [Asynchronous Request-Reply](../patterns/AsynchronousRequestReply/README.md) | Decouple back-end processing from a front-end host. This pattern is useful when back-end processing must be asynchronous, but the front end requires a clear and timely response. | Performance Efficiency |

## Tier 3 — Data management

Storing and reading data at scale, once more than one process wants the same data.

| Pattern | What it does | Pillars |
|---|---|---|
| [Cache-Aside](../patterns/CacheAside/README.md) | Load data on demand into a cache from a data store. | Reliability, Performance Efficiency |
| [Materialized View](../patterns/MaterializedView/README.md) | Generate prepopulated views over the data in one or more data stores when the data is poorly formatted for required query operations. | Performance Efficiency |
| [Index Table](../patterns/IndexTable/README.md) | Create indexes over the fields in data stores that queries frequently reference. | Reliability, Performance Efficiency |
| [Sharding](../patterns/Sharding/README.md) | Divide a data store into a set of horizontal partitions or shards. | Reliability, Cost Optimization |
| [CQRS](../patterns/CQRS/README.md) | Separate operations that read data from those that update data by using distinct interfaces. | Performance Efficiency |
| [Event Sourcing](../patterns/EventSourcing/README.md) | Use an append-only store to record a full series of events that describe actions taken on data in a domain. | Reliability, Performance Efficiency |
| [Static Content Hosting](../patterns/StaticContentHosting/README.md) | Deploy static content to a cloud-based storage service for direct client delivery. | Cost Optimization |
| [Valet Key](../patterns/ValetKey/README.md) | Use a token or key to provide clients with restricted, direct access to a specific resource or service. | Security, Cost Optimization, Performance Efficiency |

## Tier 4 — Coordination

Agreeing on outcomes across services, once a single operation spans more than one of them.

| Pattern | What it does | Pillars |
|---|---|---|
| [Compensating Transaction](../patterns/CompensatingTransaction/README.md) | Undo the work performed by a sequence of steps that collectively form an eventually consistent operation. | Reliability |
| [Saga](../patterns/Saga/README.md) | Manage data consistency across microservices in distributed transaction scenarios. | Reliability |
| [Scheduler Agent Supervisor](../patterns/SchedulerAgentSupervisor/README.md) | Coordinate a set of actions across distributed services and resources. | Reliability, Performance Efficiency |
| [Leader Election](../patterns/LeaderElection/README.md) | Coordinate actions in a distributed application by electing one instance as the leader. The leader manages a collection of collaborating task instances. | Reliability |
| [Choreography](../patterns/Choreography/README.md) | Let individual services decide when and how a business operation is processed, instead of depending on a central orchestrator. | Operational Excellence, Performance Efficiency |
| [Pipes and Filters](../patterns/PipesAndFilters/README.md) | Break down a task that performs complex processing into a series of separate elements that can be reused. | Reliability |

Saga follows Compensating Transaction because it is built on it, as the source catalogue states.

## Tier 5 — Edge and gateway

What sits between clients and services. Several of these are deployment topologies, and their
examples model the topology in process rather than deploying it.

| Pattern | What it does | Pillars |
|---|---|---|
| [Gateway Routing](../patterns/GatewayRouting/README.md) | Route requests to multiple services by using a single endpoint. | Reliability, Operational Excellence, Performance Efficiency |
| Gateway Aggregation | Use a gateway to aggregate multiple individual requests into a single request. | Reliability, Security, Operational Excellence, Performance Efficiency |
| Gateway Offloading | Offload shared or specialized service functionality to a gateway proxy. | Reliability, Security, Cost Optimization, Operational Excellence, Performance Efficiency |
| Backends for Frontends | Create separate backend services for specific frontend applications or interfaces. | Reliability, Security, Performance Efficiency |
| Gatekeeper | Protect applications and services by using a dedicated host instance to validate and sanitize requests before forwarding them to private back ends. | Security, Performance Efficiency |
| Ambassador | Create helper services that send network requests on behalf of a consumer service or application. | Reliability, Security |
| Anti-Corruption Layer | Implement a façade or adapter layer between a modern application and a legacy system. | Operational Excellence |
| Strangler Fig | Incrementally migrate a legacy system by gradually replacing pieces of functionality with new applications and services. | Reliability, Cost Optimization, Operational Excellence |
| Sidecar | Deploy components into a separate process or container to provide isolation and encapsulation. | Security, Operational Excellence |

## Tier 6 — Deployment and topology

Where components run. Last, because these are the least demonstrable in process and benefit most
from a reader who already knows the rest.

| Pattern | What it does | Pillars |
|---|---|---|
| Deployment Stamps | Deploy multiple independent copies of application components, including data stores. | Operational Excellence, Performance Efficiency |
| Geode | Deploy back-end services across geographically distributed nodes. Each node can handle client requests from any region. | Reliability, Performance Efficiency |
| Compute Resource Consolidation | Consolidate multiple tasks or operations into a single computational unit. | Cost Optimization, Operational Excellence, Performance Efficiency |
| External Configuration Store | Move configuration information out of an application deployment package to a centralized location. | Operational Excellence |
| Federated Identity | Delegate authentication to an external identity provider. | Reliability, Security, Performance Efficiency |
| Quarantine | Ensure that external assets meet a team-agreed quality level before the workload consumes them. | Security, Operational Excellence |

## What every pattern README contains

In this order. A README that reorders these has drifted from the contract.

| Section | Contains |
|---|---|
| Title and category | The pattern's name, and directly beneath it a subtitle naming its tier — `**Resilience**`, `**Messaging**`, `**Data management**`, `**Coordination**`, `**Edge and gateway**` or `**Deployment and topology**` — followed by the gloss this catalogue gives that tier |
| Intent | The sentence from this catalogue stating what the pattern does |
| Summary table | Tier, Well-Architected pillars, and a link to the source catalogue entry |
| The problem it solves | What goes wrong without it |
| When to use it | The conditions that make it the right choice |
| When not to use it | The conditions that make it the wrong one. Not optional |
| Architecture and components | The participants and how they fit, with a Mermaid diagram |
| Advantages and trade-offs | What it buys, and what it costs |
| Implementation considerations | What to get right, and what is easy to get wrong |
| Real-world cloud scenarios | Where this actually appears |
| In Azure | The real service the in-process model stands in for, **and what the model does not show** |
| What the tests assert | What the tests are about — **never how many there are** |

The duplication between this catalogue and each README is deliberate. A reader arriving at one
pattern folder should not have to open a second file to learn what the pattern is for, and a reader
comparing patterns should not have to open forty-four.

**Counts are not recorded in prose.** Nothing checks arithmetic, so a number written once drifts
silently from the moment the next test is added.

## The examples take no dependencies

Every demonstration runs in process, exits of its own accord, and references no packages: cloud
infrastructure is modelled by small fakes that live in the pattern that uses them.

That is a deliberate trade. It keeps the repository clonable and runnable anywhere with only the
.NET SDK, and it costs fidelity — which is why the **In Azure** section is mandatory in every
README and must say what its model does not show.
