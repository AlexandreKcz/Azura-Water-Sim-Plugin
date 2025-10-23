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

		[SerializeField] private float _floatingCenterOffset = 0f;
		[SerializeField] private float _airDrag = 1f;
		[SerializeField] private float _waterDrag = 10f;
		[SerializeField] private bool _affectDirection = true;
		[SerializeField] private bool _attachToSurface = false;

		private Rigidbody _rb;
		private BoxCollider _collider;
		private float _waterLine;

		private Vector3[] _waterLinePoints;
		private Vector3[] _floatPoints;

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
			_floatPoints = _waterLinePoints;
			_centerOffset = PhysicsUtils.GetCenterOfPoints(_waterLinePoints) - transform.position;
		}

		private Vector3[] getColliderFloatingPoints()
		{
			Vector3 halfSize = _collider.size * .5f;
			Vector3[] localMidpoints = new Vector3[4];
			Vector3[] floatingMidPoints = new Vector3[4];

			localMidpoints[0] = _collider.center + new Vector3(halfSize.x, _floatingCenterOffset, halfSize.z);
			localMidpoints[1] = _collider.center + new Vector3(-halfSize.x, _floatingCenterOffset, halfSize.z);
			localMidpoints[2] = _collider.center + new Vector3(-halfSize.x, _floatingCenterOffset, -halfSize.z);
			localMidpoints[3] = _collider.center + new Vector3(halfSize.x, _floatingCenterOffset, -halfSize.z);

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

		private void FixedUpdate()
		{
			float newWaterLine = 0f;
			bool pointUnderWater = false;

			_floatPoints = getColliderFloatingPoints();

			for (int i = 0; i < _floatPoints.Length; i++)
			{
				_waterLinePoints[i] = _floatPoints[i];
				_waterLinePoints[i].y = _surface.GetWaveHeight(_floatPoints[i]);
				newWaterLine += _waterLinePoints[i].y / _floatPoints.Length;
				if (_waterLinePoints[i].y > _floatPoints[i].y)
					pointUnderWater = true;
			}

			float waterLineDelta = newWaterLine - _waterLine;
			_waterLine = newWaterLine;

			_targetUp = PhysicsUtils.GetNormal(_waterLinePoints);

			Vector3 gravity = Physics.gravity;
			_rb.drag = _airDrag;

			if(_waterLine > Center.y)
			{
				_rb.drag = _waterDrag;

				if (_attachToSurface)
					_rb.position = new Vector3(_rb.position.x, _waterLine - _centerOffset.y, _rb.position.z);
				else
				{
					gravity = _affectDirection ? _targetUp * -Physics.gravity.y : -Physics.gravity;
					transform.Translate(Vector3.up * waterLineDelta * 0.9f);
				}
			}

			_rb.AddForce(gravity * Mathf.Clamp(Mathf.Abs(_waterLine - Center.y), 0, 1));

			if (pointUnderWater)
			{
				_targetUp = Vector3.SmoothDamp(transform.up, _targetUp, ref _smoothVectorRotation, 0.2f);
				_rb.rotation = Quaternion.FromToRotation(transform.up, _targetUp) * _rb.rotation;
			}
		}
	}
}
