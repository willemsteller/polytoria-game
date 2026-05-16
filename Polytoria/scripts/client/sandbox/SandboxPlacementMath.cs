using Godot;

namespace Polytoria.Sandbox;

public static class SandboxPlacementMath
{
	public static PlacementResult FromRayHit(Vector3 hitPos, Vector3 hitNormal, Vector3 itemSize, float yaw, float gridSize = 1f)
	{
		if (hitNormal.LengthSquared() < 0.001f)
		{
			return new PlacementResult { IsValid = false };
		}

		Vector3 normal = hitNormal.Normalized();

		Vector3 normalAbs = normal.Abs();
		float maxComponent = Mathf.Max(normalAbs.X, Mathf.Max(normalAbs.Y, normalAbs.Z));
		if (maxComponent <= 0.9f) // Tilted surfaces are not supported for now
		{
			return new PlacementResult { IsValid = false };
		}

		yaw = yaw % 360f;

		Vector3 rotation = Vector3.Up * yaw;
		Basis basis = Basis.FromEuler(rotation * Mathf.DegToRad(1f));

		float halfExtents = GetProjectedHalfExtents(itemSize, basis, normal);

		Vector3 center = hitPos + normal * halfExtents;
		center = SnapFootprint(center, hitPos, normal, itemSize, basis, halfExtents, gridSize);

		return new PlacementResult
		{
			IsValid = true,
			Position = center,
			Rotation = rotation,
			Normal = normal
		};
	}

	private static float GetProjectedHalfExtents(Vector3 size, Basis basis, Vector3 axis)
	{
		return GetProjectedFullExtent(size, basis, axis) * 0.5f;
	}

	private static float GetProjectedFullExtent(Vector3 size, Basis basis, Vector3 axis)
	{
		Vector3 normalized = axis.Normalized();

		Vector3 right = basis.X.Normalized();
		Vector3 up = basis.Y.Normalized();
		Vector3 forward = basis.Z.Normalized();

		return
			Mathf.Abs(right.Dot(normalized)) * size.X +
			Mathf.Abs(up.Dot(normalized)) * size.Y +
			Mathf.Abs(forward.Dot(normalized)) * size.Z;
	}

	private static Vector3 SnapFootprint(Vector3 center, Vector3 hitPos, Vector3 normal, Vector3 itemSize, Basis basis, float halfExtents, float gridSize)
	{
		if (gridSize <= 0f)
		{
			return center;
		}

		Vector3 abs = normal.Abs();

		float fX = GetProjectedFullExtent(itemSize, basis, Vector3.Right);
		float fY = GetProjectedFullExtent(itemSize, basis, Vector3.Up);
		float fZ = GetProjectedFullExtent(itemSize, basis, Vector3.Forward * -1f);

		// Floor or ceiling
		if (abs.Y >= abs.X && abs.Y >= abs.Z)
		{
			center.X = Snap(hitPos.X, fX, gridSize);
			center.Z = Snap(hitPos.Z, fZ, gridSize);
			center.Y = hitPos.Y + normal.Y.Sign() * halfExtents;
			return center;
		}

		if (abs.X >= abs.Y && abs.X >= abs.Z)
		{
			center.Y = Snap(hitPos.Y, fY, gridSize);
			center.Z = Snap(hitPos.Z, fZ, gridSize);
			center.X = hitPos.X + normal.X.Sign() * halfExtents;
			return center;
		}

		center.X = Snap(hitPos.X, fX, gridSize);
		center.Y = Snap(hitPos.Y, fY, gridSize);
		center.Z = hitPos.Z + normal.Z.Sign() * halfExtents;
		return center;
	}

	private static float Snap(float hitCoord, float footprintSize, float gridSize)
	{
		float half = footprintSize * 0.5f;
		return Mathf.Floor(((hitCoord - half) / gridSize) + 0.5f) * gridSize + half;
	}

	private static float Snap(float value, float gridSize)
	{
		return Mathf.Round(value / gridSize) * gridSize;
	}

	private static float Sign(this float value)
	{
		return value >= 0 ? 1f : -1f;
	}
}

public struct PlacementResult
{
	public bool IsValid { get; set; }
	public Vector3 Position { get; set; }
	public Vector3 Rotation { get; set; }
	public Vector3 Normal { get; set; }
}