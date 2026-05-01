using System.Reflection;
using mssqlMCP.Tools;
using Xunit;

namespace mssqlMCP.Tests.Tools;

public class McpToolRegistrationTests
{
    [Fact]
    public void McpServerToolNames_AreUnique()
    {
        var toolTypes = new[]
        {
            typeof(SqlServerTools),
            typeof(ConnectionManagerTool),
            typeof(SecurityTool),
            typeof(ApiKeyManagementTool)
        };

        var registrations = toolTypes
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(method => new
                {
                    TypeName = type.Name,
                    MethodName = method.Name,
                    ToolName = GetMcpToolName(method)
                }))
            .Where(registration => !string.IsNullOrWhiteSpace(registration.ToolName))
            .ToList();

        var duplicates = registrations
            .GroupBy(registration => registration.ToolName!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(item => $"{item.TypeName}.{item.MethodName}"))}")
            .ToList();

        Assert.Empty(duplicates);
    }

    private static string? GetMcpToolName(MethodInfo method)
    {
        var attribute = method.GetCustomAttributes()
            .FirstOrDefault(attr => attr.GetType().Name == "McpServerToolAttribute");

        return attribute?.GetType().GetProperty("Name")?.GetValue(attribute)?.ToString();
    }
}
