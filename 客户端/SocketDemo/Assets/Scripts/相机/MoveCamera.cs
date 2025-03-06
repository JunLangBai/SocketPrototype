using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveCamera : MonoBehaviour
{
    public Transform cameraPosition;

    private void Update()
    {
        //在相机支架（CameraHolder）上添加这个脚本，使相机始终跟着camerapos（播放器）移动
        transform.position = cameraPosition.position;
    }
}