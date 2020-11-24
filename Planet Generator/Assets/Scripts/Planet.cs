using System.Collections;
using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

[ExecuteInEditMode]
[InitializeOnLoad]
public class Planet : MonoBehaviour {
    // public variables
    [Header("Generation Variables")]
    public float planetRadius = 1;
    public float oceanLevel = 1;
    public float altitude = 5;
    public int icosphereSplits = 0;
    public int GPCLatitudeChunks = 25;
    public int GPCLongitudeChunks = 25;

    [Header ("Perlin Noise Variables")]
    public Vector3 offset = Vector3.zero;
    public Vector4 cellSizes = new Vector4(1, 1, 1, 1);
    public Vector4 weights = new Vector4(1, 0.25f, 0.5f, 0.1f);

    [Header("Simulation Variables")]
    public bool simulateWeather = true;
    public int totalSimulationIterations = 10000;
    public int simulationChunkSizes = 100;

    [Header("Weather Variables")]
    public bool showClouds = false;
    public float cloudSpawnChance = 0.01f;
    public float cloudModelScale = 1;
    public Vector2 cloudSizeRange = new Vector2(1, 4);
    public Vector2 cloudLifeSpanRange = new Vector2(50, 500);
    public GameObject cloudPrefab;

    [Header("Erosion Variables")]
    public int weatherlessDropletIterations = 5;
    [Range(1, 8)]
    public int erosionRadius = 1; // at 1 only the vertex where the drop's at is affected
    [Range(0, 1)]
    public float inertia = 0.05f; // At zero water will instantly change direction to flow downhill and 1 it will never change directions
    public float sedimentCapacityFactor = 4; // Multiplier for how much sediment a droplet can carry
    public float minSedimentCapacity = 0.01f; // Used to prevent carry capacity getting too close to zero on flatter terrain
    [Range(0, 1)]
    public float erodeSpeed = 0.3f;
    [Range(0, 1)]
    public float depositSpeed = 0.03f;
    [Range(0, 1)]
    public float evaporateSpeed = 0.01f;
    public float gravity = 4;
    public int maxDropletLifetime = 30;
    public float initialWaterVolume = 1;
    public float initialSpeed = 1;

    [Header("Component References")]
    public OceanGeneration ocean;
    public Material planetMaterial;
    public MeshFilter meshFilter;
    public MeshRenderer meshRenderer;

    // mesh data
    private List<Vector3> vertices = new List<Vector3>();
    private List<Vector3> normals = new List<Vector3>();
    private List<Color> colors = new List<Color>();
    private List<int> indices = new List<int>();
    private List<int> oldIndices;

    // Vertex Data Structures
    private Dictionary<string, int> vertexExistence = new Dictionary<string, int>();
    private Dictionary<int, List<int>> vertexFaces = new Dictionary<int, List<int>>();
    private List<List<List<int>>> GPCChunkMap = new List<List<List<int>>>();
    private List<int> oceanVertices = new List<int>();

    // Simulation Variables
    private List<Cloud> clouds = new List<Cloud>();
    private List<Transform> cloudObjects = new List<Transform>();
    private int simulationIterations = 0;

    // Indices and weights of erosion brush precomputed for every node
    private int[][] erosionBrushIndices;
    private float[][] erosionBrushWeights;

    private int currentErosionRadius;
    private int currentVertexCount;


    // state tracking
    // DONE: Nothing is happening
    // GENERATING_ICOSAHEDRON: The initial icosahedron model is generating
    // REFINING_TRIANGLES: The icosahedron's faces are being split to create the icosphere
    // GENERATE_PERLIN_NOISE: A perlin noise value is being calculated and applied to each vertex of the mesh
    // GENERATE_NORMALS: Each vertex is calculating it's normal
    // RUN_SIMULATION: The weather/erosion simulation is running
    private enum states {DONE, GENERATING_ICOSAHEDRON, REFINING_TRIANGLES, GENERATE_PERLIN_NOISE, GENERATE_NORMALS, RUN_SIMULATION};
    private states state = states.DONE;

    // face generating tracker
    private int currentFace = 0;

    // Approximation of the golden ratio
    private float t = (1f + Mathf.Sqrt(5f)) / 2f;

    // Constants
    private const int verticesPerFace = 3;

    // ====================================================================================== Helper Functions

    // place is the numbers place so n=1.435 and place=100 it would return 1.44
    // and if place=10 it would be 1.4
    private float RoundToPlace(float n, float place)
    {
        return Mathf.Round(n * place) / place;
    }

    // position is a position in 3D space
    // returns the Global Position Coordinates of that 3D position
    private static Vector2 positionToGPC(Vector3 position)
    {
        // Get point on equator
        Vector3 equatorPoint = new Vector3(position.x, 0, position.z);
        equatorPoint = equatorPoint.normalized;
        // Account for the poles edgecase
        if (equatorPoint == Vector3.zero)
        {
            return new Vector2(0, Mathf.Sign(position.y) * 90);
        }
        // Get longitude
        float longitude = Vector3.SignedAngle(Vector3.forward, equatorPoint, Vector3.up);
        // Get latitude
        float latitude = Vector3.SignedAngle(equatorPoint, position.normalized, Vector3.Cross(equatorPoint, Vector3.up));
        // return the GPC
        return new Vector2(longitude, latitude);
    }


    // returns the chunks a gpc should go in
    private Vector2Int GPCToMap(Vector2 GPC)
    {
        int x = Mathf.FloorToInt(((GPC.x + 180) / 360f) * GPCLongitudeChunks);
        int y = Mathf.FloorToInt(((GPC.y + 90) / 180f) * GPCLatitudeChunks);
        x = (x == GPCLongitudeChunks) ? GPCLongitudeChunks - 1 : x;
        y = (y == GPCLatitudeChunks) ? GPCLatitudeChunks - 1 : y;
        return new Vector2Int(x, y);
    }


    // Sets the state variable to pState and handles any extra computations that occur on a state transition
    private void SetState(states pState)
    {
        // Handle leaving a state
        switch (state)
        {
            case states.GENERATE_NORMALS:
                CreateMesh();
                break;
            case states.REFINING_TRIANGLES:
                GenerateSphereNormalsAndSetRadius();
                break;
        }
        // Switch the state
        state = pState;
        // Handle starting a state
        switch (state)
        {
            case states.DONE:
                Debug.Log("Done");
                break;
            case states.GENERATING_ICOSAHEDRON:
                // Initialize lists
                vertices = new List<Vector3>();
                normals = new List<Vector3>();
                indices = new List<int>();
                vertexExistence = new Dictionary<string, int>();
                vertexFaces = new Dictionary<int, List<int>>();
                colors.Clear();
                ResetGPCChunkMap();
                break;
            case states.REFINING_TRIANGLES:
                // reset the face tracker
                currentFace = 0;
                // move the current face data to old indices
                oldIndices = new List<int>(indices);
                indices.Clear();
                break;
            case states.RUN_SIMULATION:
                simulationIterations = 0;
                DestroyClouds();
                GetOceanVertices();
                ErosionInitialization();
                break;
        }
    }

    // ====================================================================================== Mesh Creation

    // Sets the vertices, normals, and faces of the planet mesh
    private void UpdatePlanetMesh()
    {
        // By default a mesh can only contain 2^16 vertices, Set this to be 2^32
        meshFilter.sharedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        meshFilter.sharedMesh.Clear(false);
        meshFilter.sharedMesh.SetVertices(vertices);
        meshFilter.sharedMesh.SetNormals(normals);
        meshFilter.sharedMesh.SetColors(colors);
        meshFilter.sharedMesh.SetTriangles(indices, 0);
    }

    // Set the data of the planet Mesh and Ocean Mesh
    private void CreateMesh()
    {
        UpdatePlanetMesh();
        meshRenderer.material = planetMaterial;
        ocean.oceanRadius = oceanLevel;
        ocean.icosphereSplits = Mathf.Min(icosphereSplits / 4, 25);
        ocean.StartGeneration();
    }


    // ====================================================================================== Generate Sphere

    // vertex is a position in space
    // returns a string that uniqely identifies a vertex in as long as 2 vertices aren't within 0.001 unity units^3 of eachother
    private string VertexToString(Vector3 vertex)
    {
        return RoundToPlace(vertex.x, 1000) + "," + RoundToPlace(vertex.y, 1000) + "," + RoundToPlace(vertex.z, 1000);
    }

    // Adds vertex to vertices if it is not already present
    // returns the index in vertices of the newly created or existing vertex
    private int AddVertex(Vector3 vertex)
    {
        // Check if the vertex exists by using a C# Dictionary
        string vString = VertexToString(vertex);
        if (vertexExistence.ContainsKey(vString))
        {
            // vertex already exists
            return vertexExistence[vString];
        }
        // vertex does not exist create it
        vertices.Add(vertex);
        int rv = vertices.Count - 1;
        // add the vertex to the existence dictionary
        vertexExistence.Add(vString, rv);
        // add extra data to keep track of for a vertex
        AddVertexData(rv);
        return rv;
    }

    // vIndex is a vertices index
    // Adds the next face index to contain vertex in vertexFaces
    private void AddVertexFace(int vIndex)
    {
        // check if the vertex already has a position in vertexFaces
        if (!vertexFaces.ContainsKey(vIndex))
        {
            // vIndex doesn't have a position, add it
            vertexFaces.Add(vIndex, new List<int>());
        }
        // add the current face to this vertex's faces
        vertexFaces[vIndex].Add(indices.Count);
    }

    // Adds each index to the indices list
    // this function serves the purpose of readability when adding faces to the mesh
    private void AddFace(int index1, int index2, int index3)
    {
        // add this face to each of the vertices face list
        AddVertexFace(index1);
        AddVertexFace(index2);
        AddVertexFace(index3);

        // Create the face
        indices.Add(index1);
        indices.Add(index2);
        indices.Add(index3);
    }

    // Hard code the vertices and faces of an Icosahedron
    // An Icosahedron can be created by 3 intersecting rectangles aligned to the planes of 3d space
    // each rectangle has a ratio of 1:t (see declaration of t for more info) then the corners of the
    // three rectangles creates the 12 vertices of a icosahedron
    // the faces indices are declared in clockwise order so the face are facing outward
    private void GenerateIcosahedron()
    {
        // vertices
        // xy plane
        AddVertex(Vector3.Normalize(new Vector3(-1f, t, 0f)));
        AddVertex(Vector3.Normalize(new Vector3(1f, t, 0f)));
        AddVertex(Vector3.Normalize(new Vector3(-1f, -t, 0f)));
        AddVertex(Vector3.Normalize(new Vector3(1f, -t, 0f)));

        // yz plane
        AddVertex(Vector3.Normalize(new Vector3(0f, -1f, t)));
        AddVertex(Vector3.Normalize(new Vector3(0f, 1f, t)));
        AddVertex(Vector3.Normalize(new Vector3(0f, -1f, -t)));
        AddVertex(Vector3.Normalize(new Vector3(0f, 1f, -t)));

        // xz plane
        AddVertex(Vector3.Normalize(new Vector3(t, 0f, -1f)));
        AddVertex(Vector3.Normalize(new Vector3(t, 0f, 1f)));
        AddVertex(Vector3.Normalize(new Vector3(-t, 0f, -1f)));
        AddVertex(Vector3.Normalize(new Vector3(-t, 0f, 1f)));

        // faces
        // faces around p0
        AddFace(0, 5, 1);
        AddFace(0, 11, 5);
        AddFace(0, 10, 11);
        AddFace(0, 7, 10);
        AddFace(0, 1, 7);

        // faces touching the faces around p0
        AddFace(11, 4, 5);
        AddFace(10, 2, 11);
        AddFace(7, 6, 10);
        AddFace(1, 8, 7);
        AddFace(5, 9, 1);

        // faces around p3
        AddFace(3, 4, 2);
        AddFace(3, 9, 4);
        AddFace(3, 8, 9);
        AddFace(3, 6, 8);
        AddFace(3, 2, 6);

        // faces touching the faces around p3
        AddFace(4, 9, 5);
        AddFace(9, 8, 1);
        AddFace(8, 6, 7);
        AddFace(6, 2, 10);
        AddFace(2, 4, 11);

        // change state to refining triangles
        SetState(states.REFINING_TRIANGLES);
    }

    // loops through each face and splits it into icosphereSplits^2 faces but only adding vertices that don't already exist
    private void RefineFaces()
    {
        int face_start = currentFace * verticesPerFace;
        Vector3Int face = new Vector3Int(oldIndices[face_start], oldIndices[face_start+1], oldIndices[face_start+2]);
        // face is a vector 3 containing the indices for the current face
        List<int> localVertexIndices = new List<int>();
        // Top Vertex
        localVertexIndices.Add(face.x);
        // split is the current split we're creating
        for (int split = 1; split < icosphereSplits+1; split++)
        {
            // Begining vertex
            int localBegin = localVertexIndices.Count;
            Vector3 beginVertex = Vector3.Lerp(vertices[face.x], vertices[face.z], (1f / icosphereSplits) * split);
            int begin = AddVertex(beginVertex);
            localVertexIndices.Add(begin);
            // Ending vertex
            Vector3 endVetex = Vector3.Lerp(vertices[face.x], vertices[face.y], (1f / icosphereSplits) * split);
            int end = AddVertex(endVetex);
            // All points inbetween
            for (int offset = 1; offset < split; offset++)
            {
                int index = AddVertex(Vector3.Lerp(vertices[begin], vertices[end], (1f / split) * offset));
                localVertexIndices.Add(index);
            }
            // After inbetween so the indices are in order
            localVertexIndices.Add(end);
            // face calculations
            // Add the left most face in split
            AddFace(localVertexIndices[localBegin], localVertexIndices[localBegin - (split)], localVertexIndices[localBegin+1]);
            for (int offset = 1; offset < split; offset++)
            {
                Vector3Int upsideDown = new Vector3Int();
                Vector3Int rightsideUp = new Vector3Int();
                upsideDown.x = rightsideUp.x = localVertexIndices[localBegin + offset];
                upsideDown.y = localVertexIndices[(localBegin - split) + (offset - 1)];
                upsideDown.z = rightsideUp.y = localVertexIndices[(localBegin - split) + offset];
                rightsideUp.z = localVertexIndices[localBegin + offset + 1];
                AddFace(upsideDown.x, upsideDown.y, upsideDown.z);
                AddFace(rightsideUp.x, rightsideUp.y, rightsideUp.z);
            }
        }
        currentFace++;
        if (currentFace == oldIndices.Count/verticesPerFace)
        {
            SetState(states.GENERATE_PERLIN_NOISE);
        }
    }

    // Loops through all vertices to make their normal to be a normalized vertex position and makes their new position
    // that normal multiplied by the planet radius makeing a perfect sphere of planet_radius size
    private void GenerateSphereNormalsAndSetRadius()
    {
        for (int vertexIndex = 0; vertexIndex < vertices.Count; vertexIndex++)
        {
            Vector3 normalizedVertex = Vector3.Normalize(vertices[vertexIndex]);
            normals.Add(normalizedVertex);
            vertices[vertexIndex] = normalizedVertex * planetRadius;
        }
    }

    // Empties ChunkMap
    private void ResetGPCChunkMap()
    {
        GPCChunkMap.Clear();
        for (int lon = 0; lon < GPCLongitudeChunks; lon++)
        {
            GPCChunkMap.Add(new List<List<int>>());
            for (int lat = 0; lat < GPCLatitudeChunks; lat++)
            {
                GPCChunkMap[lon].Add(new List<int>());
            }
        }
    }

    // Add vIndex to a chunk in the GPCChunkMap
    private void AddVertexData(int vIndex)
    {
        // Create Chunk Map for speeding up drop simulation
        Vector2 GPC = positionToGPC(vertices[vIndex]);
        Vector2Int mapIndeces = GPCToMap(GPC);
        GPCChunkMap[mapIndeces.x][mapIndeces.y].Add(vIndex);
    }

    // ====================================================================================== Generate Basic Terrain

    // Loops through all verteces and gives them an altiude based on their position and perlin noise
    private void ApplyPerlinNoise()
    {
        for (int index = 0; index < vertices.Count; index++)
        {
            float noise = NoiseFunctions.LayeredPerlinNoise(vertices[index] + offset, cellSizes, weights);
            vertices[index] += (Vector3.Normalize(vertices[index]) * noise * altitude);
        }
        SetState(states.GENERATE_NORMALS);
    }

    // generates a normal for every vertex based on the average of all faces with that vertex
    private void GenerateNormals()
    {
        for (int vertexIndex = 0; vertexIndex < vertices.Count; vertexIndex++)
        {
            GenerateNormal(vertexIndex);
        }
        SetState(states.DONE);
    }

    // gerates a normal for vertex vertexIndex based on the average of all faces with that vertex
    private void GenerateNormal(int vertexIndex)
    {
        List<int> vFaces = vertexFaces[vertexIndex];
        List<Vector3Int> faces = new List<Vector3Int>();
        for (int index = 0; index < vFaces.Count; index++)
        {
            int startIndex = vFaces[index];
            faces.Add(new Vector3Int(indices[startIndex], indices[startIndex + 1], indices[startIndex + 2]));
        }
        // faces contains every face that contains vertices[vertexIndex]
        Vector3 sum = Vector3.zero;
        for (int faceIndex = 0; faceIndex < faces.Count; faceIndex++)
        {
            Vector3 U = vertices[faces[faceIndex].y] - vertices[faces[faceIndex].x];
            Vector3 V = vertices[faces[faceIndex].z] - vertices[faces[faceIndex].x];
            sum += Vector3.Cross(U, V);
        }
        normals[vertexIndex] = Vector3.Normalize(sum);
    }

    // Fills oceanVertices with every vertex in the ocean
    private void GetOceanVertices()
    {
        oceanVertices.Clear();
        for (int i = 0; i < vertices.Count; i++)
        {
            if (Mathf.Abs(vertices[i].magnitude) < oceanLevel)
            {
                oceanVertices.Add(i);
            }
        }
    }

    // ====================================================================================== Simulation

    // Generates Clouds, Moves Clouds, Runs Erosion Simulation
    private void RunSimulation()
    {
        // Do simulationChunkSizes iterations every frame of the editor
        for (int i = 0; i < simulationChunkSizes; i++)
        {
            simulationIterations++;
            if (simulateWeather)
            {
                // Generate New Clouds
                GenerateClouds();
                // Move Clouds
                MoveClouds();
            }
            
            // Run Erosion Simulation
            ErosionSimulation();

            // End Simulation
            if (simulationIterations >= totalSimulationIterations)
            {
                simulationIterations = 0;
                SetState(states.DONE);
                break;
            }

            // Display Progress every 100 frames
            if(simulationIterations%100 == 0)
            {
                Debug.Log("Framerate: " + 1 / Time.deltaTime + " | " + ((float)simulationIterations / (float)totalSimulationIterations) * 100 + "% done");
            }
        }
        UpdatePlanetMesh();
    }

    // =============================================================== Weather

    // has a cloudSpawnChance chance to spawn a cloud above water
    private void GenerateClouds()
    {
        if (UnityEngine.Random.value < cloudSpawnChance)
        {
            int vertexIndex = oceanVertices[UnityEngine.Random.Range(0, oceanVertices.Count)];
            // Spawn a cloud
            Vector2 coords = positionToGPC(vertices[vertexIndex]);
            float cloudRadius = Mathf.Lerp(cloudSizeRange.x, cloudSizeRange.y, UnityEngine.Random.value);
            float cloudLife = Mathf.Lerp(cloudLifeSpanRange.x, cloudLifeSpanRange.y, UnityEngine.Random.value);
            float altitudeActual = altitude + planetRadius;
            float cloudAltitude = Mathf.Lerp(altitudeActual, altitudeActual + (altitude * 0.5f), UnityEngine.Random.value) + 2;
            clouds.Add(new Cloud(coords, cloudRadius, cloudAltitude, cloudLife));

            if (showClouds)
            {
                // Create Model
                Vector3 pos = Vector3.Normalize(vertices[vertexIndex]) * cloudAltitude;
                Transform cloud = Instantiate(cloudPrefab, pos, Quaternion.identity).transform;
                cloud.localScale = new Vector3(cloudRadius * cloudModelScale, cloudRadius * cloudModelScale, 0.1f);
                cloud.LookAt(transform);
                cloudObjects.Add(cloud);
            }
        }
    }

    // Moves Clouds and remove dead clouds
    private void MoveClouds()
    {
        for (int cloudIndex = 0; cloudIndex < clouds.Count; cloudIndex++)
        {
            // apply wind force
            float latitude = clouds[cloudIndex].globalCoords.y;
            if (latitude > 60)
            {
                clouds[cloudIndex].ApplyWindForce(new Vector2(1.5f, -2)); // SW
            } else if (latitude > 30)
            {
                clouds[cloudIndex].ApplyWindForce(new Vector2(-1, 1)); // NE
            }
            else if (latitude > 0)
            {
                clouds[cloudIndex].ApplyWindForce(new Vector2(1, -1)); // SW
            }
            else if (latitude > -30)
            {
                clouds[cloudIndex].ApplyWindForce(new Vector2(1, 1)); // NW
            }
            else if (latitude > -60)
            {
                clouds[cloudIndex].ApplyWindForce(new Vector2(-1, -1)); // SE
            }
            else
            {
                clouds[cloudIndex].ApplyWindForce(new Vector2(1.5f, 2)); // NW
            }

            // calculate new gpc
            clouds[cloudIndex].Move();

            // subtract life
            clouds[cloudIndex].life--;
            // destroy dead clouds
            if (clouds[cloudIndex].life <= 0)
            {
                if (showClouds)
                {
                    GameObject.DestroyImmediate(cloudObjects[cloudIndex].gameObject, true);
                    cloudObjects.RemoveAt(cloudIndex);
                }
                clouds.RemoveAt(cloudIndex);
            } else if(showClouds)
            {
                // Move cloud Objects that aren't dead
                cloudObjects[cloudIndex].position = clouds[cloudIndex].Position();
                cloudObjects[cloudIndex].LookAt(transform);
            }
        }
    }

    // Removes all clouds
    private void DestroyClouds()
    {
        for (int i = 0; i < cloudObjects.Count; i++)
        {
            GameObject.DestroyImmediate(cloudObjects[i].gameObject, true);
        }
        // Use Clear instead of new List<>() since the number of clouds will be about the same every time and there
        // is no need to free up the allocated memory to the list to just ask for it back
        cloudObjects.Clear();
        clouds.Clear();
    }

    // =============================================================== Erosion

    // ================================================ Initialization

    // initializes the erosion brushes
    private void InitializeBrushIndices()
    {
        erosionBrushIndices = new int[vertices.Count][];
        erosionBrushWeights = new float[vertices.Count][];

        for (int index = 0; index < vertices.Count; index++)
        {
            HashSet<int> indicesHashTable = new HashSet<int>();
            List<int> tIndices = new List<int>();
            List<float> weights = new List<float>();

            tIndices.Add(index);
            weights.Add(1);

            List<int> connectedFaces = vertexFaces[index];
            float weightTotals = 0;

            for (int depth = 1; depth < erosionRadius; depth++)
            {
                List<int> thisIterationsIndices = new List<int>();
                for (int fIndex = 0; fIndex < connectedFaces.Count; fIndex++)
                {
                    // connectedFaces holds the indices of the first vertex in a face so I add all vertices in the face
                    // When adding to a HashSet if the item could be added true is returned and false is returned otherwise
                    // We store the new indices of this iteration so that we can limit the pool of new connected faces to vertices we haven't checked
                    for (int i = 0; i < verticesPerFace; i++)
                    {
                        int vIndex = indices[connectedFaces[fIndex] + i];
                        if (indicesHashTable.Add(vIndex))
                        {
                            tIndices.Add(vIndex);
                            thisIterationsIndices.Add(vIndex);
                            float weight = 1 - (depth / erosionRadius);
                            weights.Add(weight);
                            weightTotals += weight;
                        }
                    }
                }
                // Get the next set of connected faces
                connectedFaces = new List<int>();
                foreach (int iIndex in thisIterationsIndices)
                {
                    connectedFaces.AddRange(vertexFaces[iIndex]);
                }
            }
            // Set weights to be ranged 0-1
            // store indices and weights values into Brush
            erosionBrushIndices[index] = new int[tIndices.Count];
            erosionBrushWeights[index] = new float[tIndices.Count];
            for (int i = 0; i < tIndices.Count; i++)
            {
                erosionBrushIndices[index][i] = tIndices[i];
                erosionBrushWeights[index][i] = weights[i] / weightTotals;
            }
        }
    }

    // initializes the erosion brushes if needed
    private void ErosionInitialization()
    {
        if (erosionBrushIndices == null || currentErosionRadius != erosionRadius || currentVertexCount != vertices.Count)
        {
            InitializeBrushIndices();
            currentErosionRadius = erosionRadius;
            currentVertexCount = vertices.Count;
        }
    }

    // =============================================== Simulation

    // Runs a single frame of the erosion simulation
    private void ErosionSimulation()
    {
        Vector2 gpc;
        int iterations = weatherlessDropletIterations;
        if (simulateWeather)
        {
            iterations = clouds.Count;
        }
        // Create a water Droplet for every cloud
        for (int cloudIndex = 0; cloudIndex < iterations; cloudIndex++)
        {
            if (simulateWeather)
            {
                Cloud cloud = clouds[cloudIndex];
                // Create water droplet at random place below cloud
                float lonOffset = UnityEngine.Random.value * cloud.radius * 2f - cloud.radius;
                float latOffset = UnityEngine.Random.value * cloud.radius * 2f - cloud.radius;
                gpc = cloud.globalCoords + new Vector2(lonOffset, latOffset);
                gpc = GPCClampToBounds(gpc);
            }
            else
            {
                gpc = new Vector2(UnityEngine.Random.value * 360 - 180, UnityEngine.Random.value * 180 - 90);
            }
            Vector3 dir = Vector3.zero;
            float speed = initialSpeed;
            float water = initialWaterVolume;
            float sediment = 0;

            int vertexIndex;
            int newVertexIndex;

            int face_start = GPCToFaceIndex(gpc);
            Vector3 pos = GetWorldSpaceFromGPC(gpc, face_start);

            // Simulate Droplet's Life
            for (int lifetime = 0; lifetime < maxDropletLifetime; lifetime++)
            {
                vertexIndex = GetClosestVertex(pos, face_start);

                // Calculate droplet's height and direction of flow
                float height = pos.magnitude;
                Direction direction = GetFlowDirection(face_start);

                // Update the droplet's direction and position (move position 1 unit regardless of speed)
                dir = (dir * inertia + direction.worldDirection * (1 - inertia));
                dir = dir.normalized;
                direction.worldDirection = dir;
                newVertexIndex = GetNextVertex(direction.GPCDirection(), vertexIndex);
                gpc = positionToGPC(pos + (dir * (vertices[newVertexIndex] - vertices[vertexIndex]).magnitude));

                // Stop Simulating droplet if it's not moving
                if (dir.x == 0 && dir.y == 0)
                {
                    break;
                }

                // Find droplets new height and calculate the delta height
                int currFaceStart = face_start;
                face_start = GetFace(gpc, newVertexIndex);
                if (face_start == -1)
                {
                    face_start = GPCToFaceIndex(gpc);
                }

                pos = GetWorldSpaceFromGPC(gpc, face_start);
                float newHeight = pos.magnitude;
                float deltaHeight = newHeight - height;

                // Calculate the droplet's sediment capacity (higher when moving fast down a slope and contains lots of water)
                float sedimentCapacity = Mathf.Max(-deltaHeight * speed * water * sedimentCapacityFactor, minSedimentCapacity);

                // We don't want to do any erosion or depositing if we havn't changed faces
                // If carrying more sediment than capacity, or if flowing uphill:
                if ((sediment > sedimentCapacity || deltaHeight > 0) && currFaceStart != face_start)
                {
                    // If moving uphill (deltaHeight > 0) try fill up to the current height, otherwise deposit a fraction of the excess sediment
                    float amountToDeposit = (deltaHeight > 0) ? Mathf.Min(deltaHeight, sediment) : (sediment - sedimentCapacity) * depositSpeed;
                    sediment -= amountToDeposit;

                    // Add Sediment to this vertex (not to radius to fill small pits)
                    vertices[vertexIndex] += vertices[vertexIndex].normalized * amountToDeposit;
                }
                else if (currFaceStart != face_start)
                {
                    // Erode a fraction of the droplet's current carry capacity.
                    // Clamp the erosion to the change in height so that it doesn't dig a hole in the terrain behind the droplet
                    float amountToErode = Mathf.Min((sedimentCapacity - sediment) * erodeSpeed, -deltaHeight);

                    // Use erosion brush to erode from all nodes inside the droplet's erosion radius
                    for (int brushPointIndex = 0; brushPointIndex < erosionBrushIndices[vertexIndex].Length; brushPointIndex++)
                    {
                        int nodeIndex = erosionBrushIndices[vertexIndex][brushPointIndex];
                        float weighedErodeAmount = amountToErode * erosionBrushWeights[vertexIndex][brushPointIndex];
                        float deltaSediment = (vertices[nodeIndex].magnitude < weighedErodeAmount) ? vertices[nodeIndex].magnitude : weighedErodeAmount;
                        vertices[nodeIndex] -= vertices[nodeIndex].normalized * deltaSediment;
                        FixNormals(nodeIndex);
                        sediment += deltaSediment;
                    }
                }

                // Update droplet's speed and water content
                speed = Mathf.Sqrt(speed * speed + deltaHeight * gravity);
                water *= (1 - evaporateSpeed);
            }
        }
    }

    // returns the direction pointing downhill when on face described by face_start
    private Direction GetFlowDirection(int face_start)
    {
        Vector3Int face = new Vector3Int(indices[face_start], indices[face_start + 1], indices[face_start + 2]);
        Vector3 faceCenter = vertices[face.x] + vertices[face.y] + vertices[face.z];
        faceCenter = faceCenter / verticesPerFace;
        Vector3 U = vertices[face.y] - vertices[face.x];
        Vector3 V = vertices[face.z] - vertices[face.x];
        Vector3 normal = Vector3.Cross(U, V);
        // Create a transformation of WorldSpace to the local space of the vertex's normal
        Quaternion aligningRotation = Quaternion.FromToRotation(Vector3.forward, normal);
        // Get the x and y directions in local space of the vector that points towards origin in world space
        Vector3 localSpaceOrigin = aligningRotation * (Vector3.zero - faceCenter);
        Vector3 localSpaceOriginFlat = new Vector3(localSpaceOrigin.x, localSpaceOrigin.y, 0);
        // Convert the x,y vector into a worldspace vector
        Vector3 downHillDirection = Quaternion.Inverse(aligningRotation) * localSpaceOriginFlat;
        downHillDirection = downHillDirection.normalized;
        // get the flow direction in terms of global position coordinates
        Direction rv = new Direction(downHillDirection, faceCenter);
        return rv;
    }

    // returns gpc so that the x value spills from -180 to 180 and the y spils from -90 to 90
    Vector2 GPCClampToBounds(Vector2 gpc)
    {
        // makesure position is correct when crossing edges of gpc
        if (gpc.x > 180)
        {
            gpc.x = -180 + (gpc.x - 180);
        }
        else if (gpc.x < -180)
        {
            gpc.x = 180 + (gpc.x + 180);
        }

        if (gpc.y > 90)
        {
            gpc.y = -90 + (gpc.y - 90);
        }
        else if (gpc.y < -90)
        {
            gpc.y = 90 + (gpc.y + 90);
        }
        return gpc;
    }

    // if gpc lies on a face connected to vIndex the face's index is returned
    // otherwise -1 is returned
    private int GetFace(Vector2 gpc, int vIndex)
    {
        int rv = -1;
        List<int> connectedFaces = vertexFaces[vIndex];
        if (positionToGPC(vertices[vIndex]) == gpc)
        {
            // the point is on a vertex just take a connected face
            return connectedFaces[0];
        }
        foreach (int face_start in connectedFaces)
        {
            float angleSum = 0;
            // Get face gpc coordinates
            List<Vector2> coords = new List<Vector2>();
            for (int i = 0; i < verticesPerFace; i++)
            {
                Vector2 coord = positionToGPC(vertices[indices[face_start + i]]);
                if ((coord - gpc).magnitude > 180)
                {
                    // coord is on other side of gpc map
                    float sign = Mathf.Sign(coord.x);
                    coord.x = -sign * (180 + Mathf.Abs((sign * 180) - coord.x));
                }
                coords.Add(coord);
            }
            // Check if gpc is in face
            Vector2 previousVec = coords[2] - gpc;
            for (int i = 0; i < verticesPerFace; i++)
            {
                Vector2 vec = coords[i] - gpc;
                angleSum += Vector2.Angle(previousVec.normalized, vec.normalized);
                previousVec = vec;
            }
            if (angleSum > 355)
            {
                rv = face_start;
            }
        }
        return rv;
    }

    // returns the index of the face gpc lies on
    // if this function returns -2 an error was logged and the function failed
    private int GPCToFaceIndex(Vector2 gpc)
    {
        // Round to tens place to avoid floating point error
        gpc = new Vector2(Mathf.Round(gpc.x * 10) / 10f, Mathf.Round(gpc.y * 10) / 10f);
        Vector2Int mapIndex = GPCToMap(gpc);
        List<int> nearbyVertices = GPCChunkMap[mapIndex.x][mapIndex.y];
        int rv = -1;
        int nextOffsetX = 1;
        int nextOffsetY = 0;
        while (rv == -1)
        {
            // Check chunk for a vertex connected to the face containing gpc
            foreach (int vIndex in nearbyVertices)
            {
                rv = GetFace(gpc, vIndex);
                if (rv != -1)
                {
                    break;
                }
            }
            if (rv == -1)
            {
                // make sure nextOffset isn't out of bounds
                if (nextOffsetX + mapIndex.x >= GPCLongitudeChunks || nextOffsetX + mapIndex.x < 0)
                {
                    nextOffsetX = (nextOffsetX <= 0) ? (-nextOffsetX) + 1 : -nextOffsetX;
                    if (nextOffsetX + mapIndex.x >= GPCLongitudeChunks || nextOffsetX + mapIndex.x < 0)
                    {
                        nextOffsetX = 0;
                        nextOffsetY = (nextOffsetY <= 0) ? (-nextOffsetY) + 1 : -nextOffsetY;
                    }
                }
                if (nextOffsetY + mapIndex.y >= GPCLatitudeChunks || nextOffsetY + mapIndex.y < 0)
                {
                    nextOffsetY = (nextOffsetY <= 0) ? (-nextOffsetY) + 1 : -nextOffsetY;
                    if (nextOffsetY + mapIndex.y >= GPCLatitudeChunks || nextOffsetY + mapIndex.y < 0)
                    {
                        // We have checked every chunk
                        // It should be imposible to get to this block
                        rv = -2;
                        Debug.LogError("Checked Every Chunk: either there was a float error when calculating angle sums or vertexFaces is missing data");
                        Debug.Log("GPC Value: " + gpc);
                        SetState(states.DONE);
                        // Made rv -2 so the program continues
                    }
                }

                // Get vertices for next chunk
                nearbyVertices = GPCChunkMap[mapIndex.x + nextOffsetX][mapIndex.y + nextOffsetY];

                // Increment Next Offsets
                nextOffsetX = (nextOffsetX <= 0) ? (-nextOffsetX) + 1 : -nextOffsetX;
            }
        }
        return rv;
    }

    // returns the vertex connected to vIndex that is most in the direction of dir on the global position coordinate system
    private int GetNextVertex(Vector2 dir, int vIndex)
    {
        // Get vIndex's neighbors
        List<int> connectedFaces = vertexFaces[vIndex];
        HashSet<int> neighbors = new HashSet<int>();
        neighbors.Add(vIndex);
        foreach (int face_start in connectedFaces)
        {
            for (int i = 0; i < verticesPerFace; i++)
            {
                neighbors.Add(indices[face_start + i]);
            }
        }
        // Find the neighbor most in direction dir
        Vector2 myCoords = positionToGPC(vertices[vIndex]);
        float minDiff = 90;
        int rv = 0;
        foreach (int neighbor in neighbors)
        {
            Vector2 neighborCoords = positionToGPC(vertices[neighbor]);
            Vector2 dirOfNeighbor = neighborCoords - myCoords;
            float angle = Vector2.Angle(dir, dirOfNeighbor);
            if (angle < minDiff && neighbor != vIndex)
            {
                minDiff = angle;
                rv = neighbor;
            }
        }

        return rv;
    }

    // Recalculates the normals for vIndex and every vertex touching vIndex
    private void FixNormals(int vIndex)
    {
        // Get a HashSet of vIndex and it's neighbors
        List<int> connectedFaces = vertexFaces[vIndex];
        HashSet<int> toFix = new HashSet<int>();
        toFix.Add(vIndex);
        foreach (int face_start in connectedFaces)
        {
            for (int i = 0; i < verticesPerFace; i++)
            {
                toFix.Add(indices[face_start + i]);
            }
        }
        // Fix normals of all vertices in toFix
        foreach(int index in toFix)
        {
            GenerateNormal(index);
        }

    }

    // gpc must be in the face described by face_start
    // returns the position in world space of gpc
    private Vector3 GetWorldSpaceFromGPC(Vector2 gpc, int face_start)
    {
        Vector3 a = vertices[indices[face_start]];
        Vector3 b = vertices[indices[face_start+1]];
        Vector3 c = vertices[indices[face_start+2]];

        Vector2 gpcA = positionToGPC(a);
        Vector2 gpcB = positionToGPC(b);
        Vector2 gpcC = positionToGPC(c);

        float distA = (gpc - gpcA).magnitude;
        float distB = (gpc - gpcB).magnitude;
        float distC = (gpc - gpcC).magnitude;
        float distSum = distA + distB + distC;

        float weightA = 1f - (distA / distSum);
        float weightB = 1f - (distB / distSum);
        float weightC = 1f - (distC / distSum);

        Vector3 sum = (a * weightA) + (b * weightB) + (c * weightC);

        return sum/ verticesPerFace;
    }

    // returns the vertex closest to pos out of all vertices in the face described by face_start
    private int GetClosestVertex(Vector3 pos, int face_start)
    {
        int rv = indices[face_start];
        for (int i = 1; i < verticesPerFace; i++)
        {
            if ((pos - vertices[rv]).magnitude > (pos - vertices[indices[face_start + i]]).magnitude)
            {
                rv = indices[face_start + i];
            }
        }

        return rv;
    }

    // =============================================================== Unity Editor

    // In the constructor of this class I set the UpdateInspector function to be called every frame of the unity editor
    static Planet() {
        EditorApplication.update += UpdateInspector;
    }

    // Updates the scene view in the editor
    static void UpdateInspector()
    {
        SceneView.RepaintAll();
    }

    // This function is called every frame the scene view is updated
    void OnRenderObject() {
        switch (state)
        {
            case states.GENERATING_ICOSAHEDRON:
                GenerateIcosahedron();
                break;
            case states.REFINING_TRIANGLES:
                RefineFaces();
                Debug.Log(currentFace / 0.2f + "% done with Sphere.");
                break;
            case states.GENERATE_PERLIN_NOISE:
                ApplyPerlinNoise();
                break;
            case states.GENERATE_NORMALS:
                GenerateNormals();
                break;
            case states.RUN_SIMULATION:
                RunSimulation();
                break;
        }
    }

    // Function that starts the generarion of the planet
    public void StartGeneration()
    {
        // change the state
        SetState(states.GENERATING_ICOSAHEDRON);
    }

    // Starts the simulation
    public void StartSimulation()
    {
        SetState(states.RUN_SIMULATION);
    }

    // moves every vertex to it's position in global position space
    public void MapToGPS()
    {
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector2 gpc = positionToGPC(vertices[i]);
            vertices[i] = new Vector3(gpc.x, gpc.y, 0);
            normals[i] = new Vector3(0, 0, -1);
        }
        UpdatePlanetMesh();
    }

    // Menu item to generate
    [MenuItem("CONTEXT/Planet/Generate")]
    static void Generate(MenuCommand command)
    {
        Planet self = (Planet) command.context;

        // start generation
        self.StartGeneration();
    }

    // Menu item to Run Simulation
    [MenuItem("CONTEXT/Planet/Run Simulation")]
    static void RunSimulation(MenuCommand command)
    {
        Planet self = (Planet)command.context;

        // start generation
        self.StartSimulation();
    }

    // Menu item to Map to Global Position Space
    // This is useful to see how vertices are mapped onto a 2D plane for some calculations
    [MenuItem("CONTEXT/Planet/Show GP Space")]
    static void ShowGPS(MenuCommand command)
    {
        Planet self = (Planet)command.context;

        // start generation
        self.MapToGPS();
    }

    // =============================================================== Helper Classes

    // This class stores droplet movement direction information
    private class Direction
    {
        public Vector3 worldDirection;
        public Vector3 faceCenter;

        public Direction(Vector3 wDir, Vector3 fCenter)
        {
            worldDirection = wDir;
            faceCenter = fCenter;
        }

        // returns the direction in terms of gpc
        public Vector2 GPCDirection()
        {
            Vector2 flowDirection = positionToGPC(faceCenter + worldDirection) - positionToGPC(faceCenter);
            return flowDirection.normalized;
        }
    }

}
