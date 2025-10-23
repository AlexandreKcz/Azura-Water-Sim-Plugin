using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

using Azura.Optimisation;
using System.Linq;

namespace Azura.WaterSim
{
	[RequireComponent(typeof(MeshFilter))]
	[RequireComponent(typeof(MeshRenderer))]
	public class WaterSurface : MonoBehaviour
	{
		[Header("Water simulation values")]

		[Tooltip("Surface dimension (in meters)")]
		[SerializeField] private int _dimension = 40;

		[Tooltip("Simulation dimension (in meters), used to have values independant from surface dimension")]
		[SerializeField] private int _simDimension = 40;

		[Tooltip("Array of octaves used for simulation, octaves can overlap")]
		[SerializeField] private WaterOctave[] _octaves = new WaterOctave[] { WaterOctave.Default };

		[Tooltip("Run simulation in fixedUpdate if checked, otherwise runs in update (unchecked by default, useful in very specific case to work with unity's physic system with fixedDeltaTime)")]
		[SerializeField] private bool _simulateInFixedUpdate = false;

		[Tooltip("Reset vertices to position 0 if a chunk is outside of simulation, otherwise simulation is frozen on this chunk. Requires extra computing that has a performance cost, use only to avoid artifacts with frozen waves")]
		[SerializeField] private bool _resetNonSimulatedChunks = false;

		/// <summary>
		/// struct used for perlin noise generation in wave simulation
		/// </summary>
		[System.Serializable]
		public struct WaterOctave
		{
			[Tooltip("Speed and direction of wave, defined by magnitude and angle of the vector2")]
			public Vector2 speed;
			[Tooltip("Spatial frequency, number of wave (small value gives great ripples, high value gives small ripples)")]
			public Vector2 scale;
			[Tooltip("Amplitude of wave, max wave height (the higher the value, the higher the wave)")]
			public float height;
			[Tooltip("Invert wave phase, recommended to give wave variety")]
			public bool alternate;

			/// <summary>
			/// Default wave value, used for octaves array default initializer to provide test values to user
			/// </summary>
			public static readonly WaterOctave Default = new()
			{
				speed = new Vector2(.5f,.5f),
				scale = new Vector2(10, 10),
				height = .1f,
				alternate = true
			};
		}

		[Header("Visual")]

		[Tooltip("UV Scale value of the generated water surface mesh")]
		[SerializeField] private float _uvScale = 2;

		[Tooltip("Flip UVs scale between each tiles")]
		[SerializeField] private bool _flipUV = false;

		[Header("Optimisation values")]

		[Tooltip("The transform the surface will use to calculate active chunks, if null : chunk optimisation will be disabled")]
		[SerializeField] private Transform _observerTransform;

		[Tooltip("Size of chunks, must be a multiple of surface dimension, otherwise outside vertices won't be simulated")]
		[SerializeField] private float _chunkSize = 5;

		[Tooltip("A radius check around the observer used to eliminate far away vertices from the simulation, won't be used if chunk simulation is disabled")]
		[SerializeField] private bool _distanceOptimisation = false;

		[Tooltip("Radius used for distanceOptimisation")]
		[SerializeField] private float _calculationMaxDistance = 5;

		[Tooltip("Bounds used to give an offset to simulation when observer is outside of surface, use if you want to simulate chunks to a certain threshold, even when observer is'nt on the surface (on the edge of a lake for exemple)")]
		[SerializeField] private bool _useBounds = true;

		[Tooltip("Bounds distance if useBounds is enabled")]
		[SerializeField] private float _bounds = 5f;

		private Vector2[] _simulatedChunks;

		public bool IsSimulating { get { return _isSimulating; } }
		private bool _isSimulating = true;

		private MeshFilter _meshFilter;
		private Mesh _mesh;

		private void Start()
		{
			GenerateMesh();
		}

#if UNITY_EDITOR

		/* For material assignation to mr when component is added */
		private void Reset()
		{
			MeshRenderer mr = GetComponent<MeshRenderer>();
			Material[] mats = new Material[1];
			mats[0] = AssetDatabase.LoadAssetAtPath<Material>("Assets/Visual/Materials/WaterDebugMaterial.mat"); //TODO : changer le path
			mr.materials = mats;
		}

#endif

		#region Utils functions

		/// <summary>
		/// Return vertices index based on x and y coordinates
		/// </summary>
		/// <param name="x">x vertice's coordinate</param>
		/// <param name="z">y vertice's coordinate</param>
		/// <returns>index of the vertice in the array</returns>
		private int index(int x, int z) { return x * (_dimension + 1) + z; }

		/// <summary>
		/// Return vertices index based on x and y coordinates ;
		/// same as @index(int x, int z) method but with float parameters for convenience
		/// </summary>
		/// <param name="x">x vertice's coordinate</param>
		/// <param name="z">y vertice's coordinate</param>
		/// <returns>index of the vertice in the array</returns>
		private int index(float x, float z) { return index((int)x, (int)z); }

		/// <summary>
		/// Convert flat pos to mesh relative vertices coordinate
		/// </summary>
		/// <param name="localPos">a flat space coordinate relative to mesh position</param>
		/// <returns>vertices position in world space coordinate</returns>
		private Vector3 getVerticePos(Vector2 localPos)
		{
			return transform.position + new Vector3(localPos.x, 0, localPos.y);
		}

		/// <summary>
		/// Find a chunk relative to a position in world space
		/// </summary>
		/// <param name="localPos">the position used to find the chunk</param>
		/// <returns>lower left corner of the active chunk</returns>
		private Vector2 getChunk(Vector3 localPos)
		{
			Vector2 pos = new Vector2(localPos.x, localPos.z);

			pos.x = (int)(pos.x / _chunkSize);
			pos.y = (int)(pos.y / _chunkSize);

			pos.x = Mathf.Clamp(pos.x, 0, (int)(_dimension / _chunkSize) - 1);
			pos.y = Mathf.Clamp(pos.y, 0, (int)(_dimension / _chunkSize) - 1);

			return pos;
		}

		private Vector2[] getChunkBatch(Vector2 observerChunk)
		{
			Vector2[] chunks = new Vector2[9];

			for(int x = 0; x < 3; x++)
				for(int y = 0; y < 3; y++)
					chunks[x * 3 + y] = new Vector2(observerChunk.x + (x-1), observerChunk.y + (y-1));

			return chunks;
		}

		public float GetWaveHeight(Vector3 position)
		{
			Vector3 scale = new Vector3(1 / transform.lossyScale.x, 0, 1 / transform.lossyScale.z);
			Vector3 localPos = Vector3.Scale((position - transform.position), scale);

			Vector3[] points = new Vector3[4];
			for (int i = 0; i < points.Length; i++)
			{
				points[i] = new Vector3(i <= 1 ? Mathf.Floor(localPos.x) : Mathf.Ceil(localPos.x), 0, i == 0 || i == 2 ? Mathf.Floor(localPos.z) : Mathf.Ceil(localPos.z));
				points[i].x = Mathf.Clamp(points[i].x, 0, _dimension);
				points[i].z = Mathf.Clamp(points[i].z, 0, _dimension);
			}

			float maxDist = Mathf.Max(	Vector3.Distance(points[0], localPos), 
										Vector3.Distance(points[1], localPos), 
										Vector3.Distance(points[2], localPos), 
										Vector3.Distance(points[3], localPos));

			float dist =  ((maxDist - Vector3.Distance(points[0], localPos))
						 + (maxDist - Vector3.Distance(points[1], localPos))
						 + (maxDist - Vector3.Distance(points[2], localPos))
						 + (maxDist - Vector3.Distance(points[3], localPos))
						 + Mathf.Epsilon);

			float height =		_mesh.vertices[index(points[0].x, points[0].z)].y * (maxDist - Vector3.Distance(points[0], localPos))
							+	_mesh.vertices[index(points[1].x, points[1].z)].y * (maxDist - Vector3.Distance(points[1], localPos))
							+	_mesh.vertices[index(points[2].x, points[2].z)].y * (maxDist - Vector3.Distance(points[2], localPos))
							+	_mesh.vertices[index(points[3].x, points[3].z)].y * (maxDist - Vector3.Distance(points[3], localPos));

			return height * transform.lossyScale.y / dist;
		}

		#endregion

		#region Mesh generation functions

		/// <summary>
		/// Generate base surface mesh, assign it to renderer and filter, check for potential errors and change index format for very large surface
		/// </summary>
		public void GenerateMesh()
		{
			_mesh = new Mesh();
			_mesh.name = $"{gameObject.name} Water Surface Mesh";

			if (_distanceOptimisation && _calculationMaxDistance >= _chunkSize * 3)
			{
				Debug.LogWarning("Warning, max distance is greater than calculated chunk, distance calculation won't provide any performance improvement");
			}

			if (_dimension >= 255)
			{
				_mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; //expérimental, pour tester une grille d'une dimension de plus de 255
				Debug.LogWarning("Water mesh is very large (>255) activating UInt32 format for mesh (possible performance cost)");
			}

			_mesh.vertices = generateVertices();
			_mesh.triangles = generateTriangles();
			_mesh.uv = generateUVs();

			_meshFilter = gameObject.GetComponent<MeshFilter>();
			_meshFilter.mesh = _mesh;
		}

		/// <summary>
		/// Generate mesh vertices based on @_dimension
		/// </summary>
		/// <returns>generated vertices</returns>
		private Vector3[] generateVertices()
		{
			Vector3[] verts = new Vector3[(_dimension + 1) * (_dimension + 1)];

			for (int x = 0; x <= _dimension; x++)
				for (int z = 0; z <= _dimension; z++)
					verts[index(x, z)] = new Vector3(x, 0, z);

			return verts;
		}

		/// <summary>
		/// Generate mesh triangles based on @_mesh.vertices
		/// </summary>
		/// <returns>generated triangles</returns>
		private int[] generateTriangles()
		{
			int[] tries = new int[_mesh.vertices.Length * 6];

			for (int x = 0; x < _dimension; x++)
			{
				for (int z = 0; z < _dimension; z++)
				{
					tries[index(x, z) * 6 + 0] = index(x, z);
					tries[index(x, z) * 6 + 1] = index(x + 1, z + 1);
					tries[index(x, z) * 6 + 2] = index(x + 1, z);
					tries[index(x, z) * 6 + 3] = index(x, z);
					tries[index(x, z) * 6 + 4] = index(x, z + 1);
					tries[index(x, z) * 6 + 5] = index(x + 1, z + 1);
				}
			}

			return tries;
		}

		/// <summary>
		/// Generate mesh uvs based on @_dimension and @_mesh.vertices
		/// </summary>
		/// <returns>generated uvs</returns>
		private Vector2[] generateUVs()
		{
			Vector2[] uvs = new Vector2[_mesh.vertices.Length];

			for (int x = 0; x <= _dimension; x++)
			{
				for (int z = 0; z < _dimension; z++)
				{
					if (_flipUV)
					{
						Vector2 vec = new Vector2((x / _uvScale) % 2, (z / _uvScale) % 2);
						uvs[index(x, z)] = new Vector2(vec.x <= 1 ? vec.x : 2 - vec.x, vec.y <= 1 ? vec.y : 2 - vec.y);
					} else
					{
						Vector2 vec = new Vector2((x / _uvScale), (z / _uvScale));
						uvs[index(x, z)] = new Vector2(vec.x % 1, vec.y % 1);
					}
				}
			}

			return uvs;
		}

		#endregion

		#region Simulation

		private void simulateVertice	(float time, int x, int z, Vector3[] vertices)
		{
			float y = 0f;
			for (int o = 0; o < _octaves.Length; o++)
			{
				if (_octaves[o].alternate)
				{
					float perl = Mathf.PerlinNoise((x * _octaves[o].scale.x) / _simDimension, (z * _octaves[o].scale.y) / _simDimension) * Mathf.PI * 2f;
					y += Mathf.Cos(perl + _octaves[o].speed.magnitude * time) * _octaves[o].height;
				} else
				{
					float perl = Mathf.PerlinNoise((x * _octaves[o].scale.x + time * _octaves[o].speed.x) / _simDimension, (z * _octaves[o].scale.y + time * _octaves[o].speed.y) / _simDimension) - 0.5f;
					y += perl * _octaves[o].height;
				}
			}

			vertices[index(x, z)] = new Vector3(x, y, z);
		}

		private void simulateChunk		(float time, Vector2 observerChunk, Vector3[] vertices)
		{
			for (int x = (int)(observerChunk.x * _chunkSize); x <= (int)(observerChunk.x * _chunkSize) + _chunkSize; x++)
				for (int z = (int)(observerChunk.y * _chunkSize); z <= (int)(observerChunk.y * _chunkSize) + _chunkSize; z++)
				{
					if (_distanceOptimisation && OptimisationHelpers.FlatSqrdDistance(getVerticePos(new Vector2(x, z)), _observerTransform.position) > (_calculationMaxDistance * _calculationMaxDistance)) continue;
					simulateVertice(time, x, z, vertices); 
				}
		}

		private void simulateSurface	(float time, Vector3[] vertices)
		{
			for (int x = 0; x <= _dimension; x++)
				for (int z = 0; z <= _dimension; z++)
					simulateVertice(time, x, z, vertices);
		}

		private void simulateChunkBatch	(float time, Vector2[] chunks, Vector3[] vertices)
		{
			for (int i = 0; i < chunks.Length; i++)
			{
				if (chunks[i].x < 0 || chunks[i].x >= _dimension / _chunkSize ||
					chunks[i].y < 0 || chunks[i].y >= _dimension / _chunkSize) continue;
				else simulateChunk(time, chunks[i], vertices);
			}
		}

		private void resetChunk(Vector2 observerChunk, Vector3[] vertices)
		{
			for (int x = (int)(observerChunk.x * _chunkSize); x <= (int)(observerChunk.x * _chunkSize) + _chunkSize; x++)
				for (int z = (int)(observerChunk.y * _chunkSize); z <= (int)(observerChunk.y * _chunkSize) + _chunkSize; z++)
				{
					vertices[index(x, z)].y = 0f;
				}
		}

		private void resetChunkBatch(Vector2[] chunks, Vector3[] vertices)
		{
			for (int i = 0; i < chunks.Length; i++)
			{
				if (chunks[i].x < 0 || chunks[i].x >= _dimension / _chunkSize ||
					chunks[i].y < 0 || chunks[i].y >= _dimension / _chunkSize) continue;
				else resetChunk(chunks[i], vertices);
			}
		}

		private Vector2[] getOldChunks(Vector2[] currentBatch, Vector2[] oldBatch)
		{
			List<Vector2> batch = new List<Vector2>();
			for(int i = 0; i < oldBatch.Length; i++)
			{
				if(!currentBatch.ToList().Contains(oldBatch[i])) batch.Add(oldBatch[i]);
			}

			return batch.ToArray();
		}

		private void simulationUpdate	(float time)
		{
			Vector3[] vertices = _mesh.vertices;

			if (_observerTransform == null)
				simulateSurface(time, vertices);
			else
			{
				if (_useBounds) _isSimulating = (_observerTransform.position.x >= this.transform.position.x - (_useBounds ? _bounds : 0) && _observerTransform.transform.position.x <= (this.transform.position.x + _dimension + (_useBounds ? _bounds : 0))) &&
					(_observerTransform.position.z >= this.transform.position.z - (_useBounds ? _bounds : 0) && _observerTransform.transform.position.z <= (this.transform.position.z + _dimension + (_useBounds ? _bounds : 0)));
				
				if (!_isSimulating) return;

				Vector2 observerChunk = getChunk(_observerTransform.position);
				simulateChunkBatch(time, getChunkBatch(observerChunk), vertices);


				//Chunks Reset
				if (_resetNonSimulatedChunks)
				{
					if (_simulatedChunks == null) _simulatedChunks = getChunkBatch(observerChunk);
					Vector2[] currentChunks = getChunkBatch(observerChunk);
					Vector2[] oldChunks = getOldChunks(currentChunks, _simulatedChunks);
					resetChunkBatch(oldChunks, vertices);
					_simulatedChunks = getChunkBatch(observerChunk);
				}
			}

			_mesh.vertices = vertices;
			_mesh.RecalculateNormals();
		}

		private void Update()
		{
			if(!_simulateInFixedUpdate) simulationUpdate(Time.time);
		}

		private void FixedUpdate()
		{
			if (_simulateInFixedUpdate) simulationUpdate(Time.fixedTime);
		}

		#endregion

		#region Gizmos
#if UNITY_EDITOR

		/// <summary>
		/// Unity's @OnDrawGizmos() base method ; 
		/// used to draw surface outside of play mode, 
		/// distance radius around observer, 
		/// chunk grid and active chunk on play mode,
		/// surface simulation bounds
		/// </summary>
		private void OnDrawGizmos()
		{
			drawMesh();
			if (_observerTransform == null) return;
			Gizmos.color = Color.yellow;
			Gizmos.DrawWireSphere(_observerTransform.transform.position, _calculationMaxDistance);

			if (Application.isPlaying) Gizmos.color = _isSimulating ? Color.green : Color.red;
			if (_useBounds) drawBounds();

			if (!Application.isPlaying)
			{
				Gizmos.color = Color.blue;

				for (int x = 0; x < _dimension / _chunkSize; x++)
				{
					for (int y = 0; y < _dimension / _chunkSize; y++)
						drawChunk(new Vector2(x, y));
				}

				return;
			}

			Vector2 observerChunk = getChunk(_observerTransform.transform.position);

			Gizmos.color = _isSimulating ? Color.green : Color.cyan;

			foreach (var chunk in getChunkBatch(observerChunk))
			{
				if (chunk.x >= 0 && chunk.x <= (int)((_dimension - 1) / _chunkSize) &&
					chunk.y >= 0 && chunk.y <= (int)((_dimension - 1) / _chunkSize))drawChunk(chunk);
			}
		}

		/// <summary>
		/// Draw a single chunk using Unity's gizmos system
		/// </summary>
		/// <param name="chunk">lower left corner of chunk</param>
		private void drawChunk(Vector2 chunk)
		{
			chunk *= _chunkSize;

			Gizmos.color = Color.cyan;
			Gizmos.DrawLine(getVerticePos(chunk), getVerticePos(new Vector2(chunk.x + _chunkSize, chunk.y)));
			Gizmos.DrawLine(getVerticePos(new Vector2(chunk.x + _chunkSize, chunk.y)), getVerticePos(new Vector2(chunk.x + _chunkSize, chunk.y + _chunkSize)));
			Gizmos.DrawLine(getVerticePos(new Vector2(chunk.x + _chunkSize, chunk.y + _chunkSize)), getVerticePos(new Vector2(chunk.x, chunk.y + _chunkSize)));
			Gizmos.DrawLine(getVerticePos(new Vector2(chunk.x, chunk.y + _chunkSize)), getVerticePos(chunk));
		}

		/// <summary>
		/// Draw simulation bounds if needed using Unity's gizmos system
		/// </summary>
		private void drawBounds()
		{
			Gizmos.DrawLine(new Vector3(this.transform.position.x - _bounds, this.transform.position.y, -_bounds), new Vector3(this.transform.position.x + _dimension + _bounds, this.transform.position.y, -_bounds));
			Gizmos.DrawLine(new Vector3(this.transform.position.x + _dimension + _bounds, this.transform.position.y, -_bounds), new Vector3(this.transform.position.x + _dimension + _bounds, this.transform.position.y, this.transform.position.y + _dimension + _bounds));
			Gizmos.DrawLine(new Vector3(this.transform.position.x + _dimension + _bounds, this.transform.position.y, this.transform.position.y + _dimension + _bounds), new Vector3(-_bounds, this.transform.position.y, this.transform.position.y + _dimension + _bounds));
			Gizmos.DrawLine(new Vector3(-_bounds, this.transform.position.y, this.transform.position.y + _dimension + _bounds), new Vector3(this.transform.position.x - _bounds, this.transform.position.y, this.transform.position.y - _bounds));

		}

		/// <summary>
		/// Draw surface mesh's bounds using Unity's gizmos system (useful if the surface has no observer because chunk won't be drawn)
		/// </summary>
		private void drawMesh()
		{
			Gizmos.color = Color.cyan;
			Gizmos.DrawWireCube(this.transform.position + new Vector3(_dimension / 2, 0, _dimension / 2), new Vector3(_dimension, 0, _dimension));
		}
#endif
		#endregion
	}
}