using System.Collections.Generic;
using Polytoria.Datamodel;

namespace Polytoria.Physics;

public static class WeldGraph
{
	private static readonly Dictionary<Part, List<Weld>> _welds = [];

	internal static void Add(Weld weld, Part a, Part b)
	{
		AddOne(a, weld);
		AddOne(b, weld);
	}

	internal static void Remove(Weld weld, Part? a, Part? b)
	{
		if (a != null)
		{
			RemoveOne(a, weld);
		}

		if (b != null)
		{
			RemoveOne(b, weld);
		}
	}

	private static void AddOne(Part part, Weld weld)
	{
		if (!_welds.TryGetValue(part, out List<Weld>? list))
		{
			list = [];
			_welds[part] = list;

			part.Deleted += () =>
			{
				WeldAssemblyManager.OnPartDeleted(part);
			};
		}

		if (!list.Contains(weld))
		{
			list.Add(weld);
		}
	}

	private static void RemoveOne(Part part, Weld weld)
	{
		if (!_welds.TryGetValue(part, out List<Weld>? list))
		{
			return;
		}

		list.Remove(weld);

		if (list.Count == 0)
		{
			_welds.Remove(part);
		}
	}

	internal static List<Weld> GetWelds(Part part)
	{
		if (!_welds.TryGetValue(part, out List<Weld>? list))
		{
			return [];
		}

		return list;
	}

	internal static Part? GetOtherPart(Weld weld, Part part)
	{
		if (weld.Part0 == part)
		{
			return weld.Part1 as Part;
		}
		else if (weld.Part1 == part)
		{
			return weld.Part0 as Part;
		}

		return null;
	}

	internal static bool AreConnected(Part start, Part target, HashSet<Part> limit)
	{
		if (start == target)
		{
			return true;
		}

		HashSet<Part> visited = [];
		Queue<Part> queue = [];

		visited.Add(start);
		queue.Enqueue(start);

		while (queue.Count > 0)
		{
			Part current = queue.Dequeue();

			foreach (Weld weld in GetWelds(current))
			{
				Part? next = GetOtherPart(weld, current);
				if (next == null || next.IsDeleted || !limit.Contains(next))
				{
					continue;
				}

				if (next == target)
				{
					return true;
				}

				if (visited.Add(next))
				{
					queue.Enqueue(next);
				}
			}
		}

		return false;
	}

	internal static HashSet<Part> GetComponentWithin(Part start, HashSet<Part> limit)
	{
		HashSet<Part> visited = [];
		Queue<Part> queue = [];

		if (!limit.Contains(start))
		{
			return visited;
		}

		visited.Add(start);
		queue.Enqueue(start);

		while (queue.Count > 0)
		{
			Part current = queue.Dequeue();

			foreach (Weld weld in GetWelds(current))
			{
				Part? next = GetOtherPart(weld, current);
				if (next == null || next.IsDeleted || !limit.Contains(next))
				{
					continue;
				}

				if (visited.Add(next))
				{
					queue.Enqueue(next);
				}
			}
		}

		return visited;
	}
}