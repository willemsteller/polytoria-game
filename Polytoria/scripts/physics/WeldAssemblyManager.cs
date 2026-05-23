using System.Collections.Generic;
using Polytoria.Datamodel;

namespace Polytoria.Physics;

public static class WeldAssemblyManager
{
	private static readonly Dictionary<Part, WeldAssembly> _assemblies = [];
	private static readonly Dictionary<Weld, (Part? part0, Part? part1)> _welds = [];

	internal static void OnWeldChanged(Weld weld, Part? old0, Part? old1, Part? new0, Part? new1)
	{
		if (old0 != null && old1 != null)
		{
			OnWeldRemoved(weld, old0, old1);
		}

		if (new0 != null && new1 != null && new0 != new1)
		{
			OnWeldAdded(weld, new0, new1);
		}
	}

	internal static void OnWeldAdded(Weld weld, Part a, Part b)
	{
		WeldGraph.Add(weld, a, b);
		_welds[weld] = (a, b);

		WeldAssembly? assA = GetAssembly(a);
		WeldAssembly? assB = GetAssembly(b);

		if (assA != null && assA == assB)
		{
			return;
		}

		HashSet<Part> merged = [];

		if (assA != null)
		{
			foreach (Part part in assA.Parts)
			{
				merged.Add(part);
			}

			assA.Destroy();
		}
		else
		{
			merged.Add(a);
		}

		if (assB != null)
		{
			foreach (Part part in assB.Parts)
			{
				merged.Add(part);
			}

			assB.Destroy();
		}
		else
		{
			merged.Add(b);
		}

		Part? preferred;

		if (assA?.Root != null && assA.Root.Anchored)
		{
			preferred = assA.Root;
		}
		else if (assB?.Root != null && assB.Root.Anchored)
		{
			preferred = assB.Root;
		}
		else
		{
			preferred = assA?.Root ?? assB?.Root ?? a;
		}

		Build(merged, preferred);
	}

	internal static void OnWeldRemoved(Weld weld, Part? a, Part? b)
	{
		WeldGraph.Remove(weld, a, b);
		_welds.Remove(weld);

		if (a == null || b == null)
		{
			return;
		}

		WeldAssembly? old = GetAssembly(a) ?? GetAssembly(b);

		if (old == null)
		{
			return;
		}

		HashSet<Part> oldParts = old.Parts;
		Part oldRoot = old.Root;

		if (WeldGraph.AreConnected(a, b, oldParts))
		{
			return;
		}

		HashSet<Part> sideA = WeldGraph.GetComponentWithin(a, oldParts);
		HashSet<Part> sideB = [];

		foreach (Part part in oldParts)
		{
			if (!sideA.Contains(part))
			{
				sideB.Add(part);
			}
		}

		old.Destroy();
		Unregister(oldParts);

		Build(sideA, sideA.Contains(oldRoot) ? oldRoot : a);
		Build(sideB, sideB.Contains(oldRoot) ? oldRoot : b);
	}

	internal static void OnPartDeleted(Part part)
	{
		WeldAssembly? old = GetAssembly(part);

		foreach (Weld weld in WeldGraph.GetWelds(part).ToArray())
		{
			Part? other = WeldGraph.GetOtherPart(weld, part);
			WeldGraph.Remove(weld, part, other);
			_welds.Remove(weld);
		}

		if (old == null)
		{
			return;
		}

		HashSet<Part> remaining = [];

		foreach (Part p in old.Parts)
		{
			if (p != part && !p.IsDeleted)
			{
				remaining.Add(p);
			}
		}

		old.Destroy();
		Unregister(old.Parts);

		HashSet<Part> unvisited = [.. remaining];

		while (unvisited.Count > 0)
		{
			Part start = default!;
			foreach (Part p in unvisited)
			{
				start = p;
				break;
			}

			HashSet<Part> component = WeldGraph.GetComponentWithin(start, remaining);

			foreach (Part p in component)
				unvisited.Remove(p);

			Build(component, component.Contains(old.Root) ? old.Root : start);
		}
	}

	private static WeldAssembly? GetAssembly(Part part)
	{
		if (_assemblies.TryGetValue(part, out WeldAssembly? assembly))
		{
			return assembly;
		}

		return null;
	}

	private static void Register(WeldAssembly assembly)
	{
		foreach (Part part in assembly.Parts)
		{
			_assemblies[part] = assembly;
		}
	}

	private static void Unregister(HashSet<Part> parts)
	{
		foreach (Part part in parts)
		{
			_assemblies.Remove(part);
		}
	}

	private static void Build(HashSet<Part> parts, Part? preferredRoot)
	{
		if (parts.Count <= 1)
		{
			foreach (Part part in parts)
			{
				_assemblies.Remove(part);
				part.DetachFromAssembly();
			}

			return;
		}

		WeldAssembly assembly = WeldAssembly.Build(parts, preferredRoot);
		Register(assembly);
	}
}