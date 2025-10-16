using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Azura.WaterSim;

namespace Azura.WaterPhysics
{
	[RequireComponent(typeof(Rigidbody))]
	[RequireComponent(typeof(BoxCollider))]
	public class Floater : MonoBehaviour
	{
		[SerializeField] private WaterSurface _surface;

		[Space(5)]

		[SerializeField] private float _airDrag = 1f;
		[SerializeField] private float _waterDrag = 10f;
		[SerializeField] private bool _affectDirection = true;
		[SerializeField] private bool _attachToSurface = false;

		private Rigidbody _rb;
		private BoxCollider _collider;
		private float _waterLine;

		private Vector3[] _waterLinePoints;

		private Vector3 _smoothVectorRotation;
		private Vector3 _targetUp;
		private Vector3 _centerOffset;

		public Vector3 Center { get { return transform.position + _centerOffset; } }

		private void Awake()
		{
			_rb = GetComponent<Rigidbody>();
			_rb.useGravity = false;

			_collider = GetComponent<BoxCollider>();
		}

		private void Start()
		{
			_waterLinePoints = getColliderFloatingPoints();
		}

		private Vector3[] getColliderFloatingPoints()
		{
			Vector3 halfSize = _collider.size * .5f;
			Vector3[] localMidpoints = new Vector3[4];
			Vector3[] floatingMidPoints = new Vector3[4];

			localMidpoints[0] = _collider.center + new Vector3(halfSize.x, 0, halfSize.z);
			localMidpoints[1] = _collider.center + new Vector3(-halfSize.x, 0, halfSize.z);
			localMidpoints[2] = _collider.center + new Vector3(-halfSize.x, 0, -halfSize.z);
			localMidpoints[3] = _collider.center + new Vector3(halfSize.x, 0, -halfSize.z);

			for (int i = 0; i < 4; i++)
				floatingMidPoints[i] = _collider.transform.TransformPoint(localMidpoints[i]);

			return floatingMidPoints;
		}

		private void OnDrawGizmos()
		{
			Gizmos.color = Color.red;

			Vector3[] floatingPointsDebug = new Vector3[4];

			if (Application.isPlaying && _waterLinePoints != null) 
				floatingPointsDebug = _waterLinePoints;
			else {
				if (_collider == null) _collider = GetComponent<BoxCollider>();
				floatingPointsDebug = getColliderFloatingPoints(); 
			}

			for (int i = 0; i < floatingPointsDebug.Length; i++)
				Gizmos.DrawSphere(floatingPointsDebug[i], 0.1f);
		}
	}
}
