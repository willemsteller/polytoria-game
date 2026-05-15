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
		Vector3 rotation = Vector3.Up * yaw;

		Basis basis = Basis.FromEuler(rotation * Mathf.DegToRad(1f));

		float halfExtents = GetProjectedHalfExtents(itemSize, basis, normal);
		Vector3 center = hitPos + normal * halfExtents;
		center = SnapToPlane(center, hitPos, normal, halfExtents, gridSize);

		return new PlacementResult
		{
			IsValid = true,
			Position = center,
			Rotation = rotation,
			Normal = normal
		};
	}

	private static float GetProjectedHalfExtents(Vector3 size, Basis basis, Vector3 normal)
	{
		Vector3 half = size * 0.5f;

		Vector3 right = basis.X.Normalized();
		Vector3 up = basis.Y.Normalized();
		Vector3 forward = basis.Z.Normalized();

		return
			Mathf.Abs(right.Dot(normal)) * half.X +
			Mathf.Abs(up.Dot(normal)) * half.Y +
			Mathf.Abs(forward.Dot(normal)) * half.Z;
	}

	private static Vector3 SnapToPlane(Vector3 center, Vector3 hitPos, Vector3 normal, float halfExtents, float gridSize)
	{
		if (gridSize <= 0f)
		{
			return center;
		}

		Vector3 abs = normal.Abs();

		// Floor or ceiling
		if (abs.Y >= abs.X && abs.Y >= abs.Z)
		{
			center.X = Snap(center.X, gridSize);
			center.Z = Snap(center.Z, gridSize);
			center.Y = hitPos.Y + normal.Y.Sign() * halfExtents;
			return center;
		}

		// X-facing wall
		if (abs.X >= abs.Y && abs.X >= abs.Z)
		{
			center.Y = Snap(center.Y, gridSize);
			center.Z = Snap(center.Z, gridSize);
			center.X = hitPos.X + normal.X.Sign() * halfExtents;
			return center;
		}

		// Z-facing wall
		center.X = Snap(center.X, gridSize);
		center.Y = Snap(center.Y, gridSize);
		center.Z = hitPos.Z + normal.Z.Sign() * halfExtents;
		return center;
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