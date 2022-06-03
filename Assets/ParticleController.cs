using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleController : MonoBehaviour
{
    #region variables

    public ComputeShader ParticleCalculation;
    public Material ParticleMaterial;
    public int numParticles = 500000;
    public float speed = 4.0f;
    public Vector3 box = new Vector3(1, 1, 1);

    private const int c_groupSize = 128;
    private int m_updateParticlesKernel;

    #endregion


    #region Particle Struct

    struct Particle
    {
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 color;
    }    
        
    #endregion


    #region Buffers

    private ComputeBuffer m_particlesBuffer;
    private const int c_particleStride = 36;
    private ComputeBuffer m_quadPoints;
    private const int c_quadStride = 12;

    #endregion

    
    #region setup

    void Start()
    {
        // Find compute kernel
        m_updateParticlesKernel = ParticleCalculation.FindKernel("UpdateParticles");

        // Find compute buffer
        m_particlesBuffer = new ComputeBuffer(numParticles, c_particleStride);

        Particle[] particles = new Particle[numParticles];

        for (int i = 0; i < numParticles; i++)
        {
            particles[i].position = new Vector3(Random.Range(-box.x, box.x), Random.Range(-box.y,box.y), Random.Range(-box.z,box.z));
            particles[i].velocity = new Vector3(Random.Range(-box.x,box.x), Random.Range(-box.y,box.y), Random.Range(-box.z,box.z)).normalized * speed;
            particles[i].color = Vector3.one;
        }

        m_particlesBuffer.SetData(particles);

        // Create quad buffer
        m_quadPoints = new ComputeBuffer(6, c_quadStride);

        m_quadPoints.SetData(new[] {
            new Vector3(-0.5f, 0.5f),
            new Vector3(0.5f, 0.5f),
            new Vector3(0.5f, -0.5f),
            new Vector3(0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f),
            new Vector3(-0.5f, 0.5f),
        });

    }

    #endregion


    #region Compute Update

    void Update()
    {
        ParticleCalculation.SetBuffer(m_updateParticlesKernel, "particles", m_particlesBuffer);
        ParticleCalculation.SetFloat("deltaTime", Time.deltaTime);
        ParticleCalculation.SetFloat("speed", speed);
        ParticleCalculation.SetFloats("box", new[] {box.x, box.y, box.z});

        int numGroups = Mathf.CeilToInt((float)numParticles / c_groupSize);
        ParticleCalculation.Dispatch(m_updateParticlesKernel, numGroups, 1, 1);
    }

    #endregion


    #region Rendering
        void OnRenderObject()
        {
            ParticleMaterial.SetBuffer("particles", m_particlesBuffer);
            ParticleMaterial.SetBuffer("quadPoints", m_quadPoints);

            ParticleMaterial.SetPass(0);

            Graphics.DrawProceduralNow(MeshTopology.Triangles, 6, numParticles);
        }
    #endregion


    #region Cleanup
    void OnDestroy()
    {
        m_particlesBuffer.Dispose();
        m_quadPoints.Dispose();
    }
    #endregion
}
