using UnityEngine;

/// <summary>
/// 씬에 하나만 배치. 체포자들이 공유하는 웨이포인트 경로.
/// PrisonerPath와 동일한 구조.
/// </summary>
public class ArrestedPath : MonoBehaviour
{
    public static ArrestedPath Instance { get; private set; }

    [SerializeField] private Transform[] waypoints;
    public Transform[] Waypoints => waypoints;

    private void Awake() => Instance = this;
}
