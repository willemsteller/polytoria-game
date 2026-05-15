using System.Text.Json.Serialization;

namespace Polytoria.Sandbox;

public class SandboxCatalog
{
	public SandboxCatalogItem[] Items { get; set; } = [];
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(SandboxCatalog))]
[JsonSerializable(typeof(SandboxCatalogItem))]
[JsonSerializable(typeof(SandboxCatalogItem[]))]
internal partial class SandboxCatalogJsonContext : JsonSerializerContext
{
}