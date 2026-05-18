using Godot;
using Polytoria.Attributes;
using Polytoria.Formats;
using Polytoria.Networking;
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

	[SyncVar]
	public bool IsSandbox { get; private set; } = false;

	public bool IsSaving => _saveStarted && !_saveFinished;
	public IReadOnlyList<SandboxCatalogItem> Items => _items;

	public void Attach(string savePath, string? entryPath = null)
	{
		_savePath = savePath;
		_entryPath = string.IsNullOrWhiteSpace(entryPath) ? null : entryPath;

		IsSandbox = true;

		CreateTools();

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

	private Instance GetObjectContainer()
	{
		Instance? existing = Root.Environment.FindChild("SandboxObjects");
		if (existing != null)
			return existing;

		Folder folder = Root.New<Folder>();
		folder.Name = "SandboxObjects";
		folder.Parent = Root.Environment;
		return folder;
	}

	public bool TryGetItem(string itemId, out SandboxCatalogItem item)
	{
		return _catalogItems.TryGetValue(itemId, out item!);
	}

	public Instance? SpawnCatalogItem(string itemId, Vector3 position, Vector3 rotation, Color color, Part.PartMaterialEnum material)
	{
		if (!_catalogItems.TryGetValue(itemId, out SandboxCatalogItem? item))
		{
			PT.PrintErr("Unknown catalog item ID: ", itemId);
			return null;
		}

		Instance? instance = item.Type switch
		{
			SandboxCatalogItemType.Part => SpawnPart(item, color, material),
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
			d.Rotation = rotation;
		}

		instance.Parent = GetObjectContainer();
		return instance;
	}

	private Instance SpawnPart(SandboxCatalogItem item, Color color, Part.PartMaterialEnum material)
	{
		Part part = Root.New<Part>();
		part.Name = item.Name;
		part.Shape = item.Shape ?? Part.ShapeEnum.Brick;
		part.Material = material;
		part.Color = color;
		part.Anchored = true;
		part.Size = GetItemSize(item);
		return part;
	}

	public void RequestPlace(string itemId, Vector3 position, Vector3 rotation, Color color, Part.PartMaterialEnum material)
	{
		RpcId(1, nameof(NetRequestPlace), itemId, position, rotation, color, material);
	}

	[NetRpc(AuthorityMode.Any, TransferMode = TransferMode.Reliable, AllowToServerOnly = true)]
	private void NetRequestPlace(string itemId, Vector3 position, Vector3 rotation, Color color, Part.PartMaterialEnum material)
	{
		if (!Root.Network.IsServer || !IsSandbox || Root.Entry?.IsSandbox != true)
		{
			return;
		}

		Player? player = Root.Players.GetPlayerFromPeerID(RemoteSenderId);
		if (player == null || !player.IsReady)
		{
			return;
		}

		if (!CanPlace(player, itemId, position))
		{
			return;
		}

		color = new Color(
			Mathf.Clamp(color.R, 0f, 1f),
			Mathf.Clamp(color.G, 0f, 1f),
			Mathf.Clamp(color.B, 0f, 1f)
		);

		SpawnCatalogItem(itemId, position, rotation, color, material);
	}

	public void RequestDelete(string netId)
	{
		RpcId(1, nameof(NetRequestDelete), netId);
	}

	[NetRpc(AuthorityMode.Any, TransferMode = TransferMode.Reliable, AllowToServerOnly = true)]
	private void NetRequestDelete(string netId)
	{
		if (!TryGetRequestingPlayer(out Player? player))
		{
			return;
		}

		if (!TryGetSandboxItem(netId, out Dynamic? item))
		{
			return;
		}

		item!.Destroy();
	}

	public void RequestPaint(string netId, Color color, Part.PartMaterialEnum material)
	{
		RpcId(1, nameof(NetRequestPaint), netId, color, material);
	}

	[NetRpc(AuthorityMode.Any, TransferMode = TransferMode.Reliable, AllowToServerOnly = true)]
	private void NetRequestPaint(string netId, Color color, Part.PartMaterialEnum material)
	{
		if (!TryGetRequestingPlayer(out Player? player))
			return;

		if (!TryGetSandboxItem(netId, out Dynamic? item))
		{
			return;
		}

		if (item is Part part)
		{
			part.Color = new Color(
				Mathf.Clamp(color.R, 0f, 1f),
				Mathf.Clamp(color.G, 0f, 1f),
				Mathf.Clamp(color.B, 0f, 1f)
			);

			part.Material = material;
		}
	}

	private bool CanPlace(Player player, string itemId, Vector3 position)
	{
		if (!_catalogItems.ContainsKey(itemId))
		{
			return false;
		}

		if (player.Position.DistanceSquaredTo(position) > 512 * 512)
		{
			return false;
		}

		// TODO: collision check, permissions etc

		return true;
	}

	public void Save()
	{
		if (!IsSandbox) throw new InvalidOperationException("SandboxService is not enabled.");
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

	private void CreateTools()
	{
		if (!Root.Network.IsServer) return;

		Inventory? defaultInv = Root.PlayerDefaults.Inventory;
		if (defaultInv == null)
		{
			PT.PrintErr("PlayerDefaults.Inventory is null, cannot create sandbox tools.");
			return;
		}

		CreateTool("Build", "SandboxTools.Build", defaultInv);
		CreateTool("Delete", "SandboxTools.Delete", defaultInv);
		CreateTool("Paint", "SandboxTools.Paint", defaultInv);
	}

	private void CreateTool(string name, string tag, Inventory inventory)
	{
		if (inventory.FindChild(name) != null)
		{
			return;
		}

		Tool tool = Root.New<Tool>();
		tool.Name = name;
		tool.Archivable = false;
		tool.Droppable = false;
		tool.Tags = ["SandboxTool", tag];
		tool.Parent = inventory;
	}

	private void OnBeforeQuit()
	{
		if (!IsSandbox || _saveStarted)
			return;

		Save();
	}

	private bool TryGetRequestingPlayer(out Player? player)
	{
		player = null;

		if (!Root.Network.IsServer || !IsSandbox || Root.Entry?.IsSandbox != true)
		{
			return false;
		}

		player = Root.Players.GetPlayerFromPeerID(RemoteSenderId);

		if (player == null || !player.IsReady)
		{
			return false;
		}

		return true;
	}

	private bool TryGetSandboxItem(string netId, out Dynamic? item)
	{
		item = null;

		NetworkedObject? obj = Root.GetNetObjectFromID(netId);

		if (obj == null || obj is not Dynamic d)
		{
			return false;
		}

		Instance? container = Root.Environment.FindChild("SandboxObjects");

		if (container == null || !d.IsDescendantOf(container))
		{
			return false;
		}

		item = d;
		return true;
	}

	private static Vector3 ReadVec3(float[] arr)
	{
		if (arr.Length != 3) throw new ArgumentException("Expected array of length 3 for Vector3");
		return new Vector3(arr[0], arr[1], arr[2]);
	}

	public static Vector3 GetItemSize(SandboxCatalogItem item)
	{
		return item.Size != null ? ReadVec3(item.Size) : Vector3.One;
	}
}