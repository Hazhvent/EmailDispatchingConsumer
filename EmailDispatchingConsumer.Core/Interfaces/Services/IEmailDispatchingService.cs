using EmailDispatchingConsumer.Core.Dto;

namespace EmailDispatchingConsumer.Core.Interfaces.Services;

public interface IEmailDispatchingService
{
    /// <summary>
    /// Adjunta el archivo PDF en Base64 a la trama, autentica contra el endpoint OAuth2 de Pegasus
    /// y despacha la notificación por correo mediante la API REST.
    /// Devuelve true si la API confirmó el registro exitoso; de lo contrario, false.
    /// </summary>
    Task<bool> DispatchEmailNotificationAsync(
        TramaEnvio tramaEnvio,
        byte[] pdfBytes,
        CancellationToken cancellationToken = default);
}