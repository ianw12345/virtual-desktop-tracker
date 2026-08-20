using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using Moq;
using VirtualDesktopHelper.Configuration;
using VirtualDesktopHelper.Interfaces;
using VirtualDesktopHelper.Models;
using VirtualDesktopHelper.Services;

namespace VirtualDesktopHelper.Tests.Services
{
    public class ClockifyApiServiceTests
    {
        [Fact]
        public async Task UploadAsync_PostsClockifyEntryWithApiKeyAndUtcTimes()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Created));
            using var client = new HttpClient(handler) { BaseAddress = new Uri("https://clockify.test/api/v1/") };
            var consolidation = new Mock<IUsageConsolidationService>();
            var entry = new DesktopUsageEntry
            {
                DesktopName = "Development",
                StartTime = new DateTime(2026, 8, 20, 9, 0, 0, DateTimeKind.Local),
                EndTime = new DateTime(2026, 8, 20, 10, 30, 0, DateTimeKind.Local)
            };
            consolidation.Setup(service => service.ConsolidateUsageEntries(It.IsAny<List<DesktopUsageEntry>>()))
                .Returns((List<DesktopUsageEntry> entries) => entries);
            var configuration = new ClockifyConfiguration
            {
                ApiKey = "test-key",
                WorkspaceId = "workspace-1",
                DefaultProjectId = "project-1",
                IsBillable = true
            };
            client.DefaultRequestHeaders.Add("X-Api-Key", configuration.ApiKey);

            using var service = new ClockifyApiService(configuration, consolidation.Object, client);
            ClockifyUploadResult result = await service.UploadAsync(new List<DesktopUsageEntry> { entry }, currentDayOnly: false);

            result.Success.Should().BeTrue();
            handler.Requests.Should().ContainSingle();
            RecordedRequest request = handler.Requests.Single();
            request.Method.Should().Be(HttpMethod.Post);
            request.Uri.Should().Be("https://clockify.test/api/v1/workspaces/workspace-1/time-entries");
            request.ApiKey.Should().Be("test-key");
            request.Body.Should().Contain("\"projectId\":\"project-1\"");
            request.Body.Should().Contain("\"description\":\"Virtual desktop: Development\"");
            request.Body.Should().Contain("\"billable\":true");
            request.Body.Should().Contain("\"start\":\"");
            request.Body.Should().Contain("Z\"");
        }

        [Fact]
        public async Task TestConnectionAsync_RejectsWorkspaceThatIsNotAccessible()
        {
            var handler = new RecordingHandler(_ => JsonResponse("[{\"id\":\"other\",\"name\":\"Other\"}]"));
            using var client = new HttpClient(handler) { BaseAddress = new Uri("https://clockify.test/api/v1/") };
            var configuration = new ClockifyConfiguration { ApiKey = "test-key", WorkspaceId = "missing", DefaultProjectId = "project" };
            client.DefaultRequestHeaders.Add("X-Api-Key", configuration.ApiKey);

            using var service = new ClockifyApiService(configuration, httpClient: client);

            Func<Task> action = () => service.TestConnectionAsync();
            await action.Should().ThrowAsync<InvalidOperationException>();
        }

        private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        private sealed class RecordingHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;
            public List<RecordedRequest> Requests { get; } = new();

            public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
            {
                _responseFactory = responseFactory;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(new RecordedRequest(
                    request.Method,
                    request.RequestUri?.ToString() ?? "",
                    request.Headers.TryGetValues("X-Api-Key", out var values) ? values.SingleOrDefault() ?? "" : "",
                    request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
                return _responseFactory(request);
            }
        }

        private sealed record RecordedRequest(HttpMethod Method, string Uri, string ApiKey, string Body);
    }
}
