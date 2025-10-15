using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Azura.WaterSim
{
	[RequireComponent(typeof(Rigidbody))]
	public class Floater : MonoBehaviour
	{
		[SerializeField] private WaterSurface _surface;

		[Space(5)]

		[SerializeField] private float _airDrag = 1f;
		[SerializeField] private float _waterDrag = 10f;
		[SerializeField] private bool _affectDirection = true;
		[SerializeField] private bool _attachToSurface = false;

		[SerializeField] private Transform[] _floatPoints;

		private Rigidbody _rb;
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
		}
	}
}
