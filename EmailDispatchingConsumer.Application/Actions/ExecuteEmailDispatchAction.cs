namespace EmailDispatchingConsumer.Application.Actions;

using EmailDispatchingConsumer.Core.Dto;
using EmailDispatchingConsumer.Core.Interfaces.Services;
using MediatR;

public sealed record ExecuteEmailDispatchActionRequest(
    TramaEnvio TramaEnvio,
    byte[] PdfBytes
) : IRequest<bool>;

public class ExecuteEmailDispatchActionHandler(
    IEmailDispatchingService emailDispatchingService)
    : IRequestHandler<ExecuteEmailDispatchActionRequest, bool>
{
    public async Task<bool> Handle(
        ExecuteEmailDispatchActionRequest request,
        CancellationToken cancellationToken)
    {
        return await emailDispatchingService.DispatchEmailNotificationAsync(
            request.TramaEnvio,
            request.PdfBytes,
            cancellationToken
        );
    }
}