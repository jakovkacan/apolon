namespace Apolon.Core.DbSet.Enums;

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