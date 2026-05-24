// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Collections.Generic;
using Godot;
using Polytoria.Attributes;
using Polytoria.Physics;

namespace Polytoria.Datamodel;

[Instantiable]
public partial class Weld : Instance
{
	Instance? _part0;
	Instance? _part1;

	[Editable, ScriptProperty]
	public Instance? Part0
	{
		get => _part0;
		set
		{
			if (_part0 == value) return;
			if (value != null && value == _part1) return;

			Part? old0 = _part0 as Part;
			Part? old1 = _part1 as Part;

			_part0 = value;
			OnPropertyChanged();

			WeldAssemblyManager.OnWeldChanged(this, old0, old1, _part0 as Part, _part1 as Part);
		}
	}

	[Editable, ScriptProperty]
	public Instance? Part1
	{
		get => _part1;
		set
		{
			if (_part1 == value) return;
			if (value != null && value == _part0) return;

			Part? old0 = _part0 as Part;
			Part? old1 = _part1 as Part;

			_part1 = value;
			OnPropertyChanged();

			WeldAssemblyManager.OnWeldChanged(this, old0, old1, _part0 as Part, _part1 as Part);
		}
	}

	[ScriptMethod]
	public void Break()
	{
		_part0 = null;
		_part1 = null;

		OnPropertyChanged(nameof(Part0));
		OnPropertyChanged(nameof(Part1));

		WeldAssemblyManager.OnWeldRemoved(this);
	}

	public override void EnterTree()
	{
		base.EnterTree();

		if (_part0 == null && Parent is Physical)
		{
			_part0 = Parent;
		}

		WeldAssemblyManager.OnWeldChanged(this, null, null, _part0 as Part, _part1 as Part);
	}

	public override void PreDelete()
	{
		WeldAssemblyManager.OnWeldRemoved(this);
		base.PreDelete();
	}

	public override void ExitTree()
	{
		WeldAssemblyManager.OnWeldRemoved(this);
		base.ExitTree();
	}
}
