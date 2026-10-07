using System.Globalization;
using System.Text.Json;
using ForgeFlow.Application.Audit;
using ForgeFlow.Domain.Audit;
using ForgeFlow.Domain.Changes;
using ForgeFlow.Domain.Common;
using ForgeFlow.Domain.Components;
using ForgeFlow.Domain.Products;
using ForgeFlow.Domain.Users;
using ForgeFlow.Domain.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ForgeFlow.Infrastructure.Persistence.Auditing;

/// <summary>Snapshot of one tracked change, turned into an <see cref="AuditLog"/> once keys are generated.</summary>
internal sealed class AuditCapture
{
    private static readonly HashSet<string> IgnoredProperties =
    [
        nameof(AuditableEntity.CreatedAtUtc),
        nameof(AuditableEntity.CreatedBy),
        nameof(AuditableEntity.UpdatedAtUtc),
        nameof(AuditableEntity.UpdatedBy),
        nameof(EngineeringChange.Version),
        nameof(EngineeringChange.LastActivityAtUtc),
        nameof(User.LastLoginAtUtc)
    ];

    private static readonly HashSet<string> MaskedProperties = [nameof(User.PasswordHash)];

    private readonly EntityEntry _entry;
    private readonly EntityState _state;
    private readonly List<AuditPropertyChange>? _changes;
    private readonly string? _entityId;
    private readonly (string Type, string Id)? _parent;

    private AuditCapture(EntityEntry entry)
    {
        _entry = entry;
        _state = entry.State;

        // Added rows still carry temporary keys, so they are read after the save instead.
        if (_state != EntityState.Added)
        {
            _changes = ReadChanges(entry, _state);
            _entityId = ReadKey(entry);
            _parent = ReadParent(entry.Entity);
        }
    }

    private bool HasChanges => _state != EntityState.Modified || _changes!.Count > 0;

    public static List<AuditCapture> Collect(ChangeTracker changeTracker) =>
        changeTracker.Entries()
            .Where(e => e.Entity is IAuditable && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => new AuditCapture(e))
            .Where(c => c.HasChanges)
            .ToList();

    public AuditLog ToAuditLog(DateTime timestampUtc, int? userId, string userName)
    {
        var action = _state switch
        {
            EntityState.Added => AuditActions.Created,
            EntityState.Deleted => AuditActions.Deleted,
            _ => AuditActions.Updated
        };
        var changes = _changes ?? ReadChanges(_entry, EntityState.Added);
        var parent = _parent ?? ReadParent(_entry.Entity);

        return new AuditLog
        {
            TimestampUtc = timestampUtc,
            UserId = userId,
            UserName = userName,
            Action = action,
            EntityType = _entry.Metadata.ClrType.Name,
            EntityId = _entityId ?? ReadKey(_entry),
            ParentEntityType = parent?.Type,
            ParentEntityId = parent?.Id,
            Summary = $"{Describe(_entry.Entity)} {action.ToLowerInvariant()}",
            ChangesJson = changes.Count == 0 ? null : JsonSerializer.Serialize(changes, AuditPropertyChange.JsonOptions)
        };
    }

    private static List<AuditPropertyChange> ReadChanges(EntityEntry entry, EntityState state)
    {
        var changes = new List<AuditPropertyChange>();
        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            if (property.Metadata.IsPrimaryKey() || IgnoredProperties.Contains(name))
            {
                continue;
            }

            var oldValue = state == EntityState.Added ? null : Format(name, property.OriginalValue);
            var newValue = state == EntityState.Deleted ? null : Format(name, property.CurrentValue);
            if (state == EntityState.Modified ? !property.IsModified || oldValue == newValue : oldValue is null && newValue is null)
            {
                continue;
            }

            changes.Add(new AuditPropertyChange(name, oldValue, newValue));
        }

        return changes;
    }

    private static string ReadKey(EntityEntry entry) =>
        string.Join(",", entry.Metadata.FindPrimaryKey()!.Properties.Select(p => Format(p.Name, entry.Property(p.Name).CurrentValue)));

    private static (string Type, string Id)? ReadParent(object entity) =>
        entity is IAuditChild child
            ? (child.ParentEntityType, child.ParentEntityId.ToString(CultureInfo.InvariantCulture))
            : null;

    private static string? Format(string propertyName, object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (MaskedProperties.Contains(propertyName))
        {
            return "***";
        }

        return value switch
        {
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            // Normalise scale so 1250.0 and 1250 compare equal and read consistently.
            decimal number => number.ToString("0.############################", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }

    private static string Describe(object entity) => entity switch
    {
        Product product => $"Product {product.ProductNumber}",
        Component component => $"Component {component.PartNumber}",
        ProductRevision revision => revision.Product is { } product
            ? $"{product.ProductNumber} Rev {revision.RevisionCode}"
            : $"Product revision {revision.RevisionCode}",
        ComponentRevision revision => revision.Component is { } component
            ? $"{component.PartNumber} Rev {revision.RevisionCode}"
            : $"Component revision {revision.RevisionCode}",
        BomItem item => item.Component is { } component
            ? $"BOM line {component.PartNumber}"
            : $"BOM line for component #{item.ComponentId}",
        ChangeAffectedItem => "Affected item",
        EngineeringChange change => change.ChangeNumber,
        WorkflowDefinition workflow => $"Workflow '{workflow.Name}'",
        WorkflowStep step => $"Workflow step '{step.Name}'",
        User user => $"User {user.Email}",
        _ => entity.GetType().Name
    };
}
