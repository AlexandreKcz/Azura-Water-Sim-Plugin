using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Azura.WaterPhysics;

namespace Azura.Demo
{
	[RequireComponent(typeof(Floater))]
	[RequireComponent(typeof(InputManager))]
    public class BoatController : MonoBehaviour
    {
		[SerializeField] private Transform _motor;
		[SerializeField] private float _steerPower = 500f;
		[SerializeField] private float _power = 5f;
		[SerializeField] private float _maxSpeed = 10f;
		[SerializeField] private float _drag = .1f;

		private Rigidbody _rb;
		private Quaternion _startRotation;

		private InputManager _input;
		private Vector2 _movement;

		private void Start()
		{
			_input = GetComponent<InputManager>();
			_rb = GetComponent<Rigidbody>();

			_startRotation = _motor.localRotation;
		}

		private void Update()
		{
			_movement = _input.PlayerMovement;
		}

		private void FixedUpdate()
		{

			Vector3 forceDirection = transform.forward;

			int steer = (int) (_movement.x * -1);

			if (steer != 0) _rb.AddForceAtPosition(steer * transform.right * _steerPower / 100f, _motor.position);
			else _rb.angularVelocity *= .95f;

			_rb.angularVelocity = Vector3.ClampMagnitude(_rb.angularVelocity, 4f);

			Vector3 forward = Vector3.Scale(new Vector3(1,0,1), transform.forward);
			Vector3 targetVel = Vector3.zero;

			if (_movement.y > 0)
				PhysicsUtils.ApplyForceToReachVelocity(_rb, forward * _maxSpeed, _power);

			_motor.SetPositionAndRotation(_motor.position, transform.rotation * _startRotation * Quaternion.Euler(0, 30f * steer, 0));

			bool movingForward = Vector3.Cross(transform.forward, _rb.velocity).y < 0;
			_rb.velocity = Quaternion.AngleAxis(Vector3.SignedAngle(_rb.velocity, (movingForward ? 1f : 0f) * transform.forward, Vector3.up) * _drag, Vector3.up) * _rb.velocity;
		}
	}
}
