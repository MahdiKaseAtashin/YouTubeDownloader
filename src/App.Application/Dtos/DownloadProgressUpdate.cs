namespace App.Application.Dtos;

public sealed record DownloadProgressUpdate(double Fraction, string StepMessage);
