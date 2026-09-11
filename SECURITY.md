# Security

## Reporting

Please do not publish secrets, private SyncHost endpoints, account-specific paths or personal identifiers in public issues.

For a public vulnerability report, provide the minimum reproducible technical detail and redact local usernames, absolute paths, tokens and private hostnames.

## Security boundaries

StarSyncUniverse is a desktop client that may optionally:

- execute a user-supplied StarBreaker binary;
- read a user-supplied Star Citizen `Data.p4k` path;
- read a user-supplied SCUnpacked dataset;
- access public enrichment providers;
- connect to a user-configured SyncHost endpoint.

Treat all configured executable paths, local datasets and remote endpoints as untrusted input. Do not bundle credentials into source, settings defaults or release archives.

## Release hygiene

Before publishing a release:

1. scan the public tree for usernames, absolute developer paths and secrets;
2. verify no `Data.p4k`, private cache, DPAPI identity, bookmark or settings files are included;
3. build from the sanitized public tree;
4. publish checksums for binary archives;
5. review `THIRD_PARTY_NOTICES.md` when external providers or assets change.
