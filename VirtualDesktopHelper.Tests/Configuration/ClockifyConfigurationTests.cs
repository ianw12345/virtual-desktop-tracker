using FluentAssertions;
using VirtualDesktopHelper.Configuration;

namespace VirtualDesktopHelper.Tests.Configuration
{
    public class ClockifyConfigurationTests
    {
        [Fact]
        public void IsConfigured_RequiresApiKeyWorkspaceAndDefaultProject()
        {
            var configuration = new ClockifyConfiguration();

            configuration.IsConfigured().Should().BeFalse();

            configuration.ApiKey = "key";
            configuration.WorkspaceId = "workspace";
            configuration.DefaultProjectId = "project";

            configuration.IsConfigured().Should().BeTrue();
        }

        [Fact]
        public void GetProjectIdForDesktop_UsesExactDesktopMappingBeforeDefault()
        {
            var configuration = new ClockifyConfiguration
            {
                DefaultProjectId = "default",
                ProjectMappings = new()
                {
                    new ClockifyProjectMapping { ProjectId = "later", DesktopName = "Development", Order = 1 },
                    new ClockifyProjectMapping { ProjectId = "first", DesktopName = "Client work", Order = 0 }
                }
            };

            configuration.GetProjectIdForDesktop("client work").Should().Be("first");
            configuration.GetProjectIdForDesktop("Client work notes").Should().Be("default");
            configuration.GetProjectIdForDesktop("Personal").Should().Be("default");
        }
    }
}
