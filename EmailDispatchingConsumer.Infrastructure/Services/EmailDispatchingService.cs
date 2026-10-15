namespace EmailDispatchingConsumer.Infrastructure.Services;

using EmailDispatchingConsumer.Core.Constants.Consumer;
using EmailDispatchingConsumer.Core.Constants.Messages;
using EmailDispatchingConsumer.Core.Dto;
using EmailDispatchingConsumer.Core.Interfaces.Services;
using EmailDispatchingConsumer.Core.Models;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

public sealed class EmailDispatchingService(
    HttpClient httpClient,
    IOptions<ApiPegasusSettings> pegasusOptions,
    ILoggingService<EmailDispatchingService> logger) : IEmailDispatchingService
{
    private readonly ApiPegasusSettings _pegasusSettings = pegasusOptions.Value;

    public async Task<bool> DispatchEmailNotificationAsync(
        TramaEnvio tramaEnvio,
        byte[] pdfBytes,
        CancellationToken cancellationToken = default)
    {
        string fileName = tramaEnvio.Documentos.Count > 0
            ? tramaEnvio.Documentos[0].nombre_documento
            : string.Empty;

        if (pdfBytes == null || pdfBytes.Length == 0)
        {
            logger.LogError(null, LogsConstants.ERROR_PDF_BYTES_EMPTY, fileName);
            return false;
        }

        try
        {
            string? token = await GetAccessTokenAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(token))
            {
                logger.LogError(null, LogsConstants.ERROR_PEGASUS_TOKEN_FAILED);
                return false;
            }

            string base64Content = Convert.ToBase64String(pdfBytes);
            if (tramaEnvio.Documentos.Count > 0)
            {
                tramaEnvio.Documentos[0].documento = base64Content;
            }
            else
            {
                tramaEnvio.Documentos.Add(new Documento
                {
                    documento = base64Content
                });
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, _pegasusSettings.NotificacionUrl)
            {
                Content = JsonContent.Create(tramaEnvio)
            };

            request.Headers.Authorization = new AuthenticationHeaderValue(
                SettingsConstants.HTTP_AUTH_BEARER_SCHEME,
                token
            );

            HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
            string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            // 1. Validar fallo a nivel de red (HTTP 4xx o 5xx)
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError(
                    null,
                    LogsConstants.ERROR_PEGASUS_NOTIFICATION_HTTP,
                    (int)response.StatusCode,
                    responseContent
                );
                return false;
            }

            // 2. Validar fallo a nivel de negocio (El HTTP 200 trampa de Pegasus)
            try
            {
                using var doc = JsonDocument.Parse(responseContent);

                if (doc.RootElement.TryGetProperty(SettingsConstants.PEGASUS_RESPONSE_ESTADO, out var estadoProp) && !estadoProp.GetBoolean())
                {
                    string mensajeError = doc.RootElement.TryGetProperty(SettingsConstants.PEGASUS_RESPONSE_MENSAJE, out var mensajeProp)
                        ? mensajeProp.GetString() ?? LogsConstants.ERROR_PEGASUS_NO_DETAIL
                        : LogsConstants.ERROR_PEGASUS_UNKNOWN;

                    logger.LogError(null, LogsConstants.ERROR_PEGASUS_API_REJECTED, mensajeError);

                    return false;
                }
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, LogsConstants.ERROR_PEGASUS_JSON_PARSE_FAILED, responseContent);
                return false;
            }

            // 3. Éxito absoluto
            logger.LogInfo(
                LogsConstants.INFO_PEGASUS_NOTIFICATION_SUCCESS,
                fileName,
                responseContent
            );

            return true;
        }
        catch (TaskCanceledException ex)
        {
            logger.LogError(ex, LogsConstants.ERROR_PEGASUS_API_TIMEOUT);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                LogsConstants.ERROR_PEGASUS_NOTIFICATION_FAILED,
                fileName,
                ex.Message
            );
            return false;
        }
    }

    private async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, _pegasusSettings.TokenUrl);
            tokenRequest.Headers.TryAddWithoutValidation(
                SettingsConstants.HTTP_HEADER_AUTHORIZATION,
                _pegasusSettings.AuthHeader
            );

            var formParams = new Dictionary<string, string>
            {
                { SettingsConstants.OAUTH_PARAM_GRANT_TYPE, SettingsConstants.OAUTH_GRANT_TYPE_PASSWORD },
                { SettingsConstants.OAUTH_PARAM_USERNAME, _pegasusSettings.Username },
                { SettingsConstants.OAUTH_PARAM_PASSWORD, _pegasusSettings.Password }
            };

            tokenRequest.Content = new FormUrlEncodedContent(formParams);

            HttpResponseMessage response = await httpClient.SendAsync(tokenRequest, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError(
                    null,
                    LogsConstants.ERROR_PEGASUS_TOKEN_HTTP,
                    (int)response.StatusCode,
                    error
                );
                return null;
            }

            string jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(jsonContent);

            if (doc.RootElement.TryGetProperty(SettingsConstants.OAUTH_RESPONSE_ACCESS_TOKEN, out var tokenProp))
            {
                return tokenProp.GetString();
            }

            logger.LogError(null, LogsConstants.ERROR_PEGASUS_TOKEN_INVALID_RESPONSE);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, LogsConstants.ERROR_PEGASUS_TOKEN_FAILED);
            return null;
        }
    }
}