using UnityEngine;

public class FaceCamera : MonoBehaviour
{
    private Camera _cam;

    private void Start()
    {
        _cam = Camera.main;
    }

    private void LateUpdate()
    {
        if (_cam == null) return;
        transform.rotation = Quaternion.Euler(0f, _cam.transform.eulerAngles.y + 180f, 0f);
    }
}
