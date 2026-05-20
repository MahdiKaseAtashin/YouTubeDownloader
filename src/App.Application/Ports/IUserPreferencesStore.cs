using App.Application.Dtos;

namespace App.Application.Ports;

public interface IUserPreferencesStore
{
    string? LastOutputFolder { get; }
    YouTubeAuthSettings YouTubeAuthSettings { get; }
    Task LoadAsync(CancellationToken cancellationToken = default);
    Task SaveLastOutputFolderAsync(string folderPath, CancellationToken cancellationToken = default);
    Task SaveYouTubeAuthSettingsAsync(YouTubeAuthSettings settings, CancellationToken cancellationToken = default);
}
