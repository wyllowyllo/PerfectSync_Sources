using System;
using UnityEngine;

[Serializable]
public class SlideUiStateDefinition
{
    [SerializeField] private string _stateId;
    [SerializeField] private UISlidePanelTransition[] _panels;

    public string StateId => _stateId;
    public UISlidePanelTransition[] Panels => _panels;
}
