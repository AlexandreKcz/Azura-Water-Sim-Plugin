using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Azura.WaterPhysics
{
    public static class PhysicsUtils
    {
        public static Vector3 GetCenterOfPoints(Vector3[] points)
        {
            Vector3 center = Vector3.zero;
            for (int i = 0; i < points.Length; i++)
                center += points[i] / points.Length;
            return center;
        }

        public static Vector3 GetNormal(Vector3[] points)
        {
            if (points.Length < 3)
                return Vector3.up;

            Vector3 center = GetCenterOfPoints(points);

            float xx = 0f, xy = 0f, xz = 0f, yy = 0f, yz = 0f, zz = 0f;

            for(int i = 0;i < points.Length; i++)
            {
                Vector3 r = points[i] - center;
                xx += r.x * r.x;
                xy += r.x * r.y;
                xz += r.x * r.z;

                yy += r.y * r.y;
                yz += r.y * r.z;
                zz += r.z * r.z;
            }

            float det_x = yy * zz - yz * yz;
            float det_y = xx * zz - xz * xz;
            float det_z = xx * yy - xy * xy;

            if (det_x > det_y && det_x > det_z)
                return new Vector3(det_x, xz * yz - xy * zz, xy * yz - xz * yy).normalized;
            if (det_y > det_z)
                return new Vector3(xz * yz - xy * zz, det_y, xy * xz - yz * xx).normalized;
            else
                return new Vector3(xy * yz - xz * yy, xy * xz - yz * xx, det_z).normalized;
        }

        public static void ApplyForceToReachVelocity(Rigidbody rb, Vector3 vel, float force = 1, ForceMode mode = ForceMode.Force)
        {
            if (force == 0 || vel.magnitude == 0) return;

            vel = vel + vel.normalized * 0.2f * rb.drag;
            force = Mathf.Clamp(force, -rb.mass / Time.fixedDeltaTime, rb.mass / Time.fixedDeltaTime);

            if (rb.velocity.magnitude == 0)
                rb.AddForce(vel * force, mode);
            else
            {
                Vector3 projectedVel = (vel.normalized * Vector3.Dot(vel, rb.velocity) / vel.magnitude);
                rb.AddForce((vel - projectedVel) * force, mode);
            }
        }
    }
}
