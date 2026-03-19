---
status: "accepted"
date: 2026-03-19
decision-makers: [ADR Portal Project Team]
consulted: []
informed: []
---
# MADR 4.0.0 as the ADR Document Format

## Context and Problem Statement

The ADR Portal must read, write, parse, and render ADRs for multiple repositories. A consistent, well-defined document format is required. Several ADR formats exist with varying structure, tooling support, and adoption. The format determines the YAML front matter schema, the markdown section headings the parser must understand, and the template presented to users when creating new ADRs.

## Decision Drivers

* Machine-parseable structure (YAML front matter) for status, date, and metadata
* Human-readable and editable outside the portal
* Active maintenance and community adoption
* Compatible with the MADR tooling ecosystem
* Extensible to support portal-specific fields (`global-id`, `global-version`) without breaking the base format

## Considered Options

* MADR 4.0.0 (Markdown Architectural Decision Records)
* Nygard format (original ADR format)
* Custom format

## Decision Outcome

Chosen option: "MADR 4.0.0", because it provides a well-structured YAML front matter block, clearly defined section headings, active maintenance, and explicit support for extension fields — all of which the portal parser and the global ADR library require.

### Consequences

* Good, because MADR 4.0.0 has a stable, published schema with YAML front matter that is straightforward to parse with YamlDotNet.
* Good, because the format is actively maintained and widely adopted in the ADR community.
* Good, because MADR explicitly allows additional YAML front matter fields, enabling portal-specific extensions (`global-id`, `global-version`) without violating the spec.
* Good, because the structured section headings (Considered Options, Pros and Cons) map directly to the AI evaluation workflow.
* Bad, because existing ADRs in repositories using Nygard or other formats must be migrated or will not be fully parsed.

### Portal-specific MADR extensions

The portal adds two optional fields to the standard MADR 4.0.0 front matter:

```yaml
global-id: "550e8400-e29b-41d4-a716-446655440000"  # assigned on acceptance; links to the global ADR library
global-version: 2                                   # last library template version this ADR was reviewed against
```

These fields are only present on accepted ADRs that have been registered in the global library.

### Rejected ADR placement

Rejected ADRs are moved to `docs/adr/rejected/` to keep the main directory clean while preserving history. Their `status` field is set to `"rejected"`.

## Pros and Cons of the Options

### MADR 4.0.0

* Good, because structured YAML front matter with a published schema.
* Good, because explicitly allows extension fields.
* Good, because section headings align with AI-assisted evaluation workflow.
* Bad, because more verbose than the minimal Nygard format.

### Nygard format

* Good, because minimal and simple — just a status line and free-form sections.
* Bad, because no standardised YAML front matter; machine parsing requires heuristics.
* Bad, because no structured "Considered Options" or "Pros and Cons" sections for AI to reason over.

### Custom format

* Good, because can be tailored exactly to portal requirements.
* Bad, because no community adoption; ADRs are not portable to other tools.
* Bad, because requires designing and maintaining a bespoke schema.

## More Information

* [MADR 4.0.0 specification](https://adr.github.io/madr/)
* [ADR-0005: File-Based ADR Storage](adr-0005-file-based-adr-storage.md)
* [ADR-0012: Global ADR Versioned Library](adr-0012-global-adr-versioned-library.md)
