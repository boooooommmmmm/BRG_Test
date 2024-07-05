using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShowFPS : MonoBehaviour
{
    public Text m_Text;

    private int frameCount = 0;
    private float totalDeltaTime = 0f;

    void Start()
    {
    }

    void Update()
    {
        if (totalDeltaTime < 1.0f)
        {
            totalDeltaTime += Time.deltaTime;
            frameCount++;
        }
        else
        {
            m_Text.text = frameCount + "";
            totalDeltaTime = 0;
            frameCount = 0;
        }
    }
}