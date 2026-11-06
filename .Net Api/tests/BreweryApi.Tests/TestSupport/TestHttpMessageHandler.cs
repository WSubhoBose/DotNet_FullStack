namespace BreweryApi.Tests.TestSupport;

public sealed class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<
        HttpRequestMessage,
        HttpResponseMessage>
        responseFactory;

    public TestHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage>
            responseFactory)
    {
        this.responseFactory =
            responseFactory
            ?? throw new ArgumentNullException(
                nameof(responseFactory));
    }

    public List<HttpRequestMessage> Requests { get; } =
        [];

    public List<string> RequestBodies { get; } = [];

    public HttpRequestMessage? LastRequest =>
        Requests.LastOrDefault();

    protected override Task<HttpResponseMessage>
        SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        Requests.Add(request);
        RequestBodies.Add(request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult() ?? string.Empty);

        return Task.FromResult(
            responseFactory(request));
    }
}