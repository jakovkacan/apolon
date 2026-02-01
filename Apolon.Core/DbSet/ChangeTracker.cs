using Apolon.Core.Exceptions;

namespace Apolon.Core.DbSet;

/// <summary>
///     Tracks entity state changes for the Unit of Work pattern.
///     Maintains collections of added, modified, and deleted entities.
/// </summary>
public class ChangeTracker
{
    private readonly HashSet<object> _deletedEntities = [];
    private readonly Dictionary<object, EntityState> _entityStates = new();
    private readonly HashSet<object> _modifiedEntities = [];
    private readonly HashSet<object> _newEntities = [];
    private readonly Dictionary<object, Dictionary<string, object>> _originalValues = new();

    // Public accessors for tracked entity collections
    public IEnumerable<object> NewEntities => _newEntities;
    public IEnumerable<object> ModifiedEntities => _modifiedEntities;
    public IEnumerable<object> DeletedEntities => _deletedEntities;
    public IEnumerable<object> TrackedEntities => _entityStates.Keys;

    /// <summary>
    ///     Tracks a new entity that should be inserted.
    /// </summary>
    public void TrackNew(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (_entityStates.ContainsKey(entity))
            throw new InvalidOperationException("Entity is already tracked");

        _newEntities.Add(entity);
        _entityStates[entity] = EntityState.Added;
    }

    /// <summary>
    ///     Tracks a modified entity that should be updated.
    ///     Captures original values for change detection.
    /// </summary>
    public void TrackModified(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        // If entity is new, don't mark as modified (it will be inserted)
        if (_newEntities.Contains(entity))
            return;

        // If entity is deleted, can't modify it
        if (_deletedEntities.Contains(entity))
            throw new InvalidOperationException("Cannot modify a deleted entity");

        // First time tracking this entity for modification - capture original values
        if (!_entityStates.TryGetValue(entity, out var value))
        {
            CaptureOriginalValues(entity);
            value = EntityState.Unchanged;
            _entityStates[entity] = value;
        }

        // Mark as modified
        if (value == EntityState.Unchanged)
        {
            _modifiedEntities.Add(entity);
            _entityStates[entity] = EntityState.Modified;
        }
    }

    /// <summary>
    ///     Tracks an entity for deletion.
    /// </summary>
    public void TrackDeleted(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        // If entity is new (not yet in DB), just remove from tracking
        if (_newEntities.Contains(entity))
        {
            _newEntities.Remove(entity);
            _entityStates.Remove(entity);
            return;
        }

        // Remove from modified if present
        _modifiedEntities.Remove(entity);

        // Add to deleted
        _deletedEntities.Add(entity);
        _entityStates[entity] = EntityState.Deleted;
    }

    /// <summary>
    ///     Tracks an entity as unchanged (typically after loading from DB).
    /// </summary>
    public void TrackUnchanged(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (_entityStates.ContainsKey(entity)) return;

        CaptureOriginalValues(entity);
        _entityStates[entity] = EntityState.Unchanged;
    }

    /// <summary>
    ///     Gets the current state of an entity.
    /// </summary>
    public EntityState GetState(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return _entityStates.GetValueOrDefault(entity, EntityState.Detached);
    }

    /// <summary>
    ///     Checks if an entity has been modified by comparing current values to original values.
    /// </summary>
    public bool HasChanges(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (!_originalValues.TryGetValue(entity, out var original))
            return false;
        var properties = entity.GetType().GetProperties();

        foreach (var prop in properties)
        {
            if (!original.ContainsKey(prop.Name))
                continue;

            var currentValue = prop.GetValue(entity);
            var originalValue = original[prop.Name];

            if (!Equals(currentValue, originalValue))
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Gets original values for a tracked entity (before modifications).
    /// </summary>
    public Dictionary<string, object> GetOriginalValues(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return (_originalValues.TryGetValue(entity, out var values)
            ? new Dictionary<string, object>(values)
            : null) ?? throw new OrmException("Entity is not being tracked");
    }

    /// <summary>
    ///     Detaches an entity from change tracking.
    /// </summary>
    public void Detach(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        _newEntities.Remove(entity);
        _modifiedEntities.Remove(entity);
        _deletedEntities.Remove(entity);
        _entityStates.Remove(entity);
        _originalValues.Remove(entity);
    }

    /// <summary>
    ///     Clears all tracked entities.
    ///     Call this after SaveChanges() completes successfully.
    /// </summary>
    public void Clear()
    {
        _newEntities.Clear();
        _modifiedEntities.Clear();
        _deletedEntities.Clear();
        _entityStates.Clear();
        _originalValues.Clear();
    }

    /// <summary>
    ///     Accepts changes for a specific entity after successful save.
    ///     Moves Added/Modified entities to Unchanged, removes Deleted entities.
    /// </summary>
    public void AcceptChanges(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var state = GetState(entity);

        switch (state)
        {
            case EntityState.Added:
            case EntityState.Modified:
                _newEntities.Remove(entity);
                _modifiedEntities.Remove(entity);
                _entityStates[entity] = EntityState.Unchanged;
                CaptureOriginalValues(entity); // Update original values
                break;

            case EntityState.Deleted:
                Detach(entity);
                break;
        }
    }

    /// <summary>
    ///     Accepts all changes after successful SaveChanges().
    /// </summary>
    public void AcceptAllChanges()
    {
        var entitiesToAccept = _entityStates.Keys.ToList();
        foreach (var entity in entitiesToAccept) AcceptChanges(entity);
    }

    /// <summary>
    ///     Gets count of entities with pending changes.
    /// </summary>
    public int GetChangeCount()
    {
        return _newEntities.Count + _modifiedEntities.Count + _deletedEntities.Count;
    }

    /// <summary>
    ///     Checks if there are any pending changes.
    /// </summary>
    public bool HasPendingChanges()
    {
        return GetChangeCount() > 0;
    }

    /// <summary>
    ///     Captures current property values as original values.
    ///     Used for change detection.
    /// </summary>
    private void CaptureOriginalValues(object entity)
    {
        if (entity == null)
            return;

        var values = new Dictionary<string, object>();
        var properties = entity.GetType().GetProperties();

        foreach (var prop in properties)
        {
            // Skip navigation properties (collections, complex types)
            if (prop.PropertyType.IsClass &&
                prop.PropertyType != typeof(string) &&
                !prop.PropertyType.IsValueType)
                continue;

            try
            {
                values[prop.Name] = prop.GetValue(entity);
            }
            catch
            {
                // Skip properties that can't be read
            }
        }

        _originalValues[entity] = values;
    }
}

/// <summary>
///     Enum representing the state of an entity in the change tracker.
/// </summary>
public enum EntityState
{
    /// <summary>
    ///     Entity is not being tracked.
    /// </summary>
    Detached = 0,

    /// <summary>
    ///     Entity is being tracked and exists in the database.
    ///     No changes have been made to property values.
    /// </summary>
    Unchanged = 1,

    /// <summary>
    ///     Entity is being tracked and will be deleted from the database.
    /// </summary>
    Deleted = 2,

    /// <summary>
    ///     Entity is being tracked and has been modified.
    ///     Will be updated in the database.
    /// </summary>
    Modified = 3,

    /// <summary>
    ///     Entity is being tracked but does not exist in the database.
    ///     Will be inserted when SaveChanges is called.
    /// </summary>
    Added = 4
}