# Contributing

This repository is a published copy of the contract. The contract is maintained together with the
HotLayout builder and the readers, so pull requests that edit the artifacts or the fixtures here
cannot be merged as they are; open an issue instead and describe what a reader or a backend cannot
do with the current files.

What helps most:

- A document or a case that a reader draws or decides differently from the others. Send the input
  and the result you expected; a row for the shared case tables is the ideal report.
- A place where a rule is unclear. The contract is the source of truth: a rule that needs a
  paragraph of explanation is usually a rule that needs another row in a case table.
- Fixes to the sample backend in `backend/` and to the README.

A change to the contract arrives together with the case-table rows that pin it and the code in
every reader that implements it. Readers follow the contract, never the other way round.
