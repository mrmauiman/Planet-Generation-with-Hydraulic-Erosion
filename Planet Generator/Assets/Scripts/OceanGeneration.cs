using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

[ExecuteInEditMode]
[InitializeOnLoad]
public class OceanGeneration : MonoBehaviour
{
    // public variables
    public float oceanRadius = 1;
    public int icosphereSplits = 0;
    public Material oceanMaterial;
    public MeshFilter meshFilter;
    public MeshRenderer meshRenderer;

    // mesh data
    private List<Vector3> vertices = new List<Vector3>();
    private List<Vector3> normals = new List<Vector3>();
    private List<int> indices = new List<int>();
    private List<int> oldIndices;

    // state tracking
    private enum states { DONE, GENERATING_ICOSAHEDRON, REFINING_TRIANGLES};
    private states state = states.DONE;

    // face generating tracker
    private int currentFace = 0;

    // Approximation of the golden ratio
    private float t = (1f + Mathf.Sqrt(5f)) / 2f;

    // Constants
    private const int verticesPerFace = 3;

    // Linear Interpolation between Vector3 datatypes
    private Vector3 Vec3Lerp(Vector3 start, Vector3 end, float weight)
    {
        return new Vector3(Mathf.Lerp(start.x, end.x, weight), Mathf.Lerp(start.y, end.y, weight), Mathf.Lerp(start.z, end.z, weight));
    }

    // Adds vertex to vertices if it is not already present
    private int AddVertex(Vector3 vertex)
    {
        for (int i = 0; i < vertices.Count; i++)
        {
            if (vertex == vertices[i])
            {
                return i;
            }
        }
        vertices.Add(vertex);
        return vertices.Count - 1;
    }

    // Adds each index to the indices list
    // this function serves the purpose of readability when adding faces to the mesh
    private void AddFace(int index1, int index2, int index3)
    {
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

    // loops through each face and splits it into 4^icosphereSplits faces but only adding vertices that don't already exist
    private void RefineFaces()
    {
        int face_start = currentFace * verticesPerFace;
        Vector3Int face = new Vector3Int(oldIndices[face_start], oldIndices[face_start + 1], oldIndices[face_start + 2]);
        // face is a vector 3 containing the indices for the current face
        List<int> localVertexIndices = new List<int>();
        // Top Vertex
        localVertexIndices.Add(face.x);
        // split is the current split we're creating
        for (int split = 1; split < icosphereSplits + 1; split++)
        {
            // Begining vertex
            int localBegin = localVertexIndices.Count;
            Vector3 beginVertex = Vec3Lerp(vertices[face.x], vertices[face.z], (1f / icosphereSplits) * split);
            int begin = AddVertex(beginVertex);
            localVertexIndices.Add(begin);
            // Ending vertex
            Vector3 endVetex = Vec3Lerp(vertices[face.x], vertices[face.y], (1f / icosphereSplits) * split);
            int end = AddVertex(endVetex);
            // All points inbetween
            for (int offset = 1; offset < split; offset++)
            {
                int index = AddVertex(Vec3Lerp(vertices[begin], vertices[end], (1f / split) * offset));
                localVertexIndices.Add(index);
            }
            // After inbetween so the indices are in order
            int localEnd = localVertexIndices.Count;
            localVertexIndices.Add(end);
            // face calculations
            // Add the left most face in split
            AddFace(localVertexIndices[localBegin], localVertexIndices[localBegin - (split)], localVertexIndices[localBegin + 1]);
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
        if (currentFace == oldIndices.Count / verticesPerFace)
        {
            SetState(states.DONE);
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
            vertices[vertexIndex] = normalizedVertex * oceanRadius;
        }
    }

    // Sets the vertices normals and faces of the mesh
    private void CreateMesh()
    {
        if (meshFilter.sharedMesh == null)
        {
            meshFilter.sharedMesh = new Mesh();
        }
        meshFilter.sharedMesh.Clear(false);
        meshFilter.sharedMesh.SetVertices(vertices);
        meshFilter.sharedMesh.SetNormals(normals);
        meshFilter.sharedMesh.SetTriangles(indices, 0);
        meshRenderer.material = oceanMaterial;
    }

    // Sets the state variable to pState and handles any extra computations that occur on a state transition
    private void SetState(states pState)
    {
        state = pState;
        switch (state)
        {
            case states.DONE:
                GenerateSphereNormalsAndSetRadius();
                CreateMesh();
                break;
            case states.GENERATING_ICOSAHEDRON:
                // Initialize lists
                vertices.Clear();
                normals.Clear();
                indices.Clear();
                break;
            case states.REFINING_TRIANGLES:
                // reset the face tracker
                currentFace = 0;
                // move the current face data to old indices
                oldIndices = new List<int>(indices);
                indices.Clear();
                break;
        }
    }

    // In the constructor of this class I set the UpdateInspector function to be called every frame of the unity editor
    static OceanGeneration()
    {
        EditorApplication.update += UpdateInspector;
    }

    // Updates the scene view in the editor
    static void UpdateInspector()
    {
        SceneView.RepaintAll();
    }

    // Update is called once per frame
    void OnRenderObject()
    {
        switch (state)
        {
            case states.GENERATING_ICOSAHEDRON:
                GenerateIcosahedron();
                break;
            case states.REFINING_TRIANGLES:
                RefineFaces();
                Debug.Log(currentFace / 0.2f + "% done with ocean.");
                break;
        }
    }

    // Function that starts the generarion of the planet
    public void StartGeneration()
    {
        // change the state
        SetState(states.GENERATING_ICOSAHEDRON);
    }

}
