using System;
using TMPro;
using UnityEngine.UI;

[Serializable]
public sealed class SoulCoreCreatureUIReferences
{
    public string creatureId;
    public TMP_Text countText;
    public TMP_Text costText;
    public TMP_Text abilityText;
    public Button summonButton;
    public Button unsummonButton;
    public Button abilityButton;
    public SoulCoreHoldButton summonHoldButton;
    public SoulCoreHoldButton unsummonHoldButton;
}
