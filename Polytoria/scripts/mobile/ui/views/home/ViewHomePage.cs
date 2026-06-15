// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;
using Polytoria.Datamodel;
using Polytoria.Mobile.Utils;
using Polytoria.Schemas.API;

namespace Polytoria.Mobile.UI;

public partial class ViewHomePage : MobileViewBase
{
	[Export] private Label _usernameLabel = null!;
	private PolytorianModel _polytorian = null!;

	public override void _EnterTree()
	{
		_polytorian = new();
		GetNode("PlayerModel").AddChild(_polytorian.GDNode);
		_polytorian.InitEntry();

		PolyMobileAuthAPI.UserAuthenticated += OnUserAuthenticated;
		_polytorian.AvatarLoaded += OnAvatarLoaded;

		base._EnterTree();
	}

	public override void _ExitTree()
	{
		PolyMobileAuthAPI.UserAuthenticated -= OnUserAuthenticated;
		_polytorian.AvatarLoaded -= OnAvatarLoaded;

		base._ExitTree();
	}

	private void OnAvatarLoaded()
	{
		((Node3D)_polytorian.GDNode).Visible = true;
		_polytorian.Animator?.PlayOneShotAnimation("Wave");
		_polytorian.SetState(CharacterModel.CharacterModelStateEnum.Idle);
	}

	private void OnUserAuthenticated(APIMeResponse response)
	{
		LoadView();
	}

	private void LoadView()
	{
		_usernameLabel.Text = PolyMobileAuthAPI.CurrentUserInfo.Username;
		_polytorian.LoadAppearance(PolyMobileAuthAPI.CurrentUserInfo.Id);
	}

	public override void ShowView(object? args)
	{
		((Node3D)_polytorian.GDNode).Visible = false;
		if (_polytorian.IsAvatarLoaded)
		{
			OnAvatarLoaded();
		}
		base.ShowView(args);
	}
}
