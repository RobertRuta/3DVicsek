using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BufferSorter;
using MergeSort;

public class Sorter : MonoBehaviour
{
    #region variables

    public BitonicMergeSort sorter;
    public ComputeShader ParticleCalculation;
    public ComputeShader SortShader;
    private int m_buildGridIndicesKernel;
    private int m_mainKernel;
    private int m_sortKernel;
    
    public int count = 1 << 10;

    #endregion


    #region Buffers

    private ComputeBuffer m_particlesBuffer;
    //private Particle[] temp_particles;
    private DisposableBuffer<uint> m_values;
    private DisposableBuffer<uint> m_keys;

    #endregion


    #region setup

    void Start()
    {

        sorter = new BitonicMergeSort(SortShader);
        m_values = new DisposableBuffer<uint>(count);
        m_keys = new DisposableBuffer<uint>(count);

        //ParticleCalculation.SetBuffer("cellIDs", m_values.Buffer);

        sorter.Init(m_keys.Buffer);
        sorter.Sort(m_keys.Buffer, m_values.Buffer);

        m_keys.Download();
        
        for (int i = 0; i < 1000; i++)
        {
            print(m_keys.Data[i] + " " + m_values.Data[m_keys.Data[i]]);
        }
    }

    #endregion


    #region ComputeUpdate

    void Update()
    {
    }

    #endregion
   
    void OnDestroy() 
    {
        if (sorter != null)
            sorter.Dispose();

        if (m_keys != null)
            m_keys.Dispose();

        if (m_values != null)
            m_values.Dispose();
    }

}
