using Apolon.Core.DbSet;
using Apolon.Core.Exceptions;

namespace Apolon.Core.Tests.DbSet;

public class ChangeTrackerTests
{
    [Fact]
    public void TrackNew_WithValidEntity_AddsEntityToNewEntities()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Test" };

        tracker.TrackNew(entity);

        Assert.Contains(entity, tracker.NewEntities);
        Assert.Equal(EntityState.Added, tracker.GetState(entity));
    }

    [Fact]
    public void TrackNew_WithNullEntity_ThrowsArgumentNullException()
    {
        var tracker = new ChangeTracker();

        Assert.Throws<ArgumentNullException>(() => tracker.TrackNew(null));
    }

    [Fact]
    public void TrackNew_WithAlreadyTrackedEntity_ThrowsInvalidOperationException()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Test" };
        tracker.TrackNew(entity);

        var exception = Assert.Throws<InvalidOperationException>(() => tracker.TrackNew(entity));

        Assert.Contains("already tracked", exception.Message);
    }

    [Fact]
    public void TrackModified_WithValidEntity_AddsEntityToModifiedEntities()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Test" };

        tracker.TrackModified(entity);

        Assert.Contains(entity, tracker.ModifiedEntities);
        Assert.Equal(EntityState.Modified, tracker.GetState(entity));
    }

    [Fact]
    public void TrackModified_WithNullEntity_ThrowsArgumentNullException()
    {
        var tracker = new ChangeTracker();

        Assert.Throws<ArgumentNullException>(() => tracker.TrackModified(null));
    }

    [Fact]
    public void TrackModified_WithNewEntity_DoesNotMarkAsModified()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Test" };
        tracker.TrackNew(entity);

        tracker.TrackModified(entity);

        Assert.DoesNotContain(entity, tracker.ModifiedEntities);
        Assert.Equal(EntityState.Added, tracker.GetState(entity));
    }

    [Fact]
    public void TrackModified_WithDeletedEntity_ThrowsInvalidOperationException()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Test" };
        tracker.TrackUnchanged(entity);
        tracker.TrackDeleted(entity);

        var exception = Assert.Throws<InvalidOperationException>(() => tracker.TrackModified(entity));

        Assert.Contains("Cannot modify a deleted entity", exception.Message);
    }

    [Fact]
    public void TrackModified_CapturesOriginalValues()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Original" };

        tracker.TrackModified(entity);
        entity.Name = "Modified";

        var originalValues = tracker.GetOriginalValues(entity);
        Assert.Equal("Original", originalValues["Name"]);
    }

    [Fact]
    public void TrackDeleted_WithValidEntity_AddsEntityToDeletedEntities()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Test" };
        tracker.TrackUnchanged(entity);

        tracker.TrackDeleted(entity);

        Assert.Contains(entity, tracker.DeletedEntities);
        Assert.Equal(EntityState.Deleted, tracker.GetState(entity));
    }

    [Fact]
    public void TrackDeleted_WithNullEntity_ThrowsArgumentNullException()
    {
        var tracker = new ChangeTracker();

        Assert.Throws<ArgumentNullException>(() => tracker.TrackDeleted(null));
    }

    [Fact]
    public void TrackDeleted_WithNewEntity_RemovesFromTracking()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Test" };
        tracker.TrackNew(entity);

        tracker.TrackDeleted(entity);

        Assert.DoesNotContain(entity, tracker.NewEntities);
        Assert.Equal(EntityState.Detached, tracker.GetState(entity));
    }

    [Fact]
    public void TrackDeleted_WithModifiedEntity_RemovesFromModifiedAndAddsToDeleted()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Test" };
        tracker.TrackModified(entity);

        tracker.TrackDeleted(entity);

        Assert.DoesNotContain(entity, tracker.ModifiedEntities);
        Assert.Contains(entity, tracker.DeletedEntities);
        Assert.Equal(EntityState.Deleted, tracker.GetState(entity));
    }

    [Fact]
    public void TrackUnchanged_WithValidEntity_TracksEntityAsUnchanged()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Test" };

        tracker.TrackUnchanged(entity);

        Assert.Equal(EntityState.Unchanged, tracker.GetState(entity));
        Assert.Contains(entity, tracker.TrackedEntities);
    }

    [Fact]
    public void TrackUnchanged_WithNullEntity_ThrowsArgumentNullException()
    {
        var tracker = new ChangeTracker();

        Assert.Throws<ArgumentNullException>(() => tracker.TrackUnchanged(null));
    }

    [Fact]
    public void TrackUnchanged_WithAlreadyTrackedEntity_DoesNotChangeState()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Test" };
        tracker.TrackNew(entity);

        tracker.TrackUnchanged(entity);

        Assert.Equal(EntityState.Added, tracker.GetState(entity));
    }

    [Fact]
    public void TrackUnchanged_CapturesOriginalValues()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Original" };

        tracker.TrackUnchanged(entity);

        var originalValues = tracker.GetOriginalValues(entity);
        Assert.Equal("Original", originalValues["Name"]);
        Assert.Equal(1, originalValues["Id"]);
    }

    [Fact]
    public void GetState_WithUntrackedEntity_ReturnsDetached()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Test" };

        var state = tracker.GetState(entity);

        Assert.Equal(EntityState.Detached, state);
    }

    [Fact]
    public void GetState_WithNullEntity_ThrowsArgumentNullException()
    {
        var tracker = new ChangeTracker();

        Assert.Throws<ArgumentNullException>(() => tracker.GetState(null));
    }

    [Fact]
    public void HasChanges_WithModifiedEntity_ReturnsTrue()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Original" };
        tracker.TrackUnchanged(entity);

        entity.Name = "Modified";

        Assert.True(tracker.HasChanges(entity));
    }

    [Fact]
    public void HasChanges_WithUnchangedEntity_ReturnsFalse()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Original" };
        tracker.TrackUnchanged(entity);

        Assert.False(tracker.HasChanges(entity));
    }

    [Fact]
    public void HasChanges_WithUntrackedEntity_ReturnsFalse()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Test" };

        Assert.False(tracker.HasChanges(entity));
    }

    [Fact]
    public void HasChanges_WithNullEntity_ThrowsArgumentNullException()
    {
        var tracker = new ChangeTracker();

        Assert.Throws<ArgumentNullException>(() => tracker.HasChanges(null));
    }

    [Fact]
    public void GetOriginalValues_WithTrackedEntity_ReturnsOriginalValues()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Original" };
        tracker.TrackUnchanged(entity);
        entity.Name = "Modified";

        var originalValues = tracker.GetOriginalValues(entity);

        Assert.Equal("Original", originalValues["Name"]);
        Assert.Equal(1, originalValues["Id"]);
    }

    [Fact]
    public void GetOriginalValues_WithUntrackedEntity_ThrowsOrmException()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Test" };

        var exception = Assert.Throws<OrmException>(() => tracker.GetOriginalValues(entity));

        Assert.Contains("not being tracked", exception.Message);
    }

    [Fact]
    public void GetOriginalValues_WithNullEntity_ThrowsArgumentNullException()
    {
        var tracker = new ChangeTracker();

        Assert.Throws<ArgumentNullException>(() => tracker.GetOriginalValues(null));
    }

    [Fact]
    public void GetOriginalValues_ReturnsClonedDictionary()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Original" };
        tracker.TrackUnchanged(entity);

        var originalValues1 = tracker.GetOriginalValues(entity);
        var originalValues2 = tracker.GetOriginalValues(entity);

        Assert.NotSame(originalValues1, originalValues2);
    }

    [Fact]
    public void Detach_WithTrackedEntity_RemovesEntityFromAllCollections()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Test" };
        tracker.TrackModified(entity);

        tracker.Detach(entity);

        Assert.Equal(EntityState.Detached, tracker.GetState(entity));
        Assert.DoesNotContain(entity, tracker.TrackedEntities);
        Assert.DoesNotContain(entity, tracker.ModifiedEntities);
    }

    [Fact]
    public void Detach_WithNullEntity_ThrowsArgumentNullException()
    {
        var tracker = new ChangeTracker();

        Assert.Throws<ArgumentNullException>(() => tracker.Detach(null));
    }

    [Fact]
    public void Detach_WithNewEntity_RemovesFromNewEntities()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Test" };
        tracker.TrackNew(entity);

        tracker.Detach(entity);

        Assert.DoesNotContain(entity, tracker.NewEntities);
    }

    [Fact]
    public void Clear_RemovesAllTrackedEntities()
    {
        var tracker = new ChangeTracker();
        var entity1 = new TestEntity { Id = 1, Name = "Test1" };
        var entity2 = new TestEntity { Id = 2, Name = "Test2" };
        var entity3 = new TestEntity { Id = 3, Name = "Test3" };
        tracker.TrackNew(entity1);
        tracker.TrackModified(entity2);
        tracker.TrackDeleted(entity3);

        tracker.Clear();

        Assert.Empty(tracker.NewEntities);
        Assert.Empty(tracker.ModifiedEntities);
        Assert.Empty(tracker.DeletedEntities);
        Assert.Empty(tracker.TrackedEntities);
    }

    [Fact]
    public void AcceptChanges_WithAddedEntity_MovesToUnchangedState()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Test" };
        tracker.TrackNew(entity);

        tracker.AcceptChanges(entity);

        Assert.Equal(EntityState.Unchanged, tracker.GetState(entity));
        Assert.DoesNotContain(entity, tracker.NewEntities);
    }

    [Fact]
    public void AcceptChanges_WithModifiedEntity_MovesToUnchangedState()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Original" };
        tracker.TrackModified(entity);
        entity.Name = "Modified";

        tracker.AcceptChanges(entity);

        Assert.Equal(EntityState.Unchanged, tracker.GetState(entity));
        Assert.DoesNotContain(entity, tracker.ModifiedEntities);
    }

    [Fact]
    public void AcceptChanges_WithModifiedEntity_UpdatesOriginalValues()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Original" };
        tracker.TrackModified(entity);
        entity.Name = "Modified";

        tracker.AcceptChanges(entity);

        var originalValues = tracker.GetOriginalValues(entity);
        Assert.Equal("Modified", originalValues["Name"]);
    }

    [Fact]
    public void AcceptChanges_WithDeletedEntity_DetachesEntity()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Test" };
        tracker.TrackUnchanged(entity);
        tracker.TrackDeleted(entity);

        tracker.AcceptChanges(entity);

        Assert.Equal(EntityState.Detached, tracker.GetState(entity));
        Assert.DoesNotContain(entity, tracker.DeletedEntities);
    }

    [Fact]
    public void AcceptChanges_WithNullEntity_ThrowsArgumentNullException()
    {
        var tracker = new ChangeTracker();

        Assert.Throws<ArgumentNullException>(() => tracker.AcceptChanges(null));
    }

    [Fact]
    public void AcceptAllChanges_MovesAllEntitiesToAppropriateState()
    {
        var tracker = new ChangeTracker();
        var newEntity = new TestEntity { Id = 1, Name = "New" };
        var modifiedEntity = new TestEntity { Id = 2, Name = "Modified" };
        var deletedEntity = new TestEntity { Id = 3, Name = "Deleted" };
        tracker.TrackNew(newEntity);
        tracker.TrackModified(modifiedEntity);
        tracker.TrackUnchanged(deletedEntity);
        tracker.TrackDeleted(deletedEntity);

        tracker.AcceptAllChanges();

        Assert.Equal(EntityState.Unchanged, tracker.GetState(newEntity));
        Assert.Equal(EntityState.Unchanged, tracker.GetState(modifiedEntity));
        Assert.Equal(EntityState.Detached, tracker.GetState(deletedEntity));
    }

    [Fact]
    public void GetChangeCount_WithMultipleTrackedEntities_ReturnsCorrectCount()
    {
        var tracker = new ChangeTracker();
        var entity1 = new TestEntity { Id = 1, Name = "Test1" };
        var entity2 = new TestEntity { Id = 2, Name = "Test2" };
        var entity3 = new TestEntity { Id = 3, Name = "Test3" };
        tracker.TrackNew(entity1);
        tracker.TrackModified(entity2);
        tracker.TrackDeleted(entity3);

        var count = tracker.GetChangeCount();

        Assert.Equal(3, count);
    }

    [Fact]
    public void GetChangeCount_WithNoChanges_ReturnsZero()
    {
        var tracker = new ChangeTracker();

        var count = tracker.GetChangeCount();

        Assert.Equal(0, count);
    }

    [Fact]
    public void GetChangeCount_WithUnchangedEntities_ReturnsZero()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Test" };
        tracker.TrackUnchanged(entity);

        var count = tracker.GetChangeCount();

        Assert.Equal(0, count);
    }

    [Fact]
    public void HasPendingChanges_WithTrackedChanges_ReturnsTrue()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Test" };
        tracker.TrackNew(entity);

        Assert.True(tracker.HasPendingChanges());
    }

    [Fact]
    public void HasPendingChanges_WithNoChanges_ReturnsFalse()
    {
        var tracker = new ChangeTracker();

        Assert.False(tracker.HasPendingChanges());
    }

    [Fact]
    public void HasPendingChanges_WithUnchangedEntities_ReturnsFalse()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Test" };
        tracker.TrackUnchanged(entity);

        Assert.False(tracker.HasPendingChanges());
    }

    [Fact]
    public void TrackedEntities_ReturnsAllTrackedEntities()
    {
        var tracker = new ChangeTracker();
        var entity1 = new TestEntity { Id = 1, Name = "Test1" };
        var entity2 = new TestEntity { Id = 2, Name = "Test2" };
        var entity3 = new TestEntity { Id = 3, Name = "Test3" };
        tracker.TrackNew(entity1);
        tracker.TrackModified(entity2);
        tracker.TrackUnchanged(entity3);

        var trackedEntities = tracker.TrackedEntities.ToList();

        Assert.Equal(3, trackedEntities.Count);
        Assert.Contains(entity1, trackedEntities);
        Assert.Contains(entity2, trackedEntities);
        Assert.Contains(entity3, trackedEntities);
    }

    [Fact]
    public void MultipleModifications_OnSameEntity_MaintainsSingleModifiedState()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Original" };
        tracker.TrackUnchanged(entity);

        entity.Name = "Modified1";
        tracker.TrackModified(entity);
        entity.Name = "Modified2";
        tracker.TrackModified(entity);

        Assert.Single(tracker.ModifiedEntities);
        Assert.Equal(EntityState.Modified, tracker.GetState(entity));
    }

    [Fact]
    public void TrackModified_AfterTrackUnchanged_TransitionsCorrectly()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Original" };
        tracker.TrackUnchanged(entity);

        tracker.TrackModified(entity);

        Assert.Equal(EntityState.Modified, tracker.GetState(entity));
        Assert.Contains(entity, tracker.ModifiedEntities);
    }

    [Fact]
    public void HasChanges_DetectsChangeInNumericProperty()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Test" };
        tracker.TrackUnchanged(entity);

        entity.Id = 2;

        Assert.True(tracker.HasChanges(entity));
    }

    [Fact]
    public void HasChanges_DetectsChangeInNullableProperty()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Test", Description = null };
        tracker.TrackUnchanged(entity);

        entity.Description = "New Description";

        Assert.True(tracker.HasChanges(entity));
    }

    [Fact]
    public void HasChanges_DoesNotDetectChangeWhenSettingSameValue()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Test" };
        tracker.TrackUnchanged(entity);

        entity.Name = "Test";

        Assert.False(tracker.HasChanges(entity));
    }

    [Fact]
    public void AcceptAllChanges_WithEmptyTracker_DoesNotThrow()
    {
        var tracker = new ChangeTracker();

        tracker.AcceptAllChanges();

        Assert.Empty(tracker.TrackedEntities);
    }

    [Fact]
    public void Clear_AfterAcceptAllChanges_RemovesAllEntities()
    {
        var tracker = new ChangeTracker();
        var entity = new TestEntity { Id = 1, Name = "Test" };
        tracker.TrackNew(entity);
        tracker.AcceptAllChanges();

        tracker.Clear();

        Assert.Empty(tracker.TrackedEntities);
    }

    [Fact]
    public void NewEntities_ReturnsOnlyAddedEntities()
    {
        var tracker = new ChangeTracker();
        var newEntity = new TestEntity { Id = 1, Name = "New" };
        var modifiedEntity = new TestEntity { Id = 2, Name = "Modified" };
        tracker.TrackNew(newEntity);
        tracker.TrackModified(modifiedEntity);

        var newEntities = tracker.NewEntities.ToList();

        Assert.Single(newEntities);
        Assert.Contains(newEntity, newEntities);
        Assert.DoesNotContain(modifiedEntity, newEntities);
    }

    [Fact]
    public void ModifiedEntities_ReturnsOnlyModifiedEntities()
    {
        var tracker = new ChangeTracker();
        var newEntity = new TestEntity { Id = 1, Name = "New" };
        var modifiedEntity = new TestEntity { Id = 2, Name = "Modified" };
        tracker.TrackNew(newEntity);
        tracker.TrackModified(modifiedEntity);

        var modifiedEntities = tracker.ModifiedEntities.ToList();

        Assert.Single(modifiedEntities);
        Assert.Contains(modifiedEntity, modifiedEntities);
        Assert.DoesNotContain(newEntity, modifiedEntities);
    }

    [Fact]
    public void DeletedEntities_ReturnsOnlyDeletedEntities()
    {
        var tracker = new ChangeTracker();
        var modifiedEntity = new TestEntity { Id = 1, Name = "Modified" };
        var deletedEntity = new TestEntity { Id = 2, Name = "Deleted" };
        tracker.TrackModified(modifiedEntity);
        tracker.TrackUnchanged(deletedEntity);
        tracker.TrackDeleted(deletedEntity);

        var deletedEntities = tracker.DeletedEntities.ToList();

        Assert.Single(deletedEntities);
        Assert.Contains(deletedEntity, deletedEntities);
        Assert.DoesNotContain(modifiedEntity, deletedEntities);
    }

    private class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
    }
}