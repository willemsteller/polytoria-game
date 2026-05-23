using System.Collections.Generic;
using System.Linq;
using Godot;
using Polytoria.Datamodel;

namespace Polytoria.Physics;

public class WeldAssembly
{
	public Part Root = null!;
	public HashSet<Part> Parts = [];
	public Dictionary<Part, Transform3D> LocalTransforms = [];
	public bool Anchored;

	internal void Destroy()
	{
		foreach (Part part in Parts)
		{
			part.DetachFromAssembly();
		}

		Parts.Clear();
		LocalTransforms.Clear();
	}

	public static WeldAssembly Build(HashSet<Part> parts, Part? preferredRoot)
	{
		if (parts.Count == 0)
		{
			throw new System.ArgumentException("Empty part set given");
		}

		Part? root;
		float totalMass = 0;
		bool hasAnchoreds = false;

		// parts are picked in this order: preferred -> anchored -> largest mass -> lowest network id
		// try to do as many checks as possible in one loop to avoid looping multiple times
		{
			// keep these variables scoped
			Part? anchored = null;
			Part? largestMass = null;
			Part? first = null;
			Part? lowestNetID = null;
			float largestMassValue = float.MinValue;

			foreach (Part part in parts)
			{
				if (first == null)
				{
					first = part;
				}

				if (part.Anchored)
				{
					anchored ??= part;
					hasAnchoreds = true;
				}

				if (largestMass == null || part.Mass > largestMassValue)
				{
					largestMass = part;
					largestMassValue = part.Mass;
				}

				if (lowestNetID == null || part.NetworkedObjectID.Hash() < lowestNetID.NetworkedObjectID.Hash())
				{
					lowestNetID = part;
				}

				totalMass += Mathf.Max(part.Mass, Physical.MinMass);
			}

			if (preferredRoot != null && parts.Contains(preferredRoot)) // .contains is fine here since it's .Contains is O(1) on a hashset
			{
				root = preferredRoot;
			}
			else if (anchored != null)
			{
				root = anchored;
			}
			else if (largestMass != null)
			{
				root = largestMass;
			}
			else
			{
				root = lowestNetID!;
			}
		}

		WeldAssembly assembly = new()
		{
			Root = root,
			Parts = parts,
			Anchored = hasAnchoreds
		};

		// unfortunately we have to loop through the parts again to set up the assembly after root was picked
		foreach (Part part in parts)
		{
			Transform3D localTrans = root.GDNode3D.GlobalTransform.AffineInverse() * part.GDNode3D.GlobalTransform;
			assembly.LocalTransforms[part] = localTrans;
			part.AttachToAssembly(assembly, root, localTrans);
		}

		root.GDRigidBody.Mass = totalMass;
		root.GDRigidBody.Freeze = assembly.Anchored;
		root.OverridePhysicsProcess = assembly.Anchored;

		if (!assembly.Anchored)
		{
			root.SetPhysicsProcess(true);
		}

		return assembly;
	}
}