# HotLayout contract

HotLayout describes a mobile or web screen as data. A manager assembles the screen from
widgets in the HotLayout builder and publishes it; the result is a JSON document that
every HotLayout reader draws natively. This repository holds the contract those readers
implement, so that a reader (or a backend) can be written without the builder.

## What is in the contract

Five artifacts, each versioned on its own. A new widget does not change the palette, and
a rebrand does not add widgets.

| Artifact | What it describes | Version |
|---|---|---|
| [`screen-schema.json`](screen-schema.json) | the screen document: which widgets, fields and tokens exist (28 widgets) | 9.33.0 |
| [`theme.json`](theme.json) | the theme: what the tokens mean (colors, spacing, radii, shadows, text styles) | 4.0.1 |
| [`data-contract.json`](data-contract.json) | the data dictionary: which values a layout may bind to, and in which form | 1.8.0 |
| [`icons.json`](icons.json) | the icon set: the drawing behind every icon name (332 icons) | 1.0.0 |
| [`placements.json`](placements.json) | the placement map: which published document each named place of an app shows | 1.1.0 |

The schema, the data dictionary and the placement map are JSON Schema documents with `x-…` blocks that carry
the rules JSON Schema cannot express (layout rules, locale resolution, action catalog,
number and date formats). Readers take those blocks as data rather than re-deriving them.

## Versions

Every artifact carries `MAJOR.MINOR.PATCH`. The reader decides from it whether it understands a document:

- the major version must match the one the reader implements;
- a minor version adds only optional fields, so a reader of an older minor can still read a newer document and ignores what it does not know;
- the policy for everything else (unknown widget, unknown token, missing translation, …) is the `x-compatibility.policy` block of the artifact: one named decision per case.

## Shared case tables

Where the schema cannot say what a reader must do, the contract says it with a table of cases:
input and expected output, written by hand. Every reader runs the same tables and must pass every row,
which is how the readers are kept from drifting apart.

There are 36 tables in 34 folders under [`fixtures/`](fixtures), named `*-cases.json`, for example
[`fixtures/actions/action-cases.json`](fixtures/actions/action-cases.json) (action addresses, event order),
[`fixtures/template/template-cases.json`](fixtures/template/template-cases.json) (text templates, plurals, money),
[`fixtures/locale/locale-cases.json`](fixtures/locale/locale-cases.json) (locale resolution).

## Fixture documents

[`fixtures/`](fixtures) also holds 40 sample screen documents (`*-screens.json`, `*-screen.json`) and the themes they use
([`fixtures/themes/`](fixtures/themes)). They validate against the schema and are what the readers render in their own tests.

## Readers

| Platform | Package | Repository |
|---|---|---|
| Web (React) | npm [`@hotlayout/react`](https://www.npmjs.com/package/@hotlayout/react) | [hotlayout/hotlayout-react](https://github.com/hotlayout/hotlayout-react) |
| Flutter | pub.dev [`hotlayout_flutter`](https://pub.dev/packages/hotlayout_flutter) | [hotlayout/hotlayout-flutter](https://github.com/hotlayout/hotlayout-flutter) |
| iOS | Swift Package Manager, `https://github.com/hotlayout/hotlayout-ios` | [hotlayout/hotlayout-ios](https://github.com/hotlayout/hotlayout-ios) |
| Android | Maven Central, `com.hotlayout:hotlayout-android` | [hotlayout/hotlayout-android](https://github.com/hotlayout/hotlayout-android) |

## Backend

[`backend/`](backend) is for the team that stores and serves documents: an OpenAPI description
([`backend/openapi.yaml`](backend/openapi.yaml)), a README, an example of the user-state payload
and a sample implementation (C# models and a PostgreSQL schema). The sample shows one possible shape; it is not a required stack.

## Links

- Product: <https://hotlayout.com>
- Documentation: <https://hotlayout.com/docs>

## License

Apache License 2.0, see [LICENSE](LICENSE). Third-party material is listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
