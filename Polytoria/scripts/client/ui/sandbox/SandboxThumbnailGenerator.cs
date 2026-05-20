using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Polytoria.Datamodel;
using Polytoria.Datamodel.Services;
using Polytoria.Sandbox;
using Polytoria.Shared;

namespace Polytoria.Client.UI.Sandbox;

public partial class SandboxThumbnailGenerator : Node
{
	private const int ThumbnailSize = 128;
	private SubViewport _viewport = null!;
	private Node3D _pivot = null!;
	private MeshInstance3D _mesh = null!;
	private Camera3D _camera = null!;
	private DirectionalLight3D _light = null!;

	public override void _Ready()
	{
		_viewport = new SubViewport
		{
			Name = "ThumbnailGeneratorViewport",
			Size = new Vector2I(ThumbnailSize, ThumbnailSize),
			TransparentBg = true,
			OwnWorld3D = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled
		};
		AddChild(_viewport);

		_pivot = new Node3D
		{
			Name = "Pivot"
		};
		_viewport.AddChild(_pivot);

		_mesh = new MeshInstance3D
		{
			Name = "ThumbnailMesh",
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
		};
		_pivot.AddChild(_mesh);

		_camera = new Camera3D
		{
			Name = "Camera",
			Position = new Vector3(4f, 3f, 6f),
			Fov = 35f
		};

		_camera.LookAt(Vector3.Zero, Vector3.Up);
		_viewport.AddChild(_camera);

		_light = new DirectionalLight3D
		{
			Name = "Light",
			RotationDegrees = new Vector3(-45f, 35f, 0f),
			LightEnergy = 1.2f
		};
		_viewport.AddChild(_light);

		base._Ready();
	}

	public async Task<Dictionary<string, Texture2D>> GeneratePartThumbnails(IReadOnlyList<SandboxCatalogItem> items)
	{
		Dictionary<string, Texture2D> results = new();

		foreach (SandboxCatalogItem item in items)
		{
			if (item.Type != SandboxCatalogItemType.Part) continue;

			Texture2D thumbnail = await Render(item);
			results[item.Id] = thumbnail;
		}

		return results;
	}

	public async Task<Texture2D> Render(SandboxCatalogItem item)
	{
		Part.ShapeEnum shape = item.Shape ?? Part.ShapeEnum.Brick;
		(Godot.Mesh mesh, Shape3D _) = Globals.LoadShape(shape.ToString());

		_mesh.Mesh = mesh;
		_mesh.Scale = SandboxService.GetItemSize(item);

		Color color = Color.FromHtml("#CCCCCC");
		Part.PartMaterialEnum material = item.Material ?? Part.PartMaterialEnum.Plastic;

		_mesh.MaterialOverride = Globals.LoadMaterial(material, color.A);
		_mesh.SetInstanceShaderParameter("color", color);

		if (_camera != null)
		{
			float max = Mathf.Max(_mesh.Scale.X, Mathf.Max(_mesh.Scale.Y, _mesh.Scale.Z));
			float distance = Mathf.Clamp(max * 2f, 4f, 20f);
			float height = distance * Mathf.Tan(Mathf.DegToRad(_camera.Fov / 2f));

			_camera.Position = new Vector3(distance * 0.55f, height, distance);
			_camera.LookAt(Vector3.Zero, Vector3.Up);
		}

		_pivot.RotationDegrees = Vector3.Up * 35f;
		_viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;

		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		Image image = _viewport.GetTexture().GetImage();
		ImageTexture tex = ImageTexture.CreateFromImage(image);

		_viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;

		return tex;
	}
}