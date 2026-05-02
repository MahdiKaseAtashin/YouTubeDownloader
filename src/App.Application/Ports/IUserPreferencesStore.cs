namespace App.Application.Ports;

public interface IUserPreferencesStore
{
    string? LastOutputFolder { get; }
    Task LoadAsync(CancellationToken cancellationToken = default);
    Task SaveLastOutputFolderAsync(string folderPath, CancellationToken cancellationToken = default);
}
