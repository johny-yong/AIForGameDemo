using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GeneralEnemyData : MonoBehaviour
{
    public enum AwarenessMode
    {
        OmniScient,
        ViewCone,
        PoissonDisc
    }

    public AwarenessMode currentAwareness;
    public TextMeshProUGUI displayText;
    public TextMeshProUGUI dottedPathText;

    public bool showDottedPath = true;
    private void Start()
    {
        displayText.text = UpdateAwarenessText(currentAwareness);
        dottedPathText.text = UpdatePathText(showDottedPath);
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyUp(KeyCode.F1))
        {
            currentAwareness = AwarenessMode.OmniScient;
            displayText.text = UpdateAwarenessText(currentAwareness);
        }
        else if (Input.GetKeyUp(KeyCode.F2))
        {
            currentAwareness = AwarenessMode.ViewCone;
            displayText.text = UpdateAwarenessText(currentAwareness);

        }
        else if (Input.GetKeyUp(KeyCode.F3))
        {
            currentAwareness = AwarenessMode.PoissonDisc;
            displayText.text = UpdateAwarenessText(currentAwareness);
        }
        else if (Input.GetKeyUp(KeyCode.F4))
        {
            showDottedPath = !showDottedPath;
            dottedPathText.text = UpdatePathText(showDottedPath);
        }

    }

    string UpdateAwarenessText(AwarenessMode current)
    {
        switch (current) {
        case AwarenessMode.OmniScient:
                return "Enemy checking mode: Omniscient";
        case AwarenessMode.ViewCone:
                return "Enemy checking mode: Viewcone";
        case AwarenessMode.PoissonDisc:
                return "Enemy checking mode: PoissonDisc";
        }

        return "Enemy checking mode:";
    }

    string UpdatePathText(bool showDottedPath)
    {
        if (showDottedPath)
        {
            return "Path Visual: True";
        }
        else
        {
            return "Path Visual: False";
        }
    }
}
