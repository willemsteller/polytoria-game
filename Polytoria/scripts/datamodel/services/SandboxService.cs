using Godot;
using Polytoria.Attributes;
using Polytoria.Formats;
using Polytoria.Sandbox;
using Polytoria.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Polytoria.Datamodel.Services;

[Static("Sandbox")]
[ExplorerExclude]
[SaveIgnore]
public sealed partial class SandboxService : Instance
{
	private const string CatalogPath = "res://assets/sandbox/catalog/catalog.json";
	private string _savePath = "";
	private string? _entryPath;

	private bool _saveStarted = false;
	private bool _saveFinished = false;

	private readonly Dictionary<string, SandboxCatalogItem> _catalogItems = new();
	private SandboxCatalogItem[] _items = [];

	public bool Enabled { get; private set; } = false;
	public bool IsSaving => _saveStarted && !_saveFinished;
	public IReadOnlyList<SandboxCatalogItem> Items => _items;

	public void Attach(string savePath, string? entryPath = null)
	{
		_savePath = savePath;
		_entryPath = string.IsNullOrWhiteSpace(entryPath) ? null : entryPath;

		Enabled = true;

		Globals.BeforeQuit += OnBeforeQuit;
	}

	public override void Init()
	{
		LoadCatalog();
		base.Init();
	}

	public override void ExitTree()
	{
		Globals.BeforeQuit -= OnBeforeQuit;
		base.ExitTree();
	}

	private void LoadCatalog()
	{
		_catalogItems.Clear();

		if (!Godot.FileAccess.FileExists(CatalogPath))
		{
			PT.PrintErr($"Sandbox catalog file not found at path: {CatalogPath}");
			_items = [];
			return;
		}

		using Godot.FileAccess file = Godot.FileAccess.Open(CatalogPath, Godot.FileAccess.ModeFlags.Read);
		string json = file.GetAsText();

		SandboxCatalog? catalog = System.Text.Json.JsonSerializer.Deserialize(json, SandboxCatalogJsonContext.Default.SandboxCatalog);
		_items = catalog?.Items ?? [];

		foreach (SandboxCatalogItem item in _items)
		{
			if (string.IsNullOrWhiteSpace(item.Id))
			{
				PT.PrintErr("Sandbox catalog item has no id: ", item.Name);
				continue;
			}

			if (_catalogItems.ContainsKey(item.Id))
			{
				PT.PrintErr("Duplicate sandbox catalog item id: ", item.Id);
				continue;
			}

			_catalogItems[item.Id] = item;
		}
	}

	public async Task<Instance?> SpawnCatalogItem(string itemId, Vector3 position, Quaternion rotation)
	{
		if (!_catalogItems.TryGetValue(itemId, out SandboxCatalogItem? item))
		{
			PT.PrintErr("Unknown catalog item ID: ", itemId);
			return null;
		}

		Instance? instance = item.Type switch
		{
			SandboxCatalogItemType.Part => SpawnPart(item),
			// SandboxCatalogItemType.Model => await SpawnModel(item, position, rotation),
			_ => null
		};

		if (instance == null)
		{
			PT.PrintErr("Failed to spawn catalog item: ", itemId);
			return null;
		}

		if (instance is Dynamic d)
		{
			d.Position = position;
			d.Quaternion = rotation;
		}

		instance.Parent = Root.Environment;
		return instance;
	}

	private Instance SpawnPart(SandboxCatalogItem item)
	{
		Part part = Root.New<Part>();
		part.Name = item.Name;
		part.Shape = item.Shape ?? Part.ShapeEnum.Brick;
		part.Material = item.Material ?? Part.PartMaterialEnum.Plastic;
		part.Color = Color.FromHtml(item.Color ?? "#FFFFFF");
		part.Anchored = true;
		part.Size = ReadVec3(item.Size!);
		return part;
	}

	private Vector3 ReadVec3(float[] arr)
	{
		if (arr.Length != 3) throw new ArgumentException("Expected array of length 3 for Vector3");
		return new Vector3(arr[0], arr[1], arr[2]);
	}

	public void Save()
	{
		if (!Enabled) throw new InvalidOperationException("SandboxService is not enabled.");
		if (_saveStarted) return;

		_saveStarted = true;
		_saveFinished = false;

		try
		{
			PT.Print("Saving sandbox world...");
			Directory.CreateDirectory(Path.GetDirectoryName(_savePath)!);

			byte[] bytes = PackedFormat.PackRuntimeWorld(Root, _entryPath);

			string tmpPath = _savePath + ".tmp";
			string backupPath = _savePath + ".bak";

			File.WriteAllBytes(tmpPath, bytes);

			if (File.Exists(_savePath))
			{
				// Keep one backup
				File.Copy(_savePath, backupPath, overwrite: true);
			}
			File.Move(tmpPath, _savePath, overwrite: true);
		}
		catch (Exception e)
		{
			PT.PrintErr($"Failed to save sandbox world: {e}");
		}
		finally
		{
			_saveFinished = true;
		}

		PT.Print("Sandbox world save completed.");
	}

	private void OnBeforeQuit()
	{
		if (!Enabled || _saveStarted)
			return;

		Save();
	}
}
