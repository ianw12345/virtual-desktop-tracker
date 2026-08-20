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
        public void GetProjectIdForDesktop_UsesFirstMatchingKeywordBeforeDefault()
        {
            var configuration = new ClockifyConfiguration
            {
                DefaultProjectId = "default",
                ProjectMappings = new()
                {
                    new ClockifyProjectMapping { ProjectId = "later", Keywords = new() { "work" }, Order = 1 },
                    new ClockifyProjectMapping { ProjectId = "first", Keywords = new() { "client" }, Order = 0 }
                }
            };

            configuration.GetProjectIdForDesktop("Client work").Should().Be("first");
            configuration.GetProjectIdForDesktop("Personal").Should().Be("default");
        }
    }
}
