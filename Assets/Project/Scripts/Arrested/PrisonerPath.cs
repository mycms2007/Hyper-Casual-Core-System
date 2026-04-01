using UnityEngine;

/// <summary>
/// 씬에 하나만 배치. 죄수들이 공유하는 웨이포인트 경로.
/// </summary>
public class PrisonerPath : MonoBehaviour
{
    public static PrisonerPath Instance { get; private set; }

    [SerializeField] private Transform[] waypoints;
    [SerializeField] private Transform arrivalFaceTarget;

    public Transform[] Waypoints => waypoints;
    public Transform ArrivalFaceTarget => arrivalFaceTarget;

    private void Awake() => Instance = this;
}
