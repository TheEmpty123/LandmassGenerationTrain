using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System;

public class MapGenerator : MonoBehaviour
{

    public enum DrawMode { NoiseMap, ColourMap, Mesh, FalloffMap }

    public DrawMode drawMode;
    public Noise.NormalizeMode normalizeMode;

    public const int mapChunkSize = 239;
    [Range(0, 6)]
    public int editorPreviewLOD;
    public float noiseScale;

    public int octaves;
    [Range(0f, 1f)]
    public float persistance;
    public float lacunarity;

    public int seed;
    public Vector2 offset;
    public float meshHeightMultiplier;
    public AnimationCurve animationCurve;

    public bool autoUpdate;
    public bool useFalloffMap;

    public TerrainType[] regions;

    float[,] falloffMap;

    Queue<MapThreadInfo<MapData>> mapDataThreadInfosQueue = new Queue<MapThreadInfo<MapData>>();
    Queue<MapThreadInfo<MeshData>> meshDataThreadInfosQueue = new Queue<MapThreadInfo<MeshData>>();

    private void Awake()
    {
        falloffMap = FallofGenerator.GenerateFalloffMap(mapChunkSize);
    }

    public void requestMapData(Action<MapData> callback, Vector2 centre)
    {
        ThreadStart threadStart = delegate
        {
            mapDataThread(callback, centre);
        };

        new Thread(threadStart).Start();
    }

    public void requestMeshData(Action<MeshData> callback, MapData mapData, int lod)
    {
        ThreadStart threadStart = delegate
        {
            meshDataThread(callback, mapData, lod);
        };

        new Thread(threadStart).Start();
    }

    void mapDataThread(Action<MapData> callback, Vector2 centre)
    {
        MapData mapData = GenerateMapData(centre);
        lock (mapDataThreadInfosQueue)
        {
            mapDataThreadInfosQueue.Enqueue(new MapThreadInfo<MapData>(callback, mapData));
        }
    }

    void meshDataThread(Action<MeshData> callback, MapData mapData, int lod)
    {
        MeshData meshData = MeshGenerator.GererateTerrainMap(mapData.heightMap, meshHeightMultiplier, animationCurve, lod);
        lock (meshDataThreadInfosQueue)
        {
            meshDataThreadInfosQueue.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
        }
    }

    public void drawMapInEditor()
    {
        MapData mapData = GenerateMapData(Vector2.zero);
        MapDisplay display = FindObjectOfType<MapDisplay>();
        if (drawMode == DrawMode.NoiseMap)
        {
            display.DrawTextureMap(TextureGenerator.TextureFromHeightMap(mapData.heightMap));
        }
        else if (drawMode == DrawMode.ColourMap)
        {
            display.DrawTextureMap(TextureGenerator.TextureFromColourMap(mapData.colourMap, mapChunkSize, mapChunkSize));
        }
        else if (drawMode == DrawMode.Mesh)
        {
            display.DrawMesh(MeshGenerator.GererateTerrainMap(mapData.heightMap, meshHeightMultiplier, animationCurve, editorPreviewLOD), TextureGenerator.TextureFromColourMap(mapData.colourMap, mapChunkSize, mapChunkSize));
        }
        else if (drawMode == DrawMode.FalloffMap)
        {
            display.DrawTextureMap(TextureGenerator.TextureFromHeightMap(FallofGenerator.GenerateFalloffMap(mapChunkSize)));
        }
    }

    private void Update()
    {
        if (mapDataThreadInfosQueue.Count > 0)
        {
            for (int i = 0; i < mapDataThreadInfosQueue.Count; i++)
            {
                MapThreadInfo<MapData> mapThreadInfo = mapDataThreadInfosQueue.Dequeue();
                mapThreadInfo.callback(mapThreadInfo.parameter);
            }
        }

        if (meshDataThreadInfosQueue.Count > 0)
        {
            for (int i = 0; i < meshDataThreadInfosQueue.Count; i++)
            {
                MapThreadInfo<MeshData> meshThreadInfo = meshDataThreadInfosQueue.Dequeue();
                meshThreadInfo.callback(meshThreadInfo.parameter);
            }
        }
    }

    MapData GenerateMapData(Vector2 centre)
    {
        float[,] mapNoise = Noise.GenerateNoiseMap(mapChunkSize + 2, mapChunkSize + 2, seed, noiseScale, octaves, persistance, lacunarity, centre + offset, normalizeMode);

        Color[] colourMap = new Color[mapChunkSize * mapChunkSize];
        for (int y = 0; y < mapChunkSize; y++)
        {
            for (int x = 0; x < mapChunkSize; x++)
            {
                if (useFalloffMap)
                {
                    mapNoise[x, y] = Mathf.Clamp01(mapNoise[x, y] - falloffMap[x, y]);
                }

                float currentHeight = mapNoise[x, y];
                for (int i = 0; i < regions.Length; i++)
                {
                    if (currentHeight >= regions[i].height)
                    {
                        colourMap[y * mapChunkSize + x] = regions[i].color;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        return new MapData(mapNoise, colourMap);
    }

    private void OnValidate()
    {
        if (lacunarity < 1) lacunarity = 1;
        if (octaves < 0) octaves = 0;

        falloffMap = FallofGenerator.GenerateFalloffMap(mapChunkSize);
    }

    struct MapThreadInfo<T>
    {
        public Action<T> callback;
        public T parameter;

        public MapThreadInfo(Action<T> callback, T parameter)
        {
            this.callback = callback;
            this.parameter = parameter;
        }
    }
}

[System.Serializable]
public struct TerrainType
{
    public string name;
    public float height;
    public Color color;
}

public struct MapData
{
    public float[,] heightMap;
    public Color[] colourMap;

    public MapData(float[,] heightMap, Color[] colourMap)
    {
        this.heightMap = heightMap;
        this.colourMap = colourMap;
    }
}