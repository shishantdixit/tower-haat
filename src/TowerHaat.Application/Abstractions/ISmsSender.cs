namespace TowerHaat.Application.Abstractions;

public interface ISmsSender
{
    Task SendOtpAsync(string phone, string code, CancellationToken cancellationToken);
}
