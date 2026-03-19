# ADR portal

This project is to be a web app to handle Arhictecture Decision Records (ADR) and it's processes. Relevant links to review:

- https://adr.github.io/

- [Maintain an architecture decision record (ADR) - Microsoft Azure Well-Architected Framework | Microsoft Learn](https://learn.microsoft.com/en-us/azure/well-architected/architect-role/architecture-decision-record)

- [Architecturally significant requirements - Wikipedia](https://en.wikipedia.org/wiki/Architecturally_significant_requirements)

## Use cases

The solution must be able to:

- Give access to existing ADRs

- Give access to superseeded ADRs

- Give access to retired ADRs

- Give access to proposed ADRs

- Use AI (Copilot SDK?) to help find alternatives when evaulating options for a proposed ADR

- Use AI (Copilot SDK?) to help evaluate options on a proposed ADR and recommend one (taking existing ADRs into account)

- When working, it should be working on a given repo on disk and keep ADR records (all states) in a predictable/standard folder structure for that repo

- Be able to be pointed to one repo as source and one as target, and evaluate what ADRs in the source would be relevant to put in the target

- Monitor a folder and when .md files are added, treat these as propsed ADRs, keeping in mind that the ID used may need updating to fit the target repos IDs

- Keep track of what ADRs are being affected by changes to new ADRs.

- Handle updating, retiring, superseeding, approving, rejecting etc. ADRs.

## Tech choices

The portal should be built using Dotnet 10 and Blazor. Make sure to use the most up to date packages and project organization as recommended, like:

- [Central Package Management | Microsoft Learn](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management)

Also, make sure to use TUnit for automatic tests - NOT xUnit, NUnit etc.
















