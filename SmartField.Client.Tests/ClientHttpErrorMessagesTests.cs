using SmartField.Client.Services;

namespace SmartField.Client.Tests;

public class ClientHttpErrorMessagesTests
{
    [Fact]
    public void HttpRequestException_IsCommunicationFailure()
    {
        var message = ClientHttpErrorMessages.FromException(
            new HttpRequestException("network details"));

        Assert.Equal(
            "Não foi possível contactar o servidor SmartField.",
            message);
        Assert.DoesNotContain("network details", message);
    }

    [Fact]
    public void TaskCanceledException_IsTimeout()
    {
        var message = ClientHttpErrorMessages.FromException(
            new TaskCanceledException("timeout details"));

        Assert.Equal(
            "O servidor SmartField demorou demasiado a responder.",
            message);
    }
}
