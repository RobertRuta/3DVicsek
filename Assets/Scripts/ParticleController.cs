using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;
using System.Runtime.InteropServices;
using MergeSort;


public class ParticleController : MonoBehaviour
{
    #region variables

    public ComputeShader ParticleCalculation;
    public ComputeShader SortShader;
    public Material ParticleMaterial;
    public int numParticles = 500000;
    public int numCells;
    public float speed = 4.0f;
    public float radius = 5.0f;
    public float noise = 0.01f;
    public Vector3 box = new Vector3(1, 1, 1);
    public uint debug_length = 27;

    public bool show_debug_gridDims = true;
    public bool show_debug_indices = true;
    public bool show_debug_cells = true;
    public bool show_debug_neighbours = true;
    public bool show_debug_particles = true;
    public bool show_debug_bounds = true;

    private float cellDim;
    [SerializeField]
    private Vector3Int gridDims;

    private const int c_groupSize = 128;
    private int m_buildGridIndicesKernel;
    private int m_updateParticlesKernel;

    BitonicMergeSort sorter;
    

    #endregion


    #region Particle Struct

    struct Particle
    {
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 color;
    }    
    struct Cell
    {
        public uint id;
        public uint3 position;
    }    
        
    #endregion


    #region Buffers

    private DisposableBuffer<Particle> m_particles;
    private DisposableBuffer<Cell> m_cells;
    private DisposableBuffer<Vector3> m_quadPoints;
    private DisposableBuffer<uint2> m_cellIndexBounds;
    private DisposableBuffer<uint> m_neighbours;
    private DisposableBuffer<float3> m_velocities;
    private DisposableBuffer<uint> m_indicesBuffer;
    private DisposableBuffer<uint> m_values;
    private DisposableBuffer<uint> m_keys;

    private int numGroups;

    #endregion


    #region setup

    void Start()
    {
        // Initialise sorter
        sorter = new BitonicMergeSort(SortShader);

        // Find kernel and set kernel indices
        m_updateParticlesKernel = ParticleCalculation.FindKernel("UpdateParticles");
        m_buildGridIndicesKernel = ParticleCalculation.FindKernel("UpdateGrid");

        // Initialise grid
        cellDim = Mathf.CeilToInt(radius);
        CalcGrid();
        numCells = gridDims.x*gridDims.y*gridDims.z;
        
        // Create buffers
            // Create particle buffer
        m_particles = new DisposableBuffer<Particle>(numParticles);
            // Create cell buffer
        m_cells = new DisposableBuffer<Cell>(numCells);
            // Create cell boundary indices buffer
        m_cellIndexBounds = new DisposableBuffer<uint2>(27);
            // Create cell boundary indices buffer
        m_neighbours = new DisposableBuffer<uint>(27);
            // Create quad buffer
        m_quadPoints = new DisposableBuffer<Vector3>(6);
            // Create index buffer
        m_indicesBuffer = new DisposableBuffer<uint>(numParticles);
            // Create index buffer
        m_velocities = new DisposableBuffer<float3>(numParticles);

        m_keys = new DisposableBuffer<uint>(numParticles);
        m_values = new DisposableBuffer<uint>(numParticles);


        // Set initial positions and velocities
        InitParticles(m_particles);




        // Initialise quad points
        InitQuads(m_quadPoints.Buffer);


        // Set radius and cellDim variables in computer shader
        ParticleCalculation.SetFloat("radius", radius);
        ParticleCalculation.SetFloat("cellDim", cellDim);
        ParticleCalculation.SetFloats("box", new[] {box.x, box.y, box.z});


        // Testing
        // Prepare to run GPU code
        numGroups = Mathf.CeilToInt((float)numParticles / c_groupSize);
    }

    #endregion


    #region Compute Update

    void Update()
    {
        // Updated compute shader variables
        ParticleCalculation.SetFloat("deltaTime", Time.deltaTime);
        ParticleCalculation.SetFloat("speed", speed);
        ParticleCalculation.SetFloat("noise", noise);
        ParticleCalculation.SetInt("numParticles", numParticles);
        
        // Recalculate grid on box rescale
        CalcGrid();
            // Send new box dimensions to GPU
        ParticleCalculation.SetFloats("box", new[] {box.x, box.y, box.z});
        ParticleCalculation.SetInts("gridDims", new[] {gridDims.x, gridDims.y, gridDims.z});


        // Dispatch particle update code on GPU
            // Update GPU buffer with CPU buffer ?? Other way round?
        ParticleCalculation.SetBuffer(m_updateParticlesKernel, "particles", m_particles.Buffer);
        ParticleCalculation.SetBuffer(m_updateParticlesKernel, "neighbourCellBounds", m_cellIndexBounds.Buffer);
        ParticleCalculation.SetBuffer(m_updateParticlesKernel, "neighbourCellIDs", m_neighbours.Buffer);
        ParticleCalculation.SetBuffer(m_updateParticlesKernel, "cellIDs", m_values.Buffer);
        ParticleCalculation.SetBuffer(m_updateParticlesKernel, "particleIDs", m_keys.Buffer);
        ParticleCalculation.SetBuffer(m_updateParticlesKernel, "temp_vels_Buffer", m_velocities.Buffer);
            // Run code
        ParticleCalculation.Dispatch(m_updateParticlesKernel, numGroups, 1, 1);

        // Dispatch grid update code on GPU
            // Update GPU buffer with CPU buffer ?? Other way round?
        ParticleCalculation.SetBuffer(m_buildGridIndicesKernel, "particleIDs", m_keys.Buffer);
        ParticleCalculation.SetBuffer(m_buildGridIndicesKernel, "cellIDs", m_values.Buffer);
            // Update GPU buffer with CPU buffer ?? Other way round?
        ParticleCalculation.SetBuffer(m_buildGridIndicesKernel, "cells", m_cells.Buffer);
        ParticleCalculation.SetBuffer(m_buildGridIndicesKernel, "particles", m_particles.Buffer);
            // Run code
        ParticleCalculation.Dispatch(m_buildGridIndicesKernel, numGroups, 1, 1);

        // Run Sorter
            // Initialise sorter
        sorter.Init(m_keys.Buffer);
        sorter.SortInt(m_keys.Buffer, m_values.Buffer);

        m_keys.Download();
        m_values.Download();

        Debug(debug_length);
    }

    #endregion

    #region Rendering
        void OnRenderObject()
        {
            ParticleMaterial.SetBuffer("particles", m_particles.Buffer);
            ParticleMaterial.SetBuffer("quadPoints", m_quadPoints.Buffer);

            ParticleMaterial.SetPass(0);

            Graphics.DrawProceduralNow(MeshTopology.Triangles, 6, numParticles);
        }
    #endregion


    #region Cleanup
    void OnDestroy()
    {
        m_particles.Dispose();
        m_cells.Dispose();
        m_quadPoints.Dispose();
        m_indicesBuffer.Dispose();
        m_values.Dispose();
        m_keys.Dispose();
        m_cellIndexBounds.Dispose();
        m_neighbours.Dispose();
    }
    #endregion

    
    #region HelperFunctions

    void InitParticles(DisposableBuffer<Particle> particles)
    {
        for (int i = 0; i < numParticles; i++)
        {
            particles.Data[i].position = new Vector3(Random.Range(0.0f, box.x), Random.Range(0.0f, box.y), Random.Range(0.0f, box.z));
            particles.Data[i].velocity = new Vector3(Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f)).normalized * speed;
        }
            // Pack this data into buffer on CPU
        particles.Upload();
    }

    void Debug(uint length)
    {
        m_particles.Download();
        if (show_debug_particles)
        {
            print("----------Particle Debug----------");
            print("Listing first " + length + " particles");

            for (int i = 0; i < length; i++)
            {
                print("Particle " + i + "\n" 
                    + "position: " + m_particles.Data[i].position + "\n"
                    + "velocity: " + m_particles.Data[i].velocity + "\n"
                    + "-----" + "\n");
            }
        }

        m_neighbours.Download();
        if (show_debug_neighbours)
        {
            print("----------Neighbour Debug----------");
            print("Listing first " + length + " neighbours");

            for (int i = 0; i < length; i++)
            {
                print("Neighbour " + i + "\n" 
                    + "ID: " + m_neighbours.Data[i] + "\n"
                    + "-----" + "\n");
            }
        }

        if (show_debug_indices)
        {
            print("----------Particle Cell Debug----------");
            print("Listing first " + length + " particle, cell pairs");
            for (int i = 0; i < length; i++)
            {
                print(i + ": " + "{" + m_keys.Data[i] + ",   " + m_values.Data[m_keys.Data[i]] + "}");
            }
        }

        m_cells.Download();     
        if (show_debug_cells)
        {
            print("----------Cell Debug----------");
            print("Listing first " + length + " Cells");
            for (int i = 0; i < length; i++)
            {
                print(i + ": " + "{" + m_cells.Data[i].id + ",   " + m_cells.Data[i].position + "}");
            }
        }
        
        m_cellIndexBounds.Download();     
        if (show_debug_bounds)
        {
            print("----------Cell Bounds Debug----------");
            print("Listing first " + length + " Bounds");
            for (int i = 0; i < length; i++)
            {
                print(i + ": " + "{" + m_cellIndexBounds.Data[i].x + ",   " + m_cellIndexBounds.Data[i].y + "}");
            }
        }
    }

    void RecalcBox()
    {
        // Recalculate box dimensions
        box.x = Mathf.Round(box.x / cellDim) * cellDim;
        box.y = Mathf.Round(box.y / cellDim) * cellDim;
        box.z = Mathf.Round(box.z / cellDim) * cellDim;
    }

    void CalcGrid()
    {
        RecalcBox();
        // How many cells along x, y, z axes
        gridDims = new Vector3Int((int) (box.x / cellDim) , (int) (box.y / cellDim), (int) (box.z / cellDim));
    }

    void InitQuads(ComputeBuffer quadBuffer)
    {
        // Initialise quadpoints buffer (individual particles)
        quadBuffer.SetData(new[] {
            new Vector3(-0.25f, 0.25f),
            new Vector3(0.25f, 0.25f),
            new Vector3(0.25f, -0.25f),
            new Vector3(0.25f, -0.25f),
            new Vector3(-0.25f, -0.25f),
            new Vector3(-0.25f, 0.25f),
        });
    }

    #endregion


}
