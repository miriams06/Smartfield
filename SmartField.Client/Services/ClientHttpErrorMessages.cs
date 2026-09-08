namespace SmartField.Client.Services;

public static class ClientHttpErrorMessages
{
    public const string CommunicationFailure =
        "Não foi possível contactar o servidor SmartField.";

    public const string Timeout =
        "O servidor SmartField demorou demasiado a responder.";

    public const string Unexpected =
        "Ocorreu um erro ao processar o pedido.";

    public static string FromException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            TaskCanceledException => Timeout,
            HttpRequestException => CommunicationFailure,
            _ => Unexpected
        };
    }
}
