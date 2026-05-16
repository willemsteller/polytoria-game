using Godot;
using Polytoria.Datamodel;

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

	public static PlacementResult FromInstance(Dynamic target, Vector3 hitPos, Vector3 hitNormal, Vector3 itemSize, float yaw, float gridSize = 1f, bool clampToFace = true)
	{
		if (hitNormal.LengthSquared() < 0.001f)
		{
			return new PlacementResult { IsValid = false };
		}

		Transform3D targetTransform = target.GetGlobalTransform();
		Vector3 targetCenter = targetTransform.Origin;

		Vector3 targetRight = targetTransform.Basis.X.Normalized();
		Vector3 targetUp = targetTransform.Basis.Y.Normalized();
		Vector3 targetForward = targetTransform.Basis.Z.Normalized();

		Vector3 targetSize = new(targetTransform.Basis.X.Length(), targetTransform.Basis.Y.Length(), targetTransform.Basis.Z.Length());

		Vector3 normal = hitNormal.Normalized();

		if (!TryGetTargetFaceAxis(normal, targetRight, targetUp, targetForward, out int normalAxis, out float normalSign, out Vector3 faceNormalWorld))
		{
			return new PlacementResult { IsValid = false };
		}

		yaw = yaw % 360f;

		Vector3 rotation = Vector3.Up * yaw;
		Basis basis = Basis.FromEuler(rotation * Mathf.DegToRad(1f));

		float halfExtents = GetProjectedHalfExtents(itemSize, basis, faceNormalWorld);

		Vector3 delta = hitPos - targetCenter;

		Vector3 localHit = new Vector3(
			delta.Dot(targetRight),
			delta.Dot(targetUp),
			delta.Dot(targetForward)
		);

		int tangentAxisA;
		int tangentAxisB;

		if (normalAxis == 0)
		{
			tangentAxisA = 1;
			tangentAxisB = 2;
		}
		else if (normalAxis == 1)
		{
			tangentAxisA = 0;
			tangentAxisB = 2;
		}
		else
		{
			tangentAxisA = 0;
			tangentAxisB = 1;
		}

		Vector3 tangentWorldA = GetAxisWorld(tangentAxisA, targetRight, targetUp, targetForward);
		Vector3 tangentWorldB = GetAxisWorld(tangentAxisB, targetRight, targetUp, targetForward);

		float placedSizeA = GetProjectedFullExtent(itemSize, basis, tangentWorldA);
		float placedSizeB = GetProjectedFullExtent(itemSize, basis, tangentWorldB);

		float targetSizeA = GetAxis(targetSize, tangentAxisA);
		float targetSizeB = GetAxis(targetSize, tangentAxisB);

		Vector3 localCenter = localHit;

		SetAxis(ref localCenter, tangentAxisA, SnapFootprintLocal(GetAxis(localHit, tangentAxisA), targetSizeA, placedSizeA, gridSize, clampToFace));
		SetAxis(ref localCenter, tangentAxisB, SnapFootprintLocal(GetAxis(localHit, tangentAxisB), targetSizeB, placedSizeB, gridSize, clampToFace));

		float targetHalf = GetAxis(targetSize, normalAxis) * 0.5f;
		SetAxis(ref localCenter, normalAxis, normalSign * (targetHalf + halfExtents));

		Vector3 worldCenter = targetCenter + targetRight * localCenter.X + targetUp * localCenter.Y + targetForward * localCenter.Z;

		return new PlacementResult
		{
			IsValid = true,
			Position = worldCenter,
			Rotation = rotation,
			Normal = faceNormalWorld
		};
	}

	public static float GetProjectedHalfExtents(Vector3 size, Basis basis, Vector3 axis)
	{
		return GetProjectedFullExtent(size, basis, axis) * 0.5f;
	}

	public static float GetProjectedFullExtent(Vector3 size, Basis basis, Vector3 axis)
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

	private static float SnapFootprintLocal(float localHitPos, float faceSize, float footprintSize, float gridSize, bool clampToFace)
	{
		if (gridSize <= 0f)
		{
			return localHitPos;
		}

		float targetMin = faceSize * -0.5f;
		float targetMax = faceSize * 0.5f;
		float placedHalf = footprintSize * 0.5f;

		float snapped = targetMin + Mathf.Floor(((localHitPos - targetMin - placedHalf) / gridSize) + 0.5f) * gridSize + placedHalf;

		if (clampToFace)
		{
			float clampMin = targetMin + placedHalf;
			float clampMax = targetMax - placedHalf;

			if (clampMin <= clampMax)
			{
				snapped = Mathf.Clamp(snapped, clampMin, clampMax);
			}
			else if (Mathf.Abs(clampMin - clampMax) <= 0.001f)
			{
				snapped = (clampMin + clampMax) * 0.5f;
			}
		}

		return snapped;
	}

	private static bool TryGetTargetFaceAxis(Vector3 hitNormal, Vector3 targetRight, Vector3 targetUp, Vector3 targetForward, out int axis, out float sign, out Vector3 faceNormalWorld)
	{
		float rtDot = hitNormal.Dot(targetRight);
		float upDot = hitNormal.Dot(targetUp);
		float fwDot = hitNormal.Dot(targetForward);

		float rt = Mathf.Abs(rtDot);
		float up = Mathf.Abs(upDot);
		float fw = Mathf.Abs(fwDot);

		if (rt >= up && rt >= fw)
		{
			axis = 0;
			sign = Sign(rtDot);
			faceNormalWorld = targetRight * sign;
			return rt >= 0.9f;
		}

		if (up >= rt && up >= fw)
		{
			axis = 1;
			sign = Sign(upDot);
			faceNormalWorld = targetUp * sign;
			return up >= 0.9f;
		}

		axis = 2;
		sign = Sign(fwDot);
		faceNormalWorld = targetForward * sign;
		return fw >= 0.9f;
	}

	private static Vector3 GetAxisWorld(int axis, Vector3 right, Vector3 up, Vector3 forward)
	{
		return axis switch
		{
			0 => right,
			1 => up,
			_ => forward
		};
	}

	private static float GetAxis(Vector3 vector, int axis)
	{
		return axis switch
		{
			0 => vector.X,
			1 => vector.Y,
			_ => vector.Z
		};
	}

	private static void SetAxis(ref Vector3 vector, int axis, float value)
	{
		if (axis == 0)
		{
			vector.X = value;
		}
		else if (axis == 1)
		{
			vector.Y = value;
		}
		else
		{
			vector.Z = value;
		}
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