using System;
using System.Collections.Generic;
using Godot;
using Polytoria.Client.Sandbox;
using Polytoria.Datamodel;
using Polytoria.Sandbox;

namespace Polytoria.Client.UI.Sandbox;

public partial class UISandboxMenu : Control
{
	public World Root = null!;
	public SandboxPlacementController Controller = null!;

	[Export] private Container _partsGrid = null!;
	[Export] private Container _colorsGrid = null!;
	[Export] private Container _materialsGrid = null!;
	[Export] private Button _closeButton = null!;
	[Export] private UISandboxPartPreview _partPreview = null!;

	private Dictionary<string, Texture2D> _partThumbnails = new();

	public override async void _Ready()
	{
		Visible = false;
		_closeButton.Pressed += () => Visible = false;

		SandboxThumbnailGenerator thumbGen = new SandboxThumbnailGenerator();
		AddChild(thumbGen);

		_partThumbnails = await thumbGen.GeneratePartThumbnails(Root.Sandbox.Items);
		thumbGen.QueueFree();

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
				Text = "",
				CustomMinimumSize = new Vector2(72, 72),
				ClipContents = true,
			};

			TextureRect thumb = new TextureRect()
			{
				CustomMinimumSize = new Vector2(68, 68),
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				MouseFilter = MouseFilterEnum.Ignore
			};

			if (_partThumbnails.TryGetValue(item.Id, out Texture2D? thumbnail))
			{
				thumb.Texture = thumbnail;
			}
			button.AddChild(thumb);

			button.Pressed += () =>
			{
				Controller.SelectedItemId = item.Id;
				_partPreview.SetPart(item, Controller.SelectedColor, Controller.SelectedMaterial);
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
				_partPreview.SetStyle(color, Controller.SelectedMaterial);
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
				FocusMode = FocusModeEnum.None,
				ClipContents = true
			};

			button.Pressed += () =>
			{
				Controller.SelectedMaterial = mat;
				_partPreview.SetStyle(Controller.SelectedColor, mat);
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