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
        PoissonDisc,
        CircularRadius
    }

    public AwarenessMode currentAwareness;
    public TextMeshProUGUI displayText;
    public TextMeshProUGUI dottedPathText;
    public TextMeshProUGUI hearingText;
    public TextMeshProUGUI canSeeLines;
    public TextMeshProUGUI toggleGaussianRandomness;

    public bool showDottedPath = true;
    public bool canHearSound = true;

    [Header("Gaussian Visibility")]
    public bool showRayLines = false;
    public bool useGaussianRandomness = true;

    private void Start()
    {
        displayText.text = UpdateAwarenessText(currentAwareness);
        dottedPathText.text = UpdatePathText(showDottedPath);
        hearingText.text = UpdateHearingText(canHearSound);
        toggleGaussianRandomness.text = UpdateGaussianText(useGaussianRandomness);
        canSeeLines.text = UpdateRayLineText(showRayLines);

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
            currentAwareness = AwarenessMode.CircularRadius;
            displayText.text = UpdateAwarenessText(currentAwareness);
        }
        else if (Input.GetKeyUp(KeyCode.F5))
        {
            showDottedPath = !showDottedPath;
            dottedPathText.text = UpdatePathText(showDottedPath);
        }

        else if (Input.GetKeyUp(KeyCode.F6))
        {
            canHearSound = !canHearSound;
            hearingText.text = UpdateHearingText(canHearSound);
        }
        else if (Input.GetKeyUp(KeyCode.F7))
        {
            if (currentAwareness == AwarenessMode.ViewCone)
            {
                useGaussianRandomness = !useGaussianRandomness;
                toggleGaussianRandomness.text = UpdateGaussianText(useGaussianRandomness);
            }
        }
        else if (Input.GetKeyUp(KeyCode.F8))
        {
            if (useGaussianRandomness)
            {
                showRayLines = !showRayLines;
                canSeeLines.text = UpdateRayLineText(showRayLines);
            }
        }

        if (!useGaussianRandomness)
        {
            showRayLines = false;
            UpdateRayLineText(showRayLines);
        }
    }
    string UpdateGaussianText(bool enabled)
    {
        return $"Gaussian Randomness: {(enabled ? "Enabled" : "Disabled")}";
    }
    string UpdateRayLineText(bool show)
    {
        return $"Ray Lines Visible: {(show ? "True" : "False")}";
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
        case AwarenessMode.CircularRadius:
                return "Enemy checking mode: Radius";
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

    string UpdateHearingText(bool hear)
    {
        if (hear)
        {
            return "Can Hear: True";
        }
        else
        {
            return "Can Hear: False";
        }
    }
}
