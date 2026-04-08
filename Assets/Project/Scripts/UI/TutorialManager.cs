using System.Collections;
using UnityEngine;

/// <summary>
/// 게임 전체 튜토리얼 흐름 관리.
/// 아래찍기화살표(존 위 bob 화살표)와 납작화살표(플레이어 주변 방향 화살표) 두 시스템을 통합 관리.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [Header("아래찍기 화살표")]
    [SerializeField] private GameObject oreArrow;
    [SerializeField] private GameObject gemDropZoneArrow;
    [SerializeField] private GameObject handcuffStackZoneArrow;
    [SerializeField] private GameObject officeZoneArrow;
    [SerializeField] private GameObject takeMoneyZoneArrow;

    [Header("납작화살표")]
    [SerializeField] private FlatArrow flatArrow;
    [SerializeField] private Transform gemDropZoneTarget;
    [SerializeField] private Transform handcuffStackZoneTarget;

    // 아래찍기화살표 단계
    // 0: 광물 화살표 표시 중
    // 1: 첫 채광 완료 → GemDropZone 화살표 대기
    // 2: GemDropZone 화살표 표시 중
    // 3: 드랍 시작 → HandcuffStackZone 화살표 대기
    // 4: HandcuffStackZone 화살표 표시 중
    // 5: 첫 수갑 습득 → OfficeZone 화살표 대기
    // 6: OfficeZone 화살표 표시 중
    // 7: 첫 수갑 드랍 → TakeMoneyZone 화살표 대기
    // 8: TakeMoneyZone 화살표 표시 중
    // 9: 완료
    private int _step = 0;

    // 납작화살표 단계
    // 0: 대기 (gems MAX 아님)
    // 1: GemDropZone 가리키는 중
    // 2: 플레이어 GemDropZone 진입 (화살표 숨김, 드랍 대기)
    // 3: HandcuffStackZone 가리키는 중
    // 4: 완료
    private int _flatPhase = 0;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        HideAll();
        Show(oreArrow);
    }

    // ── 아래찍기화살표 이벤트 ─────────────────────────────────────

    /// <summary>Ore.Die() 플레이어 경로에서 호출.</summary>
    public void OnFirstMining()
    {
        if (_step != 0) return;
        _step = 1;
        Hide(oreArrow);
        StartCoroutine(Delay(0.1f, () =>
        {
            _flatPhase = 1;
            flatArrow?.Show(gemDropZoneTarget);
        }));
        StartCoroutine(Delay(0.2f, () =>
        {
            _step = 2;
            Show(gemDropZoneArrow);
        }));
    }

    /// <summary>GemDropZone.CollectGems() 시작 시 호출.</summary>
    public void OnGemDropStarted()
    {
        if (_step != 2) return;
        _step = 3;
        StartCoroutine(Delay(0.1f, () => Hide(gemDropZoneArrow)));
    }

    /// <summary>HandcuffStackZone.SpawnSequence() 첫 수갑 생성 시 호출.</summary>
    public void OnFirstHandcuffProduced()
    {
        if (_step != 3) return;
        _step = 4;
        Show(handcuffStackZoneArrow);
    }

    /// <summary>HandcuffStackZone.TransferToPlayer() 시작 시 호출.</summary>
    public void OnFirstHandcuffPickup()
    {
        if (_step != 4) return;
        _step = 5;
        StartCoroutine(Delay(0.2f, () =>
        {
            Hide(handcuffStackZoneArrow);
            StartCoroutine(Delay(0.1f, () =>
            {
                _step = 6;
                Show(officeZoneArrow);
            }));
        }));
    }

    /// <summary>OfficeZone.TryDropHandcuffs() 첫 드랍 시 호출.</summary>
    public void OnFirstHandcuffDropped()
    {
        if (_step != 6) return;
        _step = 7;
        StartCoroutine(Delay(0.2f, () => Hide(officeZoneArrow)));
    }

    /// <summary>TakeMoneyZone에 첫 코인이 날아갈 때 호출.</summary>
    public void OnFirstCoinLaunched()
    {
        if (_step != 7) return;
        _step = 8;
        Show(takeMoneyZoneArrow);
    }

    /// <summary>TakeMoneyZone.CollectCoins() 시작 시 호출.</summary>
    public void OnFirstMoneyPickup()
    {
        if (_step != 8) return;
        _step = 9;
        Hide(takeMoneyZoneArrow);
    }

    // ── 납작화살표 이벤트 ─────────────────────────────────────────

    /// <summary>GemDropZone.OnPlayerEnter 에서 호출.</summary>
    public void OnPlayerEnteredGemDropZone()
    {
        if (_flatPhase != 1) return;
        _flatPhase = 2;
        flatArrow?.Hide();
    }

    /// <summary>GemDropZone.OnAllGemLanded 에서 호출.</summary>
    public void OnAllGemsDropped()
    {
        if (_flatPhase != 2) return;
        _flatPhase = 3;
        flatArrow?.Show(handcuffStackZoneTarget);
    }

    /// <summary>GemDropZone.OnPlayerExit 에서 호출.</summary>
    public void OnPlayerExitedGemDropZone()
    {
        if (_flatPhase != 3) return;
        _flatPhase = 4;
        StartCoroutine(Delay(0.2f, () => flatArrow?.Hide()));
    }

    // ── 유틸 ────────────────────────────────────────────────────────

    private void Show(GameObject arrow) { if (arrow != null) arrow.SetActive(true); }
    private void Hide(GameObject arrow) { if (arrow != null) arrow.SetActive(false); }

    private void HideAll()
    {
        Hide(oreArrow);
        Hide(gemDropZoneArrow);
        Hide(handcuffStackZoneArrow);
        Hide(officeZoneArrow);
        Hide(takeMoneyZoneArrow);
        flatArrow?.Hide();
    }

    private IEnumerator Delay(float seconds, System.Action action)
    {
        yield return new WaitForSeconds(seconds);
        action?.Invoke();
    }
}
