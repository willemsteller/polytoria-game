using static Polytoria.Datamodel.Part;

namespace Polytoria.Sandbox;

public class SandboxCatalogItem
{
	public required string Id { get; set; }
	public required string Name { get; set; }
	public required string Category { get; set; }

	public SandboxCatalogItemType Type { get; set; }
	public string? Model { get; set; }

	public ShapeEnum? Shape { get; set; }
	public PartMaterialEnum? Material { get; set; }
	public string? Color { get; set; }
	public float[]? Size { get; set; }
}

public enum SandboxCatalogItemType
{
	Part,
	Model
}