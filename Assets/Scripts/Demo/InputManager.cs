using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Azura.Demo
{
    public class InputManager : MonoBehaviour
    {
        public Vector2 PlayerMovement { get {  return new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical")); } }
    }
}
