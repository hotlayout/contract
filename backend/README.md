# Backend agreement

This folder is for the team that stores and serves HotLayout documents. It says what a backend has to do, and gives one example of each part (OpenAPI, C# models, SQL, a sample payload). The same material is described page by page at <https://hotlayout.com/docs>.

## In one minute: what this is for

Normally every screen of an app is drawn in its code. Moving a badge or adding a block with terms takes a developer, a build and a release to the App Store and Google Play — weeks of waiting, while the product team wants to change promotions every month.

HotLayout works differently: **a screen is described by data, not by code**. A manager assembles it in a visual builder and presses **Publish**, and the app shows the new screen without a store update.

The result of the builder is plain JSON, which we call the **document**. It has to be stored somewhere, served to the app and, when needed, rolled back. That is what is asked of the backend.

Who takes part:

| Part | Stack | Role |
|---|---|---|
| Builder | web | a manager assembles the screen; the output is a document |
| **Backend** | **yours — any stack** | **stores documents, serves them and the data** |
| Renderer (SDK) | Flutter, React, iOS, Android | reads the document, merges it with the data, draws the screen |

The protocol is plain HTTP and JSON, so the language of the backend does not matter. The examples in this folder are written in C# and PostgreSQL because some language had to be chosen: they are ONE example of the shape, not a stack requirement.

## Reading order

1. **This file** — boundaries, models, endpoints, the questions at the end.
2. **`openapi.yaml`** — import it into Swagger and look at the shape of the requests.
3. **`dotnet/UiDelivery.Models.cs`** — the models as one example in C#; at the end of the file is the rule for picking a compatible version, the most important logic of the whole delivery.
4. **`examples/user-state.json`** — the exact shape of the data the renderer reads.

The remaining artifacts (`screen-schema.json`, `data-contract.json`) are for code rather than for reading: the first validates a document, the second lists the fields.

---

This file has two halves:

- **what already exists on your side** — the data endpoints; a document only names their fields by path and changes nothing in their shape;
- **what has to be agreed** — the questions at the end; it is better to answer them before you write code.

The rest (storing documents, publishing, versions) is new work.

If something in the shape is awkward for your stack, say so. The specification is not auto-generated, so changing it is cheap until the code is written.

## What is in this folder

| File | What it is |
|---|---|
| `openapi.yaml` | **the endpoint specification** — import it into Swagger; controllers can be generated from it |
| `dotnet/UiDelivery.Models.cs` | the models as one example (C#): entities, responses, the compatible-version rule |
| `dotnet/schema.sql` | the tables as one example (PostgreSQL; for SQL Server see the note at the top of the file) |
| `examples/user-state.json` | an example of the user-data response in its real shape |
| `../data-contract.json` | the machine-readable data dictionary |
| `../screen-schema.json` | the document schema, for validation at publication |

The models are not finished code but **the shape of an agreement**: the names, types and nullability that the builder and the renderer rely on. Language, namespace, base classes and style are up to you.

---

## Three artifacts to take into your build

| File | What it describes | What the backend needs it for |
|---|---|---|
| `screen-schema.json` | which blocks and fields a document can have | validating a document at publication |
| `theme.json` | the values of the product theme tokens | validating the theme, if it is edited |
| `data-contract.json` | **which data is promised to a document** | knowing what exactly to return to the renderer |

All three are plain JSON Schema plus metadata. Any .NET validator will do (`JsonSchema.Net`, `NJsonSchema`).

The artifacts are **generated**: do not edit them by hand. They arrive together with a new version of the contract.

---

## The main boundary: document ≠ data

Two independent resources with different lifetimes.

**The document** is a form: a title here, a progress bar under it, a list under that. It is the same for all users, changes rarely and is cached for a long time.

**The data** is what gets filled into the form for a particular person. It differs for everyone, changes constantly and is cached briefly.

A document contains no values. Instead of "150 000" it holds a reference to a field:

```json
{ "kind": "binding", "path": "task.title", "fallback": "—" }
```

The renderer takes the document from one endpoint and the data from another, and merges them on the phone.

---

## The data dictionary mirrors the real API

`data-contract.json` lists everything a document has the right to ask for. The one in the parent folder is a **demonstration**: it shows the shape, not your set of fields. You replace it with your own for your product — the builder reads the dictionary at runtime.

**Field names in the dictionary must match the backend response verbatim.** The temptation is to give them "friendly" names (`task.name` instead of what the API really returns). That would cost a mapping layer between the response and the document — a third place where knowledge about fields lives, and the first one to drift. A manager has a readable name anyway: every path in the dictionary has a `label`.

The demonstration dictionary is taken from a typical set of endpoints:

| Endpoint | Collection in the dictionary | Entity |
|---|---|---|
| `GET /users/me/tasks` | `tasks` | `task` |
| `GET /users/me/task-groups` | `taskGroups` | `taskGroup` |
| the same, nested array | `taskGroup.tasks` | `task` |
| `GET /catalog/items` | `items` | `item` |
| `GET /offers` | `offers` | `offer` |

Task groups are the case for which a repeating list allows **two levels of nesting**: a group repeats in a list, and tasks repeat inside the group. A third level is not allowed.

The dictionary has two sections, and the difference between them matters.

### `paths` — single values

A flat map "path → value" for the current user:

```json
{
  "user.firstName": "Alex",
  "user.points": 1250,
  "campaign.title": "Autumn rewards"
}
```

This is how values that belong to no list travel. Task fields (`task.title` and the rest) are possible in this form too — then they describe ONE task, for example on a details screen.

Types: `string`, `number`, `date` (an ISO string), `boolean`.

**`date` is a MOMENT in time**: an ISO 8601 string with time and zone (`2026-10-31T21:59:59.000Z` or `…+02:00`). A screen can show it as a date or a time, in the user's language and in the time zone of the user's DEVICE, so a string without a zone ("2026-10-31") would be read differently by different readers. If a promotion has to end at 23:59 Kyiv time, send exactly that moment.

A missing value is `null` or an absent key. Do not send an empty string in its place: every binding in a document has its own `fallback`, and the fallback decides what is shown.

### `collections` — lists

An array of records. **Record keys carry no entity prefix:**

```json
{
  "tasks": [
    { "title": "Spend 500 with your card", "progress": 3, "completed": false },
    { "title": "Set up a recurring payment", "progress": 1, "completed": true }
  ]
}
```

That is **exactly what your list endpoint already returns** — nothing has to change.

Why no prefix: the collection `tasks` declares that its records are the entity `task`, and inside the list the document reads `task.title`. The renderer adds the prefix when it puts a record into scope. The backend returns plain fields.

A nested collection works the same way: a group carries its own `tasks` array, and inside it the document reads `task.title`.

**Give every record a field that identifies it** (`id`). A list row opens its details screen with ITS OWN record, and every action the app receives carries that record (`item.id`, `item.name`, …). When the user presses "Redeem", the app sends you the `id` from the record; a record without one cannot be named. Treat that `id` as user input: check that this very user is allowed to take this very action on this very record.

**A link in the data is a ready-made `https` address.** The "open a web page" action can take its address from the data (`action.paramBindings`: "parameter `url` — from the path `offer.url`"). The reader CHECKS such an address by its form before handing it to the app, and discards anything that does not match: the address must start with `https://` in lower case, have no user name before the host, no spaces and no non-ASCII characters (non-Latin letters in the path percent-encoded, the domain in punycode), and be at most 2048 characters. A relative address, `http://` or a storage key will not be opened by the app — the action reaches it marked "invalid". The value must be a STRING: a number or `null` counts as absent. The same holds for an app link (`myapp://offers/42`) in the "open an app link" action. Name such a path in the data dictionary with the type `string`.

### Input fields: what the screen WRITES

A screen does not only show; it can ask — a text field, a switch, a checkbox. Each field is bound to a PATH of the data dictionary — the same kind of path you use to return values — and writes what the person entered under it. The sample dictionary has example paths for this (`form.email`, `form.agreed`, `settings.notifications`, …); in your own dictionary name a path for every answer a screen collects.

What follows for the backend:

- **You can supply the value under a field's path in advance.** The field shows what is under its path: an email from the profile, the current state of a setting. The type is exactly the type of the path: a **string** for a text field (a number or `null` is shown as an empty field and is not converted to text), a **boolean `true` / `false`** for a switch and a checkbox (the string `"true"` and the number one mean "off"). An empty string and an omitted key both mean an empty field.
- **The writes go to the app, not to you** — and on EVERY change: every letter in a text field, every press of a switch. The renderer does not wait for focus loss and makes no pauses. How much of this reaches you is decided by the app: we advise sending text after a pause in typing or together with an action ("Save"), and a settings switch at once. The app receives a pair "path, value": a string from a text field, a boolean from a switch.
- **You do the validating.** A document has no "required" or "email format" rule in the form — only a place for the ERROR TEXT under a field, bound to a string path (in the sample dictionary, `form.phoneError`). When you have checked a write, put the message in the user's language under that path in the response; a non-empty string puts the field in the error state, an empty or omitted one clears it. The field limits the text length itself (`maxLength`, in Unicode code points), but that is a convenience, not a defence: it cannot be trusted, like anything that came from a client.
- **Do not trust a path any more than a form field.** The path is named by a document, and a document arrives over the network. The app limits which paths a document may write to at all; you should accept only the ones you expect, with the type you expect.
- **A document does not ask for a password, an SMS code or a card number**: there is deliberately no such field type in the contract. If you need such a step, it is a screen of the app, not of the document.

### A survey form: required fields and numbers

A field can be REQUIRED, a button can stay disabled until the required fields of its screen are filled, and a text field can be NUMERIC. The sample dictionary has example paths `survey.*` for this (among them `survey.age` of type `number`).

- **A numeric field writes a NUMBER** (a JSON number) under a path of type `number`: `12`, `36.6`. A comma and a dot typed in the field are both a decimal separator; there are no digit groups and no exponents.
- **An empty string `""` under a number path means "there is no number"**: the person cleared the field or typed something that is not a number ("12a"). Do not turn it into zero and do not reject it as a broken type — it is a legal write.
- **An older renderer (before the numeric field existed) writes a STRING under a number path** ("12"): it does not know the `valueType` field. While such apps are in people's hands, accept a string of digits under a numeric path too, or do not publish a document with a numeric field.
- **You can supply a number under the path in advance** — as a number: the string `"12"` is shown by a numeric field as empty.
- **"Required" is a convenience, not a check.** A document button will not let a new app send an empty form, but an older app does not read that field, and the document arrives over the network. Validate everything you receive as before; the format ("looks like an email", "at least 18") is not checked by a document at all — report the result as the error text (`error`).
- **A field also counts as filled by your value**: if you supply a name under the path of a required field, the button is enabled at once.
- **The value of an action parameter is a non-empty string only.** A checkbox and a number (including one from a numeric field) do not become an action parameter: the app receives the literal written in the document.

A full example response is in `examples/user-state.json`.

### Three delivery rules

They are written machine-readably in the `x-delivery` block of the artifact, so that nobody has to agree on them in words:

- **`maxCollectionItems: 50`** — the renderer will not draw more. You may return as many as you like, but the rest is cut off; if a list is longer, you need pagination at the product level, not "we'll return everything".
- **`absentCollection: "empty"`** — an absent collection counts as **empty**: the `isEmpty` condition holds both on an empty array and on an omitted key, so the empty state ("no active tasks") appears in both cases. It is the same rule as `missingConditionValue: "absent-is-empty"` in the widget schema. We still advise returning `"tasks": []`: that way the response says "the list exists and it is empty" rather than leaving it to silence.
- **`missingValue: "null-or-absent"`** — see the note on `fallback` above.

---

## What to store

This section describes a backend that also owns the authoring side — drafts, publication and rollback. A backend that only receives published documents by [webhook](#webhook-how-to-accept-a-publication) needs just one table of published versions, keyed by `documentId` and `stamp`, and no drafts: the builder keeps drafts and the publication history itself.

Two entities. The theme follows the same model but separately — it has its own version.

### Document

| Field | Type | Note |
|---|---|---|
| `id` | `Guid` | |
| `key` | `string` | a stable key by which the renderer asks for the document |
| `name` | `string` | a human name for the admin |
| `draftBody` | `jsonb` / `nvarchar(max)` | the draft; changes every minute |
| `draftUpdatedAt` | `DateTimeOffset` | |
| `draftUpdatedBy` | `Guid` | |
| `publishedVersionId` | `Guid?` | a pointer to the published version |

### Document version — **immutable**

| Field | Type | Note |
|---|---|---|
| `id` | `Guid` | |
| `documentId` | `Guid` | |
| `schemaVersion` | `string` | semver taken from the document itself, e.g. `9.28.0` |
| `body` | `jsonb` / `nvarchar(max)` | a copy at the moment of publication |
| `bytes` | `int` | |
| `publishedAt` | `DateTimeOffset` | |
| `publishedBy` | `Guid` | |
| `note` | `string?` | a comment on the publication |

A row is never updated. A rollback means moving `publishedVersionId` to an earlier row, not correcting an existing one. The audit trail comes from the same place: who released what, and when.

**Draft and publication are different fields, not a status of one record.** A single record with a status would make every autosave a potential release.

**Store `body` as whole JSON.** Breaking the screen tree into relational tables is not worth it: a migration on every contract change, slow queries, and all of that while a document is always read as a whole. The size is limited to 1 MB (`x-budget` in the schema).

---

## Endpoints

### Admin — a staff token, with a role check

```
GET    /admin/ui-documents                     list of documents
GET    /admin/ui-documents/{id}                draft + details
PUT    /admin/ui-documents/{id}/draft          autosave (If-Match: ETag)
POST   /admin/ui-documents/{id}/publish        validation → version → pointer
GET    /admin/ui-documents/{id}/versions       history
POST   /admin/ui-documents/{id}/rollback       to an earlier version
GET    /admin/ui-contract/data-fields          return the data dictionary
GET    /admin/ui-theme                         the product theme
```

Autosave uses `If-Match` with the ETag and answers `409` on a mismatch: otherwise two tabs would silently overwrite each other's work.

Publishing **must validate the document with the schema on the backend**. The builder validates too, but it runs in the browser of a staff member, and relying on it as the only barrier is not acceptable.

The schema makes three demands of the validator itself that an incomplete JSON Schema implementation silently "does not see" — check them with one test on your validator:

- `propertyNames` with `pattern: "^(?!__proto__$)"` — in `themes` (the theme) and `action.params` (the document): an own key `__proto__` is rejected. The pattern uses a negative lookahead (ECMA-262); a validator that does not support it lets the key through;
- `multipleOf: 100` for `fontWeight` (in the theme and in a font override of a document): a validator that ignores `multipleOf` lets the weight 450 through;
- `const: 0` for `spacing.none` and `radius.none`, and `maxItems: 0` for `elevation.none` in the theme — the "zero of the scale" (`x-tokens.zeroScale` in the widget schema).

The schema cannot express the depth of the tree: reject a document deeper than 32 levels before parsing it, with an iterative walk (legal depth is up to eight). The remaining rules that JSON Schema cannot express (a reference to a token that is not in the project theme; a path that is not in the dictionary) are checked by the validator of the builder — see `x-rules` in `theme.json`.

### Client — read by the renderer

```
GET /v1/ui/documents/{key}?schemaVersion=9.28.0   published document
GET /v1/ui/theme?themeVersion=4.0.1               theme
GET /v1/ui/state                                  user data
```

The last one is your existing data endpoint under any name. A document requires neither its address nor its shape: it names PATHS, and which of them are collected from where is decided by the app.

---

## Webhook: how to accept a publication

If the webhook is turned on in the project settings, after every publication a `POST` with the document arrives at your address. You store it and serve it to the app from your own servers — our server does not see the data of your users.

### What arrives

```
POST <your address>
content-type: application/json; charset=utf-8
x-webhook-event: document.published
x-webhook-delivery: <documentId>/<stamp>
x-webhook-signature: t=<seconds>,v1=<hex>
user-agent: Hotlayout-Webhook/1
```

`user-agent` is always the same: a constant, so that a firewall rule can let our request through. It is not proof of origin — only the signature proves that.

```json
{
  "version": 1,
  "event": "document.published",
  "deliveredAt": "2026-10-07T20:00:05.000Z",
  "orgId": "…",
  "projectId": "…",
  "documentId": "reward-catalog",
  "stamp": "2026-10-07T20-00-00-000Z",
  "document": { "schemaVersion": "9.28.0", "…": "…" },
  "dataContract": {
    "paths": [{ "path": "client.name", "type": "string", "label": "Client", "sampleValue": "Olena" }],
    "collections": [{ "path": "orders", "label": "Orders", "item": "order" }]
  }
}
```

`document` is the published document as is: store it without transformations. `stamp` identifies this very publication; together with `documentId` it is the key of the version.

`dataContract` is the project's data dictionary in effect at the moment of delivery: the same `paths` and `collections` as in the `data-contract.json` file (the section on the dictionary above). Your backend serves values at exactly these paths, so you see a new field of the dictionary in the same request that brought the screen bound to it. `null` means that the project has no dictionary of its own and the standard one is in effect — or that the saved dictionary cannot be read. The key was added without changing `version`: it is only an addition. **Skip body keys you do not know** — future keys will be added the same way.

### Verify the signature — a webhook must not be accepted without it

Anyone can guess a webhook address. Only the signature proves that a request is from us: HMAC-SHA256 with the secret shown once when the webhook is turned on. The key is the whole secret string, including its `whsec_` prefix.

1. Take the numbers `t` and `v1` from the `x-webhook-signature` header.
2. Compute HMAC-SHA256 of the string `<t>.<request body>` with your secret. The body is **the bytes as they arrived**, not JSON that was parsed and composed again: a serializer changes whitespace or key order, and the signature will not match.
3. Compare the result with `v1` using a **constant-time comparison**.
4. Reject the request if `t` is more than five minutes away from your clock: without this an intercepted request could be sent again a week later.

```ts
import { createHmac, timingSafeEqual } from 'node:crypto'

function isOurs(rawBody: string, header: string, secret: string, nowMs = Date.now()): boolean {
  const match = /^t=(\d+),v1=([0-9a-f]{64})$/.exec(header)
  if (match === null) return false
  const [, seconds, digest] = match
  if (Math.abs(nowMs / 1000 - Number(seconds)) > 300) return false

  const expected = createHmac('sha256', secret).update(`${seconds}.${rawBody}`).digest('hex')
  return timingSafeEqual(Buffer.from(expected), Buffer.from(digest))
}
```

The signature scheme is the same as Stripe's — if you already have code that verifies such webhooks, it will fit.

### Answer quickly and do not fear repeats

- **`2xx` within ten seconds** — delivered. Do the heavy work after responding.
- **`5xx`, `429`, `425`, `408`, a dropped connection or a timeout** — we retry: three attempts in total, with pauses of one and five seconds. After the third, the publication stays undelivered, which is visible in the delivery log in the admin.
- **`4xx` (other than the ones above)** — not retried: a second attempt would get the same answer.
- **Redirects are not followed.** `301` / `302` means "not delivered".
- **The same `x-webhook-delivery` may arrive twice** — if your response was lost on the way. Treat a repeat as already done: the key `documentId` + `stamp` is the same.

### Limits

- **Address:** `https` only, on the standard port, without a login or password. The name must resolve to an **IPv4** address (an `A` record) — we do not connect over IPv6. The address may not lead into an internal network: we make no such requests at all.
- **Rate:** at most 30 deliveries per project per ten minutes. A publication over the limit stays in the log as undelivered with the reason `rate-limited`; the document itself is published, and it can be fetched from the direct address.
- **A secret is replaced, not renewed.** Changing the address in the admin always issues a new secret — the old one stops working at that very moment.

---

## Versions: the most important part of this document

**Different renderer versions live in users' apps at the same time.** Some have updated, some have not. So the client endpoint accepts `schemaVersion` and returns **the newest document among those that this version understands**.

The compatibility rule: **the major must match; a higher minor is readable**.

| The renderer asks for | Published | Return |
|---|---|---|
| `9.2.0` | `9.2.0` | an exact match |
| `9.1.0` | `9.2.0` | the same one: the renderer skips blocks it does not know and draws the screen as far as it can |
| `8.20.0` | `9.0.0` | the latest published with major `8`; if there is none — `404`, and the renderer shows its bundled copy |

This is why copies of **all** publications must be kept, not only the last one. Otherwise, after a major update of the format, some users stay without a screen until they update the app from the store.

A practical tip: keep `schemaVersion` in its own column and parse it as semver when selecting; do not compare strings.

### When the major changes — what it means for the backend

The last row of the table above is a real situation, not an example: schema **9.0.0** and theme **4.0.0** were major versions. In a document, fields of appearance (`padding`, `background`, `radius`, `color`, `typography`…) moved from `props` to `style`, and a style field accepts not only a token name but also an object; in the theme, the token dictionaries became open — a theme declares its own names next to the required core.

For the backend the body of a document and the body of a theme stay what they were — **opaque JSON**: the models in `dotnet/` describe neither `props` nor `style`, so no field in them changed. Four things around it change:

1. **Published 8.x documents STAY 8.x.** Nobody rewrites them: a version is immutable by construction. A document becomes 9.x only when a manager opens it in the builder (which migrates the draft itself) and publishes it again. Until then an app with a 9 renderer, asking for `schemaVersion=9.0.0`, gets `404` and shows its bundled copy — so **publish again first, release the app second**, not the other way round.
2. **An app with an 8 renderer does not draw a 9 document.** After the document is published again, it still asks for `schemaVersion=8.x` and must get the LAST 8.x VERSION — the one published before the move. This is exactly why all publications are stored. Such an app will not see new edits until it updates from the store.
3. **A draft may be 8.x.** The builder migrates it when it opens it and keeps saving it as 9.x; a draft nobody has opened does not need touching. Documents do NOT need to be migrated on the backend at all — the migration rule lives in the builder, and a second copy of it would drift from the first.
4. **Validation at publication uses the schema of the SAME major as the document.** Once you update `screen-schema.json` to 9.x, validate only 9.x documents with it. Do not validate stored 8.x versions again: they will not pass the new schema, and that is not a data error.

**The theme follows the same logic, with its own version.** The client names `themeVersion`; to an app with a 3.x theme reader return the last published 3.x theme, to an app with a 4.x reader — 4.x. A 3.x reader rejects a 4.0.0 theme entirely (it may have colors with custom names, and its schema accepts no foreign names), and draws the screen with the built-in theme — not a disaster, but not what the manager saw either.

Two things that JSON Schema will NOT check for you — if you want a second barrier at publication, they are:

- **a token named by a document is declared by the project theme.** The document schema checks only the shape of a name (`^[a-z][a-zA-Z0-9]{0,39}$`); whether `brandCoral` exists in the theme is the rule `documentTokensExist` (the `x-rules` block in `theme.json`). A document with a token the theme does not have is drawn by the renderer, but WITHOUT that property. Hence the order of publishing: **a theme with a new token goes before the document that uses it**; removing a token is the opposite — after the documents;
- **all themes of a set declare the same names** (`sameTokenNamesAcrossThemes`). Otherwise a screen looks right until the user switches on the dark theme.

The schema embedded in `theme.json` (`x-schema`) describes exactly one version of the theme (`schemaDescribesExactVersion`): do not validate 3.x themes with a 4.0.0 schema, and the other way round.

---

## Cache

- **An ETag is mandatory** on the document. The renderer sends `If-None-Match`, and the backend answers `304`. Most checks change nothing — it is not worth paying for them with traffic.
- **`Cache-Control: no-cache`**, not `max-age`. The difference is essential: `no-cache` means "check with me", not "do not cache". A long `max-age` turns a publication into "some time within the hour".
- **A CDN — yes**, but with invalidation at publication.
- **Data and document are cached differently**: the document for a long time and shared by everyone, the data briefly and per person. A shared endpoint for them would make either the data stale or the document inefficient.

---

## Questions to settle before you write code

### 1. Raw values or ready-made strings?

The API returns numbers and moments: `progress: 3`, `target: 5`, `price: 1850.5`, `endsAt: "2026-10-31T23:59:59Z"`. The screen needs "3 / 5", "1,850.50 USD", "12 days left". **The document composes these**: a text can be a sentence with named places, a binding says how its value is written, and the renderer picks the plural word in the user's language. So:

- return raw values, in the types described in this file;
- do not return `progressLabel` or `deadlineLabel` next to them — two sources for one text drift apart;
- what a document cannot write today — for example hours and minutes until an event, or compact notation (`1.2K`) — is the exception: return that as a ready string.

### 2. Numeric enumerations: what does `0` mean?

If statuses arrive as numbers (`status: 1`), a visibility condition on them is possible (`eq` with a number), but the manager has to guess what `1` means. Return the status as **a string from a fixed list** (`"locked" | "inProgress" | "completed"`), as the demonstration dictionary does — then a condition can be read by eye. If you do not want to change the API, a list of values with labels is needed; the cheapest way is to return it from the same endpoint as the data dictionary, so that the builder shows "Status: active" instead of `status = 1`.

### 3. A storage key or a ready-made address?

If an image arrives as a KEY in a storage, someone has to build an address from it. The contract asks for **a ready-made `https` address in the data**: an `ImageSource` binding accepts exactly that, and the renderer must not build addresses — otherwise the building rule becomes one more contract, and the web and Flutter renderers would drift apart on the very first CDN.

### 4. Identifiers for navigation

On the transition "list → details" a document puts the identifier of the record into the parameters of the transition, and the renderer asks the host for the entity by it. So the identifier must be **a field of the record in the dictionary**.

Make sure that **the details endpoint accepts the same identifier** that arrives in the card. If details are addressed by another key, put that key into the dictionary separately.

### And three organizational ones

5. **Who may publish**, and whether a four-eyes approval is needed. If so, it is a state of the document, not an extra button in the interface.
6. **How many entry points** are needed. Their names become part of what your app and campaigns refer to, so renaming one later breaks links.
7. **How long to keep copies of publications.** They are immutable and cheap, but your organization most likely has a retention policy — and old app versions need the last document of their major for as long as they are in use.

---

## What to do with the dictionary for your API

The dictionary in `data-contract.json` is a **demonstration**. You replace it for your product, and the rules of replacement are:

- **field names are taken from the API response verbatim** — there is no mapping, because a mapping is a third place where knowledge about fields lives;
- **a collection names the entity prefix** (`tasks` → `task`), and one template works both as a list row and as a details screen;
- **a nested collection** describes an array inside a record (`taskGroup.tasks`); there are exactly two levels;
- **samples (`sampleValue`) must be realistic** — the manager sees exactly them while assembling a screen, so "text" instead of an amount makes the preview misleading;
- **return a number as a number, not as a string**: the formats `number` and `currency` and the plural of a template act only on a JSON number. The string `"1850"` is shown as it is, without separators, and a word next to it will not agree;
- **an amount is a number in currency units** (`120.5`, not an integer count of minor units): the `currency` format writes it with a currency code that lies in the DOCUMENT, not in the data;
- **a date for "N days left" is the same ISO string with time and zone**: the `daysUntil` format counts whole days from the device clock to that moment;
- **`x-sampleNow`** in `data-contract.json` is the moment at which the samples are imagined. The builder preview and the sample apps need it so that "N days left" on the samples does not depend on the day; it promises nothing to the backend;
- **fields the response does not have must not be in the dictionary**: a promise nobody keeps costs an empty place on the screen.

The builder receives the dictionary at runtime (the `dataContract` property), so replacing it needs neither a builder release nor a change of the widget schema.
