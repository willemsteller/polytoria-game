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
		List<Color> colors = GenerateColors();

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

	private static List<Color> GenerateColors(int targetCount = 165)
	{
		var tones = new (float Saturation, float Value)[]
		{
			(0.20f, 0.96f),
			(0.40f, 1.00f),
			(0.65f, 1.00f),
			(0.90f, 0.95f),

			(0.45f, 0.78f),
			(0.75f, 0.78f),

			(0.30f, 0.62f),
			(0.55f, 0.50f),
			(0.85f, 0.55f),
			(0.30f, 0.36f),
			(0.85f, 0.25f),
		};

		var baseColors = new Color[]
		{
			new Color("#0F0F0F"),
			new Color("#2F2F2F"),
			new Color("#5F5F5F"),
			new Color("#8C8C8C"),
			new Color("#B8B8B8"),
			new Color("#D9D9D9"),
			new Color("#F2F2F2"),
			new Color("#FFFFFF"),

			new Color("#4B2E1F"),
			new Color("#5C4033"),
			new Color("#8A5A3C"),
			new Color("#7A4E2A"),
			new Color("#A47545"),
			new Color("#C8A06A"),
			new Color("#E6D3B3"),
		};

		int hueCount = Mathf.CeilToInt(targetCount / (float)tones.Length);
		var colors = new List<Color>(baseColors);

		foreach (var tone in tones)
		{
			for (int i = 0; i < hueCount; i++)
			{
				float hue = i / (float)hueCount;
				hue %= 1f;

				Color c = Color.FromHsv(hue, tone.Saturation, tone.Value, 1.0f);
				colors.Add(c);

				if (colors.Count - baseColors.Length >= targetCount) return colors;
			}
		}

		return colors;
	}
}