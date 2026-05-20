namespace App.Application.Dtos;

public enum YouTubeSignInState
{
    Unknown = 0,
    Checking = 1,
    SignedIn = 2,
    NotSignedIn = 3,
    Expired = 4,
    Disabled = 5,
    Error = 6
}
