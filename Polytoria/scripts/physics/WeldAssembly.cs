using System.Collections.Generic;
using System.Linq;
using Godot;
using Polytoria.Datamodel;
using Polytoria.Scripting.Datatypes;

namespace Polytoria.Physics;

public class WeldAssembly
{
	public Part Root = null!;
	public HashSet<Part> Parts = [];
	public Dictionary<Part, Transform3D> LocalTransforms = [];
	public bool Anchored;

	internal bool Physicalized; // Set to false in creator to let creators manipulate parts in weld assemblies, physics dont apply there anyways

	internal void Destroy()
	{
		if (Physicalized)
		{
			foreach (Part part in Parts)
			{
				part.DetachFromAssembly();
			}
		}

		Parts.Clear();
		LocalTransforms.Clear();
	}

	private static bool IsBetterRootCandidate(Part candidate, Part? current, Part? preferredRoot)
	{
		if (current == null)
			return true;

		if (candidate == preferredRoot && current != preferredRoot)
			return true;

		if (current == preferredRoot && candidate != preferredRoot)
			return false;

		if (candidate.Anchored != current.Anchored)
			return candidate.Anchored;

		if (!Mathf.IsEqualApprox(candidate.Mass, current.Mass))
			return candidate.Mass > current.Mass;

		return string.CompareOrdinal(candidate.NetworkedObjectID, current.NetworkedObjectID) < 0;
	}

	public static WeldAssembly Build(HashSet<Part> parts, Part? preferredRoot)
	{
		if (parts.Count == 0)
		{
			throw new System.ArgumentException("Empty part set given");
		}

		Part? root = null;
		float totalMass = 0;
		bool hasAnchoreds = false;

		// parts are picked in this order: preferred -> anchored -> largest mass -> lowest network id
		// try to do as many checks as possible in one loop to avoid looping multiple times
		foreach (Part part in parts)
		{
			hasAnchoreds |= part.Anchored;
			totalMass += Mathf.Max(part.Mass, Physical.MinMass);

			if (IsBetterRootCandidate(part, root, preferredRoot))
			{
				root = part;
			}
		}

		root ??= preferredRoot ?? throw new System.ArgumentException("Empty part set given");

		GD.Print($"[WeldAssembly] root={root.Name} id={root.NetworkedObjectID} pos={root.Position} loaded={root.Root.IsLoaded} session={root.Root.SessionType}");

		foreach (Part part in parts)
		{
			GD.Print($"  part={part.Name} id={part.NetworkedObjectID} pos={part.Position} local={(root.GDNode3D.GlobalTransform.AffineInverse() * part.GDNode3D.GlobalTransform).Origin}");
		}

		WeldAssembly assembly = new()
		{
			Root = root,
			Parts = parts,
			Anchored = hasAnchoreds
		};

		// in creator we dont want to reparent everything since it will block them from selecting anything other than the root part
		bool isCreator = root.Root != null && root.Root.SessionType == World.SessionTypeEnum.Creator;

		Color color = (Color)PTColor.Random().ToGDClass(); // random color for debugging

		foreach (Part part in parts)
		{
			part.ForceUpdateTransform();
		}

		Transform3D rootInv = root.GDNode3D.GlobalTransform.AffineInverse();

		// unfortunately we have to loop through the parts again to set up the assembly after root was picked
		foreach (Part part in parts)
		{
			Transform3D localTrans = rootInv * part.GDNode3D.GlobalTransform;
			assembly.LocalTransforms[part] = localTrans;
			part.Color = color; // debug

			if (!isCreator)
			{
				part.AttachToAssembly(assembly, root, localTrans);
			}
		}

		if (isCreator)
		{
			return assembly;
		}

		assembly.Physicalized = true;
		root.GDRigidBody.Mass = totalMass;
		foreach (Part part in parts)
		{
			part.UpdateFreeze();
		}

		return assembly;
	}
}