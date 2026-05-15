using System;
using System.Collections.Generic;
using Godot;
using Polytoria.Datamodel;
using Polytoria.Sandbox;
using Polytoria.Shared;

namespace Polytoria.Client.Sandbox;

public partial class SandboxPlacementController : Node
{
	public World Root = null!;
	public string SelectedItemId = "part_floor_8x1x8";
	private int _selectedIndex;

	private float _yaw;

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
		Camera? camera = Root.Environment.CurrentCamera;

		if (camera == null)
			return;

		var ray = camera.ScreenPointToRay(Root.Input.MousePosition);
		if (!ray.HasValue)
			return;

		Vector3 position = Snap(ray.Value.Position + ray.Value.Normal * 0.5f, 1f);
		Vector3 rotation = Vector3.Up * Mathf.DegToRad(_yaw);

		Root.Sandbox.RequestPlace(SelectedItemId, position, rotation);
	}

	private static Vector3 Snap(Vector3 position, float gridSize)
	{
		return new Vector3(
			Mathf.Round(position.X / gridSize) * gridSize,
			Mathf.Round(position.Y / gridSize) * gridSize,
			Mathf.Round(position.Z / gridSize) * gridSize
		);
	}
}