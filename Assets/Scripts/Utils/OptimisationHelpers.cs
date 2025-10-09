using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Azura.Optimisation
{
	public class OptimisationHelpers : MonoBehaviour
	{
		public static float SqrdDistance(Vector3 pos1, Vector3 pos2)
		{
			Vector3 vec = pos2 - pos1;
			return vec.x * vec.x + vec.y * vec.y + vec.z * vec.z;
		}

		public static float SqrdDistance(Vector2 pos1, Vector2 pos2)
		{
			Vector2 vec = pos2 - pos1;
			return vec.x * vec.x + vec.y * vec.y;
		}

		public static float FlatSqrdDistance(Vector3 pos1, Vector3 pos2)
		{
			pos1.y = 0; pos2.y = 0;
			return SqrdDistance(pos1, pos2);
		}
	}
}
