using Godot;
using Polytoria.Datamodel;
using Polytoria.Datamodel.Services;
using Polytoria.Sandbox;
using Polytoria.Shared;

namespace Polytoria.Client.UI.Sandbox;

public partial class UISandboxPartPreview : TextureRect
{
	private SubViewport _viewport = null!;
	private Node3D _pivot = null!;
	private MeshInstance3D _mesh = null!;
	private Camera3D _camera = null!;
	private DirectionalLight3D _light = null!;

	private SandboxCatalogItem? _item;
	private Color _color = Colors.White;
	private Part.PartMaterialEnum _material = Part.PartMaterialEnum.Plastic;

	[Export] public float RotationSpeed { get; set; } = 35f;

	public override void _Ready()
	{
		StretchMode = StretchModeEnum.KeepAspectCentered;
		ExpandMode = ExpandModeEnum.IgnoreSize;

		_viewport = new SubViewport
		{
			Name = "PreviewViewport",
			Size = new Vector2I(256, 256),
			TransparentBg = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
			OwnWorld3D = true
		};

		AddChild(_viewport);

		_pivot = new Node3D
		{
			Name = "Pivot",
		};

		_viewport.AddChild(_pivot);

		_mesh = new MeshInstance3D
		{
			Name = "PreviewMesh",
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

		Texture = _viewport.GetTexture();

		base._Ready();
	}

	public override void _Process(double delta)
	{
		if (_pivot != null)
		{
			_pivot.RotateY(Mathf.DegToRad(RotationSpeed * (float)delta));
		}

		base._Process(delta);
	}

	public void SetPart(SandboxCatalogItem item, Color color, Part.PartMaterialEnum material)
	{
		_item = item;
		_color = color;
		_material = material;

		UpdateMesh();
	}

	public void SetStyle(Color color, Part.PartMaterialEnum material)
	{
		_color = color;
		_material = material;

		UpdateMaterial();
	}

	private void UpdateMesh()
	{
		if (_item == null || _mesh == null) return;

		Part.ShapeEnum shape = _item.Shape ?? Part.ShapeEnum.Brick;
		(Godot.Mesh mesh, Shape3D _) = Globals.LoadShape(shape.ToString());

		_mesh.Mesh = mesh;
		_mesh.Scale = SandboxService.GetItemSize(_item);

		if (_camera != null)
		{
			float max = Mathf.Max(_mesh.Scale.X, Mathf.Max(_mesh.Scale.Y, _mesh.Scale.Z));
			float distance = Mathf.Clamp(max * 2f, 4f, 20f);
			float height = distance * Mathf.Tan(Mathf.DegToRad(_camera.Fov / 2f));

			_camera.Position = new Vector3(distance * 0.55f, height, distance);
			_camera.LookAt(Vector3.Zero, Vector3.Up);
		}

		UpdateMaterial();
	}

	private void UpdateMaterial()
	{
		if (_mesh == null) return;

		Color color = _color;
		color.A = 1f;

		_mesh.MaterialOverride = Globals.LoadMaterial(_material, color.A);
		_mesh.SetInstanceShaderParameter("color", color);
	}
}