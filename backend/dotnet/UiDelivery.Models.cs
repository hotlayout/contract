// Models for delivering screen documents — ONE example in C#.
// The protocol is plain HTTP + JSON, so the same thing translates to any
// stack; this file describes a shape, not a choice of platform.
//
// This is NOT finished code but the shape of an agreement: the names, types and
// nullability that the builder and the SDK rely on. Namespace, style and base
// classes are up to you.
//
// The sources of truth these models are taken from:
//   screen-schema.json   — validation of a document
//   data-contract.json   — the data dictionary
//   backend/openapi.yaml — the endpoints

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RemoteUi.Delivery.Contracts;

// ─────────────────────────────────────────────────────────────────────────────
//  Storage
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// The document of a module's screens. The manager's unit of work.
/// </summary>
/// <remarks>
/// DRAFT AND PUBLICATION ARE DIFFERENT FIELDS, not a status of one record. A
/// single record with a status would make every autosave a potential release:
/// a manager must be able to edit without fear that phones will pick up
/// something half-finished.
/// </remarks>
public sealed class UiDocument
{
    public Guid Id { get; set; }

    /// <summary>The stable key by which the SDK asks for a document. E.g. "gamification".</summary>
    public required string Key { get; set; }

    public required string Name { get; set; }

    /// <summary>
    /// The draft as whole JSON. Breaking the screen tree into relational tables
    /// is not worth it: a migration on every contract change and slow queries,
    /// while a document is always read as a whole. The size limit is 1 MB.
    /// </summary>
    public required JsonDocument DraftBody { get; set; }

    public DateTimeOffset DraftUpdatedAt { get; set; }
    public Guid DraftUpdatedBy { get; set; }

    /// <summary>Which version is currently published. null — never published.</summary>
    public Guid? PublishedVersionId { get; set; }

    /// <summary>
    /// The draft version for optimistic concurrency. It goes into the ETag and
    /// comes back in If-Match: without it two open tabs would silently
    /// overwrite each other's work.
    /// </summary>
    [JsonIgnore]
    public byte[]? RowVersion { get; set; }
}

/// <summary>
/// A copy of the document at the moment of publication. NEVER updated.
/// </summary>
/// <remarks>
/// A rollback means moving <see cref="UiDocument.PublishedVersionId"/> to an
/// earlier row, not correcting an existing one. The audit trail comes from the
/// same place.
///
/// ALL versions must be stored, not only the last one: different SDK versions
/// live in users' apps at the same time, and an older one must get the last
/// compatible version. After a major update of the format, without the history
/// some users would be left without a screen.
/// </remarks>
public sealed class UiDocumentVersion
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }

    /// <summary>The sequence number of the publication, convenient for a person: 1, 2, 3…</summary>
    public int Version { get; set; }

    /// <summary>
    /// Semver taken from the document itself, e.g. "9.0.0". Store it in its own
    /// column and parse it as semver when selecting — strings must not be
    /// compared: "8.10.0" &lt; "8.9.0" as a string, but not as a version.
    /// </summary>
    public required string SchemaVersion { get; set; }

    public required JsonDocument Body { get; set; }
    public int Bytes { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public Guid PublishedBy { get; set; }
    public string? Note { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
//  Responses of the admin endpoints
// ─────────────────────────────────────────────────────────────────────────────

public sealed record UiDocumentSummary(
    Guid Id,
    string Key,
    string Name,
    /// <summary>
    /// Whether the draft differs from the published version.
    ///
    /// The builder shows this as a line in the header. Without it a manager
    /// cannot tell "everything is published" from "nothing was ever
    /// published" — and that is exactly where people stumble: they change a
    /// screen, look at the phone, see the old one, and suspect the connection.
    /// </summary>
    bool HasUnpublishedChanges,
    DateTimeOffset? PublishedAt,
    DateTimeOffset DraftUpdatedAt);

public sealed record UiDocumentDetail(
    Guid Id,
    string Key,
    string Name,
    bool HasUnpublishedChanges,
    DateTimeOffset? PublishedAt,
    DateTimeOffset DraftUpdatedAt,
    JsonDocument DraftBody,
    Guid? PublishedVersionId);

public sealed record SaveReceipt(DateTimeOffset SavedAt, int Bytes);

public sealed record PublishReceipt(
    Guid VersionId,
    int Version,
    DateTimeOffset PublishedAt,
    string SchemaVersion);

public sealed record VersionSummary(
    Guid Id,
    int Version,
    string SchemaVersion,
    DateTimeOffset PublishedAt,
    string PublishedBy,
    int Bytes,
    string? Note,
    bool IsCurrent);

/// <summary>A document validation error — the builder shows it to the manager.</summary>
public sealed record ValidationIssue(string Code, string Path, string Message);

// ─────────────────────────────────────────────────────────────────────────────
//  The data dictionary
// ─────────────────────────────────────────────────────────────────────────────

public enum DataValueType
{
    String,
    Number,
    Date,
    Boolean,
}

/// <summary>A single value that a document can substitute.</summary>
public sealed record DataPathDescriptor(
    /// <summary>E.g. "task.title". Matches a field of the API response.</summary>
    string Path,
    DataValueType Type,
    /// <summary>A human label for the manager: "Task title".</summary>
    string Label,
    /// <summary>A sample for the preview in the builder.</summary>
    object SampleValue);

/// <summary>A list that a document can repeat.</summary>
public sealed record DataCollectionDescriptor(
    /// <summary>
    /// The path to the array. With a dot — a nested collection, relative to the
    /// record: "taskGroup.tasks".
    /// </summary>
    string Path,
    string Label,
    /// <summary>
    /// The prefix under which a record is available inside the template: the
    /// collection "tasks" gives "task", so the document reads "task.title".
    ///
    /// That is why record keys in the response carry NO prefix — the SDK adds it.
    /// </summary>
    string Item);

public sealed record DataContract(
    IReadOnlyList<DataPathDescriptor> Paths,
    IReadOnlyList<DataCollectionDescriptor> Collections);

// ─────────────────────────────────────────────────────────────────────────────
//  The version compatibility rule
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Which version of the document to return to a client that asks for <paramref name="requested"/>.
/// </summary>
/// <remarks>
/// THE MOST IMPORTANT LOGIC OF THE WHOLE MODULE. Different SDK versions live in
/// users' apps at the same time, and each of them must get a working screen.
///
/// The rule: the major must match; a client's minor may be lower than the
/// document's. So a 9.1 client reads a 9.0 document, and a 9.0 client reads a
/// 9.1 document (it skips blocks it does not know and draws the screen as far
/// as it can).
///
/// The second half — "a 9.0 client reads a 9.1 document" — is handled by the SDK
/// itself, so it is enough for the backend to return the NEWEST document with
/// the same major.
/// </remarks>
public static class SchemaCompatibility
{
    public static UiDocumentVersion? PickCompatible(
        IEnumerable<UiDocumentVersion> published,
        string requested)
    {
        if (!TryParseMajor(requested, out var requestedMajor)) return null;

        UiDocumentVersion? best = null;
        foreach (var version in published)
        {
            if (!TryParseMajor(version.SchemaVersion, out var major)) continue;
            if (major != requestedMajor) continue;
            if (best is null || version.Version > best.Version) best = version;
        }

        // null is not a failure: the SDK shows its bundled copy and the screen stays usable.
        return best;
    }

    private static bool TryParseMajor(string semver, out int major)
    {
        major = 0;
        var dot = semver.IndexOf('.');
        return dot > 0 && int.TryParse(semver.AsSpan(0, dot), out major);
    }
}
