using Microsoft.EntityFrameworkCore;
using Signal.Domain.Entities;

namespace Signal.Application.Common.Interfaces;

public interface ISignalDbContext
{
    DbSet<Source> Sources { get; }
    DbSet<ContentItem> ContentItems { get; }
    DbSet<VideoContent> VideoContents { get; }
    DbSet<Story> Stories { get; }
    DbSet<StoryContent> StoryContents { get; }
    DbSet<Opportunity> Opportunities { get; }
    DbSet<GoogleSheetRecord> GoogleSheetRecords { get; }
    DbSet<UserPreference> UserPreferences { get; }
    DbSet<UserFeedback> UserFeedbacks { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
