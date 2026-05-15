using System;
using System.Collections.Generic;
using Godot;
using Polytoria.Datamodel;
using Polytoria.Datamodel.Services;
using Polytoria.Sandbox;
using Polytoria.Shared;

namespace Polytoria.Client.Sandbox;

public partial class SandboxPlacementController : Node
{
	public World Root = null!;
	public string SelectedItemId = string.Empty;
	private int _selectedIndex;

	private MeshInstance3D? _preview;
	private StandardMaterial3D? _previewMaterial;
	private string? _previewItemId;

	private float _yaw;

	public override void _Ready()
	{
		IReadOnlyList<SandboxCatalogItem> items = Root.Sandbox.Items;
		if (items.Count > 0)
		{
			_selectedIndex = 0;
			SelectedItemId = items[0].Id;
		}

		base._Ready();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("activate"))
		{
			TryPlace();
		}

		if (@event.IsActionPressed("sandbox_rotate"))
		{
			_yaw += 90f;
		}

		if (@event is InputEventKey k && k.Keycode == Key.B && k.IsPressed())
		{
			SelectNextItem();
		}
	}

	private void SelectNextItem()
	{
		IReadOnlyList<SandboxCatalogItem> items = Root.Sandbox.Items;
		if (items.Count == 0)
		{
			return;
		}

		_selectedIndex = (_selectedIndex + 1) % items.Count;
		SelectedItemId = items[_selectedIndex].Id;

		PT.Print("Selected sandbox item: ", items[_selectedIndex].Name);
	}

	private void TryPlace()
	{
		PlacementResult placement = GetCurrentPlacement();

		if (!placement.IsValid)
		{
			return;
		}

		Root.Sandbox.RequestPlace(
			SelectedItemId,
			placement.Position,
			placement.Rotation
		);
	}

	private PlacementResult GetCurrentPlacement()
	{
		Camera? camera = Root.Environment.CurrentCamera;

		if (camera == null) return new PlacementResult { IsValid = false };
		if (!Root.Sandbox.TryGetItem(SelectedItemId, out SandboxCatalogItem item)) return new PlacementResult { IsValid = false };

		var ray = camera.ScreenPointToRay(Root.Input.MousePosition);
		if (!ray.HasValue) return new PlacementResult { IsValid = false };
		if (ray.Value.Instance is Player || ray.Value.Instance is PolytorianModel || ray.Value.Instance is Accessory) return new PlacementResult { IsValid = false };

		Vector3 size = SandboxService.GetItemSize(item);

		return SandboxPlacementMath.FromRayHit(
			ray.Value.Position,
			ray.Value.Normal,
			size,
			_yaw,
			gridSize: 1f
		);
	}

	private void EnsurePreview(SandboxCatalogItem item)
	{
		if (_preview != null && _previewItemId == item.Id)
		{
			return;
		}

		if (_preview != null && IsInstanceValid(_preview))
		{
			_preview.QueueFree();
		}

		_preview = null;
		_previewMaterial = null;
		_previewItemId = null;

		if (item.Type != SandboxCatalogItemType.Part)
		{
			return;
		}

		Part.ShapeEnum shape = item.Shape ?? Part.ShapeEnum.Brick;
		(Godot.Mesh mesh, Shape3D _) = Globals.LoadShape(shape.ToString());

		_previewMaterial = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.2f, 1.0f, 0.2f, 0.35f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
		};

		_preview = new MeshInstance3D
		{
			Name = "PlacementPreview",
			Mesh = mesh,
			MaterialOverride = _previewMaterial,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};

		Root.Environment.GDNode.AddChild(_preview, false, InternalMode.Back);
		_previewItemId = item.Id;
	}

	public override void _Process(double delta)
	{
		if (!Root.Sandbox.IsSandbox)
		{
			return;
		}

		if (!Root.Sandbox.TryGetItem(SelectedItemId, out SandboxCatalogItem? item))
		{
			return;
		}

		EnsurePreview(item);

		if (_preview == null)
		{
			return;
		}

		PlacementResult placement = GetCurrentPlacement();
		_preview.Visible = placement.IsValid;

		if (!placement.IsValid)
		{
			return;
		}

		Vector3 size = SandboxService.GetItemSize(item);
		_preview.GlobalPosition = placement.Position;
		_preview.RotationDegrees = placement.Rotation;
		_preview.Scale = size;
	}
}