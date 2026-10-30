# Security

This repository holds the HotLayout contract: JSON artifacts, sample documents, case tables and
a sample backend. The code that reads a document lives in the readers, which treat every
document, theme and data value as untrusted input; each reader describes its limits in its own
repository (see the Readers table in the README).

If you believe you have found a vulnerability in the contract, in a reader or in the sample
backend, write to contact@hotlayout.com. Please do not open a public issue for it. We confirm
receipt within two working days and agree on a disclosure date with you.
