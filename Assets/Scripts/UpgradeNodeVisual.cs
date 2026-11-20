using UnityEngine;
using UnityEngine.UI;

public enum UpgradeNodeState
{
    Unavailable,
    Purchasable,
    Purchased
}

/// <summary>Applies the availability or purchased-currency color to an upgrade node.</summary>
public class UpgradeNodeVisual : MonoBehaviour
{
    [SerializeField] private Image nodeImage;
    [SerializeField] private UpgradeNodeState state;
    [SerializeField] private int price;
    [SerializeField] private Color purchasedCurrencyColor = new (0.82f, 0.68f, 0.32f, 1f);
    private static readonly Color PurchasableColor = new (0.78f, 0.79f, 0.78f, 1f);
    private static readonly Color UnavailableColor = new (0.24f, 0.25f, 0.26f, 1f);
    private int lastCurrency = int.MinValue;

    void Start()
    {
        RefreshState();
    }

    void Update()
    {
        var currency = GameController.instance.currency;
        if (currency != lastCurrency)
        {
            lastCurrency = currency;
            RefreshState();
        }
    }

    void OnValidate()
    {
        if (nodeImage == null) return;
        var currency = Application.isPlaying ? GameController.instance.currency : FindFirstObjectByType<GameController>().currency;
        ApplyState(currency);
    }

    void RefreshState()
    {
        ApplyState(GameController.instance.currency);
    }

    void ApplyState(int currency)
    {
        nodeImage.color = state switch
        {
            UpgradeNodeState.Purchasable when currency >= price => PurchasableColor,
            UpgradeNodeState.Purchased => purchasedCurrencyColor,
            _ => UnavailableColor
        };
    }
}
