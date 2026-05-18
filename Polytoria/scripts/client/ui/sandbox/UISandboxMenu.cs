using System;
using Godot;
using Polytoria.Client.Sandbox;
using Polytoria.Datamodel;
using Polytoria.Sandbox;
using Polytoria.Shared;

public partial class UISandboxMenu : Control
{
	public World Root = null!;
	public SandboxPlacementController Controller = null!;

	[Export] private Container _partsGrid = null!;
	[Export] private Container _colorsGrid = null!;
	[Export] private Container _materialsGrid = null!;
	[Export] private Button _closeButton = null!;

	public override void _Ready()
	{
		Visible = false;
		_closeButton.Pressed += () => Visible = false;

		CreateButtons();

		base._Ready();
	}

	private void CreateButtons()
	{
		// Parts
		foreach (SandboxCatalogItem item in Root.Sandbox.Items)
		{
			Button button = new Button()
			{
				Text = item.Name,
				CustomMinimumSize = new Vector2(120, 120),
			};

			button.Pressed += () =>
			{
				Controller.SelectedItemId = item.Id;
			};

			_partsGrid.AddChild(button);
		}

		// Colors
		Color[] colors = [
			Colors.White,
			Colors.Red,
			Colors.Green,
			Colors.Blue,
			Colors.Yellow,
			Colors.Cyan,
			Colors.Magenta,
			Colors.Orange,
			Colors.Purple,
			Colors.Brown,
			Colors.Gray
		];

		foreach (Color color in colors)
		{
			Button button = new Button()
			{
				CustomMinimumSize = new Vector2(42, 42),
				FocusMode = FocusModeEnum.None,
			};

			StyleBoxFlat style = new()
			{
				BgColor = color,
				CornerRadiusTopLeft = 8,
				CornerRadiusTopRight = 8,
				CornerRadiusBottomLeft = 8,
				CornerRadiusBottomRight = 8
			};

			button.AddThemeStyleboxOverride("normal", style);
			button.AddThemeStyleboxOverride("hover", style);
			button.AddThemeStyleboxOverride("pressed", style);

			button.Pressed += () =>
			{
				Controller.SelectedColor = color;
			};

			_colorsGrid.AddChild(button);
		}

		// Materials
		foreach (Part.PartMaterialEnum mat in Enum.GetValues<Part.PartMaterialEnum>())
		{
			Button button = new Button()
			{
				Text = mat.ToString(),
				CustomMinimumSize = new Vector2(120, 120),
				FocusMode = FocusModeEnum.None
			};

			button.Pressed += () =>
			{
				Controller.SelectedMaterial = mat;
			};

			_materialsGrid.AddChild(button);
		}
	}

	public void Toggle()
	{
		SetOpen(!Visible);
	}

	public void Close()
	{
		SetOpen(false);
	}

	private void SetOpen(bool open)
	{
		Visible = open;
		if (open)
		{
			GrabFocus();
		}
	}
}