using System.Net;
using System.Text;

namespace AbsoluteBotTests.Infrastructure;

public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<object> _results = new();
    private readonly List<HttpRequestMessage> _requests = new();

    public IReadOnlyList<HttpRequestMessage> Requests => _requests;

    public HttpMethod? LastMethod { get; private set; }

    public string? LastRequestUri { get; private set; }

    public string? LastAuthorization { get; private set; }

    public string? LastBody { get; private set; }

    public void EnqueueResponse(HttpStatusCode statusCode, string content, string mediaType = "application/json")
    {
        _results.Enqueue(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, mediaType)
        });
    }

    public void EnqueueResponse(HttpResponseMessage response) => _results.Enqueue(response);

    public void EnqueueException(Exception exception) => _results.Enqueue(exception);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requests.Add(request);
        LastMethod = request.Method;
        LastRequestUri = request.RequestUri?.ToString();
        LastAuthorization = request.Headers.Authorization?.ToString();
        LastBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (_results.Count == 0)
            throw new InvalidOperationException("No queued HTTP responses in MockHttpMessageHandler.");

        var next = _results.Dequeue();
        if (next is Exception exception)
            throw exception;

        return (HttpResponseMessage)next;
    }
}