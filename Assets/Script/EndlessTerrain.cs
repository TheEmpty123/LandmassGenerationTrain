using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static MapGenerator;

public class EndlessTerrain : MonoBehaviour
{

    public const float maxViewDst = 480;
    public Transform viewer;

    public static Vector2 viewerPosition;
    int chunkSize;
    int chunkVisibleInViewDst;
    public Material material;

    Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
    List<TerrainChunk> terrainChunkVisibleList = new List<TerrainChunk>();

    static MapGenerator mapGenerator;

    private void Start()
    {
        mapGenerator = FindObjectOfType<MapGenerator>();
        chunkSize = 240;
        chunkVisibleInViewDst = (int)maxViewDst / chunkSize;
    }

    private void Update()
    {
        viewerPosition = new Vector2(viewer.position.x, viewer.position.z);
        UpdateVisibleChunks();
    }



    void UpdateVisibleChunks()
    {
        for (int i = 0; i < terrainChunkVisibleList.Count; i++)
        {
            terrainChunkVisibleList[i].setVisible(false);
        }
        terrainChunkVisibleList.Clear();

        int currentViewerChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
        int currentViewerChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

        for (int offsetY = -chunkVisibleInViewDst; offsetY <= chunkVisibleInViewDst; offsetY++)
        {
            for (int offsetX = -chunkVisibleInViewDst; offsetX <= chunkVisibleInViewDst; offsetX++)
            {
                Vector2 viewedChunkCoord = new Vector2(currentViewerChunkCoordX + offsetX, currentViewerChunkCoordY + offsetY);

                if (terrainChunkDictionary.ContainsKey(viewedChunkCoord))
                {
                    //if the chunk already created, update it instead of create a new one
                    terrainChunkDictionary[viewedChunkCoord].updateVisiblePlane();
                    if (terrainChunkDictionary[viewedChunkCoord].isVisible())
                    {
                        terrainChunkVisibleList.Add(terrainChunkDictionary[viewedChunkCoord]);
                    }
                }
                else
                {
                    terrainChunkDictionary.Add(viewedChunkCoord, new TerrainChunk(viewedChunkCoord, chunkSize, transform, material));
                }
            }
        }
    }

    public class TerrainChunk
    {
        GameObject meshObject;
        Vector2 position;
        Bounds bound;

        MeshRenderer meshRenderer;
        MeshFilter meshFilter;

        public TerrainChunk(Vector2 coord, int chunkSize, Transform parent, Material material)
        {
            position = coord * chunkSize;
            bound = new Bounds(position, Vector2.one * chunkSize);
            Vector3 positionV3 = new Vector3(position.x, 0, position.y);

            meshObject = new GameObject("Terrain Chunk");
            meshObject.transform.position = positionV3;
            meshObject.transform.SetParent(parent);
            setVisible(false);

            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();
            
            meshRenderer.sharedMaterial = material;

            mapGenerator.requestMapData(OnMapDataReceived);
        }

        void OnMapDataReceived(MapData mapData)
        {
            mapGenerator.requestMeshData(OnMeshDataReceived, mapData);
        }

        void OnMeshDataReceived(MeshData meshData)
        {
            meshFilter.mesh = meshData.CreateMesh();
        }

        public void updateVisiblePlane()
        {
            float viewerDstFromNearestEdge = bound.SqrDistance(viewerPosition);
            bool visible = viewerDstFromNearestEdge <= maxViewDst * maxViewDst;
            setVisible(visible);
        }

        public void setVisible(bool visible)
        {
            meshObject.SetActive(visible);
        }

        public bool isVisible()
        {
            return meshObject.activeSelf;
        }
    }

}
