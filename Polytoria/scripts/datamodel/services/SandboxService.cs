using Godot;
using Polytoria.Attributes;
using Polytoria.Formats;
using Polytoria.Shared;
using System;
using System.IO;

namespace Polytoria.Datamodel.Services;

[Static("Sandbox")]
[ExplorerExclude]
[SaveIgnore]
public sealed partial class SandboxService : Instance
{
	private string _savePath = "";
	private string? _entryPath;

	private bool _saveStarted = false;
	private bool _saveFinished = false;

	public bool Enabled { get; private set; } = false;
	public bool IsSaving => _saveStarted && !_saveFinished;

	public void Attach(string savePath, string? entryPath = null)
	{
		_savePath = savePath;
		_entryPath = string.IsNullOrWhiteSpace(entryPath) ? null : entryPath;

		Enabled = true;

		Globals.BeforeQuit += OnBeforeQuit;
	}

	public override void ExitTree()
	{
		Globals.BeforeQuit -= OnBeforeQuit;
		base.ExitTree();
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
