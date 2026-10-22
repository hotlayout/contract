-- Tables for delivering screen documents.
--
-- Dialect: PostgreSQL. For SQL Server replace jsonb → nvarchar(max),
-- timestamptz → datetimeoffset, gen_random_uuid() → newid().
--
-- Separation by tenant is deliberately NOT described here: if an installation
-- serves a single product, a tenant_id column would equal a constant in every
-- query. If you need multi-tenancy, add it to both tables together with
-- uniqueness of the key.

CREATE TABLE ui_document (
    id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),

    -- The stable key by which the SDK asks for a document. It, not id, ends up
    -- in the app's call — so renaming it breaks the app.
    key                   text NOT NULL UNIQUE,
    name                  text NOT NULL,

    -- The draft as a whole document. It changes every minute and is not visible to phones.
    draft_body            jsonb NOT NULL,
    draft_updated_at      timestamptz NOT NULL DEFAULT now(),
    draft_updated_by      uuid NOT NULL,

    -- Which version is currently published. NULL — never published, which is
    -- a normal state of a new document, not an error.
    published_version_id  uuid NULL,

    created_at            timestamptz NOT NULL DEFAULT now()
);

-- A copy at the moment of publication. NEVER updated or deleted.
--
-- ALL versions must be stored: different SDK versions live in apps at the same
-- time, and an older one must get the last compatible version. Without the
-- history, the first major update of the format would leave some users
-- without a screen.
CREATE TABLE ui_document_version (
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    document_id    uuid NOT NULL REFERENCES ui_document (id) ON DELETE CASCADE,

    -- The sequence number of the publication, convenient for a person: 1, 2, 3…
    version        integer NOT NULL,

    -- Semver taken from the document itself. A separate column, because the
    -- compatible version is selected by it — and it has to be compared as
    -- semver, not as a string.
    schema_version text NOT NULL,

    body           jsonb NOT NULL,
    bytes          integer NOT NULL,
    published_at   timestamptz NOT NULL DEFAULT now(),
    published_by   uuid NOT NULL,
    note           text NULL,

    CONSTRAINT ui_document_version_unique UNIQUE (document_id, version)
);

ALTER TABLE ui_document
    ADD CONSTRAINT ui_document_published_version_fk
    FOREIGN KEY (published_version_id) REFERENCES ui_document_version (id);

-- Selecting "the latest compatible version" is the hottest query of the client
-- endpoint. The major is pulled into the index so that semver is not parsed on
-- every request; if the database allows it, it is more convenient to store
-- major/minor in separate integer columns.
CREATE INDEX ui_document_version_lookup
    ON ui_document_version (document_id, schema_version, version DESC);

-- The theme follows the same model but separately: it has its own version and
-- its own lifecycle. A rebrand adds no widgets, and a new widget does not
-- change the palette.
CREATE TABLE ui_theme (
    id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    draft_body            jsonb NOT NULL,
    draft_updated_at      timestamptz NOT NULL DEFAULT now(),
    draft_updated_by      uuid NOT NULL,
    published_version_id  uuid NULL
);

CREATE TABLE ui_theme_version (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    theme_id      uuid NOT NULL REFERENCES ui_theme (id) ON DELETE CASCADE,
    version       integer NOT NULL,
    theme_version text NOT NULL,
    body          jsonb NOT NULL,
    published_at  timestamptz NOT NULL DEFAULT now(),
    published_by  uuid NOT NULL,

    CONSTRAINT ui_theme_version_unique UNIQUE (theme_id, version)
);
