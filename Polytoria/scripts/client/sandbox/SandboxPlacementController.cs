using System.Collections.Generic;
using Godot;
using Polytoria.Client.UI;
using Polytoria.Datamodel;
using Polytoria.Datamodel.Services;
using Polytoria.Sandbox;
using Polytoria.Shared;

namespace Polytoria.Client.Sandbox;

public partial class SandboxPlacementController : Node
{
	private const float PreviewPosLerp = 50f;
	private const float PreviewRotLerp = 25f;

	public World Root = null!;
	public string SelectedItemId = string.Empty;
	private int _selectedIndex;

	private MeshInstance3D? _preview;
	private StandardMaterial3D? _previewMaterial;
	private string? _previewItemId;

	private float _yaw;

	private Vector3 _previewTargetPos;
	private Quaternion _previewTargetRot;

	private ToolMode _currentMode = ToolMode.None;
	private Color _selectedColor = new Color(1.0f, 0.3f, 0.2f);
	private Part.PartMaterialEnum _selectedMaterial = Part.PartMaterialEnum.Plastic;

	private UISandboxMenu? _menu;

	public Color SelectedColor
	{
		get => _selectedColor;
		set
		{
			_selectedColor = value;
		}
	}

	public Part.PartMaterialEnum SelectedMaterial
	{
		get => _selectedMaterial;
		set
		{
			_selectedMaterial = value;
		}
	}

	public override async void _Ready()
	{
		IReadOnlyList<SandboxCatalogItem> items = Root.Sandbox.Items;
		if (items.Count > 0)
		{
			_selectedIndex = 0;
			SelectedItemId = items[0].Id;
		}

		CoreUIRoot coreUI = await Root.CoreUI.WaitRoot();

		PackedScene scene = GD.Load<PackedScene>("res://scenes/client/ui/sandbox/build_menu.tscn");
		_menu = scene.Instantiate<UISandboxMenu>();
		_menu.Name = "SandboxMenu";
		_menu.Root = Root;
		_menu.Controller = this;

		coreUI.AddChild(_menu, true, InternalMode.Front);
		base._Ready();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (Root.CoreUI?.CoreUI != null && Root.CoreUI.CoreUI.CoreUIActive)
		{
			return;
		}

		if (@event is InputEventKey k && k.Keycode == Key.B && k.IsPressed())
		{
			if (_menu != null)
			{
				_menu.Toggle();
			}
		}

		switch (_currentMode)
		{
			case ToolMode.Build:
				BuildHandleInput(@event);
				break;
			case ToolMode.Delete:
				DeleteHandleInput(@event);
				break;
			case ToolMode.Paint:
				PaintHandleInput(@event);
				break;
		}
	}

	private void BuildHandleInput(InputEvent @event)
	{
		if (@event.IsActionPressed("activate"))
		{
			TryPlace();
		}

		if (@event.IsActionPressed("sandbox_rotate"))
		{
			_yaw += 90f;
		}
	}

	private void DeleteHandleInput(InputEvent @event)
	{
		if (@event.IsActionPressed("activate"))
		{
			TryDelete();
		}
	}

	private void PaintHandleInput(InputEvent @event)
	{
		if (@event.IsActionPressed("activate"))
		{
			if (@event is InputEventMouseButton mouse && mouse.ShiftPressed)
			{
				SampleStyle();
			}
			else
			{
				TryPaint();
			}
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
			placement.Rotation,
			_selectedColor,
			_selectedMaterial
		);
	}

	private void TryDelete()
	{
		Part? target = GetTargetPart();

		if (target == null)
		{
			return;
		}

		Root.Sandbox.RequestDelete(target.NetworkedObjectID);
	}

	private void TryPaint()
	{
		Part? target = GetTargetPart();

		if (target == null)
		{
			return;
		}

		Root.Sandbox.RequestPaint(target.NetworkedObjectID, _selectedColor, _selectedMaterial);
	}

	private void SampleStyle()
	{
		Part? target = GetTargetPart();

		if (target == null)
		{
			return;
		}

		_selectedColor = target.Color;
		_selectedMaterial = target.Material;
	}

	private PlacementResult GetCurrentPlacement()
	{
		Camera? camera = Root.Environment.CurrentCamera;

		if (camera == null) return new PlacementResult { IsValid = false };
		if (!Root.Sandbox.TryGetItem(SelectedItemId, out SandboxCatalogItem item)) return new PlacementResult { IsValid = false };

		var ray = camera.ScreenPointToRay(Root.Input.MousePosition, [Root.Players]);
		if (!ray.HasValue) return new PlacementResult { IsValid = false };

		Vector3 size = SandboxService.GetItemSize(item);

		Instance? hit = ray.Value.Instance;

		if (hit != null && hit is Part part)
		{
			if (part.Shape != Part.ShapeEnum.Sphere && part.Shape != Part.ShapeEnum.Cylinder) // skip these for now
			{
				PlacementResult clamped = SandboxPlacementMath.FromInstance(
					part,
					ray.Value.Position,
					ray.Value.Normal,
					size,
					_yaw,
					gridSize: 1f,
					clampToFace: true
				);

				PlacementResult unclamped = SandboxPlacementMath.FromInstance(
					part,
					ray.Value.Position,
					ray.Value.Normal,
					size,
					_yaw,
					gridSize: 1f,
					clampToFace: false
				);

				if (!clamped.IsValid)
				{
					return new PlacementResult { IsValid = false };
				}

				if (unclamped.IsValid && clamped.Position.DistanceSquaredTo(unclamped.Position) < 0.01f && IsPlacementSupported(unclamped, size))
				{
					return unclamped;
				}
			}
		}

		return SandboxPlacementMath.FromRayHit(
			ray.Value.Position,
			ray.Value.Normal,
			size,
			_yaw,
			gridSize: 1f
		);
	}

	private bool IsPlacementSupported(PlacementResult placement, Vector3 itemSize)
	{
		Vector3 normal = placement.Normal.Normalized();
		Basis basis = Basis.FromEuler(placement.Rotation * Mathf.DegToRad(1f));

		float halfExtents = SandboxPlacementMath.GetProjectedHalfExtents(itemSize, basis, normal);

		GetSupportPlaneAxes(normal, out Vector3 axisA, out Vector3 axisB);

		float halfA = SandboxPlacementMath.GetProjectedFullExtent(itemSize, basis, axisA) * 0.5f;
		float halfB = SandboxPlacementMath.GetProjectedFullExtent(itemSize, basis, axisB) * 0.5f;

		float inset = 0.08f;
		halfA = Mathf.Max(halfA - inset, 0f);
		halfB = Mathf.Max(halfB - inset, 0f);

		Vector3 contactCenter = placement.Position - normal * halfExtents;

		Vector3[] samples = [
			contactCenter + axisA * halfA + axisB * halfB,
			contactCenter + axisA * halfA - axisB * halfB,
			contactCenter - axisA * halfA + axisB * halfB,
			contactCenter - axisA * halfA - axisB * halfB
		];

		int supported = 0;

		foreach (Vector3 sample in samples)
		{
			Vector3 origin = sample + normal * 0.1f;
			Vector3 direction = -normal;

			var ray = Root.Environment.Raycast(origin, direction, 0.25f);

			if (!ray.HasValue || ray.Value.Instance == null)
			{
				continue;
			}

			if (ray.Value.Instance is Player || ray.Value.Instance is PolytorianModel || ray.Value.Instance is Accessory)
			{
				continue;
			}

			if (ray.Value.Normal.Normalized().Dot(normal) < 0.9f)
			{
				continue;
			}

			supported++;
		}

		return supported >= 4; // all for now
	}

	private static void GetSupportPlaneAxes(Vector3 normal, out Vector3 axisA, out Vector3 axisB)
	{
		Vector3 abs = normal.Abs();

		if (abs.Y >= abs.X && abs.Y >= abs.Z)
		{
			axisA = Vector3.Right;
			axisB = Vector3.Forward * -1f;
		}
		else if (abs.X >= abs.Y && abs.X >= abs.Z)
		{
			axisA = Vector3.Forward * -1f;
			axisB = Vector3.Up;
		}
		else
		{
			axisA = Vector3.Right;
			axisB = Vector3.Up;
		}
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
			AlbedoColor = new Color(0.2f, 1.0f, 1.0f, 0.15f),
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

		_preview.Position = _previewTargetPos;
		_preview.Quaternion = _previewTargetRot;
	}

	private void UpdatePreview()
	{
		if (!Root.Sandbox.TryGetItem(SelectedItemId, out SandboxCatalogItem? item))
		{
			return;
		}

		EnsurePreview(item);

		if (_preview == null)
		{
			return;
		}

		if (_previewMaterial != null)
		{
			Color previewColor = SelectedColor;
			previewColor.A = 0.15f;
			_previewMaterial.AlbedoColor = previewColor;
		}

		PlacementResult placement = GetCurrentPlacement();
		_preview.Visible = placement.IsValid;

		if (!placement.IsValid)
		{
			return;
		}

		Vector3 size = SandboxService.GetItemSize(item);
		_preview.Scale = size * 1.001f;

		_previewTargetPos = placement.Position;
		_previewTargetRot = Quaternion.FromEuler(placement.Rotation * Mathf.DegToRad(1f));
	}

	private Part? GetTargetPart()
	{
		Camera? camera = Root.Environment.CurrentCamera;

		if (camera == null) return null;

		var ray = camera.ScreenPointToRay(Root.Input.MousePosition, [Root.Players]);
		if (!ray.HasValue) return null;

		Instance? hit = ray.Value.Instance;

		Instance? objects = Root.Environment.FindChild("SandboxObjects");

		if (objects == null)
		{
			return null;
		}

		if (hit != null && hit is Part part && part.IsDescendantOf(objects))
		{
			return part;
		}

		return null;
	}

	public override void _Process(double delta)
	{
		if (!Root.Sandbox.IsSandbox)
		{
			return;
		}

		_currentMode = GetCurrentMode();

		if (_currentMode == ToolMode.Build)
		{
			UpdatePreview();
		}
		else
		{
			if (_preview != null)
			{
				_preview.Visible = false;
			}
		}

		if (_preview != null)
		{
			_preview.Position = _preview.Position.Lerp(_previewTargetPos, (float)delta * PreviewPosLerp);
			_preview.Quaternion = _preview.Quaternion.Slerp(_previewTargetRot.Normalized(), (float)delta * PreviewRotLerp);
		}
	}

	private ToolMode GetCurrentMode()
	{
		Player? player = Root.Players.LocalPlayer;

		if (player == null) return ToolMode.None;

		Tool? tool = player.HoldingTool;

		if (tool == null || !tool.HasTag("SandboxTool")) return ToolMode.None;

		if (tool.HasTag("SandboxTools.Build"))
		{
			return ToolMode.Build;
		}
		else if (tool.HasTag("SandboxTools.Delete"))
		{
			return ToolMode.Delete;
		}
		else if (tool.HasTag("SandboxTools.Paint"))
		{
			return ToolMode.Paint;
		}

		return ToolMode.None;
	}

	private enum ToolMode
	{
		None,
		Build,
		Delete,
		Paint
	}
}