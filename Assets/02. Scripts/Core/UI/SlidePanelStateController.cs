using System.Collections.Generic;
using UnityEngine;

public class SlidePanelStateController : MonoBehaviour
{
    [SerializeField] private SlideUiStateDefinition[] _states;
    [SerializeField] private string _initialStateId = SlideStateIds.Normal;

    private string _currentStateId;

    public string CurrentStateId => _currentStateId;

    private void Start()
    {
        if (!TryFindState(_initialStateId, out _))
        {
            Debug.LogError($"[SlidePanelStateController] Initial state '{_initialStateId}' is not defined.", this);
            return;
        }

        _currentStateId = _initialStateId;
        ApplyInitialLayout();
    }

    private void ApplyInitialLayout()
    {
        HashSet<UISlidePanelTransition> visible = BuildPanelSetForState(_currentStateId);
        HashSet<UISlidePanelTransition> all = BuildAllRegisteredPanelsUnion();

        foreach (UISlidePanelTransition p in all)
        {
            if (visible.Contains(p))
                p.SnapToRestImmediate();
            else
                p.SnapToExitEndImmediate();
        }
    }

    public void SwitchToState(string stateId)
    {
        if (stateId == _currentStateId)
            return;

        if (!TryFindState(stateId, out _))
        {
            Debug.LogWarning($"[SlidePanelStateController] Unknown state '{stateId}'.", this);
            return;
        }

        HashSet<UISlidePanelTransition> oldVisible = BuildPanelSetForState(_currentStateId);
        HashSet<UISlidePanelTransition> newVisible = BuildPanelSetForState(stateId);

        foreach (UISlidePanelTransition p in oldVisible)
        {
            if (!newVisible.Contains(p))
                p.PlayExit();
        }

        foreach (UISlidePanelTransition p in newVisible)
        {
            if (!oldVisible.Contains(p))
                p.PlayEnter();
        }

        _currentStateId = stateId;
    }

    public void SwitchToNormal()
    {
        SwitchToState(SlideStateIds.Normal);
    }

    public void SwitchToCustomizing()
    {
        SwitchToState(SlideStateIds.Customizing);
    }

    private bool TryFindState(string stateId, out SlideUiStateDefinition found)
    {
        found = null;
        if (_states == null || string.IsNullOrEmpty(stateId))
            return false;

        for (int i = 0; i < _states.Length; i++)
        {
            SlideUiStateDefinition def = _states[i];
            if (def == null || def.StateId != stateId)
                continue;

            found = def;
            return true;
        }

        return false;
    }

    private HashSet<UISlidePanelTransition> BuildPanelSetForState(string stateId)
    {
        var set = new HashSet<UISlidePanelTransition>();
        if (!TryFindState(stateId, out SlideUiStateDefinition def) || def.Panels == null)
            return set;

        UISlidePanelTransition[] panels = def.Panels;
        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i] != null)
                set.Add(panels[i]);
        }

        return set;
    }

    private HashSet<UISlidePanelTransition> BuildAllRegisteredPanelsUnion()
    {
        var set = new HashSet<UISlidePanelTransition>();
        if (_states == null)
            return set;

        for (int s = 0; s < _states.Length; s++)
        {
            SlideUiStateDefinition def = _states[s];
            if (def?.Panels == null)
                continue;

            UISlidePanelTransition[] panels = def.Panels;
            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i] != null)
                    set.Add(panels[i]);
            }
        }

        return set;
    }
}
