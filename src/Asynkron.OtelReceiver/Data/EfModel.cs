using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

#pragma warning disable CS8618

namespace Asynkron.OtelReceiver.Data;

public class OtelReceiverContext(DbContextOptions<OtelReceiverContext> options) : DbContext(options)
{
    public DbSet<LogEntity> Logs { get; set; }
    public DbSet<LogAttributeEntity> LogAttributes { get; set; }
    public DbSet<SpanEntity> Spans { get; set; }

    public DbSet<UserSettingsEntity> UserSettings { get; set; }

    public DbSet<SpanAttributeEntity> SpanAttributes { get; set; }
    public DbSet<SpanAttributeValueEntity> SpanAttributeValues { get; set; }
    public DbSet<SpanNameEntity> SpanNames { get; set; }

    public DbSet<ComponentMetadataEntity> ComponentMetaData { get; set; }

    public DbSet<SnapshotEntity> Snapshots { get; set; }

    public DbSet<MetricEntity> Metrics { get; set; }
}

[Index(nameof(TraceId))]
public class LogEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public long Timestamp { get; set; }
    public long ObservedTimestamp { get; set; }

    public string TraceId { get; set; }
    public string SpanId { get; set; }
    public string SeverityText { get; set; }
    public byte SeverityNumber { get; set; }
    public string Body { get; set; }

    public string RawBody { get; set; }

    public byte[] Proto { get; set; }

    public ICollection<LogAttributeEntity> Attributes { get; set; } = new List<LogAttributeEntity>();
}

[Index(nameof(LogId))]
[Index(nameof(Key))]
[Index(nameof(Value))]
[Index(nameof(Key), nameof(Value))]
public class LogAttributeEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int LogId { get; set; }
    public string Key { get; set; }
    public string Value { get; set; }
    public LogAttributeSource Source { get; set; }

    public LogEntity Log { get; set; }
}

public enum LogAttributeSource : byte
{
    Record = 0,
    Resource = 1
}

[Index(nameof(SpanId))]
[Index(nameof(Key))]
[Index(nameof(Value))]
[Index(nameof(Key), nameof(Value))]
[Table("SpanAttributeValues")]
public class SpanAttributeValueEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public string SpanId { get; set; }
    public string Key { get; set; }
    public string Value { get; set; }
    public SpanAttributeSource Source { get; set; }

    public SpanEntity Span { get; set; }
}

public enum SpanAttributeSource : byte
{
    Span = 0,
    Resource = 1
}

[Index(nameof(SpanId))]
[Index(nameof(TraceId))]
[Index(nameof(ServiceName))]
[Index(nameof(OperationName))]
[Index(nameof(StartTimestamp))]
[Index(nameof(EndTimestamp))]
public class SpanEntity
{
    [Key] public string SpanId { get; set; }

    public long StartTimestamp { get; set; }
    public long EndTimestamp { get; set; }
    public string TraceId { get; set; }
    public string ParentSpanId { get; set; }

    public string ServiceName { get; set; }
    public string OperationName { get; set; }
    public string[] Events { get; set; }
    public byte[] Proto { get; set; }

    //Key + ":" + Value... query checks for existence of entire string
    public string[] AttributeMap { get; set; }
}

[Index(nameof(Name))]
public class MetricEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public byte[] Proto { get; set; }
    public string[] AttributeMap { get; set; }
    public string Unit { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public ulong StartTimestamp { get; set; }
    public ulong EndTimestamp { get; set; }
}

[PrimaryKey(nameof(Key), nameof(Value))]
public record SpanAttributeEntity
{
    public string Key { get; set; }
    public string Value { get; set; }
}

[PrimaryKey(nameof(ServiceName), nameof(Name))]
public record SpanNameEntity
{
    public string ServiceName { get; set; }
    public string Name { get; set; }
}

public class UserSettingsEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public string Theme { get; set; }
    public byte TimestampType { get; set; }
}

public class ComponentMetadataEntity
{
    [Key]
    public string NamePath { get; set; } // GroupName:ComponentName
    public string Annotation { get; set; }
}

public class SnapshotEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public byte[] Proto { get; set; }
}
